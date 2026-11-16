# WaveMP

**A nostalgic media player for your self-hosted music and music videos.**

[![Main](https://github.com/wavemp-app/wavemp/actions/workflows/main.yml/badge.svg?branch=main)](https://github.com/wavemp-app/wavemp/actions/workflows/main.yml)
[![Release](https://img.shields.io/github/v/release/wavemp-app/wavemp)](https://github.com/wavemp-app/wavemp/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

> **Status:** WaveMP is in active development and already a complete player for your music and music videos. Everything listed under Features works today, except where it is marked as planned; the [releases](https://github.com/wavemp-app/wavemp/releases) describe what each milestone added. Profiles cannot be protected by a PIN yet, so do not expose WaveMP to the internet without protection.

## About

WaveMP brings back the glossy, glassy look of the classic desktop media players from the Frutiger Aero era, and combines it with what you expect from a modern music app. Point it at your media folder, and it builds a library you can play from any browser on your network.

## Features

- **Your library, your files.** WaveMP scans a folder and organizes your music. It reads tags first and falls back to your folder structure.
- **Tag editor.** Admins correct title, artist, album, album artist, year, track and disc number and genre right in WaveMP. Changes are written into the audio file, never into videos, and the previous tags are kept in the database.
- **Music and music videos together.** Switch seamlessly between the audio track, its music video, its cover and a visualization.
- **Fast library.** Covers are scaled to the size they are shown in and cached, so lists and tiles load quickly.
- **Profiles.** Pick your profile, no login required. Each profile has its own playlists and history.
- **Gapless player.** Songs follow each other without a gap, with a queue you can edit, shuffle, repeat, volume leveling (ReplayGain) and the media keys and lock screen controls of your device.
- **Equalizer, crossfade and sleep timer.** A ten-band equalizer with presets and your own curve, crossfades of up to twelve seconds that leave the songs of an album gapless, and a sleep timer that fades the music out after a time or at the end of the song or the queue.
- **Favorites and ratings.** Mark songs, albums and artists with a heart, rate songs with up to five stars and narrow the whole library down to your own music. The home page greets you with your playlists, the songs you played recently, new albums, your favorites and your top rated songs.
- **Playlists.** Each profile builds its own playlists, sorted by drag and drop, with an own cover or the covers of their songs fading into each other, plus an automatic list of the songs played recently.
- **Visualizations.** Classic Milkdrop presets plus WaveMP's own styles, *A-Force* and *BlackCap*.
- **Plays almost anything.** Formats your browser supports are streamed directly; everything else is transcoded on the fly with ffmpeg. Intel Quick Sync is supported for hardware acceleration.
- **Easy setup.** One compose file, one password, your media folder. That's it.
- **Daily backups.** The database is exported every day and before every update, and the last exports are kept next to your other data.
- **Open API.** A documented REST API (OpenAPI) for building your own clients. Native apps are planned.
- **Installable as an app (PWA)** on desktop and mobile devices.
- **English and German.** More languages will follow through community translations.
- **Themes.** The look is a theme: one CSS file plus graphics, from colors to the complete desktop layout. WaveMP ships with the dark glass theme *Midnight*; see [THEMING.md](THEMING.md) for writing your own.

## Screenshots

| | |
|---|---|
| ![Home with playlists, the songs played recently and new albums](assets/screenshots/home.png) | ![The visualization A-Force on Now Playing](assets/screenshots/visualization-a-force.png) |
| **Home** with your playlists, the songs you played recently and new albums | **A-Force**, one of WaveMP's own visualizations |
| ![The visualization BlackCap on Now Playing](assets/screenshots/visualization-blackcap.png) | ![A classic Milkdrop preset on Now Playing](assets/screenshots/visualization-milkdrop.png) |
| **BlackCap**, the calm 3D visualization | Classic **Milkdrop** presets |
| ![Now Playing with the cover and the queue](assets/screenshots/now-playing.png) | ![The page of an album](assets/screenshots/album.png) |
| **Now Playing** with cover and queue | An **album** with its songs |
| ![The profile selection](assets/screenshots/profiles.png) | ![The backups in the administration](assets/screenshots/admin-backups.png) |
| **Profiles** instead of a login | **Administration** of the daily backups |

<p>
  <img src="assets/screenshots/phone-now-playing.png" alt="Now Playing with A-Force on a smartphone" width="260">
  <img src="assets/screenshots/phone-library.png" alt="The albums of the library on a smartphone" width="260">
</p>

The screenshots show the synthetic test media of the end-to-end tests (sine tones and test pictures) in the theme *Midnight*.

## Quick Start

WaveMP runs as two containers: the app and a PostgreSQL database.

Create a `.env` file with a database password of your choice:

```env
DB_PASSWORD=change-me-to-a-long-random-password
```

Create a `docker-compose.yml` next to it:

```yaml
services:
  wavemp:
    image: ghcr.io/wavemp-app/wavemp:latest
    container_name: wavemp
    ports:
      - "8080:8080"
    environment:
      DB_HOST: wavemp-db
      DB_PASSWORD: ${DB_PASSWORD:?Please set DB_PASSWORD in .env}
    volumes:
      # Data of WaveMP that is not in the database: the daily exports of the database (backups),
      # the covers of playlists and the cache of scaled covers.
      - wavemp-data:/data
      # Append :ro to keep WaveMP from changing your files; the metadata editor is then off.
      - /path/to/your/media:/media
    depends_on:
      wavemp-db:
        condition: service_healthy
    restart: unless-stopped

  wavemp-db:
    image: postgres:18
    container_name: wavemp-db
    environment:
      POSTGRES_USER: wavemp
      POSTGRES_DB: wavemp
      POSTGRES_PASSWORD: ${DB_PASSWORD:?Please set DB_PASSWORD in .env}
    volumes:
      - wavemp-db:/var/lib/postgresql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U wavemp -d wavemp"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

volumes:
  wavemp-data:
  wavemp-db:
```

The database is not exposed outside the Docker network. The app image is also available on Docker Hub as `wavemp/wavemp`.

Start it:

```bash
docker compose up -d
```

Open `http://<your-server>:8080` and select the **Admin** profile to set up your library. WaveMP scans your media folder one minute after the start and then every six hours; changes are also picked up within seconds by watching the folder.

WaveMP can be installed as an app (PWA). Browsers require HTTPS for that, except on `localhost`, for example behind a reverse proxy with TLS. Casting to Chromecast devices, which is planned, will need HTTPS as well.

### Image tags

Images are built for `linux/amd64` and `linux/arm64` and published as `ghcr.io/wavemp-app/wavemp` and on Docker Hub as `wavemp/wavemp`, both with the same tags. Every change on `main` that passes all tests is published as a new version `1.0.N`:

| Tag | Content |
|---|---|
| `latest` | Current version; the Quick Start uses it |
| `1.0.N` | A specific version, for example `1.0.42`; use it to pin your installation and update on your own schedule |
| `1.0` | Current version of the 1.0 series |
| `edge` | Same as `latest` |
| `sha-<commit>` | The version of a specific commit on `main` (short hash) |

The running version is shown under **Settings** and reported by `GET /api/v1/health`. Each completed milestone is also marked as a [GitHub release](https://github.com/wavemp-app/wavemp/releases) with release notes, named after the version it was published with.

### Settings

| Variable | Description | Default |
|---|---|---|
| `DB_PASSWORD` | Database password, used by both containers | *(required)* |
| `ADMIN_PIN` | Protects the admin profile with a PIN (planned, not available yet) | *(none)* |
| `MEDIA_PATH` | Media folder inside the container; only change it if you mount your media elsewhere | `/media` |
| `DATA_PATH` | Data folder inside the container (cache, covers of playlists and the exports of the database); only change it if you mount the volume elsewhere | `/data` |
| `TZ` | Time zone of the server, for example `Europe/Berlin`; the daily backup runs at its time of day in this zone | `UTC` |
| `Streaming__SignedUrlLifetime` | How long a stream address stays valid after it was handed out, on top of the length of the song | `01:00:00` |
| `Streaming__MaxSignedUrlLifetime` | Upper limit of that validity, also for very long songs | `1.00:00:00` |
| `Streaming__HistoryMaxAge` | How old a play may be when a client reports it to the history, for example after it was offline | `7.00:00:00` |
| `Transcoding__RenderDevice` | Render device for hardware transcoding, for example `/dev/dri/renderD129` on a server with more than one GPU; by default the first device under `/dev/dri` | *(first device)* |

Audio, video and other media are streamed through short-lived signed addresses, bound to the song and the profile, because players and casting devices cannot send a login token. The signing key is created on the first start and kept in the database.

### Hardware transcoding (optional)

Songs and videos your browser cannot play are transcoded on the fly by ffmpeg into an HLS stream (H.264 video, AAC sound). Software transcoding always works. On a server with an Intel CPU or GPU with Quick Sync, WaveMP encodes videos on the graphics hardware instead, which needs much less processor power; the image brings the Intel drivers (VA-API and oneVPL, `linux/amd64` only).

Pass the render device into the container and give the container user access to it:

```yaml
services:
  wavemp:
    # ...
    devices:
      - /dev/dri:/dev/dri
    group_add:
      # Group of the render device on the host: stat -c '%g' /dev/dri/renderD128
      - "993"
```

The container runs as user `1654`, which is not a member of the groups of the host; `group_add` with the numeric group id of the device (usually the group `render`, sometimes `video`) lets it open the device. Check the id with `stat -c '%g' /dev/dri/renderD128` and replace `993` with it.

On every start, and when the setting changes, WaveMP looks for a render device under `/dev/dri` and tests Quick Sync (QSV) and then VA-API with a short test encode. **Administration > Transcoding** shows the result: the encoder and the device in use, or why the hardware is not used (no render device, test failed). If a transcode on the hardware fails, WaveMP repeats it in software. Set **Hardware acceleration** there to **Off (software only)** to always transcode in software, and choose the device with `Transcoding__RenderDevice` if the server has more than one GPU.

### Media folder access

- Mount the folder **read-only** (`/path/to/your/media:/media:ro`) if WaveMP should never change your files. The metadata editor then shows its fields greyed out with a hint.
- Mount it **writable** if you want to edit tags from within WaveMP. Changes are only written to audio files, never to videos, and file names stay as they are.
- The container runs as user `1654` (`app`); your files and folders must be readable for it. To edit tags, the audio files must also be writable for it, for example through a group with write permission that you add to the container with `group_add` in the compose file. No rescan is needed after changing the mount or the permissions: the editor checks the file each time it opens.

### Data folder and cache

WaveMP keeps everything that is not in the database in the volume mounted at `/data`. Its folder `cache` holds the covers scaled to 96, 300 and 1000 pixels as WebP, so lists and tiles load quickly. They are created the first time a cover is shown; after every scan WaveMP also prepares the two smaller sizes of every cover and removes those of changed or deleted pictures (job **Prepare covers** under **Administration > Jobs**). The cache is limited to 2 GB by default: when it grows larger, the files used least recently are removed. Change the limit or clear the cache under **Administration > Cache**.

The folder `playlists` holds the pictures uploaded as covers of playlists, as square WebP files in the three sizes. Unlike the cache, it belongs in your backup.

The cache also holds transcoded media in its folder `transcodes`: the segments of the HLS streams of songs and videos your browser cannot play. A part that was transcoded once comes from the cache the next time; like the covers, the segments count towards the size limit and are removed when they were used least recently.

Everything in the cache is created again when it is needed, so it does not have to be backed up. If `/data` cannot be written, WaveMP still works, shows the covers unscaled and skips songs and videos that would need transcoding; the cache page, the transcoding page and the log tell you so.

### Backup & Restore

WaveMP exports its database every day at 03:00 into the folder `backups` of the data volume (`/data/backups`), as a compressed PostgreSQL dump named after the time (UTC) and what created it, for example `wavemp-20261116T030000Z-schedule.dump`. The last 7 exports are kept; older ones are deleted after every export. Before an update changes the structure of the database, WaveMP exports it first (`...-migration.dump`); if that export fails, WaveMP does not touch the database and stops with an error in the log. **Administration > Backups** shows the last and next backup and the exports, backs up on demand, deletes exports and sets the time of day and the number of exports to keep. The time of day is the local time of the server; set the time zone with `TZ`, for example `TZ: Europe/Berlin` under `environment` of `wavemp`.

The exports stay on the same disk as the database, so copy them elsewhere with the backup tool of your choice. Back up:

- the volume `wavemp-data` (mounted at `/data`): the exports of the database in `backups` and the covers of playlists in `playlists`; its folder `cache` can be left out,
- your media folder, which WaveMP does not back up.

The database volume `wavemp-db` itself does not need to be copied: the exports contain everything in it, while a copy of the files of a running database may be inconsistent.

To restore an export, stop WaveMP, replace the database with the export and start WaveMP again. In the folder of your `docker-compose.yml`, with the file name of the export:

```bash
docker compose stop wavemp
docker compose cp wavemp:/data/backups/wavemp-20261116T030000Z-schedule.dump ./restore.dump
docker compose exec wavemp-db dropdb --username=wavemp --force wavemp
docker compose exec wavemp-db createdb --username=wavemp wavemp
docker compose exec -T wavemp-db pg_restore --username=wavemp --dbname=wavemp --no-owner < ./restore.dump
docker compose start wavemp
```

An export of an older version of WaveMP is updated to the current version on the next start. To restore on a new server, start WaveMP once with an empty database, copy the data volume back (or the export into the new volume) and restore the export as above. Restoring is not possible in the web app, so a wrong click cannot replace your library.

## Organizing Your Media

All files that belong to one track must share the **same name** in the same folder. Only the extension differs:

```
Artist/
  Album/
    cover.jpg            ← album cover
    Song.flac            ← audio
    Song.mkv             ← music video
    Song.jpg             ← track cover
```

Any folder structure works, for example:

```
Artist/Album/Song.mp3
Artist - Song.mp3
Song.mp3
```

Files are grouped without regard to case, so `Song.MP3` and `song.jpg` belong together. Which extensions count as audio, video and pictures can be changed under **Administration > Library**.

**Metadata.** WaveMP reads the tags of your files first (title, artist, album, album artist, track and disc number, year, genre). Whatever the tags leave open is taken from the folder and file names, for example:

| Path | Result |
|---|---|
| `Artist/Album/01 - Title.mp3` | artist, album, track 1, title |
| `Artist/Album/CD2/03 Title.mp3` | also disc 2 (`CD1`, `Disc 2` and similar folders are discs of one album) |
| `Artist - Album/02. Title.mp3` | artist and album from the folder |
| `Artist/2001 - Album/Title.mp3` | also the year (`(2001) Album`, `[2001] Album` and `Album (2001)` work too) |
| `Artist/Title.mp3`, `Artist - Title.mp3` | artist and title |
| `weird.mp3` | title `weird` |

A number at the start of a file name is only a track number inside an album folder or with a leading zero, so `50 Cent - Song.mp3` keeps its artist. If most file names of a folder share a pattern, for example all files in the root named `Artist - Title` or `01 - Artist - Title`, WaveMP uses it for all of them. Albums without an album artist tag get the artist of their tracks; tracks of several artists in one album folder become a compilation.

**Covers.** A picture named `cover`, `folder`, `front`, `album` or `albumart` (in this order, any picture format) is the cover of the album in its folder, and for disc folders such as `CD1` also in the folder above. A picture with the name of a track (`Song.jpg`) is the cover of that track, otherwise a picture embedded in its files is used. By default, the album cover is shown before a track's own cover.

**Duplicates.** If there are several audio files with the same name (e.g. `Song.mp3` and `Song.flac`), the one with the highest quality plays: lossless before lossy, then the higher bit depth, sample rate and bitrate. For videos the higher resolution wins, then the bitrate. All files are kept in the library.

**Scans.** Scans are incremental: only files that are new or have a new size or change time are read again, and files that are gone are removed. Hidden folders (starting with a dot) and the system folders of NAS devices and operating systems (`@eaDir`, `#recycle`, `#snapshot`, `$RECYCLE.BIN`, `System Volume Information`, `lost+found`) are skipped. If the media folder is suddenly empty while the library has files, for example because a network share is not mounted, the scan stops with an error and removes nothing. Watching the folder for changes is not reliable on network shares (NFS, SMB); the periodic scan therefore always stays active.

## Usage

1. **Select a profile.** WaveMP starts with a profile selection instead of a login. On the first start there is only the built-in **Admin** profile. The selection is remembered on the device, so the next start goes straight on. After the selection, WaveMP opens **Home**.
2. **Create profiles.** As admin, open the profile menu (your initials at the top right), choose **Administration** and then **Profiles** to add, edit or delete profiles: name, initials (created from the name if left empty), color and role (admin or user). The built-in admin profile can be renamed, but it cannot be deleted and always stays an admin. Once there are other profiles, it is shown less prominently in the selection.
3. **Switch profiles** with **Switch profile** in the profile menu.
4. **Choose your language** under **Settings** in the profile menu. It is stored with the profile and applies on all devices; before a profile is selected, WaveMP uses the language of the browser (English or German, otherwise English).
5. **Scan your library.** As admin, open **Administration > Library**: **Scan now** starts a scan of your media folder, shows its progress and then its result (files found, added, updated and removed, tracks, albums and artists, and files with problems). Below are the history of scans and the settings: how often the library is scanned, whether the media folder is watched for changes, the file extensions of audio, video and pictures and the names of album covers. **Administration > Jobs** lists all background jobs with their last and next run and runs them on demand; **Prepare covers** runs after every scan. **Administration > Cache** shows how much space the cache of scaled covers and transcoded media uses, clears it and sets its maximum size. **Administration > Transcoding** shows whether transcoding works, the state of the hardware acceleration and the running transcodes, which you can stop there, and sets the hardware acceleration, how many transcodes run at the same time, the largest height of transcoded videos and the bitrate of transcoded sound. **Administration > Backups** shows the daily exports of the database, backs up on demand and sets when and how many exports are kept (see [Backup & Restore](#backup--restore)).
6. **Browse your library.** **Library** in the sidebar (the drawer behind the menu button on smartphones) has the sections **Recently Added** (newest first), **Favorites**, **Artist**, **Album**, **Songs** (grouped by album with cover), **Genre** and **Year**. On the desktop, the button at the top of the sidebar collapses it to its icons, which show their names when you point at them, and expands it again; the sidebar stays as you left it, stored with your profile and on all devices. On smartphones the drawer always shows the whole sidebar. Albums, artists, genres and years open their own page; the details of a song (cover, artist, album, length, audio and video) open from its context menu: right click, the context menu key or a long press on touch screens. Admins also see all files of a song there, with their technical details.
7. **Edit metadata.** As admin, choose **Edit metadata** on the page of a song or in its context menu. Change title, artist, album, album artist, year, track and disc number or genre and save: WaveMP writes the changes into the audio file the song plays, keeps the previous tags in the database and reads the song again, so the library shows the new values at once. An empty field removes the value from the tags; WaveMP then takes it from the folder and file name again. Songs with a video only and files that cannot be written show the editor greyed out with a hint.
8. **Search.** Type into the search field at the top: WaveMP searches artists, albums and songs while you type, and small typos are fine. **Show all** opens the full list filtered by your text. On the desktop, the sort button next to the search sorts the current list; sort order and search text are part of the address, so you can bookmark or share them.
9. **Play.** Double click a song (tap on touch screens) or press Enter to play the list from there; **Play** and **Shuffle** on the page of an album play the whole album. The context menu of a song also offers **Play next** and **Add to queue**. The transport bar plays and pauses, skips, seeks, sets the volume (remembered on the device), shuffles and repeats the queue or one song. **Now Playing** shows the current song and the queue, where you play, move and remove songs. Songs follow each other without a gap, and the media keys, the lock screen and the notification of your device control the player. A song counts as played in your history once it has played for half its length, at most four minutes. Songs your browser cannot play are transcoded on the fly by the server; only while transcoding is unavailable (see **Administration > Transcoding**) are they skipped with a notice.
10. **Level the volume.** Under **Settings**, **Volume leveling (ReplayGain)** evens out songs that were mastered louder or quieter, by song or by album, if their files carry ReplayGain tags. It is off by default and stored with the profile.
11. **Build playlists.** **Playlists** shows the playlists of your profile and **Recently Played**, the songs you played last, newest first. **Create playlist** (also in the sidebar) asks for a name; **Add to playlist** in the context menu of a song adds it to an existing or a new playlist, as often as you like. On the page of a playlist, **Play** and **Shuffle** play it, the handle in front of each song moves it by drag and drop (with mouse, pen or finger), and the context menu of a song moves it up or down or removes it. **Rename**, **Delete** and **Change cover** (a JPEG, PNG or WebP picture of up to 10 MB, cropped to a square) change the playlist itself; without its own picture, a playlist shows the covers of up to four of its songs, one after the other. The sidebar lists your playlists below **Playlists**.
12. **Mark favorites and rate songs.** The heart marks a song, an album or an artist as favorite of your profile: at the end of each song row, on the page of a song, album or artist, in the context menu of a song (**Add to favorites**) and next to the current song in the transport bar. The stars rate a song with one to five stars; a click on the star of the current rating removes it. On the desktop, the empty hearts and stars of a row appear when you point at it; on smartphones the song lists show the heart only, the stars are on the page of the song and in the expanded player. **Library > Favorites** lists your favorite songs, albums and artists, newest first. **All music** and **My music** above the sections of the library switch between the whole library and your own music: rated and favorite songs, the songs of your favorite albums and artists and of your playlists.
13. **Start at home.** **Home** shows rows of your playlists, the songs you played recently, the albums added recently, your favorites and your top rated songs. **Show all** opens the full list, **Play** plays the recently played or top rated songs. As long as there is nothing to show, it greets you with a welcome text.
14. **Watch music videos.** **Now Playing** shows the song in one of three views, which its buttons switch with a soft crossfade: **Cover**, **Video** and **Visualization** (see the next step). The sound always comes from the song: the video plays muted and follows its position, so switching the view or leaving **Now Playing** never interrupts the music. **Video** can be chosen for songs with a video only; songs without one show their cover meanwhile. The view you choose is stored with your profile and applies on all devices. A double click on the video or its full screen button shows it in full screen. Your browser plays the video file as it is if it can, otherwise the server transcodes it. If the music in a video starts earlier or later than in the song, admins set the offset in milliseconds under **Video sync** on the page of the song; a playing video takes it over at once.
15. **Watch the music.** **Visualization** on **Now Playing** draws the music as it is heard. Its controls (on hover, on touch screens always) choose between three visualizations: **Milkdrop** with the classic Milkdrop presets (the preset pack of butterchurn), **A-Force**, WaveMP's own visualization with flowing waveforms, particles, color gradients and camera movements that react to bass, mids, highs and beats, whose presets are its style variants (Aurora, Nebula, Ember, Lagoon, Prism and Eclipse) that blend smoothly into each other, and **BlackCap**, WaveMP's own visualization with organic, floating 3D shapes and light effects that react calmly to the music: the shapes breathe with its tempo, swell gently on a beat, their curved surfaces sway with the spectrum and mirror soft lights; its presets are its scenes (Lumen, Tide, Orchid, Mercury, Halo and Dusk), which slowly morph into each other over a few seconds. The visualization starts with the first song you play. The controls also choose a preset from the list or step to the previous or next one, switch the automatic change on and off and show the visualization in full screen; a double click switches to full screen as well. While the automatic change is on, a random preset follows every 30 seconds; **Settings > Change visualization presets** sets the interval from 10 seconds to 5 minutes or turns it off. **Settings > Visualization quality** sets how much detail the visualization draws: **Auto** (the default) starts with the most detail and lowers it while the device cannot draw about 60 frames per second, then raises it again after a while of smooth drawing; **Low**, **Medium** and **High** keep a level (A-Force draws fewer particles and waveforms at a lower resolution, BlackCap fewer and less finely curved shapes and fewer lights at a lower resolution, Milkdrop uses smaller textures). The visualization, the preset you choose, the automatic change, its interval and the quality are stored with your profile. The visualization only draws while it is shown: on another view, another page or in a background tab it pauses. It needs WebGL 2 and Web Audio, which current browsers offer; on iPhones the browser offers no full screen for it.
16. **Shape the sound.** The equalizer button in the transport bar (in the expanded player on smartphones) opens the **Equalizer** below **Now Playing**: switch it on, choose a preset (Flat, Rock, Pop, Jazz, Classical, Dance, Bass, Treble, Vocal) or move the ten bands from 31 Hz to 16 kHz and the preamp, by mouse, finger or arrow keys. Moving a band turns the preset into your own curve (**Custom**), which is kept when you try another preset; **Reset** goes back to Flat. The equalizer applies at once, is stored with your profile and applies on all devices; **Settings** switches it on and off as well.
17. **Crossfade.** Under **Settings**, **Crossfade** lets one song fade into the next for up to twelve seconds when the queue moves on by itself. Off plays the songs without gaps. The crossfade button in the transport bar, between repeat and stop (in the expanded player on smartphones), switches the crossfade on and off with one click and shows whether it is on; it keeps the length set under **Settings** (five seconds until you choose one). Like the setting, the switch is stored with your profile and applies on all devices. **Do not crossfade songs of the same album** (on by default) keeps live and concept albums gapless. Equalizer and crossfade need Web Audio, which current browsers offer; without it, WaveMP plays without them.
18. **Sleep timer.** The moon button in the transport bar stops the music after 15 to 120 minutes, at the end of the current song or at the end of the queue; the button shows the remaining time, and **Off** cancels it. Shortly before the stop the music fades out (ten seconds by default, set under **Settings > Sleep timer fade-out**), then the player pauses. The timer runs on the device that plays and is not stored.

Profiles are not protected yet: everyone who can open WaveMP can select any profile, including the admin profile. An optional PIN is planned. Do not expose WaveMP to the internet without protection, for example a reverse proxy with authentication or a VPN.

## Platforms

| Platform | Status |
|---|---|
| Docker (linux/amd64, linux/arm64) | Available (`latest`, or `1.0.N` to pin a version) |
| TrueNAS, ZimaOS and other app catalogs | Planned |
| Android, iOS, Windows, macOS | Planned |

## Building from Source

Requirements: .NET 10 SDK, Node.js 24, Docker, and ffmpeg for running the service and the tests outside of Docker (Windows: `winget install --id Gyan.FFmpeg -e`, Debian/Ubuntu: `sudo apt install ffmpeg`, macOS: `brew install ffmpeg`). The Docker image brings its own ffmpeg.

```bash
git clone https://github.com/wavemp-app/wavemp.git
cd wavemp
cp .env.example .env    # then set DB_PASSWORD
docker compose -f docker-compose.dev.yml up --build
```

This builds the image from source and starts WaveMP on `http://localhost:8080`. Media files go into `./media`. If port 8080 or 5432 is already in use on your machine, set `WAVEMP_PORT` or `DB_PORT` in `.env`.

### Project layout

| Path | Content |
|---|---|
| `source/WaveMP.Service` | ASP.NET Core host (REST API, authentication, serves the web app) |
| `source/WaveMP.Core.Users` | User profiles, sessions and user settings |
| `source/WaveMP.Core.Settings` | Server settings, stored as JSON per group (for example the library settings) |
| `source/WaveMP.Core.Jobs` | Background jobs with Quartz.NET (in memory) and their run history |
| `source/WaveMP.Core.Metadata` | Reading and writing tags (TagLib#), technical information (ffprobe) and metadata from folder and file names |
| `source/WaveMP.Core.Scanning` | Library scan, grouping of files, the scan job, the file watcher and the metadata editor |
| `source/WaveMP.Core.Library` | Library queries: artists, albums, tracks, genres, years, the search and the covers |
| `source/WaveMP.Core.Streaming` | Playback: which audio and video formats a client can play, the signed media addresses and the play history |
| `source/WaveMP.Core.Transcoding` | Transcoding with ffmpeg: HLS playlists and segments in the cache, the ffmpeg processes with their limit and clean-up, and the detection of the hardware acceleration (Intel Quick Sync through QSV or VA-API) |
| `source/WaveMP.Core.Playlists` | Playlists of the profiles with their entries and covers (uploads under `/data/playlists`) and the list of recently played tracks |
| `source/WaveMP.Core.Backups` | Exports of the database with pg_dump into `/data/backups`: the daily backup job, manual exports, the number of kept exports and the export before migrations |
| `source/WaveMP.Core.Caching` | Cache under `/data/cache`: scaled covers (WebP with ffmpeg), their preparation after scans, size limit and clean-up of the files used least recently |
| `source/WaveMP.Data` | EF Core with PostgreSQL (DbContext, entities, repositories, migrations) |
| `source/WaveMP.Web` | Vue 3 web app |
| `tests/` | Unit and integration tests (NUnit), E2E tests (Playwright); `tests/WaveMP.Testing` holds helpers shared by the test projects (test databases, temporary folders, synthetic media) |
| `assets/` | Graphics, icons, themes and license texts; `assets/web` is served unchanged by the web app |
| `.github/` | GitHub Actions workflows, license check script and policy |
| `THEMING.md` | Theme API for theme authors |
| `THIRD-PARTY-NOTICES.md` | Third-party components and their licenses |
| `docs/releases` | Release notes of the milestones |

### Local development

Start only the database, then the service and the web app:

```bash
docker compose -f docker-compose.dev.yml up -d wavemp-db
dotnet tool restore
dotnet user-secrets set DB_PASSWORD "<your DB_PASSWORD>" --project source/WaveMP.Service
dotnet run --project source/WaveMP.Service        # http://localhost:5080

cd source/WaveMP.Web
npm install
npm run dev                                       # http://localhost:5173, proxies /api to the service
```

`dotnet run` uses the folder `./media` of the repository as media folder and `./data` as data folder with the cache (`MEDIA_PATH` and `DATA_PATH` in `Properties/launchSettings.json`); relative paths are resolved against the project folder. The image uses `/media` and `/data`; both folders are ignored by Git. To fill `./media` with synthetic test media (4 tracks, created with ffmpeg), build the image and run:

```bash
docker compose -f docker-compose.dev.yml build
docker run --rm -v "$PWD/media:/media" -v "$PWD/tests/WaveMP.E2E/scripts:/scripts:ro" \
  --entrypoint sh wavemp:dev /scripts/generate-media.sh /media
```

In Git Bash on Windows, prefix the command with `MSYS_NO_PATHCONV=1` and use `$(pwd -W)` instead of `$PWD`.

The backups need `pg_dump` of PostgreSQL 18, which the image contains. Without it on the development machine, manual and daily backups fail with a notice on the backup page; the export before migrations is turned off in development (`Backups:BeforeMigrations` in `appsettings.Development.json`). Set `Backups__PgDumpPath` to use a `pg_dump` outside the `PATH`.

The development server also serves an internal component overview at `http://localhost:5173/dev/components`: all parts of the theme API in all states and the app at mobile and desktop size. It is not part of the production build.

The Phosphor icons in `assets/web/icons` are copied from the npm package `@phosphor-icons/core`. To add an icon, add it to the list in `source/WaveMP.Web/scripts/sync-icons.ts` and run `npm run icons:sync` in `source/WaveMP.Web`.

If you set `DB_PORT` in `.env`, also set it for the service: `dotnet user-secrets set DB_PORT <port> --project source/WaveMP.Service`.

The service applies database migrations on start. To add a migration:

```bash
dotnet ef migrations add <Name> --project source/WaveMP.Data
```

Migrations are generated code: every file is marked with `// <auto-generated />`, so the build and `dotnet format` leave them as they are. Do not reformat or edit them by hand; only remove the byte order mark that `dotnet ef` writes (`.editorconfig` asks for UTF-8 without it). Tables, columns, keys and indexes use snake_case names; the migrations history table keeps its standard names. Commands that need the database, such as `dotnet ef migrations remove`, read `DB_HOST`, `DB_PORT` and `DB_PASSWORD` from the environment (use `DB_HOST=127.0.0.1` for the dev database).

### API client

The OpenAPI document is the only contract between server and clients. Building `WaveMP.Service` writes it to `source/WaveMP.Service/openapi.json`; at runtime it is served at `/openapi/v1.json`. The web app uses a client generated from it with [Kiota](https://learn.microsoft.com/openapi/kiota/):

```bash
cd source/WaveMP.Web
npm run generate:api
```

Commit the updated `openapi.json` and the generated client together with API changes.

Errors are returned as Problem Details ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)) with a machine-readable `code`, for example `request.not_found` or `user.name_taken`. All codes are listed in the `ProblemCode` enum of the OpenAPI document. The API never returns display texts; clients translate the codes. Invalid requests get `400` (`request.invalid`) with a list `errors`: each entry names the `field` (the JSON name of a member, for example `name` or `audioExtensions[2]`, the name of a query parameter, or an empty string for the body as a whole), a `code` from the `ValidationErrorCode` enum (`required`, `min_length`, `max_length`, `min_items`, `max_items`, `out_of_range`, `invalid_format`, `invalid_value`, `duplicate`) and, where the code has limits, `min` and `max`, for example `{ "field": "limit", "code": "out_of_range", "min": 1, "max": 100 }`. Enums are accepted as strings only, as the OpenAPI document describes them; a number is an invalid request.

Authentication is token-based. `GET /api/v1/profiles` lists the profiles without a token; selecting one with `POST /api/v1/sessions` and `{ "profileId": "<id>" }` returns a session token (JWT) that every further request sends as `Authorization: Bearer <token>`. `DELETE /api/v1/sessions/current` ends the session, for example when switching profiles. Requests without a valid token get `401` (`auth.unauthorized`), profiles without permission `403` (`auth.forbidden`); `/api/v1/users` is for admins only. The key that signs the tokens is created on the first start and kept in the database, so sessions survive restarts.

Collections are paged with the query parameters `offset` (from 0) and `limit` (1 to 100, default 50); the answer contains `items`, `offset`, `limit` and `total`.

Covers are scaled with `?size=` (1 to 4096): the server rounds the size up to 96, 300 or 1000 pixels (larger sizes give 1000) and delivers the cover as WebP, never enlarged; without `size` it delivers the original picture. Every cover carries a strong `ETag` of its version and size, `Last-Modified` and `Cache-Control: private, no-cache`, so clients keep it and ask again with `If-None-Match`, which returns `304 Not Modified` while the picture is unchanged. A changed picture gets a new ETag at once.

Admins start a scan with `POST /api/v1/scans`; the answer is `202 Accepted` with the queued scan and its `Location`. `GET /api/v1/scans/current` returns the running scan with its progress (`404`, `scan.not_running`, while none runs), `GET /api/v1/scans` the history. A second start while a scan runs gets `409` (`scan.already_running`). All background jobs are listed with `GET /api/v1/jobs` and started with `POST /api/v1/jobs/{id}/runs` (for example `library-scan`); `GET /api/v1/jobs/{id}/runs` returns their run history (30 days, at most 100 runs per job). Errors of runs and problems of files are codes such as `scan.media_folder_empty` or `file.probe_failed`. The library settings are read and changed with `GET` and `PATCH /api/v1/settings/library`.

After every full scan, and after a partial scan that changed files, the scan starts the job `cover-cache` (trigger `LibraryScan`); it prepares the covers and removes those of changed pictures from the cache, and fails with `cache.ffmpeg_unavailable` or `cache.folder_unwritable`. Admins read the usage of the cache with `GET /api/v1/cache` (total, covers, transcodes, limit and whether the cache folder can be written) and clear it with `DELETE /api/v1/cache` (`204`). The size limit is read and changed with `GET` and `PATCH /api/v1/settings/cache` (`maxSizeMegabytes`, 100 to 1,000,000; 1 MB = 1,000,000 bytes).

Admins list the exports of the database with `GET /api/v1/backups` (newest first, each with `id`, `fileName`, `createdAt`, `sizeBytes` and the `trigger` `Schedule`, `Manual` or `Migration`), start one with `POST /api/v1/backups` (`202` with the queued run of the job `database-backup` and its `Location`; `409` with `job.already_running` while one runs) and delete one with `DELETE /api/v1/backups/{id}` (`204`, or `404` with `backup.not_found`). `GET /api/v1/jobs/{id}` returns a single job; jobs that run daily name their time of day in `dailyAt` (`HH:mm`, local time of the server). Runs of the backup fail with `backup.folder_unwritable`, `backup.pg_dump_unavailable` or `backup.failed`. The settings are read and changed with `GET` and `PATCH /api/v1/settings/backup`: `scheduleTime` (`HH:mm`, default `03:00`) and `retentionCount` (1 to 100, default 7).

The library is available to every session: `GET /api/v1/artists`, `/api/v1/albums` and `/api/v1/tracks` with their items (`/{id}`), the albums of an artist (`/api/v1/artists/{id}/albums`), the tracks of an album (`/api/v1/albums/{id}/tracks`) and the lists of genres and years (`/api/v1/genres`, `/api/v1/years`). Lists are sorted with `sort` and `direction` (the defaults are documented per operation) and filtered with `q` (fault-tolerant search text), `artistId`, `albumId`, `genre`, `year`, `hasAudio` and `hasVideo` where they apply. Tracks tell with `hasAudio`, `hasVideo` and `hasCover` what they offer. `GET /api/v1/search?q=` returns the best artists, albums and tracks at once (`limit` up to 50 per group); it finds texts that contain the search text or are similar to it, using the PostgreSQL extension `pg_trgm`, which the database migration creates. `GET /api/v1/albums/{id}/cover` and `/api/v1/tracks/{id}/cover` return the cover, scaled with `size` (see above) or as the original picture (JPEG, PNG, WebP, GIF or BMP): by default the album cover (a picture such as `cover.jpg` in the album folder) before the cover of the track (a picture with its base name or embedded in its file), each falling back to the other and finally to the cover of another track of the album. Admins list all files of a track with `GET /api/v1/tracks/{id}/files`. Missing items get `404` with `artist.not_found`, `album.not_found`, `track.not_found` or `cover.not_found`.

Every profile manages its own playlists under `/api/v1/me/playlists`: `GET` lists them (paged, `sort` by `Name`, `CreatedAt` or `UpdatedAt`, `direction` and `q`), `POST` creates one with a `name` and optional first `trackIds` (`201` with `Location`), and `GET`, `PATCH` (`name`) and `DELETE` work on `/{id}`. The entries are listed with `GET /api/v1/me/playlists/{id}/tracks` in their order; `POST` adds 1 to 500 `trackIds` at the end or at a `position` (a track may appear more than once, up to 10,000 entries), `PATCH /tracks/{entryId}` moves an entry to a new `position` (from 0) and `DELETE /tracks/{entryId}` removes it. `PUT /api/v1/me/playlists/{id}/cover` takes the bytes of a JPEG, PNG or WebP picture (up to 10 MB by default, `Playlists__MaxCoverBytes`) as request body with its `Content-Type`; the server crops it to a square and stores it as WebP under `/data/playlists`. `GET` returns it scaled with `size` and `DELETE` removes it. Each playlist names its `cover`: `Custom` with a `version`, otherwise `Tracks` with up to four tracks of different albums whose covers the clients show, or `None`. `GET /api/v1/me/recently-played` returns the tracks of the play history, newest first, each once (at most 100). Errors are `playlist.not_found`, `playlist.entry_not_found`, `playlist.full`, `track.not_found`, `cover.unsupported_format` (`415`), `cover.too_large` (`413`) and `cover.unreadable` (`422`).

Every profile keeps its own favorites and ratings. `PUT /api/v1/me/favorites/{tracks|albums|artists}/{id}` marks a track, album or artist as favorite and `DELETE` removes it (both `204` and idempotent; marking an unknown item gets `404` with `track.not_found`, `album.not_found` or `artist.not_found`); `GET /api/v1/me/favorites` lists them, newest first, optionally only one `type` (`Track`, `Album` or `Artist`). `PUT /api/v1/tracks/{id}/rating` with `{ "rating": 4 }` rates a track with 1 to 5 stars, `0` removes the rating. Tracks carry `isFavorite` and `rating` of the current profile, albums and artists `isFavorite`, in every list and detail. `myMusic=true` limits the lists of tracks, albums, artists, genres and years to the music of the profile (rated or favorite tracks, the tracks of favorite albums and artists and the tracks of its playlists) and combines with all other filters; tracks sort by `Rating` (most stars first, unrated last). `GET /api/v1/me/home` returns the rows of the home page in one call: the own playlists, recently played tracks, recently added albums, favorites and top rated tracks, each the first page with up to `limit` items (1 to 50, default 20).

`GET /api/v1/tracks/{id}/playback` with the `audioFormats` the client plays decides how it plays a track: `DirectPlay` with `protocol` `File` and a signed `streamUrl` of the audio file (`/api/v1/tracks/{id}/stream`, with Range requests), or `Transcode` with a `transcodeReason` and, while transcoding is available, `protocol` `Hls` with a signed `streamUrl` of an HLS master playlist; without transcoding the `streamUrl` is missing. `GET /api/v1/tracks/{id}/video/playback` does the same for the video with the `videoContainers`, `videoCodecs` and `audioCodecs` the client plays (none sent means HLS); `GET /api/v1/tracks/{id}/video` streams the video file with Range requests. A track without audio or video gets `404` with `track.no_audio` or `track.no_video`. The video playback also names the `offsetMilliseconds` of the video against the audio (positive if the music in the video starts later); clients play the video at the position of the audio plus this offset. Admins read and set it with `GET` and `PUT /api/v1/tracks/{id}/video/offset` (`{ "offsetMilliseconds": 1500 }`, from -600000 to 600000; `400` with `out_of_range` outside, `404` with `track.not_found` or `track.no_video`); a scan keeps it. The view of the Now Playing stage is a setting of the profile: `playerViewMode` (`Cover`, `Video` or `Visualization`, default `Video`) in `GET` and `PATCH /api/v1/me/settings`. The group `visualization` of the same settings holds the `module` (`Milkdrop`, `AForce` or `BlackCap`), the `preset` the profile chose last (its name, up to 200 characters; `null` lets the player pick one, an empty string in a `PATCH` forgets it, another module forgets it as well), `autoChange` (default `true`) and `autoChangeSeconds` (5 to 600, default 30; `400` with `out_of_range` outside) and the `quality` (`Auto`, `Low`, `Medium` or `High`, default `Auto`; Auto lets the player choose the level by the frame rate of the device). The presets themselves belong to the clients: the web app draws the Milkdrop presets of butterchurn, and the presets of A-Force are its style variants (`aurora`, `nebula`, `ember`, `lagoon`, `prism`, `eclipse`), the presets of BlackCap its scenes (`lumen`, `tide`, `orchid`, `mercury`, `halo`, `dusk`). The master playlist (`/api/v1/tracks/{id}/hls/master.m3u8`, with `media=Audio` or `media=Video`) names one variant; its media playlist (`/hls/{media}/{variant}/main.m3u8`) lists the segments of the whole track (MPEG-TS, 6 seconds each), and `/hls/{media}/{variant}/segments/{index}` delivers them. Segments are transcoded when they are asked for, a jump starts the transcode at the segment asked for, and finished segments come from the cache. All media addresses are signed (query `uid`, `sid`, `exp` and `sig`, bound to the track and the profile), and the playlists hand their signature on to the addresses they contain. While all transcodes are busy or a segment is not ready in time, the server answers `503` with `Retry-After` (`transcode.busy`, `transcode.timeout`); further errors are `transcode.unavailable` (`503`), `transcode.failed` (`500`), `transcode.variant_outdated` and `transcode.segment_not_found` (`404`). Admins read the state with `GET /api/v1/transcoding/status` (ffmpeg, cache, hardware acceleration, running and allowed transcodes), list the running transcodes with `GET /api/v1/transcoding/jobs` and stop one with `DELETE /api/v1/transcoding/jobs/{jobId}` (`204`, or `404` with `transcode.job_not_found`). The settings are read and changed with `GET` and `PATCH /api/v1/settings/transcoding`: `hardwareAcceleration` (`Auto` or `Off`), `maxParallelJobs` (1 to 8, default 2), `maxVideoHeight` (480, 720, 1080, 1440 or 2160, default 1080) and `audioBitrateKbps` (128, 160, 192, 256 or 320, default 256).

Admins edit the metadata of a track with `GET` and `PATCH /api/v1/tracks/{id}/metadata`. `GET` returns the values the editor shows (the tags of the audio file, completed from the library), its `status` (`Editable`, `NoAudio` or `NotWritable`) and the `audioFilePath` it writes. `PATCH` writes `title`, `artist`, `album`, `albumArtist`, `year`, `trackNumber`, `discNumber` and `genre` into the tags of the audio file the track plays: members that are missing stay unchanged, `null` or an empty text removes the value. The previous tags are saved in the database first, and the answer holds the values after the track was read again. A track with a video only gets `409` (`track.no_audio`), an audio file that cannot be written `409` (`track.not_writable`).

### Tests

Run the commands from the repository root:

```bash
# Unit and integration tests (integration tests need the dev database)
docker compose -f docker-compose.dev.yml up -d wavemp-db
dotnet test

# Web app checks
(cd source/WaveMP.Web && npm run lint:check && npm run type-check && npm run format:check && npm run test:unit)

# E2E tests against a running instance
docker compose -f docker-compose.dev.yml up --build -d
cd tests/WaveMP.E2E
npm install
npx playwright install chromium
npm test                                          # E2E_BASE_URL defaults to http://localhost:8080
```

The tests create their media at runtime with ffmpeg (sine tones, test pictures, tags), so ffmpeg and ffprobe must be in the `PATH`, or in the folder named by `FFMPEG_PATH`; without them the media and scan tests fail with a hint. Tests in the category `Media` need ffmpeg only, tests in the category `Integration` the dev database as well (`dotnet test --filter "TestCategory!=Integration"` runs everything else).

The integration tests host the service in memory with `WebApplicationFactory` and its real startup, including the configuration check, the database migrations and the creation of the built-in admin profile. They use their own databases `wavemp_tests` and `wavemp_tests_*` (created like a new installation and dropped afterwards) and a temporary data folder for the cache, and read `DB_PASSWORD` and `DB_PORT` from the environment or from `.env`.

The E2E tests first select the built-in admin profile once (project `setup`) and store the browser state in `tests/WaveMP.E2E/playwright/.auth/` (not committed); most tests start with this session. Tests that switch profiles create their own profiles through the API and delete them afterwards.

The projects `chromium` (desktop) and `mobile` (phone) run all specs except `tests/devices`; the projects `iphone`, `ipad` and `ipad-landscape` run the specs in `tests/devices` with the viewport, touch and mobile emulation of an iPhone and an iPad in portrait and landscape (in Chromium, the only browser the setup installs). `npx playwright test --project=ipad` runs one of them.

If you set `WAVEMP_PORT` in `.env`, point the E2E tests at it, for example `E2E_BASE_URL=http://localhost:8081 npm test`.

The screenshots of this README in `assets/screenshots` are taken with Playwright from the same test media: `npm run screenshots` in `tests/WaveMP.E2E` (also with `E2E_BASE_URL`) scans the media, creates two profiles with playlists, favorites, ratings and plays, takes the pictures on the desktop and on a phone and removes the profiles again. Run it against a fresh instance, for example one started with `docker compose -f docker-compose.dev.yml up --build` after `docker compose -f docker-compose.dev.yml down --volumes`, and commit the changed pictures. `npm run test:unit` runs the unit tests of the web app (Vitest), including checks that every text comes from the translation files and that `THEMING.md` matches the code.

`npm run lint:check` only reports problems, like the CI does; `npm run lint` fixes what it can. The E2E tests check the version that `/api/v1/health` and the settings page report: exactly if `E2E_EXPECTED_VERSION` is set (the CI sets it to the version of the image it built), otherwise any version of the form `1.0.N` or `1.0.0-dev`. They scan the media folder of the running instance before the tests (project `setup`) and need the test media of `tests/WaveMP.E2E/scripts/generate-media.sh` in it (see [Local development](#local-development)): the library tests browse and search exactly these songs. With `E2E_EXPECTED_TRACKS=4` they also check the number of songs, as the CI does. "Loose Song" lasts 20 seconds, so the tests can switch the view of Now Playing while it plays. The test of the metadata editor writes genre and year of "Loose Song" and removes them again afterwards, so the media folder must be writable for the container (`generate-media.sh` makes the files it creates writable for everyone).

### License check

All NuGet and npm dependencies must have a license listed in `.github/license-policy.json`: MIT-compatible licenses only, never GPL or AGPL. This covers the NuGet packages of all projects in `WaveMP.slnx` (including transitive ones), the local .NET tools in `dotnet-tools.json` and the npm packages of every npm project, including the license check's own tools in `.github/scripts`. The CI runs the same commands:

```bash
node .github/scripts/check-licenses.mjs
node --test .github/scripts/check-licenses.test.mjs   # tests of the check itself
```

It restores the .NET tools and npm packages it needs by itself and fails if a package has a license that is not allowed or no license information. It also fails if a .NET project is not part of `WaveMP.slnx` or a tool manifest or npm project is missing from the policy (`nuget.toolManifests`, `npm.projects`), so new projects cannot be skipped. Exceptions for single packages are only added to the policy after a manual license review, npm exceptions with their exact version.

[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) lists every direct dependency of all projects with its license, plus the components of the Docker image; the image ships it under `/licenses`. The license check fails if a direct dependency (a package of `Directory.Packages.props`, a tool of `dotnet-tools.json` or a package in `dependencies` or `devDependencies` of an npm project) is missing there, or if it lists a package that no project uses any more: add a row with the name in backticks, its license and its use whenever you add a package, and remove the row when you remove one.

### Continuous integration

GitHub Actions run `.github/workflows/test.yml` for every pull request (`pull-request.yml`) and every push to `main` (`main.yml`): the .NET build with unit and media tests (with ffmpeg) and integration tests against a PostgreSQL 18 service container, lint, type check, unit tests (Vitest) and build of the web app, the license check, and the E2E tests against the Docker image with synthetic test media. After the tests pass, `main.yml` publishes the image with the tags `1.0.N`, `1.0`, `latest`, `edge` and `sha-<commit>` and checks that both registries deliver it.

Versions are `<major>.<minor>.<N>`: major and minor are the `VersionPrefix` in `Directory.Build.props` (changed by the maintainer only), N is the run number of `main.yml`. The CI passes the version to the .NET build (`-p:Version=1.0.N`), to the web app build (`WAVEMP_VERSION`) and to the Docker build (build argument `VERSION`), and sets it as the label `org.opencontainers.image.version` of the image. Builds without it, such as local builds and pull requests, report `1.0.0-dev`. Tags trigger no workflow: the GitHub release of a milestone (`v1.0.N`) marks a version that `main.yml` has already published.

All foreign Docker images in the `Dockerfile`, in `docker-compose.dev.yml` and in the workflows (base images, PostgreSQL, the static ffmpeg build, the Playwright image, BuildKit and QEMU) are pinned by the digest of their multi-arch index, for example `postgres:18@sha256:...`; the tag only shows the version. To update an image, change the tag if needed and replace the digest with the one printed by `docker buildx imagetools inspect <image>:<tag>`. The Playwright image in `test.yml` has to be updated together with `@playwright/test` in `tests/WaveMP.E2E`; the E2E job fails if the two versions differ. The `docker-compose.yml` for users keeps the plain tags, so PostgreSQL patch releases arrive with `docker compose pull`.

## License

WaveMP is licensed under the [MIT License](LICENSE).
It uses [ffmpeg](https://ffmpeg.org) and `pg_dump` of [PostgreSQL](https://www.postgresql.org) as separate processes, [TagLib#](https://github.com/mono/taglib-sharp) (LGPL-2.1, used unchanged as a separate assembly), [butterchurn](https://github.com/jberg/butterchurn) with its presets (MIT) for the Milkdrop visualization and, in the Docker image, [ICU](https://icu.unicode.org) (Unicode-3.0) for Unicode normalization and culture-aware text handling, which are licensed separately. [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) lists all third-party components with their licenses; the image ships it and the license texts under `/licenses`.
