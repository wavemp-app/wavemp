namespace WaveMP.Core.Recaps.Videos;

/// <summary>
/// The converted video, or why there is none.
/// </summary>
/// <param name="Status">Whether the video was converted.</param>
/// <param name="Path">The MP4 file; set if converted. The caller deletes it once it is sent, for example by opening it
/// with <see cref="FileOptions.DeleteOnClose"/>; the clean-up removes it otherwise.</param>
public sealed record RecapVideoResult(RecapVideoStatus Status, string? Path = null);
