# WaveMP M6: Improvements

The sixth milestone of WaveMP makes everyday listening smoother and the library easier to keep in shape. Your music can now live in several folders, smart playlists fill themselves by rules, autoplay keeps the music going after the queue has ended, and the player remembers where you stopped, also on your other devices. A mini player, keyboard shortcuts and context menus everywhere make WaveMP feel at home on the desktop. For admins, WaveMP can look up missing album info and covers online and checks the library for problems after every scan. Theme authors can now ship their own fonts.

## Library

### Several media folders

WaveMP can now scan and watch more than one folder, for example your music on one disk and your music videos on another.

- **Add folders in the app.** Mount each folder as its own volume, then add it under **Administration > Library > Media folders** with a name and its path inside the container. Folders can be renamed, moved to another path, switched off or removed there, and the list shows the state and size of each folder.
- **Your library stays.** The folder of `MEDIA_PATH` (`/media` by default) becomes the first media folder, and your existing library belongs to it.
- **Scanned one by one.** A new or moved folder is scanned at once. Every folder is scanned and watched on its own; a folder that is missing or cannot be read, for example a network share that is not mounted, is skipped and shown as a problem of the scan, and the songs of the other folders stay as they are.
- **Switch off or remove.** A switched-off folder keeps its songs in the library without updating them. Removing a folder removes its songs, with their ratings, favorites and playlist entries, from the library; the files themselves are never touched.
- **Filter by folder.** As soon as there is more than one media folder, the songs, recently added songs, albums and artists of the library can be filtered by media folder.

