# syntax=docker/dockerfile:1@sha256:4edf897a3ffa55b89f906fc8cc78afdb3f1834cc9c7083565e611a8a7d5fe99e

# Multi-arch build (linux/amd64, linux/arm64): the web app and the .NET service are built on the
# build machine's own platform ($BUILDPLATFORM). The service is cross-compiled for the target
# architecture ($TARGETARCH), so only the runtime stage runs for the target platform.
#
# All foreign images (including the Dockerfile frontend above) are pinned by the digest of their
# multi-arch index (image:tag@sha256:...); the tag only documents the version. To update an image,
# resolve the new index digest with
#   docker buildx imagetools inspect <image>:<tag>
# and replace tag and digest together.

# --- Web app (platform independent output) -------------------------------------
FROM --platform=$BUILDPLATFORM node:24-alpine@sha256:61cf3202592df80f42ff3a595636e97039b351d1bd2a1c475f897f328e4870ed AS web
WORKDIR /src/source/WaveMP.Web
COPY source/WaveMP.Web/package.json source/WaveMP.Web/package-lock.json ./
RUN npm ci
# Graphics, icons and themes, served unchanged by the web app (Vite publicDir).
COPY assets/web/ /src/assets/web/
# The logo; the build generates the favicon, the PWA icons and the Apple touch icon from it.
COPY assets/logo/wavemp-logo.svg /src/assets/logo/
COPY source/WaveMP.Web/ ./
# Major and minor version for builds without VERSION (the web app then reports <major>.<minor>.0-dev).
COPY Directory.Build.props /src/
# Version of CI images (1.0.N); empty means a development build, see Directory.Build.props.
ARG VERSION
RUN WAVEMP_VERSION="$VERSION" npm run build

