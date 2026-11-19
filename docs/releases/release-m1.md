# WaveMP M1: Foundations

The first milestone of WaveMP: a self-hosted media player for your music and music videos that you run with Docker and use from any browser on your network, on the desktop, on tablets and on smartphones. This release covers everything WaveMP can do so far.

## Setup

- **Two containers, one password.** WaveMP runs as the app and a PostgreSQL database, started with one compose file. The only setting you have to make is `DB_PASSWORD`; everything else has sensible defaults. The database is only reachable inside the Docker network.
- **Images for `linux/amd64` and `linux/arm64`** on the GitHub Container Registry (`ghcr.io/wavemp-app/wavemp`) and on Docker Hub (`wavemp/wavemp`). Use `latest` for the current version or a version tag such as `1.0.42` to pin your installation.
- **Your data stays yours.** Your media folder is mounted into the container, read-only if you like. Everything WaveMP keeps besides the database, such as the cache and the covers of your playlists, lives in the `/data` volume.
- **The running version** is shown under **Settings**.

## Profiles

- **Pick a profile, no login.** WaveMP starts with a profile selection. The choice is remembered on the device, and **Switch profile** in the profile menu changes it at any time.
- **Built-in admin profile.** On the first start there is an **Admin** profile. It creates, edits and deletes the other profiles with name, initials, color and role. Once there are other profiles, the admin profile is shown less prominently.
- **Everything per profile.** Language, playlists, favorites, ratings, history and the player settings belong to the profile and follow it to every device.
- **English and German.** Before a profile is selected, WaveMP follows the language of your browser.

## Your library

- **Scans your media folder** one minute after the start, every six hours and on demand. Watching the folder picks up new, changed and deleted files within seconds. Scans only read files that changed, and stop without removing anything if the media folder is suddenly empty, for example because a network share is not mounted.
- **Files that belong together.** All files of a song share one name in one folder: `Song.flac` for the sound, `Song.mkv` for the music video and `Song.jpg` for its cover. Any folder structure works.
- **Metadata from tags first,** then from folder and file names: artist, album, disc, track number and year are recognized in common patterns such as `Artist/Album/01 - Title.mp3`, `Artist/2001 - Album/Title.mp3` or `Artist - Title.mp3`.
- **The best file wins.** If a song exists in several formats, the one with the highest quality plays: lossless before lossy, then bit depth, sample rate and bitrate; for videos the resolution, then the bitrate.
- **Album covers and song covers.** Pictures such as `cover.jpg` or `folder.jpg` are the cover of the album, pictures with the name of a song or embedded in its file the cover of that song. By default the album cover comes first.
- **Browse** by **Recently Added**, **Favorites**, **Artist**, **Album**, **Songs**, **Genre** and **Year**, with pages for every album, artist, genre and year and the details of each song.
- **Search while you type,** across artists, albums and songs. Small typos are fine. Sort order and search text are part of the address, so you can bookmark them.
- **Fast lists.** Covers are scaled to the size they are shown in and cached.

## Tag editor

- **Correct your tags in WaveMP.** Admins edit title, artist, album, album artist, year, track and disc number and genre of a song. The changes are written into the audio file, never into a video, and file names stay as they are. The previous tags are kept.
- The editor is only available when the song has a writable audio file; otherwise it is shown greyed out with a hint.

## Player

