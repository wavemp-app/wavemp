# App catalog templates

Templates for installing WaveMP from the app catalogs of NAS systems. They are submitted to the
catalogs by hand; this folder is the source for those submissions. Installation instructions for
users are in the [README](../README.md#installation).

All templates use a fixed version tag (`1.0.N`), never `latest` or `edge`: the version of the
latest milestone release. They are raised with every milestone release (see
[Release of a milestone](#release-of-a-milestone)). `node .github/scripts/check-deploy.mjs` checks
the templates (fixed and identical versions, well-formed XML, existing files, up to date copies);
the CI runs it together with `docker compose config` for the CasaOS file. With
`--release 1.0.N --digest sha256:...` it also checks that every template names this version and that
the TrueNAS template pins this digest; the release workflow runs it like that before it creates a
release.

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
on the CDN of TrueNAS: attach `assets/logo/wavemp-logo-default-512.png` and the pictures of
`assets/screenshots/catalogs/` to the pull request and replace the addresses in `app.yaml` and
`item.yaml` with the CDN addresses the reviewer gives you.

### Unraid (`unraid/`)

`wavemp.xml` is the template of WaveMP, `wavemp-db.xml` the one of its PostgreSQL database (the
official `postgres` image). Both containers join the custom Docker network `wavemp`, so WaveMP
reaches the database by its container name `wavemp-db`; the database publishes no port. Unraid
creates the folders of new paths as root. The image starts as root, hands its data folder to
`nobody:users` (`PUID` 99 and `PGID` 100, in the advanced view) and then runs as them, so the
template needs no `--user`.

To list the templates in Community Applications, submit this repository once as a template
repository (submission form and policies of Community Applications in the Unraid forum); Community
Applications then reads the templates from `main`, so changes reach Unraid with the next push.

### ZimaOS and CasaOS (`casaos/WaveMP/`)

`docker-compose.yml` with the `x-casaos` metadata of the store (title, tagline, description and
install tips in English and German, category, architectures, icon and screenshots), plus the icon
(`icon.svg`, `icon.png`) and the screenshots (`screenshot-1.png` to `screenshot-3.png`). The icon and
the screenshots are copies of `assets/logo/wavemp-logo-default.svg`,
`assets/logo/wavemp-logo-default-512.png` and `assets/screenshots/catalogs/home.png`,
`now-playing.png` and `album.png`; the check fails if they differ. CasaOS has no hidden or required input fields: the database password is an ordinary
environment variable (`casaos` by default) that the install tips ask to change in both containers.
CasaOS creates the folders of the app as root as well. The image starts as root, hands its data
folder to the user of the system, which CasaOS and ZimaOS fill into `$PUID` and `$PGID`, and then
runs as it, so the compose file sets no `user`.

## Release of a milestone

The templates always name the version of the latest milestone release. The TrueNAS template pins
the image with its digest, which exists only after the CI has published the image, so the templates
are raised in a commit of their own after the release notes, and the GitHub release is created on
that commit. Every milestone release follows these steps, in this order:

1. **Release notes.** After the last issue of the milestone (with green CI), write
   `docs/releases/release-m<M>.md`. Link pictures and files of the repository with relative paths
   (`../../assets/screenshots/...`), not with addresses at `main`. Then pin the release notes of
   the earlier milestones to the tags of their releases with
   `node .github/scripts/release-notes.mjs --pin` (needs the tags: `git fetch --tags`), so their
   pictures keep working when files are renamed on `main` later. Commit both and push, and wait for
   the CI run (`gh run watch`). This run publishes the image of the release, `1.0.R`, where R is
   its run number: `gh run list --workflow main.yml --commit <sha> --json number --jq '.[0].number'`.
2. **Templates.** Raise all templates to `1.0.R` (see [Raising the templates](#raising-the-templates)),
   check them with `node .github/scripts/check-deploy.mjs --release 1.0.R --digest <digest>`, commit
   and push them, and wait for the CI run. This commit changes nothing outside `deploy/`. Its CI run
   publishes another image, `1.0.R+1`, from the same source; the release and the templates stay on
   `1.0.R`.
3. **GitHub release.** Start the release workflow and wait for it:

   ```bash
   gh workflow run release.yml --ref main -f version=1.0.R -f milestone=<M>
   gh run watch "$(gh run list --workflow release.yml --limit 1 --json databaseId --jq '.[0].databaseId')" --exit-status
   ```

   `.github/workflows/release.yml` checks that `1.0.R` is published on Docker Hub and GHCR with the
   same digest, that `main` differs from the commit of the image only in `deploy/`, that every
   template names `1.0.R` and pins its digest, that the release notes exist and link to existing
   files only, and that the tag does not exist yet. Only then does it create the release `v1.0.R`
   (title `WaveMP 1.0.R`) on the commit of step 2, with the release notes as its text (relative links
   and images, and addresses of the repository at a branch, point to the files at the tag). If a check fails, nothing is created: fix the
   templates, push, wait for green CI and start the workflow again. If something outside `deploy/`
   has to change, its commit publishes a new image; continue with step 2 for that image.
4. **Milestone.** Close the milestone on GitHub.

Then submit the changed templates to the catalogs that list WaveMP (Unraid reads them from `main`
by itself).

### Raising the templates

1. Look up the digest of the image: `docker buildx imagetools inspect wavemp/wavemp:1.0.R` (line
   `Digest`).
2. TrueNAS: `ix_values.yaml` (`tag: "1.0.R@sha256:..."`), `app.yaml` (`app_version: 1.0.R` and
   `version`, the version of the catalog entry, increased by one, for example `1.0.1` to `1.0.2`),
   `changelog_url` if needed.
3. Unraid: `Repository` in `wavemp.xml`.
4. CasaOS: `image` of the service `wavemp`, and in `x-casaos` `version` (`1.0.R`), `update_at` (the
   day of the commit) and `release_notes` (one sentence in English and German with the main changes
   of the milestone, and the address of the GitHub release
   `https://github.com/wavemp-app/wavemp/releases/tag/v1.0.R`).
5. PostgreSQL, if a new minor version is out: the tag and digest in `truenas/wavemp/ix_values.yaml`
   (the catalog bot also updates it), `casaos/WaveMP/docker-compose.yml` and the tag in
   `unraid/wavemp-db.xml`. A new major version needs a migration of the database and is not just a
   new tag.
6. Changed screenshots: `npm run screenshots:catalogs` in `tests/WaveMP.E2E` and copy the pictures
   into `casaos/WaveMP/` again. Their sources in `assets/screenshots/catalogs/` are outside of
   `deploy/`, so new screenshots (with their copies) are committed before step 1 of the release, not
   together with the templates.
7. Run `node .github/scripts/check-deploy.mjs --release 1.0.R --digest <digest>`.
