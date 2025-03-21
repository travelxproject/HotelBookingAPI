using HotelBookingAPI.Models.FlightModels;
using System.Text.Json;

namespace HotelBookingAPI.Services.FlightServices
{
    public class ExtractFlightInfo
    {
        public static List<FlightOfferDetail> ExtractFlightInfoDetail(string jsonResponse)
        {
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

                    // Extract total price and currency
                    decimal totalPrice = priceElement.GetProperty("total").GetDecimal();
                    string currency = priceElement.GetProperty("currency").GetString();

                    // Extract baggage information from the first traveler
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

                        // Extract main flight information
                        string departureIataCode = firstSegment.GetProperty("departure").GetProperty("iataCode").GetString();
                        string departureTerminal = firstSegment.GetProperty("departure").TryGetProperty("terminal", out JsonElement depTerminal) ? depTerminal.GetString() : "N/A";
                        string departureTime = firstSegment.GetProperty("departure").GetProperty("at").GetString();

                        string arrivalIataCode = lastSegment.GetProperty("arrival").GetProperty("iataCode").GetString();
                        string arrivalTerminal = lastSegment.GetProperty("arrival").TryGetProperty("terminal", out JsonElement arrTerminal) ? arrTerminal.GetString() : "N/A";
                        string arrivalTime = lastSegment.GetProperty("arrival").GetProperty("at").GetString();

                        string duration = itinerary.GetProperty("duration").GetString();
                        int numberOfStops = segments.Count - 1;

                        // Extract detailed connection information - TODO 
                        List<string> connectionDetails = new List<string>();
                        for (int i = 0; i < segments.Count - 1; i++)
                        {
                            var segment = segments[i];
                            var nextSegment = segments[i + 1];

                            string connectionCity = segment.GetProperty("arrival").GetProperty("iataCode").GetString();
                            string layoverDuration = CalculateLayoverDuration(segment.GetProperty("arrival").GetProperty("at").GetString(),
                                                                              nextSegment.GetProperty("departure").GetProperty("at").GetString());

                            connectionDetails.Add($"{connectionCity} ({layoverDuration})");
                        }

                        // Extract flight segments with carrier and flight number
                        List<string> segmentDetails = new List<string>();
                        foreach (var segment in segments)
                        {
                            string carrierCode = segment.GetProperty("carrierCode").GetString();
                            string flightNumber = segment.GetProperty("number").GetString();
                            string segmentDuration = segment.GetProperty("duration").GetString();

                            segmentDetails.Add($"{carrierCode}{flightNumber} ({segmentDuration})");
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
                            ConnectionCity = connectionDetails.Count > 0 ? string.Join(" → ", connectionDetails) : "Non-stop",
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

        private static string CalculateLayoverDuration(string arrivalTime, string departureTime)
        {
            if (DateTime.TryParse(arrivalTime, out DateTime arrival) &&
                DateTime.TryParse(departureTime, out DateTime departure))
            {
                TimeSpan layover = departure - arrival;
                return $"{(int)layover.TotalHours}h {layover.Minutes}m";
            }
            return "N/A";
        }
    }
}
