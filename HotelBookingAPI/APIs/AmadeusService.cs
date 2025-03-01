using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace HotelBookingAPI.APIs
{

    namespace HotelAPIProject.Services
    {
        public class AmadeusService
        {
            private readonly HttpClient _httpClient;
            private readonly IConfiguration _configuration;
            private string _accessToken;

            public AmadeusService(HttpClient httpClient, IConfiguration configuration)
            {
                _httpClient = httpClient;
                _configuration = configuration;
            }

            private async Task<string> GetAccessTokenAsync()
            {
                if (!string.IsNullOrEmpty(_accessToken)) return _accessToken;

                var clientId = _configuration["ApiKeys:AmadeusClientId"];
                var clientSecret = _configuration["ApiKeys:AmadeusClientSecret"];

                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.api.amadeus.com/v1/security/oauth2/token")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "client_id", clientId },
                    { "client_secret", clientSecret }
                })
                };

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
                _accessToken = jsonResponse["access_token"].ToString();
                return _accessToken;
            }

            public async Task<JArray> GetHotelsAsync(string location)
            {
                var token = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = $"https://test.api.amadeus.com/v2/shopping/hotel-offers?cityCode={location}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
                return (JArray)jsonResponse["data"];
            }
        }
    }


}
