﻿using System.Collections.Generic;

namespace HotelBookingAPI.Models
{
    public class HotelSearchResponse
    {
        public List<HotelOffer> Data { get; set; }
    }

    public class HotelOffer
    {
        public string HotelID { get; set; }
        public string HotelName { get; set; }
        public decimal Price { get; set; }
        public int Rating { get; set; }
        public string Location { get; set; }
        public List<string> Services { get; set; }

        public bool IsAvailable { get; set; }
    }
}