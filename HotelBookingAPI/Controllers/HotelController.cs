using Microsoft.AspNetCore.Mvc;
using HotelBookingAPI.Models;
using HotelBookingAPI.Services;
using HotelBookingAPI.APIs.HotelAPIProject.Services;

namespace HotelBookingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HotelController : ControllerBase
    {
        private readonly AmadeusService _amadeusService;

        public HotelController(AmadeusService amadeusService)
        {
            _amadeusService = amadeusService;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchHotels(
            [FromQuery] string cityCode,
            [FromQuery] string checkInDate,
            [FromQuery] string checkOutDate,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] int? rating,
            [FromQuery] string services)
        {
            var request = new HotelSearchRequest
            {
                CityCode = cityCode,
                CheckInDate = checkInDate,
                CheckOutDate = checkOutDate,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Rating = rating,
                Services = services
            };

            var response = await _amadeusService.SearchHotelsAsync(request);

            if (response == null)
            {
                return BadRequest("Failed to fetch hotel data from Amadeus API.");
            }

            return Ok(response);
        }
    }
}