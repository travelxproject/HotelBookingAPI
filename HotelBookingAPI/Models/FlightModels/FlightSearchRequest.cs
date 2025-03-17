namespace HotelBookingAPI.Models.FlightModels
{
    public class FlightSearchRequest
    {
        public string OriginLocationCode { get; set; }
        public string DestinationLocationCode { get; set; }
        public string DepartureDate { get; set; }
        public string? ReturnDate { get; set; }
        public int Adults { get; set; }
        public string? TravelClass { get; set; }
        public bool? NonStop { get; set; }
        public double? MaxPrice { get; set; }
        public string? IncludedAirlineCodes { get; set; }
        public string? ExcludedAirlineCodes { get; set; }
    } 
}