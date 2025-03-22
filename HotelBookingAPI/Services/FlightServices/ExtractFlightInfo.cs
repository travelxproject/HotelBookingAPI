using HotelBookingAPI.Models.FlightModels;
using System;
using System.Collections.Generic;
using System.Linq;
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

                    decimal totalPrice = priceElement.GetProperty("total").GetDecimal();
                    string currency = priceElement.GetProperty("currency").GetString();

                    foreach (JsonElement itinerary in itinerariesArray.EnumerateArray())
                    {
                        if (!itinerary.TryGetProperty("segments", out JsonElement segmentsArray)) continue;

                        var segments = segmentsArray.EnumerateArray().ToList();
                        var firstSegment = segments.First();
                        var lastSegment = segments.Last();

                        string departureIataCode = firstSegment.GetProperty("departure").GetProperty("iataCode").GetString();
                        string departureTerminal = firstSegment.GetProperty("departure").TryGetProperty("terminal", out JsonElement depTerminal) ? depTerminal.GetString() : "N/A";
                        string departureTime = firstSegment.GetProperty("departure").GetProperty("at").GetString();

                        string arrivalIataCode = lastSegment.GetProperty("arrival").GetProperty("iataCode").GetString();
                        string arrivalTerminal = lastSegment.GetProperty("arrival").TryGetProperty("terminal", out JsonElement arrTerminal) ? arrTerminal.GetString() : "N/A";
                        string arrivalTime = lastSegment.GetProperty("arrival").GetProperty("at").GetString();

                        string duration = itinerary.GetProperty("duration").GetString();
                        int numberOfStops = segments.Count - 1;

                        // Connection flight Info
                        Dictionary<string, object> connectionInfo = new Dictionary<string, object>();
                        List<string> connectionCities = new List<string>();
                        List<string> connectionDurations = new List<string>();
                        List<string> checkedBagsList = new List<string>();
                        List<string> cabinBagsList = new List<string>();

                        for (int i = 0; i < segments.Count - 1; i++)
                        {
                            var segment = segments[i];
                            var nextSegment = segments[i + 1];

                            string connectionCity = segment.GetProperty("arrival").GetProperty("iataCode").GetString();
                            string layoverDuration = CalculateLayoverDuration(segment.GetProperty("arrival").GetProperty("at").GetString(),
                                                                              nextSegment.GetProperty("departure").GetProperty("at").GetString());

                            connectionCities.Add(connectionCity);
                            connectionDurations.Add(layoverDuration);
                        }

                        var firstTravelerPricing = travelerPricingsArray.EnumerateArray().FirstOrDefault();
                        if (firstTravelerPricing.TryGetProperty("fareDetailsBySegment", out JsonElement fareDetails))
                        {
                            foreach (var segment in fareDetails.EnumerateArray())
                            {
                                if (segment.TryGetProperty("includedCheckedBags", out JsonElement checkedBagsElement) &&
                                    checkedBagsElement.TryGetProperty("weight", out JsonElement weightElement) &&
                                    checkedBagsElement.TryGetProperty("weightUnit", out JsonElement weightUnitElement))
                                {
                                    checkedBagsList.Add($"{weightElement.GetInt32()}{weightUnitElement.GetString()}");
                                }
                                else
                                {
                                    checkedBagsList.Add("N/A");
                                }

                                if (segment.TryGetProperty("includedCabinBags", out JsonElement cabinBagsElement) &&
                                    cabinBagsElement.TryGetProperty("weight", out JsonElement cabinWeightElement) &&
                                    cabinBagsElement.TryGetProperty("weightUnit", out JsonElement cabinWeightUnitElement))
                                {
                                    cabinBagsList.Add($"{cabinWeightElement.GetInt32()}{cabinWeightUnitElement.GetString()}");
                                }
                                else
                                {
                                    cabinBagsList.Add("N/A");
                                }
                            }
                        }

                        connectionInfo["NumberOfStops"] = numberOfStops;
                        connectionInfo["ConnectionCity"] = numberOfStops > 0 ? string.Join(" → ", connectionCities) : "Non-stop";
                        connectionInfo["CheckedBags"] = string.Join("; ", checkedBagsList);
                        connectionInfo["CabinBags"] = string.Join("; ", cabinBagsList);
                        connectionInfo["Amenities"] = "N/A"; // Placeholder as amenities aren't present in the response.
                        connectionInfo["ConnectionDuration"] = string.Join("; ", connectionDurations);

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
                            ConnectionInfo = connectionInfo
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
