using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotelBookingAPI.Models;
using System.Text.Json.Serialization;
using Afonsoft.Amadeus;

namespace HotelBookingAPI.Services
{
    public class AmadeusService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiSecret;

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
                Console.WriteLine("Access token is null or empty.");
                return null;
            }

            string url = $"https://test.api.amadeus.com/v1/reference-data/locations/hotels/by-geocode?latitude={request.Latitude}" +
                            $"&longtitude={request.Longitude}&radius=5&radiusUnit=KM";

            //var url = $"https://test.api.amadeus.com/v1/reference-data/locations/hotels/by-city?cityCode={request.CityCode}";

            //var url = $"https://test.api.amadeus.com/v1/reference-data/locations/hotels/by-city?cityCode={request.CityCode}" +
            //           $"&checkInDate={request.CheckInDate}&checkOutDate={request.CheckOutDate}";

            if (request.MinPrice.HasValue)
            {
                url += $"&minPrice={request.MinPrice}";
            }
            if (request.MaxPrice.HasValue)
            {
                url += $"&maxPrice={request.MaxPrice}";
            }
            if (request.Rating.HasValue)
            {
                url += $"&rating={request.Rating}";
            }
            if (!string.IsNullOrEmpty(request.Services))
            {
                url += $"&services={request.Services}";
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Failed to fetch hotel data from Amadeus API.");
                Console.WriteLine($"Status Code: {response.StatusCode}");
                Console.WriteLine($"Response: {await response.Content.ReadAsStringAsync()}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var hotelOffers = ParseAmadeusResponse(content);

            foreach (var hotel in hotelOffers.Data)
            {
                hotel.IsAvailable = await CheckHotelAvailability(hotel.HotelID, request.CheckInDate, request.CheckOutDate, accessToken);
            }
            return hotelOffers;
        }

        private async Task<bool> CheckHotelAvailability(string hotelID, string checkInDate, string checkOutDate, string accessToken)
        {
            string url = $"https://test.api.amadeus.com/v3/shopping/hotel-offers/by-hotel?hotelID={hotelID}" +
                          $"&checkInDate={checkInDate}&checkOutDate={checkOutDate}";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Failed to fetch hotel data from Amadeus API - CheckHotelAvailability.");
                Console.WriteLine($"Status Code: {response.StatusCode}");
                Console.WriteLine($"Response: {await response.Content.ReadAsStringAsync()}");
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(content);
            return jsonDoc.RootElement.TryGetProperty("offers", out JsonElement offers) && offers.GetArrayLength() > 0;
        }

        private HotelSearchResponse ParseAmadeusResponse(string jsonResponse)
        {
            using var jsonDoc = JsonDocument.Parse(jsonResponse);
            var root = jsonDoc.RootElement;

            var hotelList = new List<HotelOffer>();

            foreach (var hotel in root.GetProperty("data").EnumerateArray())
            {
                hotelList.Add(new HotelOffer
                {
                    HotelID = hotel.GetProperty("hotel").GetProperty("hotelID").GetString(),
                    HotelName = hotel.GetProperty("hotel").GetProperty("name").GetString(),
                    Location = hotel.GetProperty("hotel").GetProperty("address").GetProperty("cityName").GetString(),
                    Price = hotel.GetProperty("hotel")[0].GetProperty("price").GetProperty("total").GetDecimal(),
                    Rating = hotel.GetProperty("hotel")[0].TryGetProperty("rating", out JsonElement rating) ? rating.GetInt32() : 0
                });
            }
            return new HotelSearchResponse { Data = hotelList };
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var tokenUrl = "https://test.api.amadeus.com/v1/security/oauth2/token";
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _apiKey),
                new KeyValuePair<string, string>("client_secret", _apiSecret)
            });

            var response = await _httpClient.PostAsync(tokenUrl, formData);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Failed to get access token.");
                Console.WriteLine($"Status Code: {response.StatusCode}");
                Console.WriteLine($"Response: {await response.Content.ReadAsStringAsync()}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Access token response: " + content);
            try
            {
                var tokenResponse = JsonSerializer.Deserialize<AccessTokenResponse>(content);
                Console.WriteLine("Deserialized token response: " + JsonSerializer.Serialize(tokenResponse)); 
                return tokenResponse?.AccessToken;
            }
            catch (JsonException ex)
            {
                Console.WriteLine("Failed to deserialize access token response.");
                Console.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }

        public class AccessTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }
        }
    }
}