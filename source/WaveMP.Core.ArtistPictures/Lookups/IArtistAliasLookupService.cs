namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// Takes the aliases of an artist from MusicBrainz, for admins: the aliases and the sort name of its MusicBrainz artist
/// (from the tags of its songs or chosen in the online lookup of pictures) become aliases of the artist that the search
/// finds it by. Part of the online lookup of artists: while it is switched off, nothing is asked
/// (<see cref="ArtistLookupStatus.Disabled"/>); requests wait in the queue of MusicBrainz and the answer is kept in the
/// cache of the lookup.
/// </summary>
public interface IArtistAliasLookupService
{
    /// <summary>
    /// Replaces the aliases of MusicBrainz of the artist by those MusicBrainz names now (see
    /// <see cref="Data.Repositories.IArtistAliasRepository.ReplaceMusicBrainzAliasesAsync"/>).
    /// </summary>
    Task<ArtistAliasLookupResult> LookUpAsync(Guid artistId, CancellationToken cancellationToken = default);
}
