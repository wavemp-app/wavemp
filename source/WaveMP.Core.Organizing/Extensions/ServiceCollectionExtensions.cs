using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Extensions;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Jobs;
using WaveMP.Core.Organizing.Operations;
using WaveMP.Core.Organizing.Options;
using WaveMP.Core.Organizing.OwnAlbums;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.Organizing.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the organization of the media files: the preview of layout templates, the own albums with their
    /// covers in the folder <c>own-album-covers</c> of the data folder, the previews and the log of organize
    /// operations, the file moves that never replace a file and the job <c>library-organize</c>, which plans the
    /// previews of the library and runs the confirmed operations. Settings come from the section <c>Organizing</c>.
    /// Needs <c>AddWaveMpData</c>, <c>AddWaveMpSettings</c>, <c>AddWaveMpMetadata</c>, <c>AddWaveMpJobs</c> and
    /// <c>AddWaveMpScanning</c>. The cover folder is prepared and an interrupted run is put back on start by
    /// <see cref="ServiceProviderExtensions.InitializeWaveMpOrganizingAsync"/>.
    /// </summary>
    public static IServiceCollection AddWaveMpOrganizing(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<OrganizeOptions>()
            .BindConfiguration(OrganizeOptions.SectionName)
            .Configure<IConfiguration, IServiceProvider>((options, configuration, provider) =>
                options.CoverPath = DataPaths.Resolve(
                    configuration,
                    provider.GetService<IHostEnvironment>(),
                    OrganizeOptions.CoverFolderName));

        services.AddSingleton<INoReplaceFileMover, NoReplaceFileMover>();
        services.AddSingleton<IMediaWriteProbe, MediaWriteProbe>();
        services.AddSingleton<IOwnAlbumCoverStore, OwnAlbumCoverStore>();
        services.AddScoped<ITemplatePreviewService, TemplatePreviewService>();
        services.AddScoped<IOwnAlbumService, OwnAlbumService>();
        services.AddScoped<IOrganizePreviewService, OrganizePreviewService>();
        services.AddScoped<IOrganizeService, OrganizeService>();
        services.AddSingleton<OrganizeRunRegistry>();
        services.AddScoped<IOrganizeExecutor, OrganizeExecutor>();
        services.AddScoped<OrganizeRecovery>();
        services.AddWaveMpJob<LibraryOrganizeJob>(JobIds.LibraryOrganize);
        return services;
    }
}
