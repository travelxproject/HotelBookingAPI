using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HotelBookingAPI.APIs
{
    public class GooglePlacesService
    {
        private static HttpClient _httpClient = new HttpClient();
        private static string _apiKey;

        public static void Initialize(string apiKey)
        {
            _apiKey = apiKey;
        }

        public static async Task<Dictionary<string, (double, List<string>)>> GetHotelDetailsAsync(Dictionary<string, string> hotelIdName)
        {
            var hotelDetails = new Dictionary<string, (double, List<string>)>();
            int maxRetries = 3;
            int delay = 1000;

            foreach (var (hotelId, hotelName) in hotelIdName)
            {
                string? placeId = await GetPlaceIdAsync(hotelName);
                if (placeId == null)
                {
                    Console.WriteLine($"Could not find Place ID for {hotelName}");
                    continue;
                }

                string detailsUrl = $"https://maps.googleapis.com/maps/api/place/details/json" +
                                    $"?place_id={placeId}&fields=name,rating,types&key={_apiKey}";

                for (int retry = 0; retry < maxRetries; retry++)
                {
                    var response = await _httpClient.GetAsync(detailsUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        using var jsonDoc = JsonDocument.Parse(content);

                        if (jsonDoc.RootElement.TryGetProperty("result", out var result))
                        {
                            double rating = result.TryGetProperty("rating", out var ratingElement) ? ratingElement.GetDouble() : 0.0;

                            List<string> amenities = new List<string>();
                            if (result.TryGetProperty("types", out var typesElement) && typesElement.ValueKind == JsonValueKind.Array)
                            {
                                amenities = typesElement.EnumerateArray().Select(x => x.GetString()).Where(x => x != null).ToList()!;
                            }

                            hotelDetails[hotelId] = (rating, amenities);
                        }
                        break;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        Console.WriteLine($"Rate limit exceeded. Retrying in {delay / 1000} seconds...");
                        await Task.Delay(delay);
                    }
                    else
                    {
                        Console.WriteLine($"Failed to fetch hotel details. Status: {response.StatusCode}");
                        break;
                    }
                }
            }
            return hotelDetails;
        }

        private static async Task<string?> GetPlaceIdAsync(string hotelName)
        {
            string searchUrl = $"https://maps.googleapis.com/maps/api/place/findplacefromtext/json" +
                               $"?input={Uri.EscapeDataString(hotelName)}&inputtype=textquery&fields=place_id&key={_apiKey}";

            var response = await _httpClient.GetAsync(searchUrl);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                return candidates[0].GetProperty("place_id").GetString();
            }
            return null;
        }
    }

}
