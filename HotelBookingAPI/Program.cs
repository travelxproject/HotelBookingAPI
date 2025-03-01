
using HotelBookingAPI.APIs.HotelAPIProject.Services;
using HotelBookingAPI.APIs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<AmadeusService>();
//builder.Services.AddHttpClient<BookingService>();
builder.Services.AddHttpClient<GooglePlacesService>();
builder.Services.AddSingleton<IHotelService, HotelService>();

var app = builder.Build();
app.Run();


