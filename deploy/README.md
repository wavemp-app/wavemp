# App catalog templates

Templates for installing WaveMP from the app catalogs of NAS systems. They are submitted to the
catalogs by hand; this folder is the source for those submissions. Installation instructions for
users are in the [README](../README.md#installation).

All templates use fixed version tags (`1.0.N`), never `latest` or `edge`. `node
.github/scripts/check-deploy.mjs` checks the templates (fixed and identical versions, well-formed
XML, existing files, up to date copies); the CI runs it together with `docker compose config` for
the CasaOS file.

## Files

| Folder | Catalog | Where it goes |
|---|---|---|
| `truenas/wavemp/` | [TrueNAS apps](https://github.com/truenas/apps), community train | Pull request to `truenas/apps` as `ix-dev/community/wavemp/` |
| `truenas/FEATURE-REQUEST.md` | TrueNAS forum | Text of the app request in the forum (Feature Requests) |
| `unraid/wavemp.xml`, `unraid/wavemp-db.xml` | Unraid Community Applications | Served from this repository (`TemplateURL`); the repository is registered once with Community Applications |
| `casaos/WaveMP/` | [ZimaOS and CasaOS app store](https://github.com/IceWhaleTech/CasaOS-AppStore) | Pull request to `IceWhaleTech/CasaOS-AppStore` as `Apps/WaveMP/` |

### TrueNAS (`truenas/wavemp/`)

The app definition in the format of `truenas/apps`: `app.yaml` (metadata), `item.yaml` (catalog
entry, generated from `app.yaml` by the scripts of the catalog), `ix_values.yaml` (images and
constants), `questions.yaml` (the install form), `README.md`, the compose template
`templates/docker-compose.yaml` and the test values in `templates/test_values/`. PostgreSQL comes
from the `postgres` dependency of the rendering library (with its permission and upgrade helpers).

The install form asks for the database password (required, hidden), the optional admin PIN, the
user and group (568 by default), the web port, the storages for data, media and the database (ixVolume
or host path, media optionally read only), the GPU for hardware transcoding and the usual labels and
resource limits.

Before the pull request, in a clone of `truenas/apps` with the folder copied to
`ix-dev/community/wavemp/`:

```bash
./.github/scripts/ci.py --app wavemp --train community --test-file basic-values.yaml
./.github/scripts/ci.py --app wavemp --train community --test-file hostpath-values.yaml
./.github/scripts/port_validation.py
```

`ci.py` also refreshes `lib_version_hash` and `item.yaml`. The icon and the screenshots are hosted
on the CDN of TrueNAS: attach `assets/logo/wavemp-logo-512.png` and the pictures of
`assets/screenshots/catalogs/` to the pull request and replace the addresses in `app.yaml` and
`item.yaml` with the CDN addresses the reviewer gives you.

### Unraid (`unraid/`)

`wavemp.xml` is the template of WaveMP, `wavemp-db.xml` the one of its PostgreSQL database (the
official `postgres` image). Both containers join the custom Docker network `wavemp`, so WaveMP
reaches the database by its container name `wavemp-db`; the database publishes no port. WaveMP runs
as root (`--user 0:0` in the extra parameters) because Unraid creates the folders of new paths as
root.

To list the templates in Community Applications, submit this repository once as a template
repository (submission form and policies of Community Applications in the Unraid forum); Community
Applications then reads the templates from `main`, so changes reach Unraid with the next push.

### ZimaOS and CasaOS (`casaos/WaveMP/`)

`docker-compose.yml` with the `x-casaos` metadata of the store (title, tagline, description and
install tips in English and German, category, architectures, icon and screenshots), plus the icon
(`icon.svg`, `icon.png`) and the screenshots (`screenshot-1.png` to `screenshot-3.png`). The icon and
the screenshots are copies of `assets/logo/wavemp-logo.svg`, `assets/logo/wavemp-logo-512.png` and
`assets/screenshots/catalogs/home.png`, `now-playing.png` and `album.png`; the check fails if they
differ. CasaOS has no hidden or required input fields: the database password is an ordinary
environment variable (`casaos` by default) that the install tips ask to change in both containers.

## New version

For every version that should reach the catalogs:

1. Pick the version `1.0.N` of a published image (ideally the one of a GitHub release) and look up
   its digest: `docker buildx imagetools inspect wavemp/wavemp:1.0.N`.
2. TrueNAS: `ix_values.yaml` (`tag: "1.0.N@sha256:..."`), `app.yaml` (`app_version: 1.0.N` and
   `version` increased by one, for example `1.0.1`), `changelog_url` if needed.
3. Unraid: `Repository` in `wavemp.xml`.
4. CasaOS: `image` of the service `wavemp`, `version`, `update_at` and `release_notes` in `x-casaos`.
5. PostgreSQL, if a new minor version is out: the tag and digest in `truenas/wavemp/ix_values.yaml`
   (the catalog bot also updates it), `casaos/WaveMP/docker-compose.yml` and the tag in
   `unraid/wavemp-db.xml`. A new major version needs a migration of the database and is not just a
   new tag.
6. Changed screenshots: `npm run screenshots:catalogs` in `tests/WaveMP.E2E` and copy the pictures
   into `casaos/WaveMP/` again.
7. Run `node .github/scripts/check-deploy.mjs` and submit the changes to the catalogs.
