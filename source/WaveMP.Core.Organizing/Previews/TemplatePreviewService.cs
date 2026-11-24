using Microsoft.Extensions.Options;
using WaveMP.Core.Organizing.Templates;
using WaveMP.Core.Scanning.Options;
using WaveMP.Core.Settings.Templates;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.Previews;

internal sealed class TemplatePreviewService(IOrganizeRepository repository, IOptions<ScanOptions> scanOptions)
    : ITemplatePreviewService
{
    /// <summary>
    /// Most tracks and most example songs of one preview.
    /// </summary>
    public const int MaxSongs = 10;

    /// <summary>
    /// Songs of the library a preview without tracks and examples shows.
    /// </summary>
    public const int DefaultSongCount = 3;

    public async Task<TemplatePreviewResult> PreviewAsync(
        TemplatePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        LayoutTemplateParseResult parsed = LayoutTemplateSyntax.Parse(request.Template);
        if (!parsed.IsValid)
        {
            return new TemplatePreviewResult(false, false, parsed.Errors, []);
        }

        LayoutTemplate template = parsed.Template;
        int maxPathBytes = scanOptions.Value.MaxPathBytes;
        IReadOnlyList<Guid> trackIds = [.. (request.TrackIds ?? []).Take(MaxSongs)];
        IReadOnlyList<TemplateSample> samples = [.. (request.Samples ?? []).Take(MaxSongs)];
        IReadOnlyList<TemplateSongRow> songs = trackIds.Count > 0
            ? await repository.GetTemplateSongsAsync(trackIds, cancellationToken)
            : samples.Count == 0
                ? await repository.GetSampleTemplateSongsAsync(DefaultSongCount, cancellationToken)
                : [];

        List<TemplatePreviewPath> paths = [];
        foreach (TemplateSongRow song in songs)
        {
            string? longest = song.Extensions.MaxBy(PathNameSanitizer.ByteCountOf);
            RenderedPath path = LayoutTemplateRenderer.Render(
                template,
                ToValues(song),
                maxPathBytes,
                longest,
                song.BaseName);
            paths.Add(new TemplatePreviewPath(song.TrackId, path, song.PrimaryExtension));
        }

        foreach (TemplateSample sample in samples)
        {
            string? extension = NormalizeExtension(sample.Extension);
            RenderedPath path = LayoutTemplateRenderer.Render(template, sample.Values, maxPathBytes, extension);
            paths.Add(new TemplatePreviewPath(null, path, extension));
        }

        return new TemplatePreviewResult(true, template.IsFolderBased, [], paths);
    }

    internal static TemplateValues ToValues(TemplateSongRow song)
    {
        return new TemplateValues(
            song.Title,
            song.ArtistName,
            song.AlbumArtistName,
            song.AlbumTitle,
            song.Year,
            song.TrackNumber,
            song.DiscNumber,
            song.Genre,
            song.IsCompilation);
    }

    /// <summary>
    /// An extension of an example song: lower case without dot, only letters and digits, otherwise none.
    /// </summary>
    private static string? NormalizeExtension(string? extension)
    {
        string? text = extension?.Trim().TrimStart('.').ToLowerInvariant();
        return text is { Length: > 0 and <= 10 } && text.All(char.IsAsciiLetterOrDigit) ? text : null;
    }
}
