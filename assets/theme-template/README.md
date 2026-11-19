# WaveMP theme template

This folder is a complete theme package for WaveMP: change it, zip it and upload it under Administration > Themes.

| File | Content |
|---|---|
| `manifest.json` | Id, name, author and version of the theme, the version of the theme API it is written for, its variants and a preview picture per variant |
| `theme.css` | The only CSS file of the theme, with comments on every rule |
| `images/` | The pictures the CSS file uses (SVG, PNG or WebP) |
| `fonts/` (optional) | Own fonts as WOFF2 files; `theme.css` has a commented `@font-face` example. Only ship fonts whose license allows it and add their license texts (for example `OFL.txt`), which are allowed in the package |
| `preview-light.svg`, `preview-dark.svg` | The pictures of the theme picker in the settings; replace them with screenshots of your theme (480 pixels wide) |

To build a package, zip the files (not necessarily the folder itself) so that `manifest.json` lies at the root of the ZIP file or in a single folder:

```bash
cd my-theme
zip -r ../my-theme.zip .
```

The guide for theme authors with all parts, states and variables is `THEMING.md` in the WaveMP repository (https://github.com/wavemp-app/wavemp/blob/main/THEMING.md). This file is not installed with the theme.
