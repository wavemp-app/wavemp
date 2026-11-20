# WaveMP M7: Recaps

The seventh milestone of WaveMP looks back on what you listened to. Recaps tell the story of your favorite songs, artists, albums and genres for any period, as an animated story in three designs that you can save as a picture or video and share with the other profiles, and a statistics page shows the same numbers as charts and tables. Music videos now glow softly into the dark area around them in the colors of the picture. WaveMP shows its new logo everywhere, covers stay sharp on phones and high-resolution screens, albums are formed by artist and album name instead of by folder, and many fixes make the player on phones and tablets more reliable.

## Recaps and statistics

**Recap** in the sidebar, below the library and the playlists, looks back on your listening.

- **Choose the period.** **Last month**, **Last 3 months**, **Last year** (the last 12 months), **This year**, **All time** or **Custom** with two days of your choice. Days and hours count in the time zone of your browser.
- **A story of your listening.** The recap shows one section after the other: your minutes of music, your top song, your top songs, artists, albums and genres with their covers, your listening over time, your favorite hour of the day, the artists you discovered in this period and a summary.
- **At your own pace.** The story moves on by itself. Tap or click the right of the story, swipe left or press the right arrow key to go on; the left third, a swipe right or the left arrow key go back. Hold the story or press Space to pause it.
- **Three designs.** **Aurora** (dark), **Frost** (light) and **Prism** (colorful). Your device remembers the design you chose.
- **Save and share.** **Save picture** saves the shown section as a picture in story format (1080 x 1920) or square (1080 x 1080). **Save video** records the whole recap as an MP4 video with music. On phones and tablets the share sheet of the device opens, so you can send it straight to a messenger; on a computer the file is downloaded. Browsers that cannot record MP4 themselves send the video to your WaveMP server, which converts it and keeps nothing.
- **Share with the other profiles.** Switch on **Share with the other profiles** (off by default) and the recap of these days appears under **Shared recaps** of every other profile of your WaveMP, in your design. Its numbers always follow your play history. Stop sharing on the recap or under **Shared recaps**.
- **Statistics.** **Statistics** shows the same period as plain numbers: plays, songs, artists, albums, genres, active days and listening time, charts of your listening over time and by hour, and tables of your top songs, artists, albums and genres with up to 25 entries each, every entry linked to its page.

With reduced motion switched on in your system, the sections appear without animation. Every play counts the full length of the song.

## Player

### Ambient light around videos

While a music video plays on **Now Playing**, the dark area around it now glows softly in the colors of the current picture, beside portrait videos as well as in full screen.

- **Light on resources.** The light follows the video a few times per second from a tiny copy of the picture, stands still while the video is paused and pauses while the video is not visible or WaveMP is in the background.
- **Calm with reduced motion.** With reduced motion switched on in your system, the light is one calm color that matches the video.
- **Your choice.** Turn it off under **Settings > Ambient light around videos**. The choice is stored with your profile and applies on all your devices.

### Clear the playback state

**Settings > Playback state > Clear playback state** starts over: it stops the player of this device, empties its queue and forgets the saved state, so every device of your profile starts with an empty player. A device that is still playing saves its queue again.

## Library

### Albums by artist and album name

An album is now the combination of its artist and its album name: the album artist tag if a song has one, otherwise the artist of the song. Letter case and spaces at the start and end do not matter.

- **Folders no longer decide.** Songs in one folder with another artist or album name belong to other albums, and songs in different folders with the same artist and album name belong to the same album. Songs of different artists in one folder no longer become a compilation that is mixed up with other folders.
- **Compilations stay together** when their songs share an album artist such as `Various Artists`.
- **Edits move songs at once.** After you change the artist, album artist or album name of a song in the metadata editor, it moves to the matching album right away, and albums without songs disappear.

### Covers

- **Each song shows the right cover.** A picture with the name of a song (`Song.jpg`) is always shown first for it. Otherwise the song shows the cover of its album or a picture embedded in its files, after your cover rules. A song never shows the cover of another song any more, for example after you changed the artist of several songs; without any of these it has no cover. Only the cover rule of the song itself can put the album cover before a picture with its name.
- **Sharp on every screen.** Covers are now loaded in the size they are shown in, taking the pixel density of the screen into account, so they stay sharp on phones and on 4K screens. A cover that grows, for example when the mini player on a phone opens the full-screen player, or a window that moves to a sharper screen, loads a larger size.

### Filter by media folder

As soon as there is more than one media folder, genres and years can now be filtered by media folder as well, like songs, albums and artists.

