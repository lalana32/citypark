using Testcontainers.PostgreSql;

namespace CityPark.Api.Tests;

/// <summary>The API connected to a real, empty PostgreSQL 17 container.</summary>
public sealed class PostgresApiFactory : ApiFactory, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    protected override string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
