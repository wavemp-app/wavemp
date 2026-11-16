using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace WaveMP.Service.OpenApiTransformers;

/// <summary>
/// Describes the <c>Location</c> header of the responses 201 Created and 202 Accepted: every operation of the API that
/// creates a resource or queues a run answers with the address of the new resource or run
/// (<c>CreatedAtRoute</c>, <c>AcceptedAtRoute</c>).
/// </summary>
internal sealed class LocationHeaderTransformer : IOpenApiOperationTransformer
{
    private static readonly string[] _statusCodes = ["201", "202"];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Responses is null)
        {
            return Task.CompletedTask;
        }

        foreach (string statusCode in _statusCodes)
        {
            if (operation.Responses.TryGetValue(statusCode, out IOpenApiResponse? response)
                && response is OpenApiResponse concrete)
            {
                concrete.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.OrdinalIgnoreCase);
                concrete.Headers[HeaderNames.Location] = new OpenApiHeader
                {
                    Description = statusCode == "201"
                        ? "Address of the created resource."
                        : "Address of the queued run, to follow its progress.",
                    Required = true,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri-reference" },
                };
            }
        }

        return Task.CompletedTask;
    }
}
