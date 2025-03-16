using System.Text.Json;
using Microsoft.Data.SqlClient;
using Dapper;

namespace HotelBookingAPI.Database
{
    public class HotelRepositoryDB
    {   
        private readonly string _connectionString;

        public HotelRepositoryDB(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task SaveHotelsAsync(Dictionary<string, string> hotelIdName)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            foreach (var (hotelId, hotelName) in hotelIdName)
            {
                string query = @"
                IF NOT EXISTS (SELECT 1 FROM HotelDetails WHERE HotelId = @HotelId)
                BEGIN
                    INSERT INTO HotelDetails (HotelId, Name, LastUpdated)
                    VALUES (@HotelId, @Name, @LastUpdated)
                END;";

                await connection.ExecuteAsync(query, new
                {
                    HotelId = hotelId,
                    Name = hotelName,
                    LastUpdated = DateTime.UtcNow
                });
            }
        }

        public async Task SaveHotelDetailsAsync(Dictionary<string, (double Rating, List<string> Services)> hotelDetails)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            foreach (var (hotelId, (rating, services)) in hotelDetails)
            {
                var amenitiesJson = JsonSerializer.Serialize(services);

                string query = @"
                    UPDATE HotelDetails
                    SET Rating = @Rating, Amenities = @Amenities, LastUpdated = @LastUpdated
                    WHERE HotelId = @HotelId;";

                await connection.ExecuteAsync(query, new
                {
                    HotelId = hotelId,
                    Rating = rating,
                    Amenities = amenitiesJson,
                    LastUpdated = DateTime.UtcNow
                });
            }
        }

        public async Task<List<(string HotelId, string HotelName)>> GetHotelsWithoutRatingsAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            string query = "SELECT HotelId, Name FROM HotelDetails WHERE Rating IS NULL OR Amenities IS NULL";
            var hotels = await connection.QueryAsync<(string, string)>(query);

            return hotels.ToList();
        }

        public async Task<Dictionary<string, (double Rating, List<string> Services)>> GetHotelDetailsAsync(List<string> hotelIds)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            Dictionary<string, (double, List<string>)> hotelDetails = new();

            string query = "SELECT HotelId, Rating, Amenities FROM HotelDetails WHERE HotelId IN @HotelIds";
            var results = await connection.QueryAsync(query, new { HotelIds = hotelIds });

            foreach (var result in results)
            {
                double rating = result.Rating;
                List<string> services = JsonSerializer.Deserialize<List<string>>(result.Amenities) ?? new List<string>();

                hotelDetails[result.HotelId] = (rating, services);
            }

            return hotelDetails;
        }
    }
}
