namespace HotelBookingAPI.Models.HotelModels
{
    public class HotelSearchRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string CheckInDate { get; set; }
        public string CheckOutDate { get; set; }
        public int NumRomms { get; set; }
        public int NumPeople { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Rating { get; set; }
        public string? Services { get; set; }
    }
}