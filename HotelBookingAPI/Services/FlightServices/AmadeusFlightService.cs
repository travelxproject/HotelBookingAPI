using HotelBookingAPI.Database;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using System.Text.Json;
using HotelBookingAPI.Models.HotelModels;
using HotelBookingAPI.Utilities;
using HotelBookingAPI.Models.FlightModels;
using System.Buffers.Text;
using Afonsoft.Amadeus.shopping;
using Afonsoft.Amadeus.Resources;
using static Afonsoft.Amadeus.Resources.HotelOffer;

namespace HotelBookingAPI.Services.FlightServices
{
    public class AmadeusFlightService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiSecret;

        private string? _accessToken;
        private DateTime _tokenExpiration;

        public AmadeusFlightService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["ApiKeys:Amadeus"];
            _apiSecret = configuration["ApiKeys:AmadeusClientSecret"];
        }

        public async Task<FlightSearchResponse> SearchFlightsAsync(FlightSearchRequest request)
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine("Access token is null or empty.");
                return null;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            string hotelSearchUrl = $"https://test.api.amadeus.com/v2/shopping/flight-offers" +
                                    $"?originLocationCode={request.OriginLocationCode}&destinationLocationCode={request.DestinationLocationCode}" +
                                    $"&departureDate={request.DepartureDate}&adults={request.Adults}";

            string baseUrl = "https://test.api.amadeus.com/v2/shopping/flight-offers";

            var requiredParams = new List<string>
            {
                $"originLocationCode={request.OriginLocationCode}",
                $"destinationLocationCode={request.DestinationLocationCode}",
                $"departureDate={request.DepartureDate}",
                $"adults={request.Adults}"
            };

            // Optional for user to enter
            if (!string.IsNullOrEmpty(request.ReturnDate)) requiredParams.Add($"returnDate={request.ReturnDate}");
            if (!string.IsNullOrEmpty(request.TravelClass)) requiredParams.Add($"travelClass={request.TravelClass}");
            if (request.NonStop.HasValue) requiredParams.Add($"nonStop={request.NonStop.Value.ToString().ToLower()}");
            if (request.MaxPrice.HasValue) requiredParams.Add($"maxPrice={request.MaxPrice.Value}");
            if (!string.IsNullOrEmpty(request.IncludedAirlineCodes)) requiredParams.Add($"includedAirlineCodes={request.IncludedAirlineCodes}");
            if (!string.IsNullOrEmpty(request.ExcludedAirlineCodes)) requiredParams.Add($"excludedAirlineCodes={request.ExcludedAirlineCodes}");


            string flightSearchUrl = $"{baseUrl}?{string.Join("&", requiredParams)}";

            var response = await _httpClient.GetAsync(flightSearchUrl);
            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to fetch hotels. Status: {response.StatusCode}, Response: {errorResponse}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();

            var flightOffer = ExtractFlightInfo.ExtractFlightInfoDetail(content);

            // Return a list of selected flight info
            return new FlightSearchResponse { Data = flightOffer ?? new List<FlightOfferDetail>() };
        }

        


        private async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiration)
            {
                Console.WriteLine("Using cached access token.");
                return _accessToken;
            }

            var tokenUrl = "https://test.api.amadeus.com/v1/security/oauth2/token";
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _apiKey),
                new KeyValuePair<string, string>("client_secret", _apiSecret)
            });

            var response = await _httpClient.PostAsync(tokenUrl, formData);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to retrieve token! Status: {response.StatusCode}");
                Console.WriteLine($"Response: {content}");
                return string.Empty;
            }

            var tokenResponse = JsonSerializer.Deserialize<AccessTokenResponse>(content);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                Console.WriteLine("Invalid token response from Amadeus API!");
                return string.Empty;
            }

            _accessToken = tokenResponse.AccessToken;
            _tokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);

            Console.WriteLine($"New Access Token: {_accessToken} (Expires in {tokenResponse.ExpiresIn} seconds)");
            return _accessToken;
        }

        private class AccessTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }
    }
}
