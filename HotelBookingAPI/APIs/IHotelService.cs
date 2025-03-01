using System.Threading.Tasks;
using System.Collections.Generic;
using HotelBookingAPI.HotelModel;

namespace HotelBookingAPI.APIs
{

    public interface IHotelService
    {
        Task<List<HotelInfo>> GetHotelsByActivityAsync(string activity, string location);
    }

}
