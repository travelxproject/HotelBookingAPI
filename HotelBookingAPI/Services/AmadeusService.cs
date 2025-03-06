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

            var hotelIdIata = ExtractHotelIds(hotelContent);

            if (!hotelIdIata.Any())
            {
                Console.WriteLine("No hotels found in the given location.");
                return null;
            }

            var hotelOffers = await FetchHotelOffersAsync(hotelIdIata, request);

            return new HotelSearchResponse { Data = hotelOffers ?? new List<HotelOffer>() };
        }

        // Fetching the detailed hotel offer info, return as a list. Try maximum 3 times to parse hotel info with 1s sending request to API for rate limit. 
        private async Task<List<HotelOffer>?> FetchHotelOffersAsync(Dictionary<string, string> hotelIdIata, HotelSearchRequest request)
        {
            var allOffers = new List<HotelOffer>();
            int maxIdsPerRequest = 10;

            foreach (var cityGroup in hotelIdIata.GroupBy(h => h.Value))
            {
                string cityCode = cityGroup.Key;
                var hotelIds = cityGroup.Select(h => h.Key).ToList(); 

                for (int i = 0; i < hotelIds.Count; i += maxIdsPerRequest)
                {
                    var chunk = hotelIds.Skip(i).Take(maxIdsPerRequest).ToList(); 

                    string offerUrl = $"https://test.api.amadeus.com/v2/shopping/hotel-offers?hotelIds={string.Join(",", chunk)}&cityCode={cityCode}" +
                                      $"&checkInDate={request.CheckInDate}&checkOutDate={request.CheckOutDate}&roomQuantity={request.NumRomms}&adults={request.NumPeople}";

                    int maxRetries = 3;
                    int delay = 1000;

                    for (int retry = 0; retry < maxRetries; retry++)
                    {
                        var offerResponse = await _httpClient.GetAsync(offerUrl);
                        if (offerResponse.IsSuccessStatusCode)
                        {
                            var offerContent = await offerResponse.Content.ReadAsStringAsync();
                            var offers = ParseAmadeusResponse(offerContent);
                            if (offers != null) allOffers.AddRange(offers);
                            break;
                        }
                        else if (offerResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            Console.WriteLine($"Rate limit exceeded. Retrying in {delay / 1000} seconds...");
                            await Task.Delay(delay);
                            delay *= 2;
                        }
                        else
                        {
                            Console.WriteLine($"Failed to fetch hotel offers. Status: {offerResponse.StatusCode}");
                            break;
                        }
                    }
                }
            }

            return allOffers;
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
                    Currency = hotel.TryGetProperty("currency", out var currency) ? currency.GetString() : "Unknown",
                    Rating = ParseHelper.ParseIntFromJson(hotel, "rating"),
                    IsAvailable = hotelEntry.TryGetProperty("available", out var available) ? available.GetBoolean().ToString():"Unknown",
                    Services = hotelEntry.TryGetProperty("amenities", out var amenities) && amenities.ValueKind == JsonValueKind.Array
                            ? amenities.EnumerateArray().Select(a => a.GetString()).Where(s => !string.IsNullOrEmpty(s)).ToList()
                            : new List<string>()
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
