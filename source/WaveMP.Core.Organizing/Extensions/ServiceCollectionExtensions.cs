using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Organizing.Previews;

namespace WaveMP.Core.Organizing.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the organization of the media files: the preview of layout templates. Needs <c>AddWaveMpData</c>,
    /// <c>AddWaveMpSettings</c> and <c>AddWaveMpScanning</c>.
    /// </summary>
    public static IServiceCollection AddWaveMpOrganizing(this IServiceCollection services)
    {
        services.AddScoped<ITemplatePreviewService, TemplatePreviewService>();
        return services;
    }
}