### Lyrics

Songs with lyrics in their tags are now checked again when their file changes and after the waiting time of `Lyrics__LookUpAgainAfter` (30 days by default). If the tags have lost their lyrics, the lyrics download looks them up on LRCLIB.

## The new logo

WaveMP has a new default logo. It is the logo of the web app, its favicon and its icons when you install WaveMP as an app, and of the app catalog templates. The favicon switches to a dark variant in the dark mode of your browser, and the dark variants of the themes show the dark logo in the app.

## Fixes

- **Switching views on the iPhone and iPad.** Choosing **Video** on **Now Playing** on an iPhone or iPad now shows the music video; before, it kept loading and the stage stayed on the cover, so switching between cover, video and visualization seemed to do nothing.
- **Bottom navigation on phones.** **Now Playing** shows the short label **Playing** in the bottom navigation, and all entries are laid out the same way in every theme, the icon above its label, centered and at the same height.
- **Seeking at the start of a song.** Moving the position while a song is still loading, for example right after a paused start, no longer jumps back to the start of the song; the song starts where you moved it.
- **Pinstripe.** The selected entry at the top level of the sidebar keeps its white text and is readable again.
- **Library settings.** Saving the library settings changes only the values you changed, so it no longer resets values that someone else changed in the meantime.
- **More reliable administration.** The backup settings can be changed only once they are loaded, and the list of uploaded themes always shows the newest state.
- **Clean stop.** WaveMP waits for the clean-up of stopped transcodes when it shuts down.
- **Online search.** Requests to MusicBrainz now start strictly one after the other, so WaveMP always keeps to its limit of one request per second.
- **Long German labels.** Some long German words now break with a hyphen where a label needs a second line.
- **Administration navigation.** All entries of the administration navigation fit into one row on a screen 1280 pixels wide, in every theme and language.

## Themes

The theme API gains parts for the recap, the statistics and the shared recaps (story, sections, rankings, charts and tables) and for the ambient light around videos, with variables for its intensity, blur, saturation, size and transitions. All shipped themes are styled for the new views. The theme API stays at version 5, since all changes are additions. [THEMING.md](../../THEMING.md) describes the new parts.

## For developers

- **Statistics.** `GET /api/v1/me/stats` counts the plays of the own profile in a period (`from`, `to`, `timeZone`, `top`): totals, the top songs, artists, albums and genres, a timeline, the plays per hour and the new artists.
- **Shared recaps.** `/api/v1/recap-shares` lists (`scope` `Received` or `Own`, paged), creates, reads and deletes recaps shared with the other profiles; `GET .../{id}/statistics` returns the statistics of a shared recap.
- **Recap videos.** `POST /api/v1/me/recap-videos` converts a recorded WebM, Matroska or MP4 video into MP4 with H.264 and AAC.
- **Ambient light.** `PATCH /api/v1/me/settings` takes the group `video` with `ambientMode`.
- **Playback state.** `DELETE /api/v1/me/playback-state` clears the saved state of the player of a profile.
- **Media folders.** `GET /api/v1/genres` and `GET /api/v1/years` take the filter `sourceId`.
- **Cover sizes.** `?size=` of covers now rounds up to 96, 192, 300, 600, 1000 or 2000 pixels; clients pass the shown width times the pixel density of the screen.
- **New error codes:** `recap.share_not_found`, `video.too_large`, `video.unsupported_format` and `video.unreadable`.
- **New settings:** `Recaps__MaxVideoBytes` and `Recaps__ConversionTimeout`; the [README](../../README.md#settings) describes them.

All new endpoints and codes are described in the OpenAPI document.

## Upgrade notes

- **Albums are formed again.** The first scan after the update forms the albums of your library again by artist and album name, from what WaveMP already knows, without reading the files again. Songs that were grouped by folder before may now belong to other albums, and some albums may be split or joined.
- **Ambient light is on.** The ambient light around videos is switched on for every profile, including existing ones. Turn it off under **Settings > Ambient light around videos**.
- **New temporary folder.** While the server converts the video of a recap, it lives in `/data/temp/recap-videos`. WaveMP deletes it right after the conversion and removes what a restart left behind within the hour; nothing in `/data/temp` has to be backed up.
- **The new logo.** An installed WaveMP app and bookmarks may show the earlier icon until the browser updates them.

## Known limitations

- Listening time counts the full length of every played song, not the part you actually listened to.
- A shared recap shows the current numbers of its period, not those of the moment it was shared.
- The ambient light is not shown in the mini player and in the full screen of the iPhone, which the system shows.
