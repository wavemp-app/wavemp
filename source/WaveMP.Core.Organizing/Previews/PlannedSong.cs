using WaveMP.Core.Organizing.Templates;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Organizing.Previews;

/// <summary>
/// A song the planner places by the layout template.
/// </summary>
/// <param name="Song">The song with all its files as the library knows them.</param>
/// <param name="Values">The values the template is filled with.</param>
/// <param name="TagChanges">Tags to write into its primary audio file, or <c>null</c>.</param>
/// <param name="RequiresAudio">Whether the song needs an audio file (own albums write tags).</param>
/// <param name="Warnings">Warnings known before planning, e.g. that the own album joins a library album.</param>
public sealed record PlannedSong(
    OrganizeSongRow Song,
    TemplateValues Values,
    OrganizeTagValues? TagChanges = null,
    bool RequiresAudio = false,
    IReadOnlyList<OrganizeWarning>? Warnings = null);

/// <summary>
/// The uploaded cover of an own album that is written as <c>cover.{ext}</c> into the album folder.
/// </summary>
/// <param name="FullPath">The file in the data folder.</param>
/// <param name="Extension">Its extension without dot, e.g. <c>jpg</c>.</param>
/// <param name="Size">Its size in bytes.</param>
/// <param name="ModifiedAt">Its change time.</param>
/// <param name="DisplayTitle">The title of the album, for the log.</param>
public sealed record PlannedCover(
    string FullPath,
    string Extension,
    long Size,
    DateTimeOffset ModifiedAt,
    string DisplayTitle);
