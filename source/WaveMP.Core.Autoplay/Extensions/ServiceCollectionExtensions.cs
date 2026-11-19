using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WaveMP.Core.Autoplay.Selections;
using WaveMP.Core.Autoplay.Services;

namespace WaveMP.Core.Autoplay.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers autoplay: the songs that play when a list ends, chosen by the artists of the list
    /// (<see cref="IAutoplaySelector"/>). Needs <c>AddWaveMpData</c> and <c>AddWaveMpUsers</c>.
    /// </summary>
    public static IServiceCollection AddWaveMpAutoplay(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAutoplaySelector, ArtistAutoplaySelector>();
        services.AddScoped<IAutoplayService, AutoplayService>();

        return services;
    }
}
