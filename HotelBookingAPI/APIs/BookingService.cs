using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace HotelBookingAPI.APIs
{

    namespace HotelAPIProject.Services
    {
        public class BookingService
        {
            private readonly HttpClient _httpClient;
            private readonly IConfiguration _configuration;

            public BookingService(HttpClient httpClient, IConfiguration configuration)
            {
                _httpClient = httpClient;
                _configuration = configuration;
            }

            public async Task<JArray> GetHotelsAsync(string location)
            {
                var apiKey = _configuration["ApiKeys:BookingApiKey"];
                var url = $"https://api.booking.com/v1/hotels?location={location}&apiKey={apiKey}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
                return (JArray)jsonResponse["hotels"];
            }
        }
    }


}
