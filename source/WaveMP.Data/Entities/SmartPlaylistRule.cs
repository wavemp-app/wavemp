namespace WaveMP.Data.Entities;

/// <summary>
/// A condition of a smart playlist: a field of a track, how it is compared and the value it is compared with. Which
/// value is set depends on the kind of the field: <see cref="Text"/> for artist, album and genre; <see cref="Number"/>
/// (and <see cref="NumberTo"/> for <see cref="SmartPlaylistOperator.Between"/>) for year, rating, play count and
/// duration in seconds; <see cref="Number"/> as a number of days for last played and date added; none for favorite,
/// audio and video.
/// </summary>
/// <param name="Field">The field of the track.</param>
/// <param name="Operator">How the field is compared.</param>
/// <param name="Text">Text that artist, album or genre are compared with, without regard to case.</param>
/// <param name="Number">Number the field is compared with, the lower bound of a range or a number of days.</param>
/// <param name="NumberTo">Upper bound (included) of <see cref="SmartPlaylistOperator.Between"/>.</param>
public sealed record SmartPlaylistRule(
    SmartPlaylistField Field,
    SmartPlaylistOperator Operator,
    string? Text = null,
    int? Number = null,
    int? NumberTo = null);
