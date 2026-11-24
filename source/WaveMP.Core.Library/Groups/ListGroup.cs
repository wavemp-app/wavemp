using WaveMP.Data.Projections;

namespace WaveMP.Core.Library.Groups;

/// <summary>
/// A group of a list: a run of consecutive entries with the same album, artist, index letter or year.
/// </summary>
/// <param name="Kind">What the entries have in common.</param>
/// <param name="Position">Index of the first entry of the group in the list, starting at 0; the offset of the list at
/// which the group starts.</param>
/// <param name="Count">Number of entries of the group.</param>
/// <param name="Album">The album of a group of <see cref="ListGroupKind.Album"/>.</param>
/// <param name="Artist">The artist of a group of <see cref="ListGroupKind.Artist"/>.</param>
/// <param name="Letter">Index letter (A to Z or <see cref="IndexLetters.Other"/>) of the text the list is sorted by,
/// taken from the first entry of the group; <c>null</c> if the list is not sorted by a text or the first entry has
/// none.</param>
/// <param name="Year">The year of a group of <see cref="ListGroupKind.Year"/>, or in the track list sorted by year the
/// year of the first track of the group.</param>
public sealed record ListGroup(
    ListGroupKind Kind,
    int Position,
    int Count,
    GroupAlbumRow? Album = null,
    GroupArtistRow? Artist = null,
    string? Letter = null,
    int? Year = null);
