using HotelBookingAPI.Models.FlightModels;
using System.Net.Http.Json;
using System.Text.Json;

namespace HotelBookingAPI.Services.FlightServices
{
    public class ExtractFlightInfo
    {
        public static List<FlightOfferDetail> ExtractFlightInfoDetail(string jsonResponse) {

            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                JsonElement root = doc.RootElement;
                if (!root.TryGetProperty("data", out JsonElement dataArray)) return new List<FlightOfferDetail>();

                List<FlightOfferDetail> flightOffers = new List<FlightOfferDetail>();

                foreach (JsonElement flightOffer in dataArray.EnumerateArray())
                {
                    if (!flightOffer.TryGetProperty("itineraries", out JsonElement itinerariesArray)) continue;
                    if (!flightOffer.TryGetProperty("price", out JsonElement priceElement)) continue;
                    if (!flightOffer.TryGetProperty("travelerPricings", out JsonElement travelerPricingsArray)) continue;

                    // Extract price and currency
                    decimal totalPrice = priceElement.GetProperty("total").GetDecimal();
                    string currency = priceElement.GetProperty("currency").GetString();

                    // Get baggage information
                    string checkedBags = "N/A";
                    string cabinBags = "N/A";
                    var firstTravelerPricing = travelerPricingsArray.EnumerateArray().FirstOrDefault();
                    if (firstTravelerPricing.TryGetProperty("fareDetailsBySegment", out JsonElement fareDetails))
                    {
                        foreach (var segment in fareDetails.EnumerateArray())
                        {
                            if (segment.TryGetProperty("includedCheckedBags", out JsonElement checkedBagsElement) &&
                                checkedBagsElement.TryGetProperty("weight", out JsonElement weightElement) &&
                                checkedBagsElement.TryGetProperty("weightUnit", out JsonElement weightUnitElement))
                            {
                                checkedBags = $"{weightElement.GetInt32()} {weightUnitElement.GetString()}";
                            }

                            if (segment.TryGetProperty("includedCabinBags", out JsonElement cabinBagsElement) &&
                                cabinBagsElement.TryGetProperty("weight", out JsonElement cabinWeightElement) &&
                                cabinBagsElement.TryGetProperty("weightUnit", out JsonElement cabinWeightUnitElement))
                            {
                                cabinBags = $"{cabinWeightElement.GetInt32()} {cabinWeightUnitElement.GetString()}";
                            }

                            break;
                        }
                    }

                    // Process each itinerary
                    foreach (JsonElement itinerary in itinerariesArray.EnumerateArray())
                    {
                        if (!itinerary.TryGetProperty("segments", out JsonElement segmentsArray)) continue;

                        var segments = segmentsArray.EnumerateArray().ToList();
                        var firstSegment = segments.First();
                        var lastSegment = segments.Last();

                        // Ensure departure and arrival data comes from the first and last segment in the entire itinerary
                        string departureIataCode = firstSegment.GetProperty("departure").GetProperty("iataCode").GetString();
                        string departureTerminal = firstSegment.GetProperty("departure").TryGetProperty("terminal", out JsonElement depTerminal) ? depTerminal.GetString() : "N/A";
                        string departureTime = firstSegment.GetProperty("departure").GetProperty("at").GetString();

                        string arrivalIataCode = lastSegment.GetProperty("arrival").GetProperty("iataCode").GetString();
                        string arrivalTerminal = lastSegment.GetProperty("arrival").TryGetProperty("terminal", out JsonElement arrTerminal) ? arrTerminal.GetString() : "N/A";
                        string arrivalTime = lastSegment.GetProperty("arrival").GetProperty("at").GetString();

                        string duration = itinerary.GetProperty("duration").GetString();
                        int numberOfStops = segments.Count - 1;

                        List<string> connectionCities = new List<string>();
                        if (numberOfStops > 0)
                        {
                            for (int i = 0; i < segments.Count - 1; i++)
                            {
                                string connectionCity = segments[i].GetProperty("arrival").GetProperty("iataCode").GetString();
                                connectionCities.Add(connectionCity);
                            }
                        }

                        flightOffers.Add(new FlightOfferDetail
                        {
                            DepartureIataCode = departureIataCode,
                            DepartureTerminal = departureTerminal,
                            DepartureTime = departureTime,
                            ArrivalIataCode = arrivalIataCode,
                            ArrivalTerminal = arrivalTerminal,
                            ArrivalTime = arrivalTime,
                            Price = totalPrice,
                            Currency = currency,
                            Duration = duration,
                            NumberOfStops = numberOfStops,
                            ConnectionCity = string.Join(",", connectionCities),
                            CheckedBags = checkedBags,
                            CabinBags = cabinBags,
                            Amenities = new List<string>() 
                        });
                    }
                }

                return flightOffers;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing flight data: {ex.Message}");
                return new List<FlightOfferDetail>();
            }
        }

    }
}
