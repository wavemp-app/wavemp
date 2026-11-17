namespace WaveMP.Core.Scanning.Selections;

/// <summary>
/// The file selection of a track: per category the file it uses, the selection of the admin and the files to choose
/// from.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Audio">The audio file the track plays.</param>
/// <param name="Video">The video file the track plays.</param>
/// <param name="Cover">The cover of the track itself: a picture file with its base name or a picture embedded in one
/// of its files.</param>
public sealed record FileSelectionDetails(
    Guid TrackId,
    CategorySelection Audio,
    CategorySelection Video,
    CategorySelection Cover);
