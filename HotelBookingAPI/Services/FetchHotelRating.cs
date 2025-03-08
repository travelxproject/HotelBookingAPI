using System.Text.Json;

// Unused Section
public class FetchHotelRating
{
    public static async Task<Dictionary<string, string>?> FetchHotelRatingsAsync(HttpClient _httpClient, Dictionary<string, string> hotelIdIata)
    {
        var hotelRatings = new Dictionary<string, string>();
        int maxIdsPerRequest = 3; 
        int maxRetries = 3;
        int delay = 1000;

        for (int i = 0; i < hotelIdIata.Count; i += maxIdsPerRequest)
        {
            var chunk = hotelIdIata.Keys.Skip(i).Take(maxIdsPerRequest).ToList();
            Console.WriteLine("Fetching ratings for hotels: " + string.Join(", ", chunk));

            string sentimentUrl = $"https://test.api.amadeus.com/v2/e-reputation/hotel-sentiments?hotelIds={string.Join(",", chunk)}";

            for (int retry = 0; retry < maxRetries; retry++)
            {
                var response = await _httpClient.GetAsync(sentimentUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var ratings = ParseSentimentResponse(content);

                    foreach (var kvp in ratings)
                    {
                        hotelRatings[kvp.Key] = kvp.Value;
                    }
                    break; 
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    Console.WriteLine($"Rate limit exceeded. Retrying in {delay / 1000} seconds...");
                    await Task.Delay(delay);
                    //delay += 500;
                }
                else
                {
                    Console.WriteLine($"Failed to fetch hotel ratings. Status: {response.StatusCode}");
                    break;
                }
                
            }
        }
        return hotelRatings;
    }

    private static Dictionary<string, string> ParseSentimentResponse(string jsonResponse)
    {
        var ratings = new Dictionary<string, string>();

        using var jsonDoc = JsonDocument.Parse(jsonResponse);
        if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
        {
            foreach (var hotel in dataElement.EnumerateArray())
            {
                if (hotel.TryGetProperty("hotelId", out var hotelId) &&
                    //hotel.TryGetProperty("sentiment", out var sentiment) &&
                    hotel.TryGetProperty("overallRating", out var overallRating))
                {
                    ratings[hotelId.GetString()] = overallRating.GetDouble().ToString("0.0"); 
                }
                else{
                    ratings[hotelId.GetString()] = "Null";
                }
            }
        }
        return ratings;
    }
}