# --- Service (cross-compiled on the build machine for the target architecture) --
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:0294f285d062afacc8b5f6d7febb0cd4ee879320e5585fa15f2f4ebf1feb9ba7 AS build
ARG TARGETARCH
WORKDIR /src
COPY .editorconfig global.json Directory.Build.props Directory.Packages.props ./
COPY source/WaveMP.Core.Backups/WaveMP.Core.Backups.csproj source/WaveMP.Core.Backups/
COPY source/WaveMP.Core.Caching/WaveMP.Core.Caching.csproj source/WaveMP.Core.Caching/
COPY source/WaveMP.Core.Collection/WaveMP.Core.Collection.csproj source/WaveMP.Core.Collection/
COPY source/WaveMP.Core.Jobs/WaveMP.Core.Jobs.csproj source/WaveMP.Core.Jobs/
COPY source/WaveMP.Core.Library/WaveMP.Core.Library.csproj source/WaveMP.Core.Library/
COPY source/WaveMP.Core.Lyrics/WaveMP.Core.Lyrics.csproj source/WaveMP.Core.Lyrics/
COPY source/WaveMP.Core.Metadata/WaveMP.Core.Metadata.csproj source/WaveMP.Core.Metadata/
COPY source/WaveMP.Core.Playlists/WaveMP.Core.Playlists.csproj source/WaveMP.Core.Playlists/
COPY source/WaveMP.Core.Scanning/WaveMP.Core.Scanning.csproj source/WaveMP.Core.Scanning/
COPY source/WaveMP.Core.Scrobbling/WaveMP.Core.Scrobbling.csproj source/WaveMP.Core.Scrobbling/
COPY source/WaveMP.Core.Settings/WaveMP.Core.Settings.csproj source/WaveMP.Core.Settings/
COPY source/WaveMP.Core.Streaming/WaveMP.Core.Streaming.csproj source/WaveMP.Core.Streaming/
COPY source/WaveMP.Core.Subsonic/WaveMP.Core.Subsonic.csproj source/WaveMP.Core.Subsonic/
COPY source/WaveMP.Core.Transcoding/WaveMP.Core.Transcoding.csproj source/WaveMP.Core.Transcoding/
COPY source/WaveMP.Core.Users/WaveMP.Core.Users.csproj source/WaveMP.Core.Users/
COPY source/WaveMP.Data/WaveMP.Data.csproj source/WaveMP.Data/
COPY source/WaveMP.Service/WaveMP.Service.csproj source/WaveMP.Service/
RUN dotnet restore source/WaveMP.Service/WaveMP.Service.csproj -a "$TARGETARCH"
COPY source/WaveMP.Core.Backups/ source/WaveMP.Core.Backups/
COPY source/WaveMP.Core.Caching/ source/WaveMP.Core.Caching/
COPY source/WaveMP.Core.Collection/ source/WaveMP.Core.Collection/
COPY source/WaveMP.Core.Jobs/ source/WaveMP.Core.Jobs/
COPY source/WaveMP.Core.Library/ source/WaveMP.Core.Library/
COPY source/WaveMP.Core.Lyrics/ source/WaveMP.Core.Lyrics/
COPY source/WaveMP.Core.Metadata/ source/WaveMP.Core.Metadata/
COPY source/WaveMP.Core.Playlists/ source/WaveMP.Core.Playlists/
COPY source/WaveMP.Core.Scanning/ source/WaveMP.Core.Scanning/
COPY source/WaveMP.Core.Scrobbling/ source/WaveMP.Core.Scrobbling/
COPY source/WaveMP.Core.Settings/ source/WaveMP.Core.Settings/
COPY source/WaveMP.Core.Streaming/ source/WaveMP.Core.Streaming/
COPY source/WaveMP.Core.Subsonic/ source/WaveMP.Core.Subsonic/
COPY source/WaveMP.Core.Transcoding/ source/WaveMP.Core.Transcoding/
COPY source/WaveMP.Core.Users/ source/WaveMP.Core.Users/
COPY source/WaveMP.Data/ source/WaveMP.Data/
COPY source/WaveMP.Service/ source/WaveMP.Service/
# The built-in avatars, embedded into WaveMP.Core.Users.
COPY assets/avatars/ assets/avatars/
# The built web app becomes part of the service's static assets.
COPY --from=web /src/source/WaveMP.Web/dist/ source/WaveMP.Service/wwwroot/
# Version of CI images (1.0.N); empty means a development build, see Directory.Build.props.
ARG VERSION
RUN dotnet publish source/WaveMP.Service/WaveMP.Service.csproj -c Release -a "$TARGETARCH" \
    --no-self-contained --no-restore -o /app ${VERSION:+-p:Version=$VERSION}

# --- Runtime --------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:21c3f9798416cba4c0576a947c286231cb71ea2884896cb968253f5c9668f930 AS runtime

# Image metadata (OCI labels). The CI adds version, revision, creation time and the GitHub
# repository details. OCI has no label for a logo, so the icon is given in the labels that
# container managers read: Unraid (net.unraid.docker.icon) and Artifact Hub
# (io.artifacthub.package.logo-url), both pointing to the PNG export of the logo.
LABEL org.opencontainers.image.title="WaveMP" \
      org.opencontainers.image.description="A nostalgic media player for your self-hosted music and music videos." \
      org.opencontainers.image.source="https://github.com/wavemp-app/wavemp" \
      org.opencontainers.image.licenses="MIT" \
      net.unraid.docker.icon="https://raw.githubusercontent.com/wavemp-app/wavemp/main/assets/logo/wavemp-logo-512.png" \
      io.artifacthub.package.logo-url="https://raw.githubusercontent.com/wavemp-app/wavemp/main/assets/logo/wavemp-logo-512.png"

