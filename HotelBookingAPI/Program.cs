using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using HotelBookingAPI.Services.HotelServices;
using HotelBookingAPI.Services.FlightServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false, // Set to `true` if you have a known issuer
            ValidateAudience = false, // Set to `true` if you have a known audience
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("YourSecretKeyHere")) // Replace with your secret key
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{

    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hotel & Flight Booking API",
        Version = "v1",
        Description = "API for searching and booking hotels and flights using Amadeus API.",
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer <token> in the field below."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
    options.EnableAnnotations();
    options.SupportNonNullableReferenceTypes();
});

builder.Services.AddHttpClient<AmadeusService>();
builder.Services.AddHttpClient<AmadeusFlightService>();

builder.WebHost.UseUrls("http://localhost:6011");

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Hotel & Flight Booking API v1");
    options.RoutePrefix = "swagger"; 
});
app.UseAuthentication(); 

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();
