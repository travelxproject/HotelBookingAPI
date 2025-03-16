using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;
using HotelBookingAPI.Services.HotelServices;
using HotelBookingAPI.Models.HotelModels;


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
        /// <param name="latitude">Latitude</param>
        /// <param name="longitude">Longitude</param>
        /// <param name="checkInDate">Check In Date (yyyy-MM-dd)</param>
        /// <param name="checkOutDate">Check Out Date (yyyy-MM-dd)</param>
        /// <param name="minPrice">Lowest Pirce (Optional)</param>
        /// <param name="maxPrice">Highest Price (Optional)</param>
        /// <param name="rating">Rating (Optional)</param>
        /// <param name="services">Included Services (Optional)</param>
        /// <returns>Searched Result</returns>
        [Produces("application/json")]
        [HttpGet("search")]
        [SwaggerOperation(
            Summary = "Search hotels by location",
            Description = "Find hotels based on conditions")]
        [SwaggerResponse(200, "Success", typeof(HotelSearchResponse))]
        [SwaggerResponse(400, "Invalid Request")]
        [ProducesResponseType(typeof(HotelSearchResponse), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SearchHotels(
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] string checkInDate,
            [FromQuery] string checkOutDate,
            [FromQuery] int numRomms,
            [FromQuery] int numPeople,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] string? rating,
            [FromQuery] string? services)
        {
            var request = new HotelSearchRequest
            {
                Latitude = latitude,
                Longitude = longitude,
                CheckInDate = checkInDate,
                CheckOutDate = checkOutDate,
                NumRomms = numRomms,
                NumPeople = numPeople,
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