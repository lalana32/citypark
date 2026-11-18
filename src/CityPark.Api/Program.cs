using CityPark.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("CityPark")
    ?? throw new InvalidOperationException("Connection string 'CityPark' is not configured."));

var app = builder.Build();

app.Run();
