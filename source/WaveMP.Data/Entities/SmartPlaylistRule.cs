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
/// <param name="Text">Text that artist, album or genre are compared with. For a rule with
/// <see cref="ReferenceId"/>, the name of the entry when it was last linked.</param>
/// <param name="Number">Number the field is compared with, the lower bound of a range or a number of days.</param>
/// <param name="NumberTo">Upper bound (included) of <see cref="SmartPlaylistOperator.Between"/>.</param>
/// <param name="ReferenceId">The artist, album or genre a rule with <see cref="SmartPlaylistOperator.Is"/> or
/// <see cref="SmartPlaylistOperator.IsNot"/> names, by id, so the rule follows a new spelling of the name. A rule
/// whose name matches no single entry has none and compares its normalized text instead (see
/// <see cref="Texts.NameKeys"/>).</param>
public sealed record SmartPlaylistRule(
    SmartPlaylistField Field,
    SmartPlaylistOperator Operator,
    string? Text = null,
    int? Number = null,
    int? NumberTo = null,
    Guid? ReferenceId = null)
{
    /// <summary>
    /// Whether the rule names one artist, album or genre and can therefore refer to it by id.
    /// </summary>
    public bool CanReference()
    {
        return Field is SmartPlaylistField.Artist or SmartPlaylistField.Album or SmartPlaylistField.Genre
            && Operator is SmartPlaylistOperator.Is or SmartPlaylistOperator.IsNot;
    }
}
