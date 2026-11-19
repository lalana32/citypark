using CityPark.Infrastructure;
using CityPark.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("CityPark")
    ?? throw new InvalidOperationException("Connection string 'CityPark' is not configured."));

var app = builder.Build();

// Demo lots for local development; a no-op in every other environment.
await app.Services.SeedDevelopmentDataAsync(app.Environment);

app.Run();
