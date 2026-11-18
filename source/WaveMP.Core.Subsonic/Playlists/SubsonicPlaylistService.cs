using WaveMP.Core.Playlists.Playlists;
using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Browsing;
using WaveMP.Core.Subsonic.Elements;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Core.Users.Profiles;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Playlists;

internal sealed class SubsonicPlaylistService(IPlaylistService playlists, SubsonicElementLoader loader)
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

        Page<PlaylistItem> page = await playlists.ListAsync(
            new PlaylistListQuery(user.Id, 0, SubsonicLimits.Unlimited),
            cancellationToken);
        return SubsonicResponse.Ok(new SubsonicElement("playlists").AddList(
            "playlist",
            page.Items.Select(item => SubsonicElements.Playlist(item, user.Name))));
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

        return parameters.GetId("id") is { } id && await playlists.DeleteAsync(user.Id, id, cancellationToken)
            ? SubsonicResponse.Ok()
            : SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
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
