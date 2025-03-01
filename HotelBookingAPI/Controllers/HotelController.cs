using HotelBookingAPI.APIs;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotelBookingAPI.HotelModel;

namespace HotelBookingAPI.Controllers
{

    namespace HotelAPIProject.Controllers
    {
        [ApiController]
        [Route("api/[controller]")]
        public class HotelController : ControllerBase
        {
            private readonly IHotelService _hotelService;

            public HotelController(IHotelService hotelService)
            {
                _hotelService = hotelService;
            }

            [HttpGet("search")]
            public async Task<IActionResult> GetHotels([FromQuery] string activity, [FromQuery] string location)
            {
                if (string.IsNullOrEmpty(activity) || string.IsNullOrEmpty(location))
                {
                    return BadRequest("Activity and location parameters are required.");
                }

                var hotels = await _hotelService.GetHotelsByActivityAsync(activity, location);
                return Ok(hotels);
            }
        }
    }


}
