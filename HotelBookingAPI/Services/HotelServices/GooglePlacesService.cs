using HotelBookingAPI.Database;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HotelBookingAPI.Services.HotelServices
{
    public class GooglePlacesService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly HotelRepositoryDB _hotelRepositoryDB;

        public GooglePlacesService(HttpClient httpClient, IConfiguration configuration, HotelRepositoryDB hotelRepositoryDB)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GooglePlaces:ApiKey"];
            _hotelRepositoryDB = hotelRepositoryDB;
        }

        public async Task ProcessHotelsAsync()
        {
            var hotels = await _hotelRepositoryDB.GetHotelsWithoutRatingsAsync(); // List of Hotel names
            if (!hotels.Any()) return;

            // Step 1: Fetch all Place IDs in parallel
            var placeIdResults = await Task.WhenAll(hotels.Select(async h => (h.HotelId, await GetPlaceIdAsync(h.HotelName))));
            var hotelPlaceMap = placeIdResults.Where(x => x.Item2 != null)
                                              .ToDictionary(x => x.HotelId, x => x.Item2!);

            // Step 2: Fetch all hotel details in parallel
            var detailsResults = await Task.WhenAll(hotelPlaceMap.Select(async h => (h.Key, await FetchHotelDetailsFromGoogle(h.Value))));

            // Step 3: Update DB with fetched details
            var hotelDetails = detailsResults.Where(x => x.Item2 != null)
                                             .ToDictionary(x => x.Key, x => x.Item2!.Value);
            if (hotelDetails.Any())
            {
                await _hotelRepositoryDB.SaveHotelDetailsAsync(hotelDetails);
            }
        }

        private async Task<(double rating, List<string> amenities)?> FetchHotelDetailsFromGoogle(string placeId)
        {
            string url = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={placeId}&fields=rating,types&key={_apiKey}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(content);

            if (!jsonDoc.RootElement.TryGetProperty("result", out var result)) return null;

            double rating = result.TryGetProperty("rating", out var r) ? r.GetDouble() : 0.0;
            var amenities = result.TryGetProperty("types", out var types) && types.ValueKind == JsonValueKind.Array
                ? types.EnumerateArray().Select(x => x.GetString()).Where(x => x != null).ToList()!
                : new List<string>();

            return (rating, amenities);
        }

        private async Task<string?> GetPlaceIdAsync(string hotelName)
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
