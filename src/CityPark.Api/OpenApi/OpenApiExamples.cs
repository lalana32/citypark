using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.OpenApi;

namespace CityPark.Api.OpenApi;

public static class OpenApiExamples
{
    /// <summary>Adds an example body to a response the endpoint already declares.</summary>
    public static RouteHandlerBuilder WithResponseExample(
        this RouteHandlerBuilder builder,
        int statusCode,
        object example,
        string contentType = "application/json")
    {
        // Serialized once, with the same camelCase names the API writes.
        var node = JsonSerializer.SerializeToNode(example, JsonSerializerOptions.Web);
        var status = statusCode.ToString(CultureInfo.InvariantCulture);

        return builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            // Throws instead of skipping, so a typo fails the OpenAPI test rather than losing the example.
            if (operation.Responses is null
                || !operation.Responses.TryGetValue(status, out var response)
                || response.Content is null
                || !response.Content.TryGetValue(contentType, out var media))
            {
                throw new InvalidOperationException($"The operation declares no {status} response with {contentType}.");
            }

            media.Example = node?.DeepClone();
            return Task.CompletedTask;
        });
    }

    public static RouteHandlerBuilder WithParameterExample(this RouteHandlerBuilder builder, string name, string example) =>
        builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            if (operation.Parameters?.FirstOrDefault(p => p.Name == name) is not OpenApiParameter parameter)
            {
                throw new InvalidOperationException($"The operation has no parameter '{name}'.");
            }

            parameter.Example = JsonValue.Create(example);
            return Task.CompletedTask;
        });
}
