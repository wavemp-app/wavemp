using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace WaveMP.Service.OpenApiTransformers;

/// <summary>
/// Describes the request body of an action with the XML comment of its body parameter. The XML comment generator
/// writes the comment of every parameter that is not an operation parameter into the request body, so the comment of
/// the last one wins, which is the cancellation token ("Aborts the request.") of every action. Runs after the XML
/// comment transformers and before <see cref="XmlCommentLineBreakTransformer"/>.
/// </summary>
internal sealed class RequestBodyDescriptionTransformer(XmlDocumentation documentation) : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.RequestBody is not OpenApiRequestBody requestBody)
        {
            return Task.CompletedTask;
        }

        ParameterInfo? body = context.Description.ParameterDescriptions
            .Where(parameter => parameter.Source == BindingSource.Body)
            .Select(parameter => (parameter.ParameterDescriptor as ControllerParameterDescriptor)?.ParameterInfo)
            .FirstOrDefault(parameter => parameter is not null);
        if (body?.Member is MethodInfo method && body.Name is { } name)
        {
            requestBody.Description = documentation.FindParameter(method, name);
        }

        return Task.CompletedTask;
    }
}
