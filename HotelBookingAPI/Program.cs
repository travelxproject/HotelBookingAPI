using HotelBookingAPI.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hotel Booking API",
        Version = "v1",
        Description = "API for searching and booking hotels using Amadeus API.",
    });
});

builder.Services.AddHttpClient<AmadeusService>();

builder.WebHost.UseUrls("http://localhost:6011");

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Hotel Booking API v1");
    options.RoutePrefix = "swagger"; 
});

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();
