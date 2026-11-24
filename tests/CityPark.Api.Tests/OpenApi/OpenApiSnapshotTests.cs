namespace CityPark.Api.Tests.OpenApi;

/// <summary>
/// The frontend generates its types from web/openapi/v1.json. This test fails when the API's
/// document no longer matches that file. Set UPDATE_OPENAPI_SNAPSHOT=1 to rewrite it.
/// </summary>
public class OpenApiSnapshotTests(OpenApiContractFactory factory) : IClassFixture<OpenApiContractFactory>
{
    private const string UpdateVariable = "UPDATE_OPENAPI_SNAPSHOT";

    [Fact]
    public async Task Document_MatchesTheFrontendSnapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var served = Normalize(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), ct));
        var path = SnapshotPath();

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, served, ct);
            return;
        }

        Assert.True(File.Exists(path), $"{path} is missing. Run the tests with {UpdateVariable}=1 to create it.");
        var committed = Normalize(await File.ReadAllTextAsync(path, ct));
        Assert.True(
            served == committed,
            $"The OpenAPI document differs from {path}. Run the tests with {UpdateVariable}=1, then npm run generate in web/.");
    }

    // Git checks files out with CRLF on Windows: the snapshot itself, and the C# sources whose
    // multi-line XML comments become descriptions with an escaped \r\n inside the JSON strings.
    private static string Normalize(string json) =>
        json.ReplaceLineEndings("\n").Replace(@"\r\n", @"\n", StringComparison.Ordinal).TrimEnd() + "\n";

    private static string SnapshotPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CityPark.slnx")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Repository root (CityPark.slnx) not found.")
            : Path.Combine(directory.FullName, "web", "openapi", "v1.json");
    }
}
