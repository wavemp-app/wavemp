using System.ComponentModel.DataAnnotations;
using WaveMP.Core.Lyrics.Uploads;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Lyrics an admin uploads for a track.
/// </summary>
public sealed class UploadLyricsDto
{
    /// <summary>
    /// The lyrics in the LRC format (lines with timestamps, also enhanced LRC with word timestamps) or as plain text,
    /// at most 262144 characters; at least one line has to have text.
    /// </summary>
    [Required]
    [StringLength(ILyricsUploader.MaxLength)]
    public required string Text { get; init; }

    /// <summary>
    /// Whether the lyrics are also published to LRCLIB, with the title, artists, album and duration of the track.
    /// </summary>
    public bool Publish { get; init; }
}
