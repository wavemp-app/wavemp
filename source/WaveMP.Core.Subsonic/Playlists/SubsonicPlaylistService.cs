using WaveMP.Core.Playlists.Playlists;
using WaveMP.Core.Playlists.SmartPlaylists;
using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Browsing;
using WaveMP.Core.Subsonic.Elements;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Core.Users.Profiles;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Playlists;

internal sealed class SubsonicPlaylistService(
    IPlaylistService playlists,
    ISmartPlaylistService smartPlaylists,
    SubsonicElementLoader loader)
    : ISubsonicPlaylistService
{
    public async Task<SubsonicResponse> GetPlaylistsAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // Only admins may list the playlists of others in Subsonic; the Subsonic API of WaveMP has no admin functions.
        if (parameters.Get("username") is { } userName && !IsUser(user, userName))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.NotAuthorized);
        }

        var query = new PlaylistListQuery(user.Id, 0, SubsonicLimits.Unlimited);
        Page<PlaylistItem> page = await playlists.ListAsync(query, cancellationToken);
        Page<SmartPlaylistItem> smart = await smartPlaylists.ListAsync(query, cancellationToken);

        // The normal playlists come first, then the smart ones, each sorted by name.
        return SubsonicResponse.Ok(new SubsonicElement("playlists").AddList(
            "playlist",
            [
                .. page.Items.Select(item => SubsonicElements.Playlist(item, user.Name)),
                .. smart.Items.Select(item => SubsonicElements.SmartPlaylist(item, user.Name)),
            ]));
    }

    public async Task<SubsonicResponse> GetPlaylistAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.Has("id"))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        if (SubsonicIds.IsSmartPlaylist(parameters.Get("id")))
        {
            return SubsonicIds.ParseSmartPlaylist(parameters.Get("id")) is { } smartId
                ? await SmartPlaylistAsync(user, smartId, cancellationToken)
                : SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
        }

        return parameters.GetId("id") is { } id
            ? await PlaylistAsync(user, id, cancellationToken)
            : SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
    }

    public async Task<SubsonicResponse> CreatePlaylistAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseIds(parameters.GetAll("songId"), out List<Guid> songIds))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
        }

        string? name = parameters.Get("name");
        if (parameters.Has("playlistId"))
        {
            if (SubsonicIds.IsSmartPlaylist(parameters.Get("playlistId")))
            {
                return await SmartPlaylistReadOnlyAsync(user, parameters.Get("playlistId"), cancellationToken);
            }

            if (parameters.GetId("playlistId") is not { } playlistId
                || await playlists.GetAsync(user.Id, playlistId, cancellationToken) is null)
            {
                return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
            }

            if (name is not null)
            {
                PlaylistResult renamed = await playlists.RenameAsync(user.Id, playlistId, name, cancellationToken);
                if (!renamed.IsOk)
                {
                    return Failed(renamed.Status);
                }
            }

            Page<PlaylistEntryItem>? entries = await playlists.ListEntriesAsync(
                user.Id,
                playlistId,
                0,
                SubsonicLimits.Unlimited,
                cancellationToken);
            foreach (PlaylistEntryItem entry in entries?.Items ?? [])
            {
                await playlists.RemoveEntryAsync(user.Id, playlistId, entry.Entry.Id, cancellationToken);
            }

            if (songIds.Count > 0)
            {
                PlaylistEntriesResult added = await playlists.AddEntriesAsync(
                    user.Id,
                    playlistId,
                    songIds,
                    null,
                    cancellationToken);
                if (!added.IsOk)
                {
                    return Failed(added.Status);
                }
            }

            return await PlaylistAsync(user, playlistId, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        PlaylistResult created = await playlists.CreateAsync(user.Id, name, songIds, cancellationToken);
        return created.IsOk
            ? await PlaylistAsync(user, created.Item.Playlist.Id, cancellationToken)
            : Failed(created.Status);
    }

    public async Task<SubsonicResponse> UpdatePlaylistAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.Has("playlistId"))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        if (SubsonicIds.IsSmartPlaylist(parameters.Get("playlistId")))
        {
            return await SmartPlaylistReadOnlyAsync(user, parameters.Get("playlistId"), cancellationToken);
        }

        if (parameters.GetId("playlistId") is not { } playlistId
            || await playlists.GetAsync(user.Id, playlistId, cancellationToken) is null)
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
        }

        if (!TryParseIds(parameters.GetAll("songIdToAdd"), out List<Guid> songIds))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
        }

        if (parameters.Get("name") is { } name)
        {
            PlaylistResult renamed = await playlists.RenameAsync(user.Id, playlistId, name, cancellationToken);
            if (!renamed.IsOk)
            {
                return Failed(renamed.Status);
            }
        }

        IReadOnlyList<string> removals = parameters.GetAll("songIndexToRemove");
        if (removals.Count > 0)
        {
            Page<PlaylistEntryItem>? entries = await playlists.ListEntriesAsync(
                user.Id,
                playlistId,
                0,
                SubsonicLimits.Unlimited,
                cancellationToken);
            IReadOnlyList<PlaylistEntryItem> items = entries?.Items ?? [];
            HashSet<int> indexes = [];
            foreach (string value in removals)
            {
                if (!int.TryParse(value, out int index) || index < 0 || index >= items.Count)
                {
                    return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
                }

                indexes.Add(index);
            }

            // Entries are removed by their ids, so the indexes keep referring to the playlist before the change.
            foreach (int index in indexes)
            {
                await playlists.RemoveEntryAsync(user.Id, playlistId, items[index].Entry.Id, cancellationToken);
            }
        }

        if (songIds.Count > 0)
        {
            PlaylistEntriesResult added = await playlists.AddEntriesAsync(
                user.Id,
                playlistId,
                songIds,
                null,
                cancellationToken);
            if (!added.IsOk)
            {
                return Failed(added.Status);
            }
        }

        return SubsonicResponse.Ok();
    }

    public async Task<SubsonicResponse> DeletePlaylistAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.Has("id"))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        // Deleting a smart playlist in an app deletes it, as in the web app; its songs stay in the library.
        bool deleted = SubsonicIds.IsSmartPlaylist(parameters.Get("id"))
            ? SubsonicIds.ParseSmartPlaylist(parameters.Get("id")) is { } smartId
                && await smartPlaylists.DeleteAsync(user.Id, smartId, cancellationToken)
            : parameters.GetId("id") is { } id && await playlists.DeleteAsync(user.Id, id, cancellationToken);
        return deleted ? SubsonicResponse.Ok() : SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
    }

    private async Task<SubsonicResponse> PlaylistAsync(
        SubsonicUser user,
        Guid playlistId,
        CancellationToken cancellationToken)
    {
        PlaylistItem? playlist = await playlists.GetAsync(user.Id, playlistId, cancellationToken);
        Page<PlaylistEntryItem>? entries = await playlists.ListEntriesAsync(
            user.Id,
            playlistId,
            0,
            SubsonicLimits.Unlimited,
            cancellationToken);
        if (playlist is null || entries is null)
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
        }

        SubsonicElement element = SubsonicElements.Playlist(playlist, user.Name);
        element.AddList(
            "entry",
            await loader.SongsAsync(
                user.Id,
                [.. entries.Items.Select(entry => entry.Track)],
                "entry",
                cancellationToken));
        return SubsonicResponse.Ok(element);
    }

    /// <summary>
    /// A smart playlist with its songs of right now, computed as for the tracks of the smart playlist in the REST API.
    /// </summary>
    private async Task<SubsonicResponse> SmartPlaylistAsync(
        SubsonicUser user,
        Guid smartPlaylistId,
        CancellationToken cancellationToken)
    {
        SmartPlaylistItem? playlist = await smartPlaylists.GetAsync(user.Id, smartPlaylistId, cancellationToken);
        Page<TrackRow>? tracks = await smartPlaylists.ListTracksAsync(
            user.Id,
            smartPlaylistId,
            0,
            SubsonicLimits.Unlimited,
            cancellationToken);
        if (playlist is null || tracks is null)
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
        }

        SubsonicElement element = SubsonicElements.SmartPlaylist(playlist, user.Name);
        element.AddList("entry", await loader.SongsAsync(user.Id, tracks.Items, "entry", cancellationToken));
        return SubsonicResponse.Ok(element);
    }

    /// <summary>
    /// The answer to a change of a smart playlist: its songs follow its rules, so the user may not change them; a
    /// smart playlist that does not exist is not found.
    /// </summary>
    private async Task<SubsonicResponse> SmartPlaylistReadOnlyAsync(
        SubsonicUser user,
        string? value,
        CancellationToken cancellationToken)
    {
        return SubsonicIds.ParseSmartPlaylist(value) is { } smartId
            && await smartPlaylists.GetAsync(user.Id, smartId, cancellationToken) is not null
                ? SubsonicResponse.Failed(SubsonicErrorCode.NotAuthorized)
                : SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
    }

    private static bool IsUser(SubsonicUser user, string userName)
    {
        return ProfileNames.TryNormalize(userName, out string name)
            && string.Equals(
                ProfileNames.ToNormalized(name),
                ProfileNames.ToNormalized(user.Name),
                StringComparison.Ordinal);
    }

    private static bool TryParseIds(IReadOnlyList<string> values, out List<Guid> ids)
    {
        ids = [];
        foreach (string value in values)
        {
            if (SubsonicIds.Parse(value) is not { } id)
            {
                return false;
            }

            ids.Add(id);
        }

        return true;
    }

    /// <summary>
    /// Missing playlists and songs are "not found"; an invalid name or a full playlist are generic errors, the
    /// specification has no codes for them.
    /// </summary>
    private static SubsonicResponse Failed(PlaylistStatus status)
    {
        return SubsonicResponse.Failed(status switch
        {
            PlaylistStatus.NotFound or PlaylistStatus.EntryNotFound or PlaylistStatus.TrackNotFound =>
                SubsonicErrorCode.NotFound,
            _ => SubsonicErrorCode.Generic,
        });
    }
}