# The Alpine base image runs .NET in invariant globalization mode, without ICU. WaveMP needs ICU
# for Unicode normalization (profile names, tags) and culture-aware comparisons, so ICU with the
# data of all cultures is installed and the invariant mode is turned off (the service also sets
# InvariantGlobalization=false, so it fails at start instead of running without ICU).
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# ffmpeg and ffprobe are used as external processes only (never linked); the cache scales covers
# to WebP with the libwebp encoder of the Alpine ffmpeg package.
# pg_dump exports the database into /data/backups, every day and before migrations. Its major
# version must match the PostgreSQL server of docker-compose.yml (postgres:18); the build fails if
# the package delivers another one. tzdata lets TZ set the time zone of the daily backup. su-exec
# (MIT) lets the entrypoint switch from root to the user PUID and the group PGID.
RUN apk add --no-cache ffmpeg icu-data-full icu-libs postgresql18-client su-exec tzdata \
    && pg_dump --version | grep -q '^pg_dump (PostgreSQL) 18\.' \
    && mkdir -p /licenses/ffmpeg /licenses/icu /licenses/phosphor-icons /licenses/postgresql /licenses/su-exec /licenses/taglibsharp /licenses/wavemp /data /media \
    && ffmpeg -hide_banner -version > /licenses/ffmpeg/VERSION \
    && pg_dump --version > /licenses/postgresql/VERSION \
    && chown "$APP_UID:$APP_UID" /data

# Hardware transcoding with Intel Quick Sync (amd64 only): the VA-API drivers (iHD for current
# GPUs, i965 for older ones) and the oneVPL runtime for h264_qsv. They are only used when a render
# device (/dev/dri) is passed into the container; otherwise videos are transcoded in software.
ARG TARGETARCH
RUN if [ "$TARGETARCH" = "amd64" ]; then \
        apk add --no-cache intel-media-driver libva-intel-driver onevpl-intel-gpu; \
    fi

COPY LICENSE /licenses/wavemp/LICENSE
# Overview of all third-party components and their licenses.
COPY THIRD-PARTY-NOTICES.md /licenses/THIRD-PARTY-NOTICES.md
COPY assets/licenses/ffmpeg/ /licenses/ffmpeg/
COPY assets/licenses/icu/ /licenses/icu/
COPY assets/licenses/postgresql/ /licenses/postgresql/
COPY assets/licenses/su-exec/ /licenses/su-exec/
COPY assets/licenses/taglibsharp/ /licenses/taglibsharp/
COPY assets/licenses/intel-media-driver/ /licenses/intel-media-driver/
COPY assets/licenses/libva-intel-driver/ /licenses/libva-intel-driver/
COPY assets/licenses/onevpl-intel-gpu/ /licenses/onevpl-intel-gpu/
COPY assets/licenses/intel-gmmlib/ /licenses/intel-gmmlib/
COPY assets/web/icons/phosphor/LICENSE /licenses/phosphor-icons/LICENSE
# The visualization bundles butterchurn and its preset pack (both MIT) into the web app; their
# license texts come from the installed npm packages, so they always match the bundled version.
COPY --from=web /src/source/WaveMP.Web/node_modules/butterchurn/LICENSE /licenses/butterchurn/LICENSE
COPY --from=web /src/source/WaveMP.Web/node_modules/butterchurn-presets/LICENSE /licenses/butterchurn-presets/LICENSE

WORKDIR /app
COPY --from=build /app ./

# The image starts as root, so that it also works where the folders of the mounts are created as
# root (Unraid, ZimaOS and CasaOS). The entrypoint hands the top level of the data folder to the
# user PUID and the group PGID (1654, the user app of the base image, by default) and runs WaveMP
# as this user with su-exec. Started as another user (user: in compose, --user), it runs WaveMP as
# that user unchanged.
COPY --chmod=0755 source/WaveMP.Service/docker-entrypoint.sh /usr/local/bin/wavemp-entrypoint
ENV PUID=1654 \
    PGID=1654
EXPOSE 8080
ENTRYPOINT ["wavemp-entrypoint", "dotnet", "WaveMP.Service.dll"]
