# WaveMP M4: Playback

The fourth milestone of WaveMP takes your music beyond the browser: cast songs and music videos to Chromecast and AirPlay devices, send what you listen to to Last.fm and ListenBrainz, and play your library with Subsonic apps on your phone. The image now also fits NAS systems better, with a configurable user for the files WaveMP writes.

## Casting

- **Cast to your devices.** The new cast button in the transport bar (in the expanded player on smartphones) opens the device selection of your browser: Chrome and Edge show your Chromecast devices and TVs with Chromecast built in, Safari on Mac, iPhone and iPad shows its AirPlay menu.
- **Picks up where you are.** Choose a device, and the current song goes on there at the same position. The device shows title, artist, album and cover; with the **Video** view chosen, it plays the music video of the song.
- **Controlled from WaveMP.** The queue stays in WaveMP, and play, pause, seek, skip and the volume slider control the device. The transport bar and **Now Playing** show **Playing on** with the name of the device.
- **Plays what the device can.** Songs the device cannot play are transcoded by the server. While casting, the device plays the music as it is, so the equalizer, crossfade, volume leveling and the visualization are greyed out.
- **Back to the browser.** Open the device selection again to stop casting; the song then waits paused in the browser at the same position.
- **HTTPS for Chromecast.** Chromecast needs WaveMP to be opened over HTTPS; without it the button is greyed out and says so. AirPlay works without HTTPS. Browsers without casting support, such as Firefox, show no cast button. For Chromecast, the browser loads the Cast SDK from Google when WaveMP starts.
- **Longer-lived addresses for devices.** A casting device may load a song again long after it started, for example after a pause. How long its stream and cover addresses stay valid can be changed with `Streaming__CastSignedUrlLifetime` (6 hours on top of the length of the song by default).

## Scrobbling

- **Last.fm and ListenBrainz.** Under **Settings > Scrobbling**, every profile connects its own Last.fm or ListenBrainz account, and the songs it listens to are sent there.
- **Easy to connect.** **Connect ListenBrainz** asks for your user token. **Connect Last.fm** takes you to Last.fm, where you allow WaveMP to scrobble, and then back to WaveMP.
- **The usual rules.** While a song plays, WaveMP reports it as "Now Playing". Once you have listened to half of it, or to 4 minutes of longer songs, the play is scrobbled; songs shorter than 30 seconds are never scrobbled.
- **Nothing gets lost.** Scrobbles wait in a queue on the server and are sent in the background. When a service is down or WaveMP restarts, they are sent later, with longer and longer pauses of up to an hour between the tries. Scrobbles older than 14 days are dropped, because the services no longer accept them.
- **Always in view.** The settings show for each service whether it is connected, as which account, when the last song was sent and how many songs are still waiting. If a service no longer accepts WaveMP, for example because you revoked its access, the settings show **Access revoked**; connect it again, and the waiting scrobbles are sent.
- **Last.fm API account.** Last.fm needs an API account for each installation. Create one at Last.fm and put its key and secret into `LASTFM_API_KEY` and `LASTFM_SHARED_SECRET` in your `.env` file; the README explains the steps. ListenBrainz needs no setup on the server.
- Access to both services is stored encrypted.

## Subsonic apps

- **Your library in Subsonic apps.** WaveMP now speaks the Subsonic API (version 1.16.1 with the OpenSubsonic extensions), so apps made for Subsonic and Navidrome servers play your library, for example Symfonium on Android and play:Sub or Substreamer on iPhone and iPad.
- **Off until you allow it.** Under **Settings > Subsonic apps**, switch on **Allow Subsonic apps** for your profile. While it is off, no app can sign in as the profile.
- **App passwords.** **Create app password** creates a password just for the apps, shown only once and stored encrypted. **Create new app password** replaces it, **Revoke app password** stops all apps of the profile. Your PIN is never used for the apps.
- **Simple sign-in.** In the app, enter the address you open WaveMP with, the name of your profile and the app password. The settings page shows the address and the user name to use.
- **Same music, same profile.** The apps see the same library with the favorites, ratings, playlists and play history of the profile. Plays from the apps are scrobbled like plays in the web app, and **Now Playing** in the apps shows what the profiles are playing.
- **Streams that fit.** Formats a phone usually plays are streamed as they are; other formats and songs above the bitrate an app asks for are transcoded on the fly.
- **Downloads only when allowed.** Downloads of the original files for offline use are off by default. An admin allows them for all profiles under **Administration > Library** with **Subsonic apps may download songs**.

## Installation

- **Choose the user of WaveMP.** The image now starts as root, hands the data folder to the user `PUID` and the group `PGID` (both `1654` by default) and then runs WaveMP as this user. Files WaveMP writes, such as edited tags, the exports of the database and the cache, belong to this user. A data folder that your server created as root now works without further steps.
- **Hardware transcoding without extra groups.** The groups of the render devices are added by themselves, so hardware transcoding no longer needs `group_add`. Groups you added with `group_add` are kept.
- **Your own user still works.** If you start the container with `user:` (`--user`), as TrueNAS does, WaveMP runs as that user as before, and `PUID` and `PGID` are ignored. `PUID=0` runs WaveMP as root.
- **Keys in the data folder.** WaveMP keeps the keys it protects data with, such as the access to scrobbling services and the app passwords, in the new folder `keys` in `/data`. Keep it in your backup together with the rest of `/data`.
- **A quieter start.** WaveMP no longer logs warnings about missing keys at every start when it runs as a user without a home folder, as on TrueNAS.

## Improvements

- **Review button in place.** In the theme Midnight, the review button of admins now sits in the header before the server status and matches the look of the tabs, instead of a new column after the profile button.
- **Playlists during a scan.** Creating a playlist or adding songs to it while a scan removes one of these songs no longer causes a server error; WaveMP reports the missing song instead.

## For developers

- **New API endpoints** for scrobbling (`/api/v1/me/scrobbling`), the song that is playing (`/api/v1/me/now-playing`) and the access of Subsonic apps (`/api/v1/me/subsonic`), all described in the OpenAPI document.
- **Cast addresses.** The playback endpoints of songs and videos take the new parameter `target` (`Local` or `Cast`); for casting they also return a signed address of the cover.
- **Subsonic API** under `/rest`, separate from the WaveMP API, in XML and JSON.

## Upgrade notes

- **Unraid and CasaOS.** Installations that ran WaveMP as root keep working. To run it as another user, set `PUID` and `PGID` (for example `99` and `100` on Unraid), remove `--user 0:0` or `user: "0:0"`, and hand the existing data folder to that user once, for example `chown -R 99:100 /mnt/user/appdata/wavemp`.
- `docker exec` into the container now runs as root by default.

## Known limitations

- Casting and Subsonic apps need the device or the app to reach your WaveMP server. Do not expose WaveMP to the internet without protection; use a VPN or a reverse proxy with HTTPS.
- Subsonic apps cannot use bookmarks, shares, podcasts or similar songs, and WaveMP offers no admin functions through the Subsonic API.
- WaveMP is not listed in the app catalogs of TrueNAS, Unraid, ZimaOS and CasaOS yet.
- WaveMP ships with the theme Midnight only.
