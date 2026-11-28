using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WaveMP.Core.Analysis.Decoding;
using WaveMP.Core.Analysis.Jobs;
using WaveMP.Core.Analysis.Options;
using WaveMP.Core.Analysis.Services;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Extensions;

namespace WaveMP.Core.Analysis.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the song analysis (settings from the section <c>Analysis</c>): where the real song lies in audio files
    /// and videos, their loudness, the corrections of the admin and the job <c>song-analysis</c>, which is started
    /// after scans. Needs <c>AddWaveMpData</c>, <c>AddWaveMpSettings</c>, <c>AddWaveMpMetadata</c>,
    /// <c>AddWaveMpJobs</c>, <c>AddWaveMpTranscoding</c> and <c>AddWaveMpLyrics</c>.
    /// </summary>
    public static IServiceCollection AddWaveMpAnalysis(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<AnalysisOptions>().BindConfiguration(AnalysisOptions.SectionName);

        services.AddSingleton<ISoundReader, FfmpegSoundReader>();
        services.AddScoped<ISongAnalyzer, SongAnalyzer>();
        services.AddScoped<ISongAnalysisRunner, SongAnalysisRunner>();
        services.AddScoped<ISongSegmentService, SongSegmentService>();

        services.AddWaveMpJob<SongAnalysisJob>(JobIds.SongAnalysis);

        return services;
    }
}
