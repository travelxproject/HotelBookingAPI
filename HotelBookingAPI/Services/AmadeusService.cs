using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotelBookingAPI.Models;
using static Afonsoft.Amadeus.Resources.HotelOffer;

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
        }

        public async Task<HotelSearchResponse> SearchHotelsAsync(HotelSearchRequest request)
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine("❌ Access token is null or empty.");
                return null;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            string hotelSearchUrl = $"https://test.api.amadeus.com/v1/reference-data/locations/hotels/by-geocode?latitude={request.Latitude}" +
                                    $"&longitude={request.Longitude}&radius=5&radiusUnit=KM";

            var hotelResponse = await _httpClient.GetAsync(hotelSearchUrl);


            if (!hotelResponse.IsSuccessStatusCode)
            {
                var errorResponse = await hotelResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to fetch hotels. Status: {hotelResponse.StatusCode}, Response: {errorResponse}");
                return null;
            }

            var hotelContent = await hotelResponse.Content.ReadAsStringAsync();
            var hotelIds = ExtractHotelIds(hotelContent);

            if (!hotelIds.Any())
            {
                Console.WriteLine("No hotels found in the given location.");
                return null;
            }

            // TODO: need to iterate teach hotelID 
            string offerUrl = $"https://test.api.amadeus.com/v3/shopping/hotel-offers?hotelIds={string.Join(",", hotelIds)}" +
                              $"&checkInDate={request.CheckInDate}&checkOutDate={request.CheckOutDate}";

            var offerResponse = await _httpClient.GetAsync(offerUrl);

            if (!offerResponse.IsSuccessStatusCode)
            {
                var offerError = await offerResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to fetch hotel offers. Status: {offerResponse.StatusCode}, Response: {offerError}");
                return null;
            }

            var offerContent = await offerResponse.Content.ReadAsStringAsync();
            return ParseAmadeusResponse(offerContent);
        }

        private List<string> ExtractHotelIds(string jsonResponse)
        {
            var hotelIds = new List<string>();
            using var jsonDoc = JsonDocument.Parse(jsonResponse);
            
            if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
            {
                Console.WriteLine("dataElement: " + dataElement);
                foreach (var hotel in dataElement.EnumerateArray())
                {
                    if (hotel.TryGetProperty("hotelId", out var hotelId))
                    {
                        hotelIds.Add(hotelId.GetString());
                    }
                }
            }
            return hotelIds;
        }

        private HotelSearchResponse ParseAmadeusResponse(string jsonResponse)
        {
            using var jsonDoc = JsonDocument.Parse(jsonResponse);
            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
            {
                throw new KeyNotFoundException("Key 'data' not found in Amadeus API response.");
            }

            var hotelList = new List<HotelOffer>();
            foreach (var hotelEntry in dataElement.EnumerateArray())
            {
                var hotel = hotelEntry.GetProperty("hotel");
                hotelList.Add(new HotelOffer
                {
                    HotelID = hotel.GetProperty("hotelId").GetString(),
                    HotelName = hotel.GetProperty("name").GetString(),
                    Location = hotel.GetProperty("address").GetProperty("cityName").GetString(),
                    Price = hotelEntry.TryGetProperty("offers", out var offers) && offers.GetArrayLength() > 0
                        ? offers[0].GetProperty("price").GetProperty("total").GetDecimal()
                        : 0.0m,
                    Rating = hotel.TryGetProperty("rating", out var rating) ? rating.GetInt32() : 0
                });
            }
            return new HotelSearchResponse { Data = hotelList };
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
