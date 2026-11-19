using WaveMP.Data.Repositories;

namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// The result of the check of a smart playlist entered by a user.
/// </summary>
/// <param name="Errors">Every rejected field; empty if the smart playlist is valid.</param>
/// <param name="Name">The normalized name; empty if it is not valid.</param>
/// <param name="Criteria">The normalized rules, sort order and limit; rejected rules are left out.</param>
public sealed record SmartPlaylistValidation(
    IReadOnlyList<SmartPlaylistError> Errors,
    string Name,
    SmartPlaylistCriteria Criteria)
{
    public bool IsValid => Errors.Count == 0;
}
