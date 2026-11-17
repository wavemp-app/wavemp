# App request for the TrueNAS forum

Ready text for the app request in the TrueNAS forum (category **Feature Requests**, tag **apps**).
Before posting, check that the version and the links are still current (see `deploy/README.md`).

---

**Title:** Add WaveMP (self-hosted music and music video player) to the community apps

**Text:**

Hello everyone,

I would like to propose **WaveMP** for the community train of the TrueNAS apps catalog.

WaveMP is a self-hosted media player for music and music videos with a nostalgic look inspired by
the classic desktop media players: glass, gloss and soft gradients. It scans a media folder, keeps
the library in a PostgreSQL database and plays everything in the browser and on the phone, where it
can be installed as an app.

**Features**

- Music and music videos side by side, with the files of a song grouped by name
- Synchronized lyrics, visualizations, playlists, favorites and ratings
- Profiles for everyone in the household, optionally protected with a PIN
- Transcoding with ffmpeg of everything the browser cannot play, with optional Intel Quick Sync
- Daily export of the database into the data folder
- English and German, more languages through Weblate

**Facts for the catalog**

- License: MIT
- Image: `wavemp/wavemp` on Docker Hub (also `ghcr.io/wavemp-app/wavemp`), `linux/amd64` and
  `linux/arm64`, fixed version tags `1.0.N`
- Runs as any non-root user (568 works), no capabilities, no host network
- Needs PostgreSQL 18 (the `postgres` dependency of the library) and two storages: data (`/data`)
  and media (`/media`, read only is possible)
- Health check: `GET /api/v1/health`
- Optional GPU passthrough (`/dev/dri`) for hardware transcoding of videos

**Links**

- Source code and documentation: https://github.com/wavemp-app/wavemp
- Docker Hub: https://hub.docker.com/r/wavemp/wavemp
- Website: https://wavemp-app.github.io

An app definition in the format of the catalog (app.yaml, questions.yaml, compose template and test
values, based on the `postgres` dependency of the library) is ready in the repository under
`deploy/truenas`. I am happy to open the pull request against truenas/apps and to keep the app up
to date.

Thank you!
