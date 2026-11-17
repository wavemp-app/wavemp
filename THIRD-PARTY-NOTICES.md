# Third-Party Notices

WaveMP is licensed under the [MIT License](LICENSE). It is built on the open-source components listed below, each under its own license.

- **Packages** (NuGet and npm): every package that a project of this repository uses directly, with its license. The packages they depend on in turn are not listed one by one; the [license check](README.md#license-check) makes sure that every one of them, down to the last transitive dependency, has a license allowed by `.github/license-policy.json`: MIT-compatible licenses only, never GPL or AGPL. The license check also fails if a direct dependency is missing here or if this file lists a package that no project uses any more.
- **Docker image**: the programs and libraries the image brings besides WaveMP itself. Their license texts are shipped in the image under `/licenses`, together with the license of WaveMP (`/licenses/wavemp`) and this file (`/licenses/THIRD-PARTY-NOTICES.md`).
- **Graphics**: icons from other sources. Everything else under `assets/` was created for WaveMP and is licensed under its MIT license; see [assets/LICENSES.md](assets/LICENSES.md).

## Docker image

| Component | License | Use |
|---|---|---|
| .NET runtime and ASP.NET Core 10 (base image `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`) | MIT | Runs the service |
| Alpine Linux (packages of the base image) | Various open-source licenses, named per package (`apk info --license <package>`) | Operating system of the image |
| FFmpeg (Alpine package `ffmpeg`) | GPL-2.0-or-later AND LGPL-2.1-or-later | `ffprobe` reads the technical details of media files, `ffmpeg` transcodes songs and videos to HLS and crops and scales covers and the pictures of the profiles; both run as separate processes and are never linked. License texts, notice and build configuration under `/licenses/ffmpeg` |
| ICU (Alpine packages `icu-libs`, `icu-data-full`) | Unicode-3.0 | Unicode normalization and culture-aware text handling of .NET; `/licenses/icu` |
| pg_dump (Alpine package `postgresql18-client`) | PostgreSQL | Exports the database (backups); runs as a separate process; `/licenses/postgresql` |
| tzdata (Alpine package `tzdata`) | Public domain | Time zones for `TZ` |
| Intel Media Driver for VA-API (Alpine package `intel-media-driver`, amd64 only) | MIT AND BSD-3-Clause | Hardware transcoding with Intel Quick Sync; `/licenses/intel-media-driver` |
| Intel VA-API driver for older GPUs (Alpine package `libva-intel-driver`, amd64 only) | MIT | Hardware transcoding on older Intel GPUs; `/licenses/libva-intel-driver` |
| Intel VPL GPU runtime (Alpine package `onevpl-intel-gpu`, amd64 only) | MIT | Hardware transcoding with Quick Sync (QSV); `/licenses/onevpl-intel-gpu` |
| Intel Graphics Memory Management Library (dependency of the media driver, amd64 only) | MIT | Used by the Intel media driver; `/licenses/intel-gmmlib` |

The database runs in its own container from the official image `postgres:18` (PostgreSQL License), which the `docker-compose.yml` of the README starts next to WaveMP; it is not part of the WaveMP image.

## Service (.NET)

NuGet packages of the service and its libraries (`source/`), shipped in the image under `/app`.

| Package | License | Use |
|---|---|---|
| `EFCore.NamingConventions` | Apache-2.0 | snake_case names of tables and columns |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | MIT | Session tokens; includes `Microsoft.IdentityModel.*` and `System.IdentityModel.Tokens.Jwt` (MIT) |
| `Microsoft.AspNetCore.OpenApi` | MIT | OpenAPI document; includes `Microsoft.OpenApi` (MIT) |
| `Microsoft.EntityFrameworkCore.Design` | MIT | Database migrations at development time (not shipped) |
| `Microsoft.EntityFrameworkCore.Relational` | MIT | Database access; includes Entity Framework Core (MIT) |
| `Microsoft.Extensions.ApiDescription.Server` | MIT | Writes `openapi.json` during the build (not shipped) |
| `Microsoft.Extensions.Configuration.Abstractions` | MIT | Configuration of the libraries |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | MIT | Database check of `/api/v1/health` |
| `Microsoft.Extensions.Hosting.Abstractions` | MIT | Background services of the libraries |
| `Microsoft.Extensions.Identity.Core` | MIT | Hashes the PINs of the profiles (`PasswordHasher`) |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | MIT | Options of the libraries |
| `Microsoft.Extensions.Options.DataAnnotations` | MIT | Validation of the options at start |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | PostgreSQL | PostgreSQL provider of Entity Framework Core; includes `Npgsql` (PostgreSQL) |
| `Quartz` | Apache-2.0 | Background jobs (in memory) |
| `TagLibSharp` | LGPL-2.1-only | Reads and writes tags; used unchanged as a separate assembly (dynamic linking). License text, notice and source under `/licenses/taglibsharp` |

## Web app (npm)

Packages bundled into the web app, which the image serves from `/app/wwwroot`.

| Package | License | Use |
|---|---|---|
| `@microsoft/kiota-abstractions` | MIT | Generated API client |
| `@microsoft/kiota-bundle` | MIT | Generated API client; includes the Kiota serializers (MIT), `@std-uritemplate/std-uritemplate` (Apache-2.0), `@opentelemetry/api` (Apache-2.0), `tinyduration` (MIT) and `tslib` (0BSD) |
| `@microsoft/kiota-http-fetchlibrary` | MIT | Generated API client |
| `butterchurn` | MIT | Milkdrop visualization; includes `ecma-proposal-math-extensions`, `babel-runtime`, `core-js` and `regenerator-runtime` (all MIT). License text under `/licenses/butterchurn` |
| `butterchurn-presets` | MIT | Milkdrop presets; license text under `/licenses/butterchurn-presets` |
| `hls.js` | Apache-2.0 | Plays transcoded songs and videos (HLS) |
| `pinia` | MIT | State of the web app |
| `sortablejs` | MIT | Drag and drop in playlists |
| `vue` | MIT | User interface; includes `@vue/*` (MIT) |
| `vue-i18n` | MIT | Translations; includes `@intlify/*` (MIT) |
| `vue-router` | MIT | Navigation |

The service worker of the installed app (PWA) contains Workbox (`workbox-*`, MIT), added by `vite-plugin-pwa` during the build.

Packages for building, checking and testing the web app (not shipped):

| Package | License | Use |
|---|---|---|
| `@intlify/unplugin-vue-i18n` | MIT | Compiles the translation files |
| `@phosphor-icons/core` | MIT | Source of the icons copied to `assets/web/icons/phosphor` |
| `@resvg/resvg-js` | MPL-2.0 | Renders the PNG icons and the favicon from the logo during the build; includes the native renderer of its platform (`@resvg/resvg-js-<platform>`, MPL-2.0), used unchanged |
| `@tsconfig/node24` | MIT | TypeScript settings for Node.js |
| `@types/node` | MIT | Type definitions of Node.js (also used by the E2E tests) |
| `@types/sortablejs` | MIT | Type definitions of SortableJS |
| `@vitejs/plugin-vue` | MIT | Build of Vue components |
| `@vue/eslint-config-typescript` | MIT | Lint rules |
| `@vue/test-utils` | MIT | Unit tests of components |
| `@vue/tsconfig` | MIT | TypeScript settings for Vue |
| `eslint` | MIT | Lint |
| `eslint-config-prettier` | MIT | Lint rules |
| `eslint-plugin-oxlint` | MIT | Lint rules |
| `eslint-plugin-vue` | MIT | Lint rules |
| `jiti` | MIT | Loads the TypeScript configuration of ESLint |
| `jsdom` | MIT | Browser environment of the unit tests |
| `npm-run-all2` | MIT | Runs npm scripts together |
| `oxlint` | MIT | Lint |
| `prettier` | MIT | Formatting |
| `svgo` | MIT | Optimizes the logo (`npm run logo:export`) |
| `typescript` | Apache-2.0 | Type check |
| `vite` | MIT | Build and development server |
| `vite-plugin-pwa` | MIT | Manifest and service worker of the installable app |
| `vite-plugin-vue-devtools` | MIT | Vue developer tools in development |
| `vitest` | MIT | Unit tests |
| `vue-eslint-parser` | MIT | Lint of Vue components |
| `vue-tsc` | MIT | Type check of Vue components |

## Tests and tools

Packages and tools for tests, code generation and the license check (not shipped).

| Package | License | Use |
|---|---|---|
| `AwesomeAssertions` | Apache-2.0 | Assertions of the .NET tests |
| `Microsoft.AspNetCore.Mvc.Testing` | MIT | API integration tests (`WebApplicationFactory`) |
| `Microsoft.Extensions.Hosting` | MIT | Host of the .NET tests |
| `Microsoft.NET.Test.Sdk` | MIT | Runs the .NET tests |
| `NSubstitute` | BSD-3-Clause | Test doubles of the .NET tests |
| `NUnit` | MIT | .NET test framework |
| `NUnit.Analyzers` | MIT | Analyzers for NUnit tests |
| `NUnit3TestAdapter` | MIT | Runs NUnit tests with `dotnet test` |
| `@playwright/test` | Apache-2.0 | End-to-end tests and the screenshots of the README |
| `dotnet-ef` | MIT | .NET tool: creates database migrations |
| `microsoft.openapi.kiota` | MIT | .NET tool: generates the API client of the web app |
| `nuget-license` | Apache-2.0 | .NET tool: license check of the NuGet packages |
| `licensee` | Apache-2.0 | License check of the npm packages |
| `spdx-expression-parse` | MIT | License check: reads license expressions |
| `spdx-whitelisted` | MIT | License check: compares license expressions with the policy |

## Graphics

| Component | License | Use |
|---|---|---|
| Phosphor Icons (npm package `@phosphor-icons/core` 2.1.1, unchanged) | MIT | Icons of the web app in `assets/web/icons/phosphor`; license text under `/licenses/phosphor-icons` |
