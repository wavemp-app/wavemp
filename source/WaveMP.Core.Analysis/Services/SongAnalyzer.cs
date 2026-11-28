using Microsoft.Extensions.Logging;
using WaveMP.Core.Analysis.Decoding;
using WaveMP.Core.Analysis.Detection;
using WaveMP.Core.Analysis.Features;
using WaveMP.Core.Analysis.Options;
using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Parsers;
using WaveMP.Core.Lyrics.Services;
using WaveMP.Core.Metadata.Files;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Data.Segments;

namespace WaveMP.Core.Analysis.Services;

internal sealed class SongAnalyzer(
    ISongAnalysisRepository repository,
    ISoundReader reader,
    IMediaFileLocator locator,
    ILyricsRepository lyricsRepository,
    ILyricsService lyrics,
    ILyricsDownloader downloader,
    ISettingsSnapshot<LibrarySettings> settings,
    TimeProvider timeProvider,
    ILogger<SongAnalyzer> logger) : ISongAnalyzer
{
    public async Task<SongAnalysisResult> AnalyzeAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        SongAnalysisTrackRow? track = await repository.FindTrackAsync(trackId, cancellationToken);
        if (track is null)
        {
            return new SongAnalysisResult(SongAnalysisOutcome.TrackNotFound);
        }

        if (track.Audio is null && track.Video is null)
        {
            return new SongAnalysisResult(SongAnalysisOutcome.NoFiles);
        }

        SoundReading? audio = track.Audio is { } audioFile ? await ReadAsync(audioFile, cancellationToken) : null;
        SoundReading? video = track.Video is { } videoFile ? await ReadAsync(videoFile, cancellationToken) : null;
        bool failed = audio?.Status == SongAnalysisStatus.Failed || video?.Status == SongAnalysisStatus.Failed;
        List<MediaFileAnalysis> results = [];
        bool lyricsTransient;
        if (audio is { Features: { } audioFeatures } && video is { Features: { } videoFeatures })
        {
            // Two recordings: the audio file is the reference, the video is aligned with it first.
            RecordingAlignment? alignment = RecordingAligner.Align(audioFeatures, videoFeatures);
            if (alignment is { Matches: true })
            {
                // The audio file is the reference: the whole file is the song.
                results.Add(Create(track.Audio!, audio, null, mismatch: false));
                results.Add(Create(
                    track.Video!,
                    video,
                    new DetectedSegment(alignment.Pieces, SongSegmentMethod.Alignment, alignment.Confidence),
                    mismatch: false));
            }
            else
            {
                // Another version: no mapping between them; the video may still show its intro by its structure.
                bool mismatch = alignment is not null;
                results.Add(Create(track.Audio!, audio, null, mismatch));
                results.Add(Create(track.Video!, video, StructureDetector.Detect(videoFeatures), mismatch));
            }

            await repository.SaveAsync(results, cancellationToken);

            // Then the lyrics, with the duration of the real song (the audio file).
            lyricsTransient = await LookUpLyricsAsync(trackId, cancellationToken);
        }
        else
        {
            (MediaFile File, SoundReading Reading)? single = audio is { Features: not null }
                ? (track.Audio!, audio)
                : video is { Features: not null } ? (track.Video!, video) : null;
            if (track.Audio is { } silentAudio && audio is { Features: null })
            {
                results.Add(Create(silentAudio, audio, null, mismatch: false));
            }

            if (track.Video is { } silentVideo && video is { Features: null })
            {
                results.Add(Create(silentVideo, video, null, mismatch: false));
            }

            LyricsForAlignment? found = null;
            if (single is { } one)
            {
                // One recording: the lyrics first (by title and artist), then the song by their times, otherwise by
                // the structure of the file.
                found = await FindLyricsAsync(trackId, cancellationToken);
                SoundFeatures features = one.Reading.Features!;
                DetectedSegment? byLyrics = found?.LineStarts is { } starts
                    ? LyricsAligner.Align(features, starts, found.Duration)
                    : null;
                DetectedSegment? chosen = byLyrics is { Confidence: >= SongSegment.ConfidentFrom }
                    ? byLyrics
                    : StructureDetector.Detect(features) ?? byLyrics;
                results.Add(Create(one.File, one.Reading, chosen, mismatch: false));
            }

            await repository.SaveAsync(results, cancellationToken);
            lyricsTransient = found?.Transient == true || await SaveLyricsAsync(trackId, found, cancellationToken);
        }

        bool needsReview = results
            .Select(result => SongSegment.Of(result, null, null, null))
            .Any(segment => segment.NeedsReview);
        return new SongAnalysisResult(SongAnalysisOutcome.Analyzed, needsReview, failed, lyricsTransient);
    }

    private async Task<SoundReading> ReadAsync(MediaFile file, CancellationToken cancellationToken)
    {
        if (!locator.TryGetFullPath(file.SourceId, file.RelativePath, out string fullPath) || !File.Exists(fullPath))
        {
            logger.LogInformation(
                "The file {Path} is gone; its song analysis waits for the next scan",
                file.RelativePath);
            return SoundReading.Failed;
        }

        return await reader.ReadAsync(fullPath, cancellationToken);
    }

    /// <summary>
    /// The result for a file: what was found, or the whole file.
    /// </summary>
    private MediaFileAnalysis Create(MediaFile file, SoundReading reading, DetectedSegment? found, bool mismatch)
    {
        bool hasPieces = found is { Pieces.Count: > 0 };
        return new MediaFileAnalysis
        {
            MediaFileId = file.Id,
            Version = AnalysisOptions.Version,
            FileSize = file.Size,
            FileModifiedAt = file.ModifiedAt,
            AnalyzedAt = timeProvider.GetUtcNow(),
            Status = reading.Status,
            Method = hasPieces ? found!.Method : SongSegmentMethod.WholeFile,
            Confidence = hasPieces ? Math.Round(found!.Confidence, 3) : null,
            Mismatch = mismatch,
            Pieces = hasPieces ? [.. found!.Pieces] : [],
            LoudnessLufs = reading.LoudnessLufs is { } lufs ? Math.Round(lufs, 2) : null,
        };
    }

    /// <summary>
    /// Looks up the lyrics of a track that was never looked up, as the download job would. Returns whether LRCLIB
    /// could not be asked.
    /// </summary>
    private async Task<bool> LookUpLyricsAsync(Guid trackId, CancellationToken cancellationToken)
    {
        if (!settings.Current.DownloadLyrics)
        {
            return false;
        }

        LyricsTrackRow? row = await lyricsRepository.FindTrackAsync(trackId, cancellationToken);
        if (row is null || row.Download is not null || row.LyricsFilePath is not null)
        {
            return false;
        }

        LyricsDownloadResult result = await downloader.DownloadAsync(
            trackId,
            LyricsDownloadMode.Automatic,
            cancellationToken);
        return result.IsTransient;
    }

    /// <summary>
    /// The synchronized lyrics to align a single file with: synchronized lyrics the track has, otherwise (if the
    /// download is on and the track was never looked up, or only plain lines of another version were kept) a lookup
    /// of LRCLIB without relying on the duration.
    /// </summary>
    private async Task<LyricsForAlignment?> FindLyricsAsync(Guid trackId, CancellationToken cancellationToken)
    {
        LyricsResult existing = await lyrics.GetAsync(trackId, cancellationToken);
        if (existing.IsFound && !existing.Lyrics.OtherVersion && StartsOf(existing.Lyrics.Document) is { } own)
        {
            return new LyricsForAlignment(own, null, null, Transient: false, LookedUp: false);
        }

        if (!settings.Current.DownloadLyrics)
        {
            return null;
        }

        LyricsTrackRow? row = await lyricsRepository.FindTrackAsync(trackId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        bool ownOtherVersion = row.Download is { OtherVersion: true } download
            && download.Status is LyricsDownloadStatus.SavedToFile or LyricsDownloadStatus.SavedToDatabase;
        bool neverLookedUp = row.Download is null && row.LyricsFilePath is null && !existing.IsFound;
        if (!ownOtherVersion && !neverLookedUp)
        {
            return null;
        }

        LyricsCandidate candidate = await downloader.FindCandidateAsync(trackId, cancellationToken);
        if (candidate.IsTransient)
        {
            return new LyricsForAlignment(null, null, null, Transient: true, LookedUp: neverLookedUp);
        }

        IReadOnlyList<TimeSpan>? starts = candidate.Record?.SyncedLyrics is { } synced
            ? StartsOf(LrcParser.Parse(synced))
            : null;
        return new LyricsForAlignment(
            starts,
            candidate.Duration,
            starts is null ? null : candidate.Record,
            Transient: false,
            LookedUp: neverLookedUp);
    }

    /// <summary>
    /// Saves the lyrics found for the alignment once the segment is stored (so the duration of the song decides
    /// whether they fit), or looks up a track that was never looked up as the download job would. Returns whether
    /// LRCLIB could not be asked.
    /// </summary>
    private async Task<bool> SaveLyricsAsync(
        Guid trackId,
        LyricsForAlignment? found,
        CancellationToken cancellationToken)
    {
        if (found?.Record is { } record)
        {
            LyricsDownloadResult saved = await downloader.SaveCandidateAsync(trackId, record, cancellationToken);
            return saved.IsTransient;
        }

        return found is { LookedUp: true } && await LookUpLyricsAsync(trackId, cancellationToken);
    }

    /// <summary>
    /// When the sung lines of synchronized lyrics start; <c>null</c> for static lyrics.
    /// </summary>
    private static IReadOnlyList<TimeSpan>? StartsOf(LyricsDocument document)
    {
        if (document.Type == LyricsType.Static)
        {
            return null;
        }

        TimeSpan[] starts = [.. document.Lines
            .Where(line => line.Start is not null && line.Text.Length > 0)
            .Select(line => line.Start!.Value)];
        return starts.Length == 0 ? null : starts;
    }

    /// <summary>
    /// Lyrics to align a file with.
    /// </summary>
    /// <param name="LineStarts">When the sung lines start; <c>null</c> without synchronized lyrics.</param>
    /// <param name="Duration">Duration of the recording the lyrics belong to, if known.</param>
    /// <param name="Record">The track of LRCLIB they come from, to be saved after the analysis.</param>
    /// <param name="Transient">Whether LRCLIB could not be asked.</param>
    /// <param name="LookedUp">Whether the track was never looked up before, so the result is to be stored.</param>
    private sealed record LyricsForAlignment(
        IReadOnlyList<TimeSpan>? LineStarts,
        TimeSpan? Duration,
        LrclibRecord? Record,
        bool Transient,
        bool LookedUp);
}
