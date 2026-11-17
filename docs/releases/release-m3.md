# WaveMP M3: Profiles and lyrics

The third milestone of WaveMP makes profiles your own: give them a picture and protect them with a PIN. **Now Playing** gets a fourth view with synchronized lyrics to sing along, admins choose which files and covers a song uses and check new songs in a review inbox, and WaveMP now has its own logo and is ready for the app catalogs of common NAS systems.

## Profiles

- **Profile pictures.** Under **Settings > Profile picture**, give your profile one of eight built-in pictures or an own JPEG, PNG or WebP picture of up to 10 MB. Drag and zoom it until the square shows what you want, then save. **Remove picture** shows your initials again.
- **Everywhere you see your profile.** The picture appears in the profile selection, in the profile menu, in the settings and on the admin page. Admins change and remove the pictures of all profiles under **Administration > Profiles**.
- **Optional PIN.** Under **Settings > PIN** every profile can set, change or remove a PIN of 4 to 8 digits. A protected profile shows a lock in the selection and asks for its PIN on a digit pad before it opens; the keyboard works as well.
- **Safe against guessing.** After 5 wrong PINs in a row, the profile is locked for 5 minutes, and WaveMP shows how long the lock lasts. Both values can be changed with `Pins__MaxFailedAttempts` and `Pins__LockoutDuration`.
- **Signed out elsewhere.** Setting or changing a PIN signs the profile out on all other devices.
- **Forgotten PINs.** Admins remove the PIN of a profile under **Administration > Profiles**, which also ends a lock at once.
- **Admin PIN from the start.** Set `ADMIN_PIN` in your `.env` file to protect the built-in admin profile from the first start. While the variable is set, the admin PIN cannot be changed in the app; a new value signs the admin profile out on all devices. A forgotten admin PIN is replaced the same way.
- PINs are stored only as salted hashes, never in plain text.

## Lyrics

- **Sing along.** **Lyrics** is the new fourth view of **Now Playing**, next to Cover, Video and Visualization. It can be chosen for songs with lyrics.
- **Synchronized lyrics follow the music.** The current line is large and bright in the middle, the others are dimmed. With a time per word, the current word fills while it is sung. A click or tap on a line jumps there.
- **Calm and readable.** If you scroll yourself, the lyrics follow the music again after a few seconds. Lyrics without times scroll calmly over the song. With reduced motion switched on in your system, the lyrics jump instead of scrolling.
- **From your files.** WaveMP reads `.lrc` files with the name of the song (`Song.lrc`), including enhanced LRC with a time per word, and lyrics stored in the tags of your files (ID3, Vorbis and MP4).
- **Download from LRCLIB.** Switch on **Download lyrics from LRCLIB** under **Administration > Library** (off by default), and WaveMP looks up the lyrics of new songs after each scan, preferring synchronized lyrics. They are saved as `Song.lrc` next to the song, or in the database if the folder cannot be written. An existing `.lrc` file is never replaced. While the download is on, the titles, artists, albums and lengths of your songs are sent to LRCLIB.
- **For admins** the page of a song shows where its lyrics come from and whether they are synchronized, and **Download lyrics** looks them up at once.

## Library management

- **Choose the files of a song.** **Administration > Files** lists all songs with their audio files, videos and covers, with filters such as songs with several audio files and a search by title, folder or file name. Open a song and pick the audio file, video or cover it uses instead of the one of the highest quality, or **Automatic** to go back to the quality rule.
- **Choices that last.** A chosen file stays in use across scans. If it is deleted, the song falls back to the automatic choice, and the page shows the missing file until it is back.
- **Cover rules.** Choose whether the album cover or the song's own cover comes first: for the whole library under **Administration > Library**, and for single albums and songs under **Cover** on their pages. The most specific rule wins, and a preview shows the result before you save it.
- **Review inbox.** New songs wait in a review inbox until an admin has checked their tags. A button at the top of every page shows how many songs are waiting. The inbox shows which tags each song lacks, sorts, filters and searches, and marks many songs as reviewed at once.
- **Review song by song.** A song opens in the metadata editor with **Save and mark as reviewed**, **Mark as reviewed** and **Skip**, and the next song follows by itself.
- After the update to this version, all songs of your library start unreviewed once.

## Languages

- **More languages from the community.** WaveMP is now translated on Hosted Weblate. Every language with a translation appears under **Settings > Language**, named in its own language, and texts that are not translated yet are shown in English.
- **Regional variants.** Before a profile is selected, WaveMP follows the language of the browser, also for regional variants such as German for `de-AT`.

## Improvements

- **Back where you were.** A **Back** button on the pages of songs, albums, artists, genres, years and playlists returns to the previous page. Back, in the app or in the browser, now returns to a list at the same place, with its filters and the songs already loaded, without loading it again.
- **The WaveMP logo.** WaveMP has its own logo, used in the app, as the icon of the browser tab and of the installed app on desktop, Android and iPhone.
- **Time zones on the backup page.** **Administration > Backups** and **Administration > Jobs** now name the time zone of the server next to the daily schedule, and show the last and next run with the time zone of your browser.
- **Quick view changes are kept.** Switching the views of **Now Playing** or collapsing the sidebar quickly in a row now always stores the last choice.
- **Quieter log.** The log no longer shows errors about a missing Kerberos library on every start, nor errors about missing tables on the very first start with an empty database.
- **Deleted profiles.** Actions of a profile that is deleted at the same moment no longer cause server errors.

## Installation

- **Docker Hub first.** The README and the compose file now use the image `wavemp/wavemp` from Docker Hub. The image on GitHub Container Registry stays available as an alternative.
- **NAS systems.** The README explains the installation on TrueNAS, Unraid, ZimaOS and CasaOS, Synology Container Manager and QNAP Container Station, including the media folder, backups and hardware transcoding.
- **App catalog templates.** Templates for the app catalogs of TrueNAS, Unraid and ZimaOS/CasaOS are ready. Until WaveMP is listed in these catalogs, they can be installed by hand as described in the README.

## For developers

- **New API endpoints** for PINs, profile pictures, file selections, cover rules, lyrics and the review inbox, all described in the OpenAPI document.
- **Language as tag.** The language of a profile is now a language tag such as `en`, `de` or `pt-BR` instead of a fixed list.

## Known limitations

- A PIN keeps other people in your household out of a profile; it is not a login. Do not expose WaveMP to the internet without protection, for example a reverse proxy with authentication or a VPN.
- WaveMP is not listed in the app catalogs of TrueNAS, Unraid, ZimaOS and CasaOS yet.
- WaveMP ships with the theme Midnight only.
