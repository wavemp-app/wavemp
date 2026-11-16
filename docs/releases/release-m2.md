# WaveMP M2: Visualizations

The second milestone of WaveMP brings the music to the screen: the **Visualization** view of Now Playing now draws what you hear, with the classic Milkdrop presets and two visualizations of its own, A-Force and BlackCap. Your library is also safer: WaveMP exports its database every day and before every update.

## Visualizations

- **The music, drawn as you hear it.** **Visualization** on **Now Playing** is no longer a placeholder. It starts with the first song you play and moves with the sound of the player.
- **Three visualizations to choose from.** The controls of the visualization switch between **Milkdrop**, **A-Force** and **BlackCap**. They appear when you point at the visualization and always stay visible on touch screens.
- **Presets.** Choose a preset from the list or step to the previous or next one. While the automatic change is on, a random preset follows every 30 seconds; **Settings > Change visualization presets** sets the interval from 10 seconds to 5 minutes or turns the change off.
- **Full screen** with the full screen button or a double click.
- **Kept with your profile.** The visualization, the preset you chose, the automatic change and its interval follow your profile to every device.
- **Saves power.** The visualization only draws while you can see it: on another view, another page or in a background tab it pauses.
- **Quality to match your device.** **Settings > Visualization quality** sets how much detail is drawn. **Auto**, the default, starts with the most detail and lowers it while your device cannot keep up a smooth picture, then raises it again; **Low**, **Medium** and **High** keep one level.
- Visualizations need WebGL 2 and Web Audio, which current browsers offer. On iPhones the browser offers no full screen for them.

### Milkdrop

- **The classic Milkdrop presets**, more than a hundred of them from the preset pack of butterchurn, with their colorful, ever-changing patterns.

### A-Force

- **WaveMP's own visualization** with flowing waveforms, sparkling particles, color gradients and a moving camera that react to the bass, the mids, the highs and the beat of the music.
- **Six styles** as its presets: Aurora, Nebula, Ember, Lagoon, Prism and Eclipse. A new style blends smoothly into the picture instead of cutting over.

### BlackCap

- **WaveMP's calm visualization** with organic, floating 3D shapes and soft light. The shapes breathe with the tempo, swell gently on a beat, sway with the spectrum and mirror the lights around them.
- **Six scenes** as its presets: Lumen, Tide, Orchid, Mercury, Halo and Dusk. A new scene slowly morphs forms, colors and motion over a few seconds.

## Backups

- **Daily exports of your database.** Every day at 03:00, WaveMP exports its database into the folder `backups` of the data volume (`/data/backups`). The last 7 exports are kept.
- **Before every update.** When a new version changes the structure of the database, WaveMP exports it first, and leaves the database untouched if that export fails.
- **Administration > Backups** shows the last and the next backup and all exports, backs up on demand, deletes exports and sets the time of day and the number of exports to keep.
- **Time zone.** The daily backup runs at the local time of the server; set the time zone with `TZ`, for example `TZ: Europe/Berlin`.
- **Restore in a few commands.** The README explains which folders to copy to your own backup and how to restore an export. Restoring is not possible in the web app, so a wrong click cannot replace your library.

## Improvements

- **Every page starts at the top.** Opening another page no longer keeps the scroll position of the previous one, while back and forward return to where you were.
- **Try again in the administration.** If a page of the administration or its settings cannot be loaded, for example while the server restarts, a **Try again** button loads it again without reloading the app.

## For developers

- **More precise API description.** The OpenAPI document now describes the address of created resources and queued runs (`Location`) and the request bodies of all operations.

## Documentation

- **Screenshots** in the README show WaveMP on the desktop and on a phone.
- **Third-party notices.** `THIRD-PARTY-NOTICES.md` lists every component WaveMP is built on with its license. The Docker image ships it under `/licenses`, together with the license texts of the visualization packages.
- **Backup & Restore** in the README, and a description of the crossfade button and the collapsible sidebar in the usage guide.

## Known limitations

- Profiles are not protected yet: everyone who can open WaveMP can select any profile, including the admin profile. Do not expose WaveMP to the internet without protection, for example a reverse proxy with authentication or a VPN.
- WaveMP ships with the theme Midnight only.
