using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotelBookingAPI.APIs;
using HotelBookingAPI.Models;
using HotelBookingAPI.Utilities;

namespace HotelBookingAPI.Services
{
    public class AmadeusService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiSecret;

        private string? _accessToken;
        private DateTime _tokenExpiration;

        public AmadeusService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["ApiKeys:Amadeus"];
            _apiSecret = configuration["ApiKeys:AmadeusClientSecret"];

            // Initialize the Google API key before calling the method
            GooglePlacesService.Initialize(configuration["ApiKeys:GooglePlacesApiKey"]);
        }

        // General entry point to enter longitude and latitude to search hotels 
        public async Task<HotelSearchResponse> SearchHotelsAsync(HotelSearchRequest request)
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine("Access token is null or empty.");
                return null;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            string hotelSearchUrl = $"https://test.api.amadeus.com/v1/reference-data/locations/hotels/by-geocode" +
                                    $"?latitude={request.Latitude}&longitude={request.Longitude}&radius=3&radiusUnit=KM";

            var hotelResponse = await _httpClient.GetAsync(hotelSearchUrl);
            if (!hotelResponse.IsSuccessStatusCode)
            {
                var errorResponse = await hotelResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to fetch hotels. Status: {hotelResponse.StatusCode}, Response: {errorResponse}");
                return null;
            }

            var hotelContent = await hotelResponse.Content.ReadAsStringAsync();

            var hotelIdIata = ExtractHotelIds(hotelContent);

            if (!hotelIdIata.Any())
            {
                Console.WriteLine("No hotels found in the given location.");
                return null;
            }

            var hotelIdName = ExtractHotelNames(hotelContent);

            if (!hotelIdName.Any())
            {
                Console.WriteLine("No hotels found in the given location.");
            }

            var hotelRatingService = await GooglePlacesService.GetHotelDetailsAsync(hotelIdName);
           
            var hotelOffers = await FetchHotelOffers.FetchHotelOffersAsync(_httpClient, hotelIdIata, request, hotelRatingService);

            return new HotelSearchResponse { Data = hotelOffers ?? new List<HotelOffer>() };
        }


        // Extract hotel ID from a bunch of hotel data
        private Dictionary<string, string> ExtractHotelIds(string jsonResponse)
        {
            var hotelIdIata = new Dictionary<string, string>();
            using var jsonDoc = JsonDocument.Parse(jsonResponse);

            if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var hotel in dataElement.EnumerateArray())
                {
                    if (hotel.TryGetProperty("hotelId", out var hotelId) && hotelId.ValueKind == JsonValueKind.String)
                    {
                        string hotelIdStr = hotelId.GetString();
                        string cityCodeStr = "Unknown"; 

                        if (hotel.TryGetProperty("iataCode", out var iataCode) && iataCode.ValueKind == JsonValueKind.String)
                        {
                            cityCodeStr = iataCode.GetString();
                        }

                        hotelIdIata[hotelIdStr] = cityCodeStr;
                    }
                }
            }
            return hotelIdIata;
        }

        private Dictionary<string, string> ExtractHotelNames(string jsonResponse)
        {
            var hotelIdName = new Dictionary<string, string>();
            using var jsonDoc = JsonDocument.Parse(jsonResponse);

            if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var hotel in dataElement.EnumerateArray())
                {
                    if (hotel.TryGetProperty("hotelId", out var hotelId) && hotelId.ValueKind == JsonValueKind.String)
                    {
                        string hotelIdStr = hotelId.GetString();
                        string name = "Unknown";

                        if (hotel.TryGetProperty("name", out var nameCode) && nameCode.ValueKind == JsonValueKind.String)
                        {
                            name = nameCode.GetString();
                        }

                        hotelIdName[hotelIdStr] = name;
                    }
                }
            }
            return hotelIdName;
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
