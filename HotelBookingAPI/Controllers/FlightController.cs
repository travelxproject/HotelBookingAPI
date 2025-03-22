using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using HotelBookingAPI.Services.FlightServices;
using HotelBookingAPI.Models.FlightModels;
using System.Threading.Tasks;

namespace HotelBookingAPI.Controllers
{
    [ApiController]
    [Route("api/flights")]
    public class FlightController : ControllerBase
    {
        private readonly AmadeusFlightService _amadeusFlightService;

        public FlightController(AmadeusFlightService amadeusFlightService)
        {
            _amadeusFlightService = amadeusFlightService;
        }

        /// <param name="originLocationCode">Origin airport code (e.g., MEL)</param>
        /// <param name="destinationLocationCode">Destination airport code (e.g., SIN)</param>
        /// <param name="departureDate">Departure Date (yyyy-MM-dd)</param>
        /// <param name="returnDate">Return Date (Optional, yyyy-MM-dd)</param>
        /// <param name="adults">Number of Adult Passengers</param>
        /// <param name="travelClass">Travel Class (Optional: Economy, Business, First)</param>
        /// <param name="nonStop">Non-stop flights only (Optional: true/false)</param>
        /// <param name="maxPrice">Maximum price (Optional)</param>
        /// <param name="includedAirlineCodes">Filter by specific airlines (Optional, comma-separated codes)</param>
        /// <param name="excludedAirlineCodes">Exclude specific airlines (Optional, comma-separated codes)</param>
        /// <returns>Flight search results</returns>
        [Produces("application/json")]
        [HttpGet("search")]
        [SwaggerOperation(
            Summary = "Search flights",
            Description = "Find flights based on criteria"
        )]
        [SwaggerResponse(200, "Success", typeof(FlightSearchResponse))]
        [SwaggerResponse(400, "Invalid Request")]
        [ProducesResponseType(typeof(FlightSearchResponse), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SearchFlights(
            [FromQuery] string originLocationCode,
            [FromQuery] string destinationLocationCode,
            [FromQuery] string departureDate,
            [FromQuery] string? returnDate,
            [FromQuery] int adults,
            [FromQuery] string? travelClass,
            [FromQuery] bool? nonStop,
            [FromQuery] double? maxPrice,
            [FromQuery] string? includedAirlineCodes,
            [FromQuery] string? excludedAirlineCodes)
        {
            if (string.IsNullOrWhiteSpace(originLocationCode) || string.IsNullOrWhiteSpace(destinationLocationCode) || string.IsNullOrWhiteSpace(departureDate))
            {
                return BadRequest("Origin, destination, and departure date are required.");
            }

            if (!DateTime.TryParse(departureDate, out _))
            {
                return BadRequest("Invalid departure date format. Expected format: yyyy-MM-dd.");
            }

            if (!string.IsNullOrEmpty(returnDate) && !DateTime.TryParse(returnDate, out _))
            {
                return BadRequest("Invalid return date format. Expected format: yyyy-MM-dd.");
            }

            var request = new FlightSearchRequest
            {
                OriginLocationCode = originLocationCode,
                DestinationLocationCode = destinationLocationCode,
                DepartureDate = departureDate,
                ReturnDate = returnDate,
                Adults = adults,
                TravelClass = travelClass,
                NonStop = nonStop,
                MaxPrice = maxPrice,
                IncludedAirlineCodes = includedAirlineCodes,
                ExcludedAirlineCodes = excludedAirlineCodes
            };

            var response = await _amadeusFlightService.SearchFlightsAsync(request);

            if (response == null)
            {
                return BadRequest("Failed to fetch flight data from Amadeus API.");
            }

            return Ok(response);
        }
    }
}
