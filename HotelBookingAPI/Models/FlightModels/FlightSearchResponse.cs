using System.Collections.Generic;

namespace HotelBookingAPI.Models.FlightModels
{
    public class FlightSearchResponse
    {
        public List<FlightOfferDetail> Data { get; set; }
    }

    public class FlightOfferDetail
    {
        public string DepartureIataCode { get; set; }
        public string DepartureTerminal { get; set; }
        public string DepartureTime { get; set; }
        public string ArrivalIataCode { get; set; }
        public string ArrivalTerminal { get; set; }
        public string ArrivalTime { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
        public string Duration { get; set; }
        public int NumberOfStops { get; set; }
        public string CheckedBags { get; set; }
        public string CabinBags { get; set; }
        public List<string>? Amenities { get; set; }
    }
}