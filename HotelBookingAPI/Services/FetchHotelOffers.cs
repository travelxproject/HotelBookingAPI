using HotelBookingAPI.Models;
using HotelBookingAPI.Utilities;
using System.Text.Json;
using static Afonsoft.Amadeus.Resources.HotelOffer;

namespace HotelBookingAPI.Services
{
    public class FetchHotelOffers
    {
        // Fetching the detailed hotel offer info, return as a list. Try maximum 3 times to parse hotel info with 1s sending request to API for rate limit. 
        public static async Task<List<HotelOffer>?> FetchHotelOffersAsync(HttpClient _httpClient, Dictionary<string, string> hotelIdIata, 
            HotelSearchRequest request, Dictionary<string, (double, List<string>)> hotelRating) 
        {
            var allOffers = new List<HotelOffer>();
            int maxIdsPerRequest = 20;

            foreach (var cityGroup in hotelIdIata.GroupBy(h => h.Value))
            {
                string cityCode = cityGroup.Key;
                Console.WriteLine("cityCode: " + cityCode);
                var hotelIds = cityGroup.Select(h => h.Key).ToList();

                for (int i = 0; i < hotelIds.Count; i += maxIdsPerRequest)
                {
                    var chunk = hotelIds.Skip(i).Take(maxIdsPerRequest).ToList();

                    Console.WriteLine("chunks: " + string.Join(", ", chunk));

                    string offerUrl = $"https://test.api.amadeus.com/v3/shopping/hotel-offers?hotelIds={string.Join(",", chunk)}&checkInDate={request.CheckInDate}" +
                                      $"&checkOutDate={request.CheckOutDate}&roomQuantity={request.NumRomms}&adults={request.NumPeople}";

                    int maxRetries = 3;
                    int delay = 1000;

                    for (int retry = 0; retry < maxRetries; retry++)
                    {
                        var offerResponse = await _httpClient.GetAsync(offerUrl);
                        if (offerResponse.IsSuccessStatusCode)
                        {
                            var offerContent = await offerResponse.Content.ReadAsStringAsync();
                            var offers = ParseAmadeusResponse(offerContent, hotelRating);
                            if (offers != null) allOffers.AddRange(offers);
                            break;
                        }
                        else if (offerResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            Console.WriteLine($"Rate limit exceeded. Retrying in {delay / 1000} seconds...");
                            await Task.Delay(delay);
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

        private static List<HotelOffer>? ParseAmadeusResponse(string jsonResponse, Dictionary<string, (double, List<string>)> hotelRatingService)
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
                {
                    Console.WriteLine("Hotel entry is missing...Continue to the next parsing...");
                    continue;
                }

                var HotelID = hotel.TryGetProperty("hotelId", out var hotelId) ? hotelId.GetString() : "Unknown";

                double rating = 0.0;  
                List<string> services = new List<string>();  

                if (hotelRatingService.TryGetValue(HotelID, out var ratingAndServices))
                {
                    rating = ratingAndServices.Item1;  
                    services = ratingAndServices.Item2 ?? new List<string>(); 
                }

                var hotelOffer = new HotelOffer
                {
                    HotelID = HotelID,
                    HotelName = hotel.TryGetProperty("name", out var hotelName) ? hotelName.GetString() : "Unknown",
                    Location = hotel.TryGetProperty("cityCode", out var cityCode) ? cityCode.GetString() : "Unknown",
                    Price = ParseHelper.ParseDecimalFromJson(hotelEntry, "offers[0].price.total"),
                    Currency = hotel.TryGetProperty("offers", out var offers) && offers[0].TryGetProperty("price", out var price) &&
                               price.TryGetProperty("currency", out var currency) ? currency.GetString(): "Unknown",
                    Rating = rating.ToString(),
                    IsAvailable = hotelEntry.TryGetProperty("available", out var available) ? available.GetBoolean().ToString() : "Unknown",
                    Services = services
                };

                hotelList.Add(hotelOffer);
            }

            return hotelList;
        }
    }
}