- **Plays almost anything.** Formats your browser supports play directly. Everything else is transcoded on the fly, so songs and videos your browser cannot handle play as well. Parts that were transcoded once come from the cache the next time.
- **Gapless playback** from one song to the next.
- **Transport bar** with play and pause, previous and next, seek, volume, mute, shuffle, repeat (all or one song), stop and a crossfade switch.
- **Queue.** Play an album, a playlist or a list from any song, or add songs with **Play next** and **Add to queue**. **Now Playing** shows the last three songs you played, the current song and everything that follows, each with its cover; you can play, move and remove songs there.
- **Media keys and lock screen.** The player follows the media keys, the lock screen and the notification of your device and keeps playing in the background.
- **Volume leveling (ReplayGain)** by song or by album, for files with ReplayGain tags. Off by default.
- **Equalizer** with ten bands from 31 Hz to 16 kHz, a preamp and the presets Flat, Rock, Pop, Jazz, Classical, Dance, Bass, Treble and Vocal, or your own curve.
- **Crossfade** of up to twelve seconds, switched on and off right in the transport bar. Songs of the same album stay gapless unless you change that under **Settings**.
- **Sleep timer** that stops the music after 15 to 120 minutes, at the end of the song or at the end of the queue, with a soft fade-out.
- **History.** A song counts as played once it has played for half its length, at most four minutes.

## Music videos

- **Cover and video, switched without a break.** **Now Playing** shows the song as cover or as music video. The sound always comes from the song, so switching the view never interrupts the music. Your choice of view is kept with your profile.
- **Full screen** with a double click or the full screen button.
- **Portrait videos** are shown whole, undistorted and centered, also in full screen.
- **Video sync.** If the music in a video starts earlier or later than in the song, admins set an offset for that song.
- **Hardware transcoding (optional).** On servers with Intel Quick Sync, videos are transcoded on the graphics hardware. Software transcoding always works, and **Administration > Transcoding** shows which one is in use.

## Playlists, favorites and ratings

- **Playlists per profile.** Create, rename and delete playlists, add songs as often as you like, sort them by drag and drop with mouse, pen or finger, and play or shuffle them.
- **Playlist covers.** Upload your own picture, or let the playlist show the covers of up to four of its songs, one after the other.
- **Recently Played** lists the songs you played last.
- **Favorites.** Mark songs, albums and artists with a heart.
- **Ratings** from one to five stars for songs.
- **My music.** One switch narrows the whole library down to your own music: rated and favorite songs, the songs of your favorite albums and artists, and the songs in your playlists.
- **Home** greets you with your playlists, the songs you played recently, the albums added recently, your favorites and your top rated songs.

## Administration

- **Library:** start a scan and follow its progress and result, see the history of scans, and set how often the library is scanned, whether the folder is watched and which file extensions count as audio, video and pictures.
- **Jobs:** all background jobs with their last and next run, started on demand.
- **Cache:** how much space the cache uses, its maximum size (2 GB by default) and a button to clear it.
- **Transcoding:** whether transcoding works, the hardware acceleration, the running transcodes, the number of transcodes at the same time, the largest height of transcoded videos and the bitrate of transcoded sound.

## Look and feel

- **Midnight**, a dark, glossy glass theme with a glowing play button and a desktop layout with tabs, toolbar, sidebar and transport bar.
- **Made for every screen.** On smartphones WaveMP has a bottom navigation, a mini player and a full-screen player. On iPhone and iPad the status bar blends into the header, and only the content scrolls, in portrait and in landscape.
- **Collapsible sidebar** on the desktop, down to its icons. The choice is kept with your profile.
- **Installable as an app (PWA)** on desktop and mobile devices. Browsers require HTTPS for that, except on `localhost`.
- **Keyboard and screen readers.** Options that exclude each other, such as the views of Now Playing or **All music** and **My music**, are grouped switches you can operate with the arrow keys.
- **Themes.** The look of WaveMP is a theme: one CSS file plus graphics. Theme authors find everything they need in [THEMING.md](https://github.com/wavemp-app/wavemp/blob/v1.0.24/THEMING.md).

## For developers

- **Open REST API** with an OpenAPI document, so you can build your own clients.

## Known limitations

- Profiles are not protected yet: everyone who can open WaveMP can select any profile, including the admin profile. Do not expose WaveMP to the internet without protection, for example a reverse proxy with authentication or a VPN.
- The **Visualization** view of Now Playing is a placeholder for now.
- WaveMP ships with the theme Midnight only.