The [README](https://github.com/wavemp-app/wavemp/blob/v1.0.88/README.md#several-media-folders) shows how to mount further folders.

### Album info from the internet

Missing tags and covers can now be looked up on MusicBrainz, the open music encyclopedia, and in the Cover Art Archive.

- **Off until you switch it on.** Switch on **Search album info online** under **Administration > Library**. While it is off, WaveMP sends no requests to either service. You can also point WaveMP to your own MusicBrainz mirror there.
- **Search a song or an album.** Admins choose **Search online** on the page of a song or album. WaveMP shows the matches, the best first, with how well they agree with what your library has.
- **Compare before you apply.** The selected match is compared with the current values field by field, together with a preview of its cover. The fields the song or album is missing, and the cover if it has none, are chosen at first; every field can be chosen or left out on its own. Nothing changes until you choose **Apply**.
- **Written like your own edits.** The chosen tags go through the metadata editor: only audio files are written, never videos, and the previous tags are kept. For an album, album, album artist, year and genre are written into every song of the album that can be written. The cover is saved in the folder of the album or next to the song, following your cover rules; a picture it replaces is kept in `/data/online-covers/replaced`, and covers for folders that cannot be written are kept in `/data/online-covers`.

The tags of MusicBrainz are in the public domain (CC0). The covers in the Cover Art Archive belong to their rights holders; WaveMP fetches them only for your own library.

### Library check

After every scan, WaveMP now looks for problems in your library, and **Administration > Check** shows what it found:

- songs without a cover,
- songs whose file lacks title, artist or album,
- songs without an album,
- possible duplicates: the same artist and title with lengths within 5 seconds,
- files that cannot be read,
- and, as hints only, videos without an audio file of the same name and songs without a video.

Filter the problems by kind, each with its number, search them by title, artist, album or folder and sort them by folder, artist, title or newest. Every problem links to the page of its song and straight to where it is fixed: **Edit metadata**, **Search online** or **Show files**. A possible duplicate names and links the other song as well. **Check now** checks the library again at once, for example after you fixed some songs.

### Lyrics

Songs that LRCLIB had no lyrics for, or listed as instrumental, are now looked up again after 30 days, because LRCLIB keeps growing. The waiting time is set with `Lyrics__LookUpAgainAfter`; `00:00:00` never looks such songs up again.

## Playlists

### Smart playlists

**New smart playlist** on the **Playlists** page builds a playlist from rules instead of by hand.

- **Start from a template:** **4 stars and more**, **Never played**, **Recently added**, **Most played** or **Favorites**.
- **Or write your own rules.** Each rule has a field (artist, album, genre, year, rating, favorite, play count, last played, date added, length, audio file or music video), a condition and a value, for example *Genre contains Rock* or *Last played not in the last 90 days*. **Songs must match** chooses whether a song needs all rules or any of them.
- **Order and limit** sorts the songs and can keep only the first ones, for example the 50 most played.
- **Always up to date.** The songs are worked out anew whenever you open or play the smart playlist, so it follows your library, ratings and plays.
- **Like any playlist.** Smart playlists stand among your playlists and in the sidebar with a magic wand, show the covers of their first songs and play and shuffle like any playlist. The page of a smart playlist describes its rules in words; **Edit rules** changes them.
- **Convert to playlist** turns a smart playlist into a normal playlist with the songs it holds at that moment, which you can then sort and change by hand.

## Player

### Autoplay

When the queue has ended and repeat is off, autoplay now goes on with similar songs until you stop.

- **Similar songs.** It plays more songs of the artists in the list first, and artists that appear more often come more often. When they run out, it goes on with songs of the same genres from similar decades.
- **Your taste counts.** Favorites and songs with four or five stars come a little more often, songs with one star never, and songs you played in the last 24 hours only when nothing else is left. Songs that only have a music video are included unless you turn that off in the settings.
- **See what comes next.** **Now Playing** shows the next songs under **Autoplay** below the queue. Choose one to play it at once, remove a song you do not want, or turn autoplay off right there. Songs you add to the queue always come first.
- **On and off.** The autoplay button in the transport bar (in the expanded player on smartphones) and **Settings > Autoplay** switch it on and off. The choice is stored with your profile and applies on all your devices.

### Continue where you stopped

WaveMP now remembers for each profile what you listened to: the queue, the song and the position in it, shuffle, repeat and the album or playlist it came from.

- **After a reload or the next start**, the player shows that song paused at its position; **Play** goes on from there.
- **On your other devices.** Open WaveMP on another device and it offers **Continue listening from** the device you listened on last, for example *Firefox · Windows*; **Continue** plays on there. The device that changed the state last wins, and a paused device that comes back to the front picks up a newer state from another device.

### Mini player

The compact mode button at the right of the transport bar now opens the mini player: cover, title, artist, progress, previous, play and pause, next and the volume of the running song, and its music video while the **Video** view is chosen on **Now Playing**.

- **In Chrome and Edge** it is a small floating window that stays above your other windows, so you can control the music while you work in other programs. You can resize it, and it shows the cover beside the controls when it is wide.
- **In other browsers**, such as Firefox and Safari, the WaveMP window shows the mini player with nothing else around it; make the window small to keep it at hand.
- The button again, the close button of the mini player or Escape closes it. The mini player looks like the theme you chose.

### Keyboard shortcuts

On a computer or a tablet with a keyboard, WaveMP now reacts to the keys of a desktop player:

| Key | Action |
|---|---|
| Space | Play or pause |
| Left / Right | Back or forward 5 seconds |
| Ctrl+Left / Ctrl+Right | Previous or next song |
| Up / Down | Volume up or down |
| M | Mute or unmute |
| S | Shuffle on or off |
| R | Repeat: off, the list, the song |
| F | Full screen: the video or the visualization on **Now Playing**, otherwise the whole app |
| Ctrl+F | Search the library |
| ? | Show all keyboard shortcuts |

On a Mac, an iPhone or an iPad, Command takes the place of Ctrl. The keys rest while you type in a field and while a dialog or menu is open, and the playback keys also work in the floating mini player. **Keyboard shortcuts** in the profile menu shows the same overview.

## Context menus

Songs, albums, artists, playlists, the entries of the queue and **Now Playing** now each have a context menu with what you can do with them, from playing and queuing to adding to a playlist or to your favorites, rating, and going to the album or artist.

- **Open it your way:** right click, the context menu key or Shift+F10 on a keyboard, and a long press or the **More options** button on touch screens.
- **Submenus** group longer choices, such as the stars of **Rate**. The arrow keys move through the menu, Escape closes it.
- **Several songs at once.** Ctrl (Cmd on a Mac) with a click selects several songs of a list, Shift with a click a range and Ctrl+A all of them. The context menu then plays, queues, rates or adds all selected songs at once.

## Themes

- **Fonts in theme packages.** Uploaded themes can now bring their own fonts as WOFF2 files and use them with `@font-face`. WaveMP checks every font before it installs the theme, and fonts are served from your WaveMP server only. The author of a theme is responsible for the license of its fonts; [THEMING.md](https://github.com/wavemp-app/wavemp/blob/v1.0.88/THEMING.md#fonts-of-an-uploaded-theme) explains how to add fonts and which licenses allow it. The largest font is 2 MiB by default and can be changed with `Themes__MaxFontBytes`.
- **Checked again after an update.** A newer version of WaveMP may check theme packages more strictly. On every start WaveMP now checks the installed themes again with its current rules. A theme that no longer passes stays available to the profiles, but **Administration > Themes** marks it as outdated and lists its problems, so you know what to fix in a new version of the theme.
- **Midnight looks the same everywhere.** Midnight now brings its own font, Selawik (SIL Open Font License 1.1), so it looks the same on Windows, macOS, iOS and Android.

## Improvements

- **Readable status tiles.** The labels of the status tiles in the administration, for example on the pages of the library and the cache, are no longer cut off: long labels wrap onto further lines, long values such as the address of the server break where needed, and the labels and values of a row stay aligned.

## For developers

- **Media folders.** `GET`, `POST`, `PATCH` and `DELETE` on `/api/v1/sources` manage the media folders. `GET /api/v1/tracks`, `GET /api/v1/albums` and `GET /api/v1/artists` take the filter `sourceId`.
- **Smart playlists.** `/api/v1/me/smart-playlists` lists, creates, changes and deletes the smart playlists of a profile; `GET .../{id}/tracks` returns their songs and `POST .../{id}/conversions` turns one into a normal playlist. `GET /api/v1/smart-playlist-templates` lists the built-in templates.
- **Autoplay.** `POST /api/v1/me/autoplay/next` chooses the next songs of autoplay for a list. `PATCH /api/v1/me/settings` takes the group `autoplay` (`enabled`, `includeVideoOnly`).
- **Playback state.** `GET` and `PUT /api/v1/me/playback-state` read and save the state of the player of a profile.
- **Online search.** `GET` and `PATCH /api/v1/settings/online-metadata` configure the search; `GET /api/v1/tracks/{id}/online-matches` and `GET /api/v1/albums/{id}/online-matches` search, `POST /api/v1/tracks/{id}/online-metadata` and `POST /api/v1/albums/{id}/online-metadata` apply a match, and `GET /api/v1/online-releases/{releaseId}/cover` delivers a cover preview.
- **Library check.** `GET /api/v1/library/issues` lists the problems (paged, with filter, search and sort order) and `GET /api/v1/library/issues/summary` counts them by kind. The check runs as the job `library-check`.
- **Themes.** `GET /api/v1/themes` and `GET /api/v1/themes/{id}` now name in `status` (`Current` or `Outdated`) and `problems` whether a theme passes the checks of this version. New error codes cover fonts in packages. The theme API gains parts for the new views and stays at version 5, since all changes are additions.
- **New settings:** `Lyrics__LookUpAgainAfter`, `LibraryCheck__DuplicateDurationTolerance`, `OnlineMetadata__RequestTimeout`, `OnlineMetadata__MaxAttempts`, `OnlineMetadata__MaxCoverBytes` and `Themes__MaxFontBytes`; the [README](https://github.com/wavemp-app/wavemp/blob/v1.0.88/README.md#settings) describes them.

All new endpoints and codes are described in the OpenAPI document.

## Upgrade notes

- **Your media folder.** Existing libraries become the first media folder, the one of `MEDIA_PATH`. Nothing has to be scanned again.
- **Autoplay is on.** Autoplay is switched on for every profile, including existing ones. Turn it off with its button in the transport bar or under **Settings > Autoplay**.
- **Back up the new folder.** Covers from the online search that could not be saved next to the music, and pictures replaced by an applied cover, live in `/data/online-covers`. If your backup leaves out single folders of `/data`, add this one.
- **Lyrics are looked up again.** If the lyrics download is on, songs that LRCLIB had no lyrics for at a lookup more than 30 days ago are looked up again in the next runs of the download.
- **Outdated themes.** After the update, the theme page may mark uploaded themes as outdated. They keep working; upload a new version that fixes the listed problems.

## Known limitations

- The floating mini player needs Chrome or Edge; other browsers show it in the WaveMP window. The music always plays in the WaveMP window, so leave it open.
- The files of one song belong together by their base name in one folder, so they cannot be spread over two media folders.
- Subsonic apps see the normal playlists only, not the smart playlists.
- The online search for album info is for admins only.
