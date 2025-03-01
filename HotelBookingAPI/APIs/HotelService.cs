using HotelBookingAPI.APIs.HotelAPIProject.Services;
using HotelBookingAPI.HotelModel;
using Newtonsoft.Json.Linq;

namespace HotelBookingAPI.APIs
{
    public class HotelService : IHotelService
    {
        private readonly AmadeusService _amadeusService;
        private readonly BookingService _bookingService;
        private readonly GooglePlacesService _googlePlacesService;

        public HotelService(AmadeusService amadeusService, BookingService bookingService, GooglePlacesService googlePlacesService)
        {
            _amadeusService = amadeusService;
            _googlePlacesService = googlePlacesService;
            _bookingService = bookingService;
        }

        public async Task<List<HotelInfo>> GetHotelsByActivityAsync(string activity, string location)
        {
            var hotels = new List<HotelInfo>();

            var amadeusHotels = await _amadeusService.GetHotelsAsync(location);
            var bookingHotels = await _bookingService.GetHotelsAsync(location);
            var googleHotels = await _googlePlacesService.GetHotelsAsync(location);

            foreach (var hotel in amadeusHotels)
            {
                hotels.Add(new HotelInfo
                {
                    Name = hotel["hotel"]["name"].ToString(),
                    Address = hotel["hotel"]["address"]["lines"][0].ToString(),
                    Price = hotel["offers"][0]["price"]["total"].ToString(),
                    Rating = hotel["hotel"]["rating"].ToString(),
                    Services = string.Join(", ", hotel["hotel"]["amenities"].ToObject<string[]>())
                });
            }

            return hotels;
        }
    }


}
