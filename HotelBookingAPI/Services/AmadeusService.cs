using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            var hotelIds = ExtractHotelIds(hotelContent);

            if (!hotelIds.Any())
            {
                Console.WriteLine("No hotels found in the given location.");
                return null;
            }

            var hotelOffers = await FetchHotelOffersAsync(hotelIds, request);

            return new HotelSearchResponse { Data = hotelOffers ?? new List<HotelOffer>() };
        }

        // Fetching the detailed hotel offer info, return as a list. Try maximum 3 times to parse hotel info with 1s sending request to API for rate limit. 
        private async Task<List<HotelOffer>?> FetchHotelOffersAsync(List<string> hotelIds, HotelSearchRequest request)
        {
            var allOffers = new List<HotelOffer>();
            int maxIdsPerRequest = 10; 

            var hotelIdChunks = hotelIds.Chunk(maxIdsPerRequest);

            foreach (var chunk in hotelIdChunks)
            {
                // TODO: use https://developers.amadeus.com/self-service/category/hotels/api-doc/hotel-search/api-reference/v/2.0 previous version to fetch
                // hotel info. Mandatory: cityCode, latitude, longitude & hotelIds
                string offerUrl = $"https://test.api.amadeus.com/v3/shopping/hotel-offers?hotelIds={string.Join(",", chunk)}" +
                                  $"&checkInDate={request.CheckInDate}&checkOutDate={request.CheckOutDate}";

                int maxRetries = 3;
                int delay = 1000;

                for (int i = 0; i < maxRetries; i++)
                {
                    var offerResponse = await _httpClient.GetAsync(offerUrl);
                    Console.WriteLine("offerResponse = " + offerResponse);
                    if (offerResponse.IsSuccessStatusCode)
                    {
                        var offerContent = await offerResponse.Content.ReadAsStringAsync();
                        var offers = ParseAmadeusResponse(offerContent);
                        if (offers != null)
                        {
                            allOffers.AddRange(offers);
                        }
                        break; 
                    }
                    else if (offerResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        Console.WriteLine($"Rate limit exceeded. Retrying in {delay / 1000} seconds...");
                        await Task.Delay(delay);
                        delay *= 2; // Exponential backoff (1s -> 2s -> 4s -> 8s -> 16s)
                    }
                    else
                    {
                        var offerError = await offerResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"Failed to fetch hotel offers. Status: {offerResponse.StatusCode}, Response: {offerError}");
                        break; 
                    }
                }
            }

            return allOffers;
        }

        // Extract hotel ID from a bunch of hotel data
        private List<string> ExtractHotelIds(string jsonResponse)
        {
            var hotelIds = new List<string>();
            using var jsonDoc = JsonDocument.Parse(jsonResponse);

            if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
            {
                foreach (var hotel in dataElement.EnumerateArray())
                {
                    if (hotel.TryGetProperty("hotelId", out var hotelId) && hotelId.ValueKind == JsonValueKind.String)
                    {
                        hotelIds.Add(hotelId.GetString());
                    }
                }
            }
            return hotelIds;
        }

        private List<HotelOffer>? ParseAmadeusResponse(string jsonResponse)
        {
            var hotelList = new List<HotelOffer>();

            using var jsonDoc = JsonDocument.Parse(jsonResponse);
            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine("Key 'data' not found in Amadeus API response.");
                return null;
            }

            foreach (var hotelEntry in dataElement.EnumerateArray())
            {
                if (!hotelEntry.TryGetProperty("hotel", out var hotel) || hotel.ValueKind != JsonValueKind.Object)
                    continue;
                Console.WriteLine("Price: "+ParseHelper.ParseDecimalFromJson(hotelEntry, "offers[0].price.total"));

                var hotelOffer = new HotelOffer
                {
                    HotelID = hotel.TryGetProperty("hotelId", out var hotelId) ? hotelId.GetString() : "Unknown",
                    HotelName = hotel.TryGetProperty("name", out var hotelName) ? hotelName.GetString() : "Unknown",
                    Location = hotel.TryGetProperty("cityCode", out var cityCode) ? cityCode.GetString(): "Unknown",
                    Price = ParseHelper.ParseDecimalFromJson(hotelEntry, "offers[0].price.total"), 
                    Rating = ParseHelper.ParseIntFromJson(hotel, "rating"),
                    IsAvailable = hotelEntry.TryGetProperty("available", out var available) ? available.GetBoolean().ToString():"Unknown"
                };

                hotelList.Add(hotelOffer);
            }

            return hotelList;
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
