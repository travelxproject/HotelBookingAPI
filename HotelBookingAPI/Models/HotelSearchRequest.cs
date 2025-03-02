namespace HotelBookingAPI.Models
{
    public class HotelSearchRequest
    {
        public string CityCode { get; set; }
        public string CheckInDate { get; set; }
        public string CheckOutDate { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? Rating { get; set; }
        public string Services { get; set; }
    }
}