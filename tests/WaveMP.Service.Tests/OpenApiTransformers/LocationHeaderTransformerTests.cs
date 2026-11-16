using AwesomeAssertions;
using Microsoft.OpenApi;
using WaveMP.Service.OpenApiTransformers;

namespace WaveMP.Service.Tests.OpenApiTransformers;

[TestFixture]
public class LocationHeaderTransformerTests
{
    [TestCase("201")]
    [TestCase("202")]
    public async Task TransformAsync_CreatedOrAccepted_DescribesTheLocationHeader(string statusCode)
    {
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                [statusCode] = new OpenApiResponse { Description = "Created." },
                ["400"] = new OpenApiResponse { Description = "Invalid." },
            },
        };

        await new LocationHeaderTransformer().TransformAsync(operation, null!, CancellationToken.None);

        IOpenApiHeader location = operation.Responses[statusCode].Headers!["Location"];
        location.Required.Should().BeTrue();
        location.Schema!.Type.Should().Be(JsonSchemaType.String);
        location.Schema.Format.Should().Be("uri-reference");
        location.Description.Should().NotBeNullOrEmpty();
        operation.Responses["400"].Headers.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_OtherResponses_AddsNoHeader()
    {
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "One page." },
                ["204"] = new OpenApiResponse { Description = "Deleted." },
            },
        };

        await new LocationHeaderTransformer().TransformAsync(operation, null!, CancellationToken.None);

        operation.Responses.Values.Should().AllSatisfy(response => response.Headers.Should().BeNullOrEmpty());
    }

    [Test]
    public async Task TransformAsync_WithoutResponses_DoesNothing()
    {
        OpenApiOperation operation = new();

        Func<Task> transform = () =>
            new LocationHeaderTransformer().TransformAsync(operation, null!, CancellationToken.None);

        await transform.Should().NotThrowAsync();
    }
}
