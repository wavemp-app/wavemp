# WaveMP Theme API

Version 7

This document describes how themes change the look of the WaveMP web app: the parts of the DOM a theme can address, their states, the CSS variables and the icons. It is meant for theme authors.

## Overview

A theme is exactly one CSS file plus its graphics and, if it brings its own, its fonts. The themes shipped with WaveMP live in `/assets/web/themes/<id>/` and are served as `/themes/<id>/theme.css`, their graphics under `/themes/<id>/images/` and their fonts (WOFF2, with the license text) under `/themes/<id>/fonts/`; Midnight, for example, ships the font Selawik (SIL Open Font License 1.1), so it looks the same on every system. Admins can upload further themes as ZIP packages, see [Uploaded themes](#uploaded-themes). The app loads the active theme with a `<link id="wavemp-theme">` element.

The DOM is the same for every theme, except where a theme switches on one of the [Layout options](#layout-options) on the desktop. Every element a theme can address has a stable `data-part` attribute, for example `data-part="transport"` or `data-part="btn-play"`. States are expressed with `data-state` tokens and ARIA attributes. Icons and labels are always separate child elements (`data-part="icon"`, `data-part="label"`), so the pseudo-elements `::before` and `::after` of all areas and buttons are free for the theme, for example for gloss and glow effects.

The root element carries the active theme, its shown variant and the version of this API:

```html
<html lang="en" data-theme="midnight" data-theme-variant="dark" data-theme-api="7">
```

Every profile chooses its theme on the settings page; the app switches it at once, without reloading the page. A theme can have a light and a dark variant, see [Variants](#variants).

A theme can change everything: colors, graphics, fonts, sizes and the desktop layout. Only the mobile layout is fixed (see [Responsive layout](#responsive-layout)).

Selectors should use attribute selectors with the part names, for example:

```css
[data-part='transport'] [data-part='btn-play']:hover {
    --wmp-image-play-button-hover: url('/themes/example/images/play-hover.svg') center / 100% 100% no-repeat;
}
```

## Cascade

The base styles of WaveMP live completely in cascade layers (`wavemp.reset`, `wavemp.base`, `wavemp.components`, `wavemp.layout`, `wavemp.dev`, `wavemp.mobile`). A theme has no layer, so its rules always win against the base styles, independent of specificity and loading order. Rules of the theme are therefore simple and need no `!important`.

Rules for theme files:

- No `@layer` rules: the theme must stay outside the layers of the base.
- No `@import` rules and no external URLs (`http:`, `https:` or `//`): a theme only uses its own files.
- Every `url()` is root-absolute, for example `url('/themes/midnight/images/glass-button.svg')`. Relative URLs inside CSS variables are resolved against the stylesheet that uses the variable (the base CSS of the app), not against the theme file, so they would point to the wrong place.
- `[hidden]` always hides an element; this cannot be overridden.

## Responsive layout

The layout uses CSS Grid with `grid-template-areas`. The names of the grid areas are the part names, for example `header`, `toolbar`, `sidebar`, `content`, `transport` and `bottom-nav`.

- **Below 64rem** (`@media (width < 64rem)`), the fixed mobile base applies to every theme: a header with menu button, logo, server status and profile button, the search as toolbar, the content, the transport bar as mini player above the bottom navigation, the sidebar as drawer and the full-screen player (`transport` with the state `expanded`: `swipe-grip` and `btn-collapse` at the top, the cover with `player-media` and the title, `player-view-switch` while it is open, then the actions, the seek bar and the controls; swiped down it follows the finger with `translate`). Its layout properties are `!important` inside a cascade layer and cannot be changed by a theme, not even with `!important`. Colors, graphics, fonts and sizes still come from the theme. On detail pages (with a `detail-header`) the mobile base hides `content-title` visually, because `detail-title` shows the name.
- **From 64rem on** (`@media (width >= 64rem)`), the layout of the theme applies. The base provides a neutral desktop layout that a theme replaces in its own `@media (width >= 64rem)` rules. The breakpoint is fixed, because media queries cannot use variables.

The app fills the screen up to its edges (`viewport-fit=cover`); on iOS, the installed app reaches under the status bar (`black-translucent`). The base keeps the areas out of the safe areas: `app` has a padding of `env(safe-area-inset-top)`, `env(safe-area-inset-right)` and `env(safe-area-inset-left)` that shows `--wmp-theme-color`, and the bottom navigation (mobile) and the transport bar (desktop) pad their bottom with `env(safe-area-inset-bottom)`. The app also hands `--wmp-theme-color` to the browser as `<meta name="theme-color">` and uses it as background of the root element, so the status bar and the bars of the browser blend into the header: set it to the color at the top of the header. Themes that set the padding of `transport` on the desktop should keep `max(..., env(safe-area-inset-bottom))` at its bottom.

The page itself never scrolls: `app` is exactly as high as the screen, and only its areas scroll (`sidebar` and `content`, on Now Playing also `queue`, and the docked `queue-pane`), each with `overscroll-behavior: contain`. `sidebar` and `content` are containing blocks (`position: relative`), so absolutely positioned content inside them, for example a hidden label, cannot make the page wider or higher. A theme that makes other areas scroll should do the same. When another page opens, the app scrolls `content` (and the window) back to its top; back and forward (and `btn-detail-back`) return to the position the page had when it was left. Pages with lists (the home page, the library and its search, albums, artists, genres, years, the playlists, the review inbox of the administration) stay in the background with their loaded content while another page is shown, so back and forward find them unchanged, however far their list was loaded; a theme should therefore not rely on a page being built anew. Changes of the query alone (sorting, the scope of the library, the search term) keep the position.

The DOM is a superset: all areas exist on every device, including `tabs` and `bottom-nav`, `logo`, `content-title`, `btn-menu`, `sidebar-backdrop`, `btn-sidebar-toggle` and `btn-collapse`. The desktop layout of a theme decides which of them are visible. A part that is hidden in a grid layout should get `display: none`; parts that are not part of `grid-template-areas` would otherwise create implicit grid cells. To hide a label or a title but keep it for assistive technology, use the pattern from [Rules](#rules).

These parts are grids with named areas:

| Grid | Areas | Base desktop layout |
|---|---|---|
| `app` | `header`, `toolbar`, `sidebar`, `content`, `queue-pane`, `transport`, `bottom-nav` | `'header header' 'toolbar toolbar' 'sidebar content' 'transport transport'`; with the docked play queue ([Layout options](#layout-options)) `'header header header' 'toolbar toolbar toolbar' 'sidebar content queue-pane' 'transport transport transport'` |
| `header` | `btn-menu`, `logo`, `nav-history`, `breadcrumb`, `tabs`, `server-status`, `btn-profile` | `'logo nav-history tabs server-status btn-profile'`; for admins `'logo nav-history tabs . server-status btn-profile'`, the empty cell takes `btn-review-inbox`. With the breadcrumb in the header ([Layout options](#layout-options)) `'logo nav-history breadcrumb tabs server-status btn-profile'`, for admins `'logo nav-history breadcrumb tabs . server-status btn-profile'` |
| `toolbar` | `breadcrumb`, `view-switch`, `sort`, `search` | `'breadcrumb view-switch sort search'`; with the breadcrumb in the header its cell stays empty |
| `transport` | `seek`, `now-playing-mini`, `transport-controls`, `mode-switch`, `btn-collapse` | `'seek seek seek' 'now-playing-mini transport-controls mode-switch'`; `now-playing-actions` shares the area `mode-switch` (`justify-self: start`, `mode-switch` with `justify-self: end`), so the transport controls stay in the middle |

The `list` is no grid with areas but one grid with columns: `list-header`, `list-group`, `list-group-rows` and `list-row` are subgrids (`grid-template-columns: subgrid`) that span all columns of the list, so column headers and cells share the same columns and stay in line while the list scrolls and its width changes. Its columns are `--wmp-list-columns`; on the desktop, the base puts a first column of `--wmp-list-group-info-width` in front of them for `list-group-info` (`grid-column: 1`), lets `list-group-rows` and the first `list-column` start in the second column and lets `list-group-header` span all columns. On mobile, `list-group-info` spans all columns above the rows. Column headers and cells get the padding at the start and end of the row (`--wmp-space-s` on the first and the last one); `list-header` and `list-row` have no padding and no border of their own. A theme keeps them in line as long as it gives `list-column` and `list-cell` the same padding and border widths, and draws frames of rows inside them (for example with an inset `box-shadow`) instead of with a `border`. Column headers clip their content at their column; the `label` of the column header `favorite` is too wide for the heart column and is kept for assistive technology only. The video `badge` of a song sits before its title in the `list-cell` `title`; while one song of the list has a video (state `has-video` of `list`), the column header and the cells `title` are grids with a first column of `--wmp-list-badge-width` for the badge and a second one for the `label`, so the titles of songs with and without a video stay in line on the desktop and on phones.

The `logo` in `header` is a link to the start page on every device; the base keeps the color of the header for it and draws no underline.

On the desktop, the profile can collapse the sidebar with `btn-sidebar-toggle`; the choice is kept per profile. The collapsed `sidebar` has the state `collapsed` and shows only the icons of the tree: the base hides the `label`s of the `tree-item`s visually (they show as tooltip and stay for assistive technology), leaves out `tree-toggle` and the indentation. While it is collapsed, the base sets `--wmp-sidebar-width` on `app` to `--wmp-sidebar-collapsed-width`, so the base layout and every theme layout that sizes the sidebar column with `var(--wmp-sidebar-width)` narrow it; a theme that sets `--wmp-sidebar-width` should do so on `:root`, not on `app`. On mobile the drawer always shows the whole tree and the mobile base hides `btn-sidebar-toggle`.

Example of a desktop layout with the sidebar on the right:

```css
@media (width >= 64rem) {
    [data-part='app'] {
        grid-template-areas:
            'header header'
            'toolbar toolbar'
            'content sidebar'
            'transport transport';
        grid-template-columns: minmax(0, 1fr) var(--wmp-sidebar-width);
    }
}
```

### Touch targets

Below 64rem, everything a finger taps is at least `--wmp-touch-target-size` high, and never less than `2.75rem` (44 pixels): the `logo` in `header`, `btn-review-inbox`, the `tree-item`s of the drawer, the `segment`s, `now-playing-mini`, checkbox `field`s, the `select`s and inputs (through `--wmp-field-height`) and all buttons. Buttons with only an icon (`btn-item-menu`, also on tiles, `btn-favorite`, `btn-clear` of `search`, the icon buttons of `visualization-controls`) and `review-select` are also at least as wide, `btn-preset-picker` and `btn-color-scheme-picker` are as high; the `segment`s of a `segmented-control` are as high and keep the width of their label; `tree-toggle` takes a column of that width and height at the start of its `tree-item`, and the base indents every level of the tree by that column (the start padding of `tree-item` is part of the protected layout), so the entries of a node start below its icon. The minimum sizes are `!important` inside the mobile layer: a theme can make a target larger, not smaller. Links (`link`, `review-title`, `file-selection-title`, `library-issue-title`) are inline blocks with a padding above and below their line, which a theme cannot change either, so they reach the same height without a background of their own. A horizontal `slider` is as high as a touch target and a vertical one as wide (the `slider-thumb` stays small; the whole slider takes the touch); the thin `seek` of the mini player is no touch target. The `chart-bar`s of the statistics are exempt, the table below the chart lists the same numbers. On phones the `meta-item`s of a `list-group-meta` stand in a row that wraps, so their links do not pile up next to the cover, and every `meta-item` of a `detail-info` is as high as a touch target, with name and value centered in it.

## Layout options

Some parts of the desktop layout are optional. A theme switches them on with these variables on `:root`; the app reads them from the computed style of the root element when the theme is loaded or switched and when the window crosses 64rem, and changes the DOM accordingly. A theme may set them in a rule for `:root` or inside its `@media (width >= 64rem)` rules. The defaults keep the DOM and the layout of a theme that sets none of them exactly as they are. Below 64rem no option applies: phones always show the fixed mobile base. An empty or unknown value counts as the default.

| Variable | Values (default first) | Effect on the desktop |
|---|---|---|
| `--wmp-layout-breadcrumb` | `toolbar`, `header` | `header`: the `breadcrumb` (with all its parts) is a child of `header` right after `nav-history` instead of the first child of `toolbar`. There is always exactly one `breadcrumb`. The base gives it the grid area `breadcrumb` of `header` and, for a `header` with a `breadcrumb` (`:has(> [data-part='breadcrumb'])`), the template `'logo nav-history breadcrumb tabs server-status btn-profile'` (for admins with the empty cell for `btn-review-inbox` before `server-status`), in which the breadcrumb takes the free width. A theme with its own `grid-template-areas` for `header` adds the area `breadcrumb` in a rule for `[data-part='header']:has(> [data-part='breadcrumb'])` |
| `--wmp-layout-queue-pane` | `none`, `docked` | `docked`: the play queue is a pane beside the content, the `queue-pane`, a child of `app` right after `content`, on every page but Now Playing (which shows the `queue` itself). It holds the `segmented-control` `queue-pane-view` (`segment`s `queue` and `lyrics`), the `queue-pane-header` with the current song, the `queue` (the same parts as on Now Playing, without `autoplay`) or the `lyrics` of the current song, and the `queue-pane-summary`. The base gives it the grid area `queue-pane` and, for an `app` with a pane (`:has(> [data-part='queue-pane'])`), the template `'header header header' 'toolbar toolbar toolbar' 'sidebar content queue-pane' 'transport transport transport'` with a third column of `--wmp-queue-pane-width`. The pane scrolls itself like `sidebar`; inside it the base lets the `queue` scroll between the current song and the summary, keeping its current entry in view. A theme with its own `grid-template-areas` for `app` adds the area `queue-pane` in a rule for `[data-part='app']:has(> [data-part='queue-pane'])` |
| `--wmp-layout-view-options` | `off`, `on` | `on`: `view-switch` is enabled on the pages that show the songs of the library page by page (songs, recently added, an artist, a genre, a year) and opens the view menu, a `context-menu` with the `menuitemradio` entries `view-grouped` (grouped by album, the default) and `view-list` (a plain list) with the icon slots of the same names. The choice is part of the address of the page (`?view=list`). In the plain list the `list` has the state `ungrouped` and one `list-group` without `list-group-header` and without `list-group-info`; the base then gives the `list` only the columns `--wmp-list-columns` and lets the column headers and `list-group-rows` start in its first column. A theme with its own `grid-template-columns` for `list` sets them for `[data-part='list'][data-state~='ungrouped']` as well. While the option is `off`, the lists stay grouped by album and `view-switch` stays disabled |

`--wmp-queue-pane-width` (`18rem`) is the width of the column of the docked play queue.

Of the shipped themes, Daylight switches on all three options (`assets/web/themes/daylight/theme.css`): it is a working example of a desktop layout with the breadcrumb in the header, the docked play queue with its tabs at the height of the toolbar and the view options.

Example: a theme that shows the breadcrumb in the header on the desktop.

```css
@media (width >= 64rem) {
    :root {
        --wmp-layout-breadcrumb: header;
    }
}
```

## Variants

A theme has one or two variants, `light` and `dark`, both in its one CSS file. The root element always names the shown variant in `data-theme-variant`, also for a theme with one variant. A theme with both variants follows the appearance of the profile (settings: "Light or dark" with `system`, `light` and `dark`; `system` follows the dark mode of the device); a theme with one variant always shows it. The variants of a shipped theme are listed in its registry entry (`source/WaveMP.Web/src/themes/themes.ts`).

The CSS of the theme selects the dark variant with the attribute, never with `@media (prefers-color-scheme)`, because the profile may choose another appearance than the device:

```css
:root {
    --wmp-color-text: #1b1b1b;
    --wmp-color-surface: #fbfbfb;
}

:root[data-theme-variant='dark'] {
    color-scheme: dark;
    --wmp-color-text: #ffffff;
    --wmp-color-surface: #272727;
}
```

The media features a theme may use are `width` (the layout breakpoint), `prefers-reduced-motion`, `prefers-reduced-transparency: reduce`, `prefers-contrast: more`, `forced-colors: active`, `hover: hover` and `pointer: coarse`; a test checks this for the shipped themes.

Each variant has a preview picture for the theme picker of the settings, `/assets/web/themes/<id>/preview-<variant>.webp` (480 pixels wide, the artist overview on the desktop, which shows the chrome of the theme and its cover placeholders), made with `npm run theme:screenshots -- --theme <id> --preview` (see [Checking a theme](#checking-a-theme)).

### Cover color

While a song with a cover is current, the app sets the dominant color of its cover on the root element: `--wmp-cover-color` (for example `rgb(31 92 160)`) and `--wmp-cover-color-contrast` (`#000000` or `#ffffff`, whichever reads better on the cover color). Without a current song or cover both are missing, so a theme always gives a fallback:

```css
[data-part='transport'] {
    background: color-mix(in srgb, var(--wmp-cover-color, #8aa4c8) 35%, transparent);
}
```

The color changes with the song; a theme can let it fade with `transition` on the property that uses it.

## Parts

| Part | Element / role | Parent | Description |
|---|---|---|---|
| `app` | `div` | | Grid of all main areas |
| `header` | `header` | `app` | Top bar |
| `btn-menu` | `button` (`aria-expanded`, `aria-controls`) | `header` | Opens the sidebar as drawer (mobile) |
| `logo` | `a` (in `header`), `span` (in `profile-select`) | `header`, `profile-select` | App logo with `logo-image` and `label`; in `header` a link to the start page |
| `logo-image` | `span` | `logo` | Graphic of the logo (`--wmp-image-logo`) |
| `nav-history` | `div` (`role="group"`) | `header` | Back and forward buttons |
| `btn-back` | `button` | `nav-history` | Back in the history |
| `btn-forward` | `button` | `nav-history` | Forward in the history |
| `tabs` | `nav` | `header` | Main navigation: an `indicator` and one `tab` per entry; tells the active `tab` with the runtime variables of an active element, see [Active element of a group](#active-element-of-a-group) |
| `tab` | `a` (`aria-current`) | `tabs` | Entry of the main navigation with `icon`, `label` and an `indicator` of its own (the arrow of the active tab) |
| `server-status` | `div` (`role="status"`) | `header` | Connection to the server with `status-indicator` and `label` |
| `status-indicator` | `span` | `server-status` | Dot that shows the state |
| `btn-review-inbox` | `a` (`aria-label` with the number, `aria-current`) | `header` | Entry of the review inbox, only for admins: `icon` (slot `review-inbox`), `label` and, while songs are unreviewed, a `badge` with the number as `label`. It has no grid area of its own (`grid-row: 1`): it takes the first free cell of the first row of `header`. The base layouts leave an empty cell (`.`) for it before `server-status`; in a layout without a free cell it gets a new column at the end of the header, so a theme with its own `grid-template-areas` should leave a `.` for it, for example in a rule for `header:has(> [data-part='btn-review-inbox'])`. On mobile it shows the `icon` and the number only |
| `btn-profile` | `button` (`aria-haspopup="menu"`, `aria-expanded`) | `header` | Opens the profile menu (settings, switch profile); `avatar` and `label` with the name |
| `toolbar` | `div` | `app` | Bar below the header |
| `breadcrumb` | `nav` | `toolbar`, or `header` (after `nav-history`) where the theme sets `--wmp-layout-breadcrumb: header` on the desktop | Location of the current page; there is always exactly one |
| `breadcrumb-list` | `ol` | `breadcrumb` | List of the breadcrumb items |
| `breadcrumb-item` | `li` | `breadcrumb-list` | One location with link and separator |
| `breadcrumb-link` | `a`, or `span` (`aria-current="page"`) for the current page | `breadcrumb-item` | Name of a location; the first one has the icon `breadcrumb-root` |
| `breadcrumb-separator` | `span` | `breadcrumb-item` | Separator icon between two items |
| `view-switch` | `button` (`aria-haspopup="menu"`) | `toolbar` | View options of the list: disabled, unless the theme switches on `--wmp-layout-view-options` and the page shows the songs of the library ([Layout options](#layout-options)); then it opens the view menu (grouped by album or a plain list) |
| `sort` | `button` (`aria-haspopup="menu"`) | `toolbar` | Opens the sort options of the current list (a `context-menu` with `menuitemradio` entries); disabled where the list cannot be sorted, always hidden on mobile |
| `search` | `div` (`role="search"`) | `toolbar` | Search field with `icon`, `input` and `btn-clear` |
| `input` | `input` (`aria-invalid`) | `search`, `field` | Text input |
| `btn-clear` | `button` | `search` | Clears the search; `hidden` while the field is empty |
| `sidebar` | `aside` | `app` | Navigation tree; a drawer on mobile; on the desktop collapsible to its icons, see [Responsive layout](#responsive-layout) |
| `btn-sidebar-toggle` | `button` (`aria-expanded`, `aria-controls`) | `sidebar` | Collapses the sidebar to its icons (icon `sidebar-collapse`) or expands it again (icon `sidebar-expand`); before the `tree`; desktop only |
| `sidebar-backdrop` | `div` | `app` | Backdrop behind the drawer (mobile) |
| `tree` | `ul` (`role="tree"`) | `sidebar` | Tree view: an `indicator` (a `li` with `role="none"`) and its `tree-node`s; tells the `tree-item` of the current page among the visible items with the runtime variables of an active element, see [Active element of a group](#active-element-of-a-group) |
| `tree-node` | `li` (`role="none"`) | `tree`, `tree-group` | Node with its item and its children |
| `tree-item` | `a` or `span` (`role="treeitem"`) | `tree-node` | Row of a node with `tree-toggle`, `icon` and `label` |
| `tree-toggle` | `span` | `tree-item` | Expand and collapse icon of nodes with children |
| `tree-group` | `ul` (`role="group"`) | `tree-node` | Children of a node |
| `content` | `main` | `app` | Content of the current page |
| `content-title` | `h1` | `content` | Title of the page |
| `view` | `div` | `content` | Page, see `data-view` in [States](#states) |
| `placeholder-notice` | `p` (`role="note"`) | `view` | Notice that the content is a placeholder; currently only shown in the component overview |
| `empty-state` | `div` | `view`, `stage`, `profile-select`, `section`, `queue-pane` | Message for empty content, or that something could not be loaded |
| `empty-state-title` | `p` | `empty-state` | Title of the message |
| `empty-state-text` | `p` | `empty-state` | Text of the message |
| `link` | `a` | `empty-state`, `view`, `section`, `meta-value` | Text link in the content, for example to an album, an artist, a genre or a year |
| `stage` | `div` | `view` | Stage of the Now Playing page: `stage-views`, `visualization-controls` while the visualization is shown, `stage-notice` if the video fails, then `stage-info` (or `empty-state` while nothing plays) and `stage-actions`. The base makes it a size container named `wavemp-stage` (`container: wavemp-stage / inline-size`), so it takes the width of its place and never widens the page; below a width of `32rem` it shows the `segment`s of `stage-actions` with their icons only (the `label`s stay for assistive technology), from a width of `44rem` on it lays out `visualization-controls` in one row |
| `stage-views` | `div` | `stage` | The views of the current song on top of each other in a box of a stable size (16:9): one `stage-view` each for the cover, the video (songs with a video only), the visualization and the lyrics (songs with lyrics only) |
| `stage-view` | `div` (`data-kind`) | `stage-views` | One view of the stage: `cover` (the `cover`), `video` (the `video-stage`), `visualization` (the `visualization`) or `lyrics` (the `lyrics`). Only the shown view is visible (`data-state="active"`); the base fades between them with an opacity transition of `--wmp-stage-transition-duration`. The video view stays in the page while it is not shown, its video waits paused; the lyrics view stays while the song has lyrics and does no work while it is hidden |
| `stage-notice` | `p` (`role="status"`) | `stage` | Hint that the video of the song cannot be played while the song goes on (only while the video view is chosen and the video failed), or with `data-kind="cast"` the casting device that plays ("Playing on ...") |
| `stage-info` | `div` | `stage` | Title, artist and album of the current song |
| `stage-title` | `p` | `stage-info` | Title of the current song |
| `stage-subtitle` | `p` | `stage-info` | Artist and album of the current song |
| `stage-actions` | `div` | `stage` | Chooses the view of the stage with a `segmented-control` (`data-item="stage-view"`) of the `segment`s `cover`, `video`, `visualization` and `lyrics` (with the icons `view-cover`, `view-video`, `view-visualization` and `view-lyrics`); while a song is current. The choice is kept per profile; switching never interrupts the music. `cover` is checked for songs without a video while the video is chosen and for songs without lyrics while the lyrics are chosen, `video` while the video still loads; `video` is disabled for songs without a video, `lyrics` for songs without lyrics |
| `lyrics` | `div` (`data-words`) | `stage-view`, `queue-pane` | The lyrics of the current song with `lyrics-lines`. Its state tells whether they follow the music (`synchronized`) or not (`static`); `data-words="true"` marks synchronized lyrics with word timestamps. It carries `--wmp-item-count` (the number of lines) and, while synchronized, `--wmp-active-index` (the current line, `-1` before the first). Synchronized lyrics scroll the current line into the middle (smoothly, with `prefers-reduced-motion` at once); static lyrics scroll calmly from the top to the bottom over the song while it plays, not with `prefers-reduced-motion`. After the listener scrolls, the view waits a few seconds before it follows again |
| `lyrics-lines` | `ol` (`aria-label`) | `lyrics` | The scrolling list of the lines, one `lyrics-line` each; the base fades it out at the top and bottom edges |
| `lyrics-line` | `li` (`data-index`, `data-empty`, `aria-current`) | `lyrics-lines` | A line of the lyrics: in synchronized lyrics a `lyrics-line-button`, in static lyrics a `label`. `data-index` is its index, `data-empty="true"` marks a part without singing (no text). In synchronized lyrics the state tells whether it was sung (`past`), is sung (`active`, also `aria-current`) or comes later (`upcoming`); the active line carries `--wmp-line-progress`. The base shows the active line larger and bright, the others dimmed |
| `lyrics-line-button` | `button` | `lyrics-line` | Seeks to the start of the line on click or tap; holds the line as `label`, or one `lyrics-word` per word for lines with word timestamps. An empty line has an `aria-label` |
| `lyrics-word` | `span` | `lyrics-line-button` | A word or syllable of a line with word timestamps, with its spaces. Its state is `past`, `active` or `upcoming`; within lines that are not active it has the state of its line. The active word carries `--wmp-word-progress`, with which the base fills it from the start to the end while it is sung |
| `visualization` | `div` (`data-module`, `data-quality`, `data-intensity`) | `stage-view` | The visualization of the music, drawn by a module (`data-module`: `milkdrop` with Milkdrop presets, `aforce`, the own visualization A-Force with its modes as presets and color schemes, or `blackcap`, the own visualization BlackCap with its modes as presets and color schemes) in sync with what is heard, at a quality level (`data-quality` while it draws or pauses) and an intensity (`data-intensity`); a flash guard keeps every module below three flashes a second: `visualization-canvas` and `visualization-message` until it draws; nothing lies on the picture, its controls are `visualization-controls` below the `stage-views`. Its state (see [States](#states)) tells whether it loads, waits for the music, draws or pauses; nothing is drawn while the view is hidden, the page is in the background or the stage is out of the viewport. Shown in full screen as a whole, without controls (`fullscreen`; with `btn-fullscreen`, a double click, the key F or the context menu of the stage, only while the visualization view is shown; Escape, a double click or F leave it) |
| `visualization-canvas` | `canvas` (`aria-hidden`) | `visualization` | The drawing of the module; the base lays it over the whole `visualization` and hides it until the module draws. A new canvas element may replace it while the visualization runs |
| `visualization-message` | `p` (`role="status"`) | `visualization` | Hint while the visualization does not draw, with an `icon` (`view-visualization`) and a `label`: while it loads, waits for the music (the player has no output before its first song), cannot be shown by the browser or failed; the state of `visualization` tells which |
| `visualization-controls` | `div` (`role="group"`) | `stage` | Controls of the visualization, a toolbar right below the `stage-views` while the visualization view is shown, so they never cover the picture: `btn-preset-previous`, `btn-preset-picker`, `btn-preset-next`, `btn-auto-change`, `btn-fullscreen`, `btn-color-scheme-picker` (only for modules with color schemes), the `segmented-control` `visualization-module` (the `segment`s `milkdrop`, `aforce` and `blackcap`; the choice is kept per profile and starts the visualization again with the other module); the `context-menu`s of the presets (`data-kind="visualization-presets"`) and of the color schemes (`data-kind="visualization-color-schemes"`) follow it in the `stage`, outside the toolbar, so they inherit nothing from it. States `loading` until the presets of the module are loaded, then `ready`; additionally `has-color-schemes` while the module offers color schemes. The base lays it out as a grid with named areas (`previous`, `picker`, `next`, `auto-change`, `fullscreen`, `scheme`, `module`) as wide as the `stage-views`: on a stage narrower than `44rem` the buttons share the first row (the picker takes the free width) and the module choice is centered in a second row, with `has-color-schemes` `btn-color-scheme-picker` (at most `12rem` wide) centered in a third row; from `44rem` on everything is one row, the picker at most `18rem` wide, then `btn-color-scheme-picker`, the module choice and `btn-auto-change` and `btn-fullscreen`. The buttons keep their places whatever the preset is called. The base pulls it closer to the `stage-views` than the other parts of the `stage` (a negative top margin) |
| `btn-preset-picker` | `button` (`aria-haspopup="menu"`, `aria-expanded`) | `visualization-controls` | Shows the name of the current preset (`label`, cut off with an ellipsis) and the `icon` `dropdown`, and opens the presets of the module as a `context-menu` (`data-kind="visualization-presets"`, one `menuitemradio` `menu-item` per preset, the current one checked and focused): on the desktop always above the button (`data-placement="above"`), on phones as a sheet at the bottom of the screen (`data-placement="sheet"`). If the room above the button is too small, the page first scrolls the button down. States `open` and `closed`; disabled until the presets are loaded |
| `btn-preset-previous` | `button` | `visualization-controls` | Shows the preset before the current one in the list (icon `preset-previous`); disabled until the presets are loaded. A chosen preset is kept per profile |
| `btn-preset-next` | `button` | `visualization-controls` | Shows the preset after the current one in the list (icon `preset-next`); disabled until the presets are loaded |
| `btn-color-scheme-picker` | `button` (`aria-haspopup="menu"`, `aria-expanded`) | `visualization-controls` | Only for modules with color schemes: shows the `icon` `color-scheme`, the name of the current color scheme (`label`, cut off with an ellipsis; on a stage narrower than `44rem` visually hidden, it stays the accessible name) and the `icon` `dropdown`, and opens the color schemes of the module as a `context-menu` (`data-kind="visualization-color-schemes"`, one `menuitemradio` `menu-item` per color scheme, the current one checked and focused), placed like the presets of `btn-preset-picker`. The color scheme is chosen apart from the preset, kept per profile and never changed by the automatic change. States `open` and `closed` |
| `btn-auto-change` | `button` (`aria-pressed`) | `visualization-controls` | Turns the automatic change of the preset on and off (icon `auto-change`, states `on` and `off`); the interval is set on the settings page, both are kept per profile. The automatic change picks a random preset and waits while nothing is drawn |
| `video-stage` | `div` (`data-orientation`) | `stage-view` | The video of the current song with `video-backdrop`, `video` and `btn-fullscreen`; shown in full screen as a whole (a double click switches as well, only while the video view is shown). When it closes, the song simply goes on. `data-orientation` and `--wmp-video-aspect-ratio` describe the picture of the video, so the theme can design the free areas beside a portrait video or above and below a wide one |
| `video-backdrop` | `span` (`aria-hidden`) | `video-stage` | Decorative layer behind the `video` that fills the whole `video-stage`; it shows in the free areas around the picture. Without a theme rule it is transparent and the background of the stage (`--wmp-stage-view-background`) shows |
| `video-ambient` | `div` (`aria-hidden`) | `video-stage` | The ambient light between `video-backdrop` and `video`: soft light in the colors of the current picture that fills the whole `video-stage` and so shows in the free areas around the picture, also in full screen. Its state (see [States](#states)) tells whether it is off, waits for a picture, draws, pauses or shows one static color (`--wmp-video-ambient-color`) for `prefers-reduced-motion: reduce`. The profile switches it on and off on the settings page (on by default). The base sets its opacity to `--wmp-video-ambient-intensity` and hides it while it is `off` or `waiting` |
| `video-ambient-canvas` | `canvas` | `video-ambient` | One of two canvases with a tiny picture of the video (at most 32 pixels on the long side, in its aspect ratio), drawn by turns about four times per second while the video plays and is visible; the newest one is `current`, the one before it `previous`. The base stretches both over the `video-ambient`, enlarges them by `--wmp-video-ambient-scale`, blurs them by `--wmp-video-ambient-blur`, saturates them by `--wmp-video-ambient-saturation` and fades the `current` one in above the `previous` one in `--wmp-video-ambient-transition-duration`, so the colors flow smoothly; hidden while the `video-ambient` is `static` |
| `video` | `video` (`aria-label`) | `video-stage` | The video element, always muted and without native controls: the sound comes from the player, the video follows its position plus the offset of the song; the transport bar controls both. The base lays it over the whole `video-stage` with a transparent background and shows the picture whole, undistorted and centered (`object-fit: contain`), also in full screen and for portrait videos |
| `btn-fullscreen` | `button` | `video-stage`, `visualization-controls` | Shows the `video-stage` or the `visualization` in full screen (icon `fullscreen`) or leaves it (icon `exit-fullscreen`); disabled where the browser has no full screen and while its view is not shown. On the iPhone the video element itself goes full screen with the controls of the system; the visualization has no full screen there. In `visualization-controls` it takes the grid area `fullscreen` |
| `cover` | `span` | `stage-view`, `list-group-info`, `now-playing-mini`, `queue-item-info`, `queue-pane-header`, `autoplay-item-info`, `tile`, `detail-header`, `review-item` | Cover: `cover-image`, or the placeholder icon (`data-state="placeholder"`); on tiles and detail pages of artists, genres and years the `icon` of their kind |
| `cover-image` | `img` (`alt=""`) | `cover` | Picture of the cover, loaded from the server |
| `cover-preview` | `figure` (`aria-busy` while it loads) | `form` | Preview of a cover rule on the page of an album or song (admins): the `cover` that the chosen rule would show and a `label` as caption, before the rule is saved |
| `list` | `div` (`role="grid"`) | `view` | Table of tracks grouped by album |
| `list-header` | `div` (`role="row"`) | `list` | Column headers |
| `list-column` | `span` (`role="columnheader"`) | `list-header` | Column header with `label` |
| `list-group` | `div` (`role="rowgroup"`) | `list` | Group of rows, for example an album |
| `list-group-header` | `div` (`role="row"`) | `list-group` | Heading row of the group; missing in the one group of a plain list (state `ungrouped` of `list`) |
| `list-group-title` | `span` (`role="rowheader"`) | `list-group-header` | Title of the group |
| `list-group-info` | `div` (`role="row"`) | `list-group` | Cover and details of the group |
| `list-group-meta` | `div` (`role="gridcell"`) | `list-group-info` | Details of the group |
| `meta-item` | `span` | `list-group-meta`, `detail-info` | One detail with `label` and `meta-value`, see `data-field` in [States](#states) |
| `meta-value` | `span` | `meta-item` | Value of a detail, often a `link` |
| `list-group-rows` | `div` (`role="presentation"`) | `list-group` | Rows of the group |
| `list-row` | `div` (`role="row"`, `aria-selected`) | `list-group-rows` | Row of a track |
| `list-cell` | `span` (`role="gridcell"`) | `list-row` | Cell of a row; in the column `title` the video `badge` of a song with a video (first) and the title as `label` |
| `badge` | `span` | `list-cell`, `user-meta`, `scan-status`, `run-item`, `run-meta`, `job-meta`, `file-meta`, `transcode-meta`, `backup-meta`, `scrobble-meta`, `theme-meta`, `stat-value`, `btn-review-inbox`, `review-meta`, `review-tags`, `library-issue-meta`, `segment` | Small hint with `icon` and `label`, for example that a video exists; in `user-meta` the role, in `scan-status`, `run-item`, `run-meta` and `job-meta` a state, in `file-meta` the kind and use of a file, in `transcode-meta` the media of a transcode, in `stat-value` the state of the hardware acceleration, in `review-meta` and `review-tags` the review and the missing tags, in `theme-meta` that an uploaded theme is outdated or blocked, in `btn-review-inbox` the number of unreviewed songs, in `library-issue-meta` the kind of a problem of the library check, in a `segment` of `issue-type` the number of its problems, each with a visible `label` |
| `tile-list` | `ul` | `view`, `section` | Albums, artists, genres or years as tiles |
| `tile-item` | `li` | `tile-list` | One tile |
| `tile` | `a` | `tile-item` | Link to the page of the entry with `cover`, `tile-title` and optionally `tile-subtitle`, see `data-kind` in [States](#states) |
| `tile-title` | `span` | `tile` | Name of the entry |
| `tile-subtitle` | `span` | `tile` | Detail of the entry, for example the album artist or the number of songs |
| `tile-badge` | `span` | `tile` (after the cover) | Symbol of the kind of the entry with an `icon`, on tiles of smart playlists the icon `smart-playlist`; the base lays it on the top start corner of the cover (the first child of `tile` and the badge share the first cell of its grid) |
| `detail-header` | `div` | `view` | Head of a detail page (album, artist, genre, year, song, playlist) with a `cover` (or `playlist-cover`) and `detail-info` |
| `detail-info` | `div` | `detail-header` | `detail-title`, the details as `meta-item` and optionally `detail-actions` |
| `detail-title` | `h2` | `detail-info` | Name of the album, artist, genre, year or song |
| `detail-actions` | `div` | `detail-info` | Actions of a detail page as `button`s, for example "Edit metadata" of a song for admins, see `data-item` in [States](#states) |
| `btn-detail-back` | `button` (`data-variant`) | `view` (directly before `detail-header`), `dialog-header` (before `dialog-title`, metadata editor) | Back to the previous page at its position, with the icon `detail-back` and a `label`; on a page that was opened directly it leads to the overview the page belongs to (for example the songs from the page of a song), in the metadata editor it closes the editor. On detail pages a `button` of `data-variant="default"`, in the metadata editor `data-variant="icon"` |
| `load-more` | `button` | `view`, `section` | Loads the next page of a list; also loads by itself when it scrolls into view |
| `file-list` | `ul` | `section` | Files of a song on its page, for admins |
| `file-item` | `li` | `file-list` | One file with `file-path` and `file-meta` |
| `file-path` | `span` | `file-item`, `file-selection-info`, `file-choice`, `review-info` | Path of the file relative to the media folder; in `file-selection-info` the folder and base name of the files of the song, in a `file-choice` the path of the file (or of a selected file that is gone) or, for the automatic choice, its name; in `review-info` the folder and base name of the files of the song |
| `file-meta` | `span` | `file-item`, `file-selection-info`, `file-choice` | Kind, use and problem of the file as `badge` with a visible `label`, and its technical details (format, bitrate, resolution, size, ...) as `label`s; in `file-selection-info` the categories with a selection as `badge`s and the number of audio files, videos and covers as `label`s (`data-field` `audio`, `video`, `cover`); in a `file-choice` the kind, the `badge`s `embedded`, `active`, `best`, `lossless` or `missing` and the technical details, for the automatic choice the file it picks as `label` |
| `playlist-cover` | `span` (`aria-hidden`, `data-count`) | `tile`, `detail-header`, `playlist-choice` | Cover of a playlist without a picture of its own: a 2x2 grid of four `playlist-cover-tile`s with the covers of up to twelve of its songs (of different albums); `data-count` and `--wmp-item-count` hold their number. The first four fill the tiles (with fewer, they repeat on the diagonal: AB/BA, AB/CA or all four the same); with more than four the tiles change one at a time (state `rotating`), calmly and never two at once on the page, while the cover is in view. The base lays out the grid with the gap `--wmp-playlist-cover-gap`. A playlist with its own picture shows a `cover` instead, one without any song cover a `cover` with the icon `playlist` |
| `playlist-cover-tile` | `span` (`data-index`) | `playlist-cover` | One tile of the grid with its `playlist-cover-slide`s, in reading order: `data-index` `0` top start, `1` top end, `2` bottom start, `3` bottom end. State `changing` while a new cover enters it. The base clips the tile, so a new cover appears from its edge |
| `playlist-cover-slide` | `span` (`data-from`) | `playlist-cover-tile` | One song cover (a `cover`) of a tile. The shown one is `active`; a new one is added above it in the state `loading` (invisible, while its picture loads), then becomes `entering` and slides in over the shown one from the outer edge of its tile named in `data-from` (`start` or `top` for the top start tile, `end` or `top` for the top end tile, `start` or `bottom` for the bottom start tile, `end` or `bottom` for the bottom end tile, chosen at random; `start` and `end` follow the writing direction). Once it is in place it becomes `active` and the slide below it goes. The base animates `entering` with `translate` (keyframes `wmp-playlist-cover-from-left`, `-right`, `-top` and `-bottom`) in `--wmp-playlist-cover-slide-duration` with `--wmp-playlist-cover-slide-easing`; with `prefers-reduced-motion` it fades in (`wmp-playlist-cover-fade-in`, `--wmp-playlist-cover-fade-duration`). A theme can replace the `animation` of `entering`; the change ends with its `animationend`, without an animation after a few seconds |
| `playlist-choices` | `ul` (`aria-label`) | `dialog-body` | Playlists of the profile in the dialog "Add to playlist" |
| `playlist-choice` | `button` | `playlist-choices` (in a `li`) | One playlist with its cover, its name as `label` and `playlist-choice-count`; adds the songs at its end |
| `playlist-choice-count` | `span` | `playlist-choice` | Number of songs of the playlist |
| `drag-handle` | `span` | `list-cell` with `data-column="handle"` | Handle that drags an entry of a playlist to another place (mouse, pen and touch), with `icon` and a `label` for screen readers; the menu of the entry moves it with the keyboard |
| `smart-templates` | `div` | `section` `templates` of the editor of a smart playlist | The built-in templates as `button`s (`data-item` `four-stars-and-more`, `never-played`, `recently-added`, `most-played`, `favorites`); the one applied last has the state `applied`. Only when a smart playlist is created |
| `smart-rules` | `ol` | `section` `rules` of the editor of a smart playlist | The rules as `smart-rule`s; the base makes it a size container (`wavemp-smart-rules`) and puts the values of a rule below field and condition when it is narrower than 36rem |
| `smart-rule` | `li` (`aria-label`, `data-kind`, `data-values`) | `smart-rules` | One rule: the `field`s `rule-field` and `rule-operator`, `smart-rule-values` and the `button` `remove-rule`; `data-kind` is the kind of the field, `data-values` the values it needs, see [Other attributes](#other-attributes) |
| `smart-rule-values` | `div` (`data-values`) | `smart-rule` | The values of a rule as `field`s: `rule-text`, `rule-number` and, for a range, `rule-number-to`; empty for yes/no fields |
| `smart-summary` | `section` (`aria-label`) | `view` (`data-view="smart-playlist"`, after `detail-header`) | The rules of a smart playlist in words, with `smart-summary-intro`, `smart-summary-rules` and `smart-summary-order` |
| `smart-summary-intro` | `p` | `smart-summary` | Whether the songs meet all or any of the rules, or that all songs belong to it without rules |
| `smart-summary-rules` | `ul` | `smart-summary` | The rules as `smart-summary-rule`s |
| `smart-summary-rule` | `li` | `smart-summary-rules` | One rule in words, for example `Genre is "Rock"` |
| `smart-summary-order` | `p` | `smart-summary` | Sort order and limit of the songs in words |
| `btn-favorite` | `button` (`aria-pressed`) | `detail-actions`, `list-cell` with `data-column="favorite"`, `now-playing-actions` | Heart that marks a song, album or artist as favorite of the profile, with `icon` and `label`; in lists it is no tab stop (the menu of the song offers the same) |
| `rating` | `span` (`role="group"`) | `meta-value` of the `meta-item` `rating`, `list-cell` with `data-column="rating"`, `now-playing-actions` | Rating of a song with five `rating-star`s; `data-value` holds the stars (0 to 5), `--wmp-item-count` the number of stars and `--wmp-active-index` the index of the star of the rating (`-1` without one) |
| `rating-star` | `button` (`aria-pressed` for the star of the rating) | `rating` | One star with `icon` and `label`; a click rates the song with it, a click on the star of the rating removes it; in lists no tab stop |
| `scope-switch` | `div` | `view` (library) | Switch between the whole library and the "my music" of the profile: a `segmented-control` (`data-item="scope"`) with the `segment`s `all-music` and `my-music` |
| `segmented-control` | `div` (`role="radiogroup"`, `aria-label` or `aria-labelledby`) | `scope-switch`, `stage-actions`, `field`, any | Mutually exclusive options as one connected group of buttons: an `indicator` and one `segment` per option. One tab stop; the arrow keys, Home and End check another option. Its `data-item` names the choice (see [Other attributes](#other-attributes)); it carries the runtime variables of an active element (`--wmp-item-count`, `--wmp-active-index`, `--wmp-indicator-x`, `--wmp-indicator-y`, `--wmp-indicator-width` and `--wmp-indicator-height`), so a theme can move the `indicator` to the checked segment with CSS only, for example as a highlight that slides between the segments |
| `segment` | `button` (`role="radio"`, `aria-checked`, `data-variant="default"`) | `segmented-control` | One option with an optional `icon`, its `label` and an optional `badge` after it (for example the number of problems of a filter); `data-item` names the option. The base joins the segments to one bar and gives the checked one the active image of buttons (`--wmp-image-default-button-active`). In a `field` the segments shrink where the options do not fit side by side (for example on a phone) and their `label`s wrap to more lines, so the control never runs out of the field; a theme that lays something over the foot or top of a segment (such as a line on the `indicator`) keeps room for the lines with `padding-block` |
| `shelf` | `section` | `view` (home) | One row of the home page with `shelf-header` and a `tile-list` of its first entries |
| `shelf-header` | `div` | `shelf` | `shelf-title` and `shelf-actions` |
| `shelf-title` | `h2` | `shelf-header` | Name of the row |
| `shelf-actions` | `div` | `shelf-header` | Actions of the row (`button` `play`) and a `link` to the whole list |
| `queue` | `section` (`aria-labelledby`) | `view`, `queue-pane` | Queue on the Now Playing page: a `section-title`, the `button` `clear-queue`, the `queue-list` and, while autoplay goes on after the list, `autoplay`; missing while the queue is empty. On the desktop the base shows it beside the stage and lets it scroll; there it keeps the current entry in view. In the docked `queue-pane` the same, without `autoplay` |
| `queue-list` | `ol` (`start`) | `queue` | Entries of the queue in playing order: the last three played songs, the current one and all that follow; `start` is the position of the first shown entry in the whole queue |
| `queue-item` | `li` (`aria-current`) | `queue-list` | One entry with `queue-item-info`, `queue-item-duration` and `queue-item-actions` |
| `queue-item-info` | `button` | `queue-item` | `cover` of the song, title and artist of the entry; plays it |
| `queue-item-title` | `span` | `queue-item-info` | Title of the song |
| `queue-item-artist` | `span` | `queue-item-info` | Artist of the song |
| `queue-item-duration` | `span` | `queue-item` | Duration of the song |
| `queue-item-actions` | `div` (`role="group"`) | `queue-item` | Icon `button`s `move-up`, `move-down` and `remove` |
| `autoplay` | `section` (`aria-labelledby`, `aria-describedby`, `aria-busy` while songs are chosen) | `queue` | Autoplay after the `queue-list`: the songs that play when the list has ended. A `section-title` (`h3`), the `button` `turn-off-autoplay` (with the `icon` `autoplay`), `autoplay-hint` and the `autoplay-list`, or `autoplay-status` while it has no songs; only there while autoplay is on for the profile, repeat is off and the queue has songs of a list |
| `autoplay-hint` | `p` | `autoplay` | What autoplay does, in one sentence |
| `autoplay-list` | `ol` | `autoplay` | The waiting songs in the order they play |
| `autoplay-item` | `li` | `autoplay-list` | One song with `autoplay-item-info`, `autoplay-item-duration` and `autoplay-item-actions`; it has the context menu of a song |
| `autoplay-item-info` | `button` | `autoplay-item` | `cover` of the song, title and artist; plays the song at once and skips the songs before it |
| `autoplay-item-title` | `span` | `autoplay-item-info` | Title of the song |
| `autoplay-item-artist` | `span` | `autoplay-item-info` | Artist of the song |
| `autoplay-item-duration` | `span` | `autoplay-item` | Duration of the song |
| `autoplay-item-actions` | `div` (`role="group"`) | `autoplay-item` | The icon `button` `remove` and `btn-item-menu` |
| `autoplay-status` | `p` (`role="status"`) | `autoplay` | Instead of the `autoplay-list`: songs are being chosen, or no matching song is left |
| `queue-pane` | `aside` (`aria-label`) | `app` | The docked play queue beside the content, where the theme sets `--wmp-layout-queue-pane: docked` ([Layout options](#layout-options)); desktop only and never on Now Playing. In this order: the `segmented-control` `queue-pane-view` (`segment`s `queue` and `lyrics`, `queue` at first), `queue-pane-header`, then for `queue` the `queue` (an `empty-state` while the queue is empty) and for `lyrics` the `lyrics` of the current song (an `empty-state` for songs without lyrics), and `queue-pane-summary`. The `queue` and the `lyrics` stay in the pane while the other one is chosen, hidden with `hidden`; the lyrics only follow the music while they are shown |
| `queue-pane-header` | `div` | `queue-pane` | The current song: its `cover`, `queue-pane-title` and `queue-pane-subtitle`; while nothing is current (state `empty`) only a `queue-pane-title` saying so |
| `queue-pane-title` | `p` | `queue-pane-header` | Title of the current song |
| `queue-pane-subtitle` | `p` | `queue-pane-header` | Artist of the current song |
| `queue-pane-summary` | `footer` | `queue-pane` | The number of songs in the queue and their total duration as `label`s (`data-field` `count` and `duration`); while the duration of a song is unknown, the duration is the sum of the known ones and says "at least" |
| `transport` | `section` | `app` | Transport bar; mini player and full-screen player on mobile |
| `seek` | `div` | `transport` | Progress bar with a `slider` |
| `swipe-grip` | `span` (`aria-hidden`) | `transport` | Grip at the top of the full-screen player (mobile) that shows it can be swiped down; the base hides it elsewhere and draws it as a short rounded bar in `currentColor` |
| `now-playing-mini` | `button` | `transport` | Cover and title of the current track; opens Now Playing (desktop) or the full-screen player (mobile); in the full-screen player it opens and closes `player-view-switch` (`aria-expanded`, `aria-controls`) |
| `player-media` | `span` | `now-playing-mini` | The `cover` of the current track and, in the full-screen player, the chosen view above it: `player-video` or `player-visualization`. On the bar the base gives it `display: contents`, so the `cover` stays a child of the layout of `now-playing-mini`; in the full-screen player (mobile) it takes the room the other rows leave as a size container, the `cover` is the largest square that fits there, and the video and the visualization lie on it in the same square |
| `player-video` | `video` (muted, `aria-hidden`) | `player-media` | The music video of the current song in the full-screen player, following the music, while the profile chose the video view, the song has one and no casting device plays; the video moves here from the stage of Now Playing while the full-screen player is open. The base keeps it invisible until it is ready (state `video` of `player-media`), shows it whole (`object-fit: contain`) on `--wmp-stage-view-background` and fades it in with `--wmp-stage-transition-duration` |
| `player-visualization` | `span` (`aria-hidden`, `data-module`, `data-intensity`) | `player-media` | The visualization of the profile (same module, preset, color scheme, quality and intensity as on Now Playing) as a picture only, with a `visualization-canvas`; in the full-screen player while the profile chose the visualization and no casting device plays. Its states are those of `visualization`; meanwhile the `visualization` of the stage of Now Playing below the player does not draw |
| `player-view-switch` | `div` (`hidden` while closed) | `transport` | The view switch of the full-screen player (mobile): a `segmented-control` (`data-item="player-view"`) with the `segment`s `cover`, `video` and `visualization` (icons `view-cover`, `view-video` and `view-visualization`), the same choice as `stage-actions` of Now Playing. Tapping `now-playing-mini` opens and closes it; the base fades it in. `video` is disabled for songs without a video, `visualization` while casting; `cover` is checked while the lyrics are chosen |
| `now-playing-meta` | `span` | `now-playing-mini` | Title and artist |
| `now-playing-title` | `span` | `now-playing-meta` | Title of the current track, in a `marquee` |
| `now-playing-artist` | `span` | `now-playing-meta` | Artist of the current track, in a `marquee` |
| `marquee` | `span` | `now-playing-title`, `now-playing-artist`, `compact-player-title`, `compact-player-artist` | The text of its place, which never overflows it. A text too long for its place rests cut off for 3 seconds with its end fading out (`--wmp-marquee-fade-size`), scrolls slowly (30 pixels per second) by `--wmp-marquee-distance` in `--wmp-marquee-duration`, rests 2 seconds at its end with its start fading out and starts again from the beginning with a short fade; with `prefers-reduced-motion` it does not move and ends with an ellipsis. The phases are its states; the base moves it with `translate` |
| `cast-status` | `span` | `now-playing-meta` | The casting device that plays ("Playing on ..."), or the one that connects; only while casting |
| `now-playing-actions` | `div` (`role="group"`) | `transport` | `btn-favorite` and `rating` of the current song; empty (state `empty`) while nothing plays; on mobile only in the full-screen player |
| `transport-message` | `p` (`role="status"`) | `transport` | Notice of the player, for example that a song was skipped; empty while there is none. The base shows it above the transport bar, outside the grid |
| `resume-notice` | `div` (`role="status"`) | `transport` | "Continue listening from" a device: the player took over the saved state of another device of the profile and waits paused at its position. An `icon` (`resume-device`), a `label` with the name of the device in `resume-device`, the `button`s `btn-resume` and `btn-dismiss-resume`; only there until it is dismissed, the player plays or another song becomes current. The base shows it above the transport bar, outside the grid, on mobile across the width above the mini player and not in the full-screen player |
| `resume-device` | `span` | `label` of `resume-notice` | Name of the other device, for example "Firefox · Windows" |
| `btn-resume` | `button` (`data-variant="primary"`) | `resume-notice` | Plays the state that was taken over from the saved position, with the `icon` `play` |
| `btn-dismiss-resume` | `button` (`data-variant="icon"`) | `resume-notice` | Hides `resume-notice`, with the `icon` `close` |
| `transport-time` | `span` (`aria-hidden="true"`) | `transport` | Elapsed time and length of the current song as text, for a display in the transport bar (the `seek` slider tells them to assistive technology). Hidden by the base and on mobile; a theme shows it with `display` |
| `time-elapsed` | `span` | `transport-time`, `compact-player-time` | Elapsed time, for example `1:23` |
| `time-duration` | `span` | `transport-time`, `compact-player-time` | Length of the song, for example `3:45` |
| `transport-controls` | `div` (`role="group"`) | `transport` | Playback controls |
| `btn-shuffle` | `button` (`aria-pressed`) | `transport-controls` | Shuffle |
| `btn-repeat` | `button` (`aria-pressed`) | `transport-controls` | Repeat off, all or one |
| `btn-crossfade` | `button` (`aria-pressed`) | `transport-controls` | Turns the crossfade of the profile on and off (the length stays as set on the settings page); pressed while it is on; disabled while casting. Between `btn-repeat` and `btn-stop`; on mobile in the full-screen player |
| `btn-autoplay` | `button` (`aria-pressed`) | `transport-controls` | Turns autoplay of the profile on and off: similar songs play when the list has ended and repeat is off; pressed while it is on. Between `btn-crossfade` and `btn-stop`; on mobile in the full-screen player |
| `btn-stop` | `button` | `transport-controls` | Stop |
| `btn-prev` | `button` | `transport-controls`, `compact-player-controls` | Previous track |
| `btn-play` | `button` | `transport-controls`, `compact-player-controls` | Play and pause |
| `btn-next` | `button` | `transport-controls`, `compact-player-controls` | Next track |
| `btn-mute` | `button` (`aria-pressed`) | `transport-controls`, `compact-player-volume` | Mute |
| `volume` | `div` | `transport-controls`, `compact-player-volume` | Volume with a `slider` |
| `mode-switch` | `div` (`role="group"`) | `transport` | Buttons for player modes, the equalizer, the sleep timer and casting |
| `btn-compact` | `button` (`aria-pressed`) | `mode-switch` | Opens the mini player (`compact-player`) and closes it again; pressed while it is open. Hidden on mobile |
| `btn-equalizer` | `button` | `mode-switch` | Opens the equalizer (`data-view="equalizer"`); disabled while casting |
| `sleep-timer` | `div` | `mode-switch` | Sleep timer: `btn-sleep-timer`, `sleep-timer-remaining` and its menu (`context-menu`) |
| `btn-sleep-timer` | `button` (`aria-pressed`, `aria-haspopup="menu"`, `aria-expanded`) | `sleep-timer` | Opens the menu of the sleep timer; pressed while a timer runs |
| `sleep-timer-remaining` | `span` (`aria-hidden`) | `sleep-timer` | Remaining time of a timer with a duration, or the end the timer waits for; only while a timer runs |
| `cast` | `div` | `mode-switch` | Casting to Chromecast or AirPlay: `btn-cast` and, without HTTPS, `cast-hint`; missing in browsers that cannot cast (e.g. Firefox) |
| `btn-cast` | `button` | `cast` | Opens the device selection of the browser or the operating system, which also ends casting. Without HTTPS (Chromecast) it carries `aria-disabled="true"`, `aria-expanded` and `aria-describedby` and shows or hides `cast-hint` instead |
| `cast-hint` | `p` (`role="note"`) | `cast` | Why Chromecast is unavailable (it needs HTTPS); only without HTTPS. The base shows it above the button while it is open |
| `btn-collapse` | `button` | `transport` | Closes the full-screen player (mobile) |
| `compact-player` | `section` (`aria-label`) | `body` of the floating window, or beside `app` | The mini player (compact mode), which controls the running playback; the music always plays in the window of the app. Where the browser has the Document Picture-in-Picture API (Chrome, Edge), it fills a floating window that stays above the other windows (`data-kind="window"`); this window gets the styles of the app and the attributes of the root element (theme, variant, language, cover color), so all rules of the theme apply there, and it is a viewport of its own. Other browsers show it in the window of the app (`data-kind="inline"`) while `app` is `hidden`; Escape and `btn-compact-close` return to the app. The base makes it a size container (`wavemp-compact-player`) with the colors `--wmp-compact-player-color` and `--wmp-compact-player-background`; it fills the floating window, and in the window of the app it is a card of `--wmp-compact-player-width` by `--wmp-compact-player-height` in the middle of the screen |
| `compact-player-panel` | `div` | `compact-player` | Grid of the mini player with the areas `media`, `info`, `seek`, `time`, `controls`, `volume` and `close`; the base puts `media` above the other areas and, in a container wider than 1.25 times its height or lower than `16rem`, beside them (at most 40 % of the width). Below a height of `12rem` the base hides `compact-player-time` and the `volume` slider |
| `compact-player-media` | `div` | `compact-player-panel` | The `cover` of the current song and, while the mini player shows its video, `compact-player-video` above it. The base makes it a size container; the `cover` takes its shorter side |
| `compact-player-video` | `video` (muted) | `compact-player-media` | The music video of the current song, following the music, while the song has one, the profile chose the video view of Now Playing and no casting device plays; the video moves here from the stage of Now Playing while the mini player is open. The base keeps it invisible until it is ready (state `video` of `compact-player-media`), with the background `--wmp-stage-view-background` |
| `compact-player-info` | `div` | `compact-player-panel` | `compact-player-title` and `compact-player-artist` |
| `compact-player-title` | `span` | `compact-player-info` | Title of the current song, or that nothing plays, in a `marquee` |
| `compact-player-artist` | `span` | `compact-player-info` | Artist of the current song, in a `marquee`; empty while nothing plays |
| `compact-player-seek` | `div` | `compact-player-panel` | Progress bar with a `slider` |
| `compact-player-time` | `span` (`aria-hidden="true"`) | `compact-player-panel` | `time-elapsed` and `time-duration` below the progress bar |
| `compact-player-controls` | `div` (`role="group"`) | `compact-player-panel` | `btn-prev`, `btn-play` and `btn-next`, the same parts as in `transport-controls`: rules of the theme for these parts without `transport` in the selector apply here as well |
| `compact-player-volume` | `div` (`role="group"`) | `compact-player-panel` | `btn-mute` and `volume` |
| `btn-compact-close` | `button` | `compact-player-panel` | Closes the mini player and returns to the app (icon slot `expand-player`) |
| `equalizer` | `section` | `view` | The equalizer of the profile: `equalizer-controls`, an `alert` if the browser cannot apply it, and `equalizer-bands` |
| `equalizer-controls` | `div` (`role="group"`) | `equalizer` | `button` `power` (`aria-pressed`), the `field` `equalizer-preset` with a `select`, `button` `reset` |
| `equalizer-bands` | `div` (`role="group"`) | `equalizer` | The preamp, `equalizer-scale` and one `equalizer-band` per frequency, side by side; the mobile base scrolls them horizontally |
| `equalizer-band` | `div` | `equalizer-bands` | One band (`data-kind`): `equalizer-value`, a vertical `slider` and a `label` with the frequency or "Preamp" |
| `equalizer-value` | `span` (`aria-hidden`) | `equalizer-band` | Gain of the band in dB |
| `equalizer-scale` | `div` (`aria-hidden`) | `equalizer-bands` | Three `label`s with the highest gain, 0 dB and the lowest gain, from top to bottom |
| `bottom-nav` | `nav` | `app` | Main navigation at the bottom (mobile): an `indicator` and one `bottom-nav-item` per entry; tells the active entry with the runtime variables of an active element, see [Active element of a group](#active-element-of-a-group) |
| `bottom-nav-item` | `a` (`aria-current`) | `bottom-nav` | Entry with `icon` and `label`; the mobile base lays out every entry the same way: the `icon` above the `label`, both centered and at the same height in all entries, the `label` in at most two centered lines. An entry whose label takes two lines makes all entries (and the `indicator` of the current one) taller instead of pushing its icon up. The top padding centers an entry with one line in `--wmp-bottom-nav-height`; the padding (by default `--wmp-space-s` on every side) keeps a highlight drawn inside the entry clear of icon and label. A shortened label (Now Playing shows "Playing") carries the full one as `title` and `aria-label` of the entry |
| `profile-select` | `main` | `body` (without `app`) | Profile selection, shown before a profile is selected; with `logo`, title, `profile-list` (or `pin-entry` while the PIN of a profile is entered) and, on the first start, a hint |
| `profile-select-title` | `h1` | `profile-select` | Title of the profile selection |
| `profile-select-hint` | `p` (`role="note"`) | `profile-select` | Hint on the first start, when only the built-in admin profile exists |
| `profile-list` | `ul` | `profile-select` | Profiles to select |
| `profile-item` | `li` | `profile-list` | One profile |
| `profile-tile` | `button` | `profile-item` | Selects the profile; `avatar` and `label` (name) |
| `avatar` | `span` (`aria-hidden`) | `profile-tile`, any | The picture of the profile (`avatar-image`), or its initials (`avatar-initials`) on the color of the profile (`--wmp-profile-color`) while it has no picture, while the picture loads and if it cannot be loaded |
| `avatar-initials` | `span` | `avatar` | Initials of the profile |
| `avatar-image` | `img` (`alt=""`) | `avatar` | Square picture of the profile (uploaded or built-in), loaded in the size the avatar is shown in; the base lets it cover the avatar |
| `profile-lock` | `span` (`role="img"`, `aria-label`) | `profile-tile` | Lock symbol of a profile with a PIN, with `icon` (slot `profile-lock`) |
| `pin-entry` | `form` (`aria-labelledby`) | `profile-select` | PIN entry for a profile with a PIN, shown instead of `profile-list`: `avatar`, `pin-entry-title`, an `alert` after a wrong PIN or during a lockout, `pin-pad` and `pin-entry-actions` |
| `pin-entry-title` | `h2` | `pin-entry` | Asks for the PIN of the profile |
| `pin-entry-actions` | `div` | `pin-entry` | The `button` `back` to the profiles |
| `pin-pad` | `div` | `pin-entry` | Digit pad: `pin-field`, `pin-dots` and `pin-keys`; digits typed on a keyboard also count |
| `pin-field` | `input` (`type="password"`, `inputmode="none"`) | `pin-pad` | The entered PIN; takes the focus and the keys of a keyboard. The base hides it visually and shows `pin-dots` instead |
| `pin-dots` | `span` (`aria-hidden`, `data-count`) | `pin-pad` | One `pin-dot` per entered digit |
| `pin-dot` | `span` | `pin-dots` | One entered digit |
| `pin-keys` | `div` (`role="group"`) | `pin-pad` | The twelve `pin-key`s in the order of a phone keypad: `1` to `9`, `delete`, `0`, `submit` |
| `pin-key` | `button` (`data-key`) | `pin-keys` | One key with a `label` (the digit, or a visually hidden text for `delete` and `submit`); `delete` and `submit` also carry an `icon` (slots `pin-delete` and `pin-submit`). `submit` is the submit button of `pin-entry` and disabled until 4 digits are entered, `delete` while nothing is entered |
| `profile-summary` | `div` | `view` | The current profile on the settings page: `avatar` and `label` with the name |
| `theme-picker` | `div` (`role="radiogroup"`, `aria-labelledby`) | `field` (`data-field="theme"`) | The themes on the settings page as tiles with a preview: an `indicator` and one `theme-option` per theme. One tab stop; the arrow keys, Home and End choose another theme, which shows at once. It carries the runtime variables of an active element |
| `theme-option` | `button` (`role="radio"`, `aria-checked`) | `theme-picker` | One theme: `theme-preview`, `label` (the name of the theme) and `theme-variants`; `data-item` is the id of the theme, `data-variants` its variants |
| `theme-preview` | `span` | `theme-option`, `theme-item` | Frame of the preview picture; `data-kind` is the variant it shows (the one the profile would see) |
| `theme-preview-image` | `img` (`alt=""`) | `theme-preview` | Preview picture: `/themes/<id>/preview-<variant>.webp` for shipped themes, the preview of the manifest for uploaded themes; missing while the picture cannot be loaded |
| `theme-variants` | `span` | `theme-option`, `theme-meta` | The variants of the theme as text, for example "Light and dark" |
| `avatar-editor` | `div` (`data-state`) | `dialog-body` of the `dialog` `avatar` | Changes the picture of a profile: the current `avatar`, a `segmented-control` `avatar-mode` (`segment`s `preset` and `upload`), an `alert` on errors, then `avatar-presets`, or `avatar-crop` with a `form-note` and the `button` `choose-file` |
| `avatar-presets` | `div` (`role="radiogroup"`, `aria-label`) | `avatar-editor` | Choice of a built-in picture: an `indicator` and one `avatar-preset` per picture; tells the chosen picture with the runtime variables of an active element, see [Active element of a group](#active-element-of-a-group) |
| `avatar-preset` | `button` (`role="radio"`, `aria-checked`, `data-item`) | `avatar-presets` | One built-in picture (`data-item` is its id, for example `bubble`) with `avatar-preset-image` and a visually hidden `label` |
| `avatar-preset-image` | `img` (`alt=""`) | `avatar-preset` | The built-in picture; missing while it loads |
| `avatar-crop` | `div` (`data-state`) | `avatar-editor` | Cropping of an own picture before the upload: `avatar-crop-frame`, a `form-note`, `avatar-crop-preview` and the `field` `zoom` with a `slider`. Carries `--wmp-crop-scale`, `--wmp-crop-x` and `--wmp-crop-y` |
| `avatar-crop-frame` | `div` (`role="group"`, `tabindex`) | `avatar-crop` | Square frame with the visible part of the picture; dragging it or the arrow keys move the picture |
| `avatar-crop-image` | `img` (`alt=""`) | `avatar-crop-frame`, `avatar-crop-preview` | The chosen picture, placed behind the frame with the runtime variables of `avatar-crop` |
| `avatar-crop-preview` | `div` (`role="img"`, `aria-label`) | `avatar-crop` | Small preview of the square that is uploaded |
| `view-actions` | `div` | `view` | Actions of a page, for example "Add profile" |
| `user-list` | `ul` | `view` | Profiles on the admin page |
| `user-item` | `li` | `user-list` | One profile with `avatar`, `user-info` and `user-actions` |
| `user-info` | `div` | `user-item` | Name and details of the profile |
| `user-name` | `span` | `user-info` | Name of the profile |
| `user-meta` | `span` | `user-info` | Role and built-in state as `badge` with a visible `label`, and the creation date as `label` |
| `user-actions` | `div` | `user-item` | Buttons to edit and delete the profile (`data-item` `edit`, `avatar`, `reset-pin` and `delete`) |
| `source-list` | `ul` | `section` | Media folders of the library on the library page of the administration |
| `source-item` | `li` | `source-list` | One media folder with `source-info` and `source-actions` |
| `source-info` | `div` | `source-item` | Name, path and details of the media folder |
| `source-name` | `span` | `source-info` | Name of the media folder |
| `source-path` | `span` | `source-info` | Path of the folder in the container |
| `source-meta` | `span` | `source-info` | State badges with a visible `label` (`active` or `inactive`, `unavailable`, `watched`, `media-path`) and the number of songs and files as `label` |
| `source-actions` | `div` | `source-item` | Buttons to switch the media folder on or off (`data-item="toggle"`, a `button` with a visible `label`), to edit it (`edit`) and to remove it (`delete`) |
| `source-filter` | `div` | `view` (library) | Filter of the songs, recently added songs, albums and artists by media folder, after `scope-switch`; a `field` with a `select` of all media folders. Only shown while the library has more than one media folder |
| `admin-nav` | `nav` | `view` | Navigation between the pages of the administration (library, files, review, jobs, cache, transcoding, backups, themes, profiles; the page of the files of a song belongs to files): an `indicator` and one `admin-nav-item` per page; tells the current page with the runtime variables of an active element, see [Active element of a group](#active-element-of-a-group) |
| `admin-nav-item` | `a` (`aria-current`) | `admin-nav` | Page of the administration with `icon` and `label` |
| `section` | `section` (`aria-labelledby`) | `view` | Part of a page with a `section-title` |
| `section-title` | `h2`; `h3` in `autoplay` | `section`, `queue`, `autoplay` | Title of the section, of the queue or of autoplay |
| `scan-status` | `div` | `section` | State of the library scan: `badge` with a visible `label`, while running the phase as `label`, a `progress` and its value as `label`, and the button that starts a scan |
| `progress` | `div` (`role="progressbar"`) | `scan-status`, any | Progress bar; the share done is `--wmp-progress-value` |
| `progress-track` | `div` | `progress` | Background of the bar |
| `progress-fill` | `div` | `progress-track` | Done part of the bar |
| `stat-list` | `dl` | `section`, `backup-status`, `library-check-status` | Statistics of a scan, the areas of the cache, the state of the backups, the versions on the settings page or what Subsonic apps need to sign in |
| `stat-item` | `div` | `stat-list` | One statistic: `label` (`dt`) and `stat-value` |
| `stat-value` | `dd` | `stat-item` | Number of the statistic, or the version on the settings page |
| `run-list` | `ul` | `section` | Earlier runs, for example the history of scans |
| `run-item` | `li` | `run-list` | One run with a status `badge` with a visible `label`, its time as `label` and `run-meta` |
| `run-meta` | `span`, or `div` above the statistics | `run-item`, `section` | Details of a run as `label`s: trigger, duration, changes, error; above the statistics also the status `badge` |
| `issue-list` | `ul` | `disclosure` | Files with problems |
| `issue-item` | `li` | `issue-list` | One file with `issue-path` and the problem as `label`; for a media folder the scan skipped `issue-source` without `issue-path` |
| `issue-path` | `span` | `issue-item` | Path of the file relative to its media folder |
| `issue-source` | `span` | `issue-item` | Name of the media folder, before `issue-path`: for a skipped media folder, and for every entry while the problems lie in several media folders |
| `disclosure` | `details` | `section`, any | Content that opens on demand |
| `disclosure-summary` | `summary` | `disclosure` | Always visible line of a `disclosure` with `label` and optionally `field-hint` |
| `job-list` | `ul` | `view` | Background jobs |
| `job-item` | `li` | `job-list` | One job with `job-info` and `job-actions` |
| `job-info` | `div` | `job-item` | Name, details and the error of the last run as `label` |
| `job-name` | `span` | `job-info` | Name of the job |
| `job-meta` | `span` | `job-info` | Status `badge` of the last run with a visible `label`, and last run, next run and schedule as `label`s |
| `job-actions` | `div` | `job-item` | Button that runs the job now |
| `cache-usage` | `div` | `section` | Used space of the cache: a `meter`, the used space and the limit as `label`, a `stat-list` with the areas and the number of files, a hint as `label` and the button that clears the cache |
| `lyrics-lookups` | `div` (`data-state`, `aria-busy`) | `section` `lyrics-lookups` | Stored results of the online lyrics lookup that found no lyrics, on the library page of the administration: while loading a `label`, then a `stat-list` (the `stat-item`s `not-found`, `instrumental` and `wait`, the wait before such songs are looked up again), a `form-note`, an `alert` after forgetting and `view-actions` with the `button` `forget-lyrics-lookups`. State `loading`, `has-results` or `empty` (nothing to forget, the button is disabled) |
| `transcoding-status` | `div` | `section` | Status of the transcoding: an `alert` while nothing can be transcoded, a `stat-list` (the `stat-item` `hardware` with a `badge` of the state of the hardware acceleration, then `ffmpeg`, `cache`, `running-jobs`, `encoder` and, if known, `device`), why the hardware is not used and a hint, each as `label` |
| `transcode-list` | `ul` (`aria-label`) | `section` | Running transcodes; an `empty-state` instead while none runs |
| `transcode-item` | `li` | `transcode-list` | One running transcode with `transcode-info` and `transcode-actions` |
| `transcode-info` | `div` | `transcode-item` | `transcode-title`, `transcode-meta` and a `progress` of its parts |
| `transcode-title` | `span` | `transcode-info` | Title and artist of the song |
| `transcode-meta` | `span` | `transcode-info` | A `badge` of the media (audio or video), the encoder, the parts done and the start time as `label`s |
| `transcode-actions` | `div` | `transcode-item` | Button `cancel` that stops the transcode |
| `meter` | `div` (`role="meter"`) | `cache-usage`, any | Level within a range, for example the used space of the cache; the share is `--wmp-meter-value`. It uses the variables of `progress` |
| `meter-track` | `div` | `meter` | Background of the meter |
| `meter-fill` | `div` | `meter-track` | Filled part of the meter |
| `backup-status` | `div` | `section` | State of the database backups: a `stat-list` (the `stat-item` `status` with a `badge` of the last run, then `last-run`, `next-run`, `schedule`, `backups` and `size`), the error of the last run and a hint as `label`, and the button that exports the database now |
| `backup-list` | `ul` (`aria-label`) | `section` | Exports of the database, newest first; an `empty-state` instead while there is none |
| `backup-item` | `li` (`data-kind`) | `backup-list` | One export with `backup-info` and `backup-actions` |
| `backup-info` | `div` | `backup-item` | `backup-name` and `backup-meta` |
| `backup-name` | `span` | `backup-info` | File name of the export |
| `backup-meta` | `span` | `backup-info` | A `badge` of what created the export, its time and its size as `label`s |
| `backup-actions` | `div` | `backup-item` | Button that deletes the export |
| `theme-list` | `ul` (`aria-label`) | `section` | Uploaded themes on the theme page of the administration; an `empty-state` instead while there is none |
| `theme-item` | `li` (`data-item`, `data-variants`) | `theme-list` | One uploaded theme with `theme-preview`, `theme-info`, `theme-actions` and, while it is outdated or blocked, `theme-problems`; `data-item` is its id, `data-variants` its variants; state `busy` while it is replaced or deleted, `outdated` while it no longer passes the checks of the server, `blocked` while it breaks a security rule of the server and is no longer served |
| `theme-info` | `div` | `theme-item` | `theme-name` and `theme-meta` |
| `theme-name` | `span` | `theme-info` | Name of the theme |
| `theme-meta` | `span` | `theme-info` | A `badge` `outdated` or `blocked` with a visible `label` while the theme is outdated or blocked, then author, version, `theme-variants`, size and installation date of the theme as `label`s |
| `theme-actions` | `div` | `theme-item` | Buttons `replace` (upload a new version) and `delete` |
| `theme-problems` | `div` | `theme-item` | Why an outdated or blocked theme no longer passes the checks of the server: a `form-note` and `theme-errors`; state `outdated` or `blocked` like its `theme-item`; the base gives it a row of its own below `theme-info` and `theme-actions` |
| `theme-errors` | `ul` (`aria-label`) | `section`, `theme-problems` | Why an uploaded theme package was rejected or an installed theme is outdated, one `theme-error` per problem |
| `theme-error` | `li` (`data-item`) | `theme-errors` | One problem; `data-item` is its code, for example `css.external_url` (see [Uploaded themes](#uploaded-themes)) |
| `theme-error-location` | `span` | `theme-error` | File and line of the problem; missing for problems of the package as a whole |
| `theme-error-message` | `span` | `theme-error` | Message of the problem |
| `scrobble-list` | `ul` (`aria-label`, `aria-busy` while it loads) | `section` | Scrobbling services of the profile on the settings page (Last.fm, ListenBrainz) |
| `scrobble-item` | `li` (`data-item`) | `scrobble-list` | One service with `scrobble-info`, a `form-note` while the service needs attention, and `scrobble-actions` |
| `scrobble-info` | `div` | `scrobble-item` | `scrobble-name` and `scrobble-meta` |
| `scrobble-name` | `span` | `scrobble-info` | Name of the service |
| `scrobble-meta` | `span` | `scrobble-info` | A `badge` of the state of the connection, then the account, the time of the last sent song and the number of songs waiting to be sent as `label`s |
| `scrobble-actions` | `div` | `scrobble-item` | Buttons that connect and disconnect the service |
| `file-selection-filters` | `div` | `section` | Search, filter and sort order of the file selection: a `search` and the `segmented-control`s `file-filter` and `file-sort` |
| `file-selection-list` | `ul` (`aria-label`, `aria-busy` while it loads again) | `section` | Songs of the file selection; an `empty-state` instead while none matches |
| `file-selection-item` | `li` | `file-selection-list` | One song with `file-selection-info` |
| `file-selection-info` | `div` | `file-selection-item` | `file-selection-title`, artist and album as `label`, `file-path` and `file-meta` |
| `file-selection-title` | `a` | `file-selection-info` | Title of the song; opens the page where its files are selected |
| `file-choices` | `div` (`role="radiogroup"`, `aria-labelledby`) | `section` | The files a song can use in one category (`data-item` `audio`, `video` or `cover`): an `indicator` and one `file-choice` per option. One tab stop; the arrow keys, Home and End move the focus, Space and Enter select (every selection is saved at once). It carries the runtime variables of an active element, see [Active element of a group](#active-element-of-a-group) |
| `file-choice` | `button` (`role="radio"`, `aria-checked`) | `file-choices` | One option with `file-path` and `file-meta`; `data-item` is `automatic` (the file of the highest quality), `file` or `missing` (a selected file that is gone) |
| `online-search` | `div` (`data-owner`, `data-state`, `aria-busy`) | `dialog-body` of the `dialog` `online-search` | The online search for album info of a song or album (admins only), a size container `wavemp-online-search`: while searching a `progress`; then `alert`s for an error, the `online-section`s `matches` and `compare` and the `form-note` `online-source` with the note on the sources; after applying one `alert` per result. The base widens the `dialog` `online-search` to 46rem |
| `online-section` | `section` (`data-item`) | `online-search` | `matches` (the matches) or `compare` (the comparison of the selected match), each with an `online-section-title` |
| `online-section-title` | `h3` | `online-section` | Heading of the section |
| `online-matches` | `div` (`role="radiogroup"`, `aria-labelledby`, `aria-disabled`) | `online-section` `matches` | The matches, the best first: an `indicator` and one `online-match` each. One tab stop; the arrow keys, Home and End select. It carries the runtime variables of an active element, see [Active element of a group](#active-element-of-a-group) |
| `online-match` | `button` (`role="radio"`, `aria-checked`, `data-state`) | `online-matches` | One match with `online-match-title`, `online-match-artist`, for a song `online-match-album`, `online-match-release` and `online-match-agreement`; the agreement is the runtime variable `--wmp-agreement` |
| `online-match-title` | `span` | `online-match` | Title of the song or name of the album the match suggests |
| `online-match-artist` | `span` | `online-match` | Artist of the song or album artist; missing if the match knows none |
| `online-match-album` | `span` | `online-match` | Album of the song, or a text for a song without album |
| `online-match-release` | `span` | `online-match` | The release with one `online-match-detail` per known detail |
| `online-match-detail` | `span` (`data-field`) | `online-match-release` | `year`, `country`, `format` (for example CD) or `tracks` (number of songs) |
| `online-match-agreement` | `span` | `online-match` | Agreement with the song or album: a `meter` and the percentage as `label` (hidden from screen readers, the `meter` names it) |
| `online-compare` | `table` | `online-section` `compare` | Current and suggested values side by side: the header row `online-compare-head` and one `online-compare-row` per field and for the cover. Below a width of 30rem of `online-search` the rows are grids: the field spans the row, the current and suggested values share the line below |
| `online-compare-head` | `tr` | `online-compare` | Column headers: field, now, suggested |
| `online-compare-row` | `tr` (`data-field`, `data-state`, `data-active`) | `online-compare` | One field (`data-field` as in the metadata editor: `title`, `artist`, `album`, `album-artist`, `year`, `number`, `disc`, `genre`) or the cover (`cover`); `data-active="true"` while it is chosen to be applied |
| `online-compare-field` | `th` | `online-compare-row` | The `field` with the checkbox that chooses the field or the cover |
| `online-compare-current` | `td` | `online-compare-row` | The current value as `label` (a text for an empty value), for the cover the current `cover` |
| `online-compare-suggested` | `td` | `online-compare-row` | The suggested value as `label`, for the cover the `cover` from the Cover Art Archive, a `progress` while it loads or a `label` without one |
| `review-filters` | `div` | `section` | Search, filter and sort order of the review inbox: a `search` and the `segmented-control`s `review-filter` and `review-sort` |
| `review-bulk` | `div` (`role="group"`) | `section` | Bulk action of the review inbox: a `field` with a `checkbox` that selects all loaded unreviewed songs, the number of selected songs as `label` (`data-field="selected-count"`) and the `button` `mark-selected-reviewed` |
| `library-check-status` | `idle`, `queued` or `running` |
| `library-issue-item` | `problem` or `hint` (videos without audio and songs without video are hints only) |
| `empty-state` in `data-view="admin-checks"` | `clean` when the last check found no problems, `unchecked` before the first check, `filtered` when no problem matches the filter or search |
| `review-list` | `ul` (`aria-label`, `aria-busy` while it loads again) | `section` | Songs of the review inbox, one `review-item` each |
| `review-item` | `li` | `review-list` | One song: `review-select`, its `cover` and `review-info` |
| `review-select` | `label` | `review-item` | The area around the `checkbox` of the song (disabled once the song is reviewed, then with the state `disabled`); a tap on it selects the song |
| `review-info` | `div` | `review-item` | `review-title`, artist and album as `label` (`data-field="context"`), `file-path` (folder and base name of the files) and `review-meta` |
| `review-title` | `a` | `review-info` | Title of the song; opens it in the metadata editor of the review inbox |
| `review-meta` | `span` | `review-info` | A `badge` `reviewed` for songs reviewed since the list was loaded, when the song was added as `label` (`data-field="first-seen-at"`) and `review-tags` |
| `review-tags` | `span` | `review-meta`, `detail-info` | Hint which tags the file of a song lacks: `icon` (slot `tags-missing`), a `label` and one `badge` `missing-tag` per field (`data-field` `title`, `artist`, `album`, `year`, `number` or `genre`, like the fields of the metadata editor); missing while the tags are complete |
| `review-actions` | `div` (`role="group"`) | `view` | Actions of a song of the review inbox: the `button`s `save-and-review` (submits the metadata editor), `mark-reviewed` and `skip` |
| `review-nav` | `nav` | `view` | Previous and next song of the review inbox: the `button`s `previous` and `next` and the position as `label` (`data-field="position"`) |
| `library-check-status` | `div` | `section` | State of the library check: a `stat-list` (while a check waits or runs the `stat-item` `status` with a `badge` `queued` or `running`, then `checked-at`, `tracks`, `problems` and `hints`), the error of the last run (`label` `data-item="error"`) and a hint as `label`, and the `button` `run-check` that checks the library now |
| `library-issue-filters` | `div` | `section` | Filter, search and sort order of the problems: the `segmented-control` `issue-type` (one `segment` per kind of problem and `all`, each with a `badge` holding the number), a `search` and the `segmented-control` `issue-sort` |
| `library-issue-list` | `ul` (`aria-label`, `aria-busy` while it loads again) | `section` | Problems of the library check; an `empty-state` instead while none matches |
| `library-issue-item` | `li` | `library-issue-list` | One problem: `library-issue-info` and `library-issue-actions`; `data-item` is the kind of problem (`unreadable-file`, `missing-tags`, `unknown-album`, `missing-cover`, `possible-duplicate`, `video-without-audio` and `audio-without-video`) |
| `library-issue-info` | `div` | `library-issue-item` | `library-issue-meta`, `library-issue-title`, artist and album as `label`, `file-path` (the folder and base name of the files of the song, or the path of an unreadable file) and `library-issue-detail` |
| `library-issue-meta` | `span` | `library-issue-info` | The kind of problem as `badge` and the length of the song as `label` (`data-field="duration"`) |
| `library-issue-title` | `a` | `library-issue-info` | Title of the song; opens the page of the song |
| `library-issue-detail` | `p` | `library-issue-info` | What is wrong as `label` (the missing tags, why a file is unreadable, ...); for a possible duplicate also the `link` `related-track` to the other song, its `file-path` and its length as `label` (`data-field="related-duration"`) |
| `library-issue-actions` | `div` (`role="group"`, `aria-label`) | `library-issue-item` | Where the problem is fixed: the `link`s `edit-metadata` (the metadata editor of the song), `search-online` (its online search) and `show-files` (the page of its files); songs without an audio file only have `show-files`, unreadable files without a song no actions |
| `button` | `button` | any | Generic button, see `data-variant` in [States](#states) |
| `icon` | `span` (`aria-hidden`) | any | Icon wrapper with the slot in `data-icon`, see [Icons](#icons) |
| `icon-glyph` | `span` | `icon` | Masked shape of the icon |
| `label` | `span` | any | Text of a button, entry or area |
| `indicator` | `span` (`aria-hidden`), in `tree` a `li` (`role="none"`, `aria-hidden`) | `tab`, `tabs`, `bottom-nav`, `admin-nav`, `tree`, `color-picker`, `segmented-control`, `file-choices`, `online-matches` | In a `tab` the small arrow of the active tab (with the icon `tab-indicator`). As first child of a group with one active element an empty decorative layer that the base hides and lays over the active element, for themes that show it, for example as a line or a highlight that slides between the entries; see [Active element of a group](#active-element-of-a-group) |
| `slider` | `div` | `seek`, `volume`, `equalizer-band`, any | Slider with `slider-track` and `slider-thumb`; horizontal, or vertical (`data-orientation="vertical"`, from the bottom to the top) in the equalizer |
| `slider-track` | `div` | `slider` | Track of the slider |
| `slider-fill` | `div` | `slider-track` | Filled part of the track |
| `slider-thumb` | `div` (`role="slider"`) | `slider` | Knob, focusable |
| `field` | `label`, or `div` around a `color-picker` or a `segmented-control` | `form`, any | Text field or selection with `label`, `input`, `select`, `color-picker` or `segmented-control` (few fixed options, for example the ReplayGain mode, the role of a profile and the hardware acceleration), and optionally `field-hint` and `field-error`; with `data-variant="checkbox"` a `checkbox` before its `label` |
| `checkbox` | `input type="checkbox"` (`:checked`) | `field`, `review-select` | Checkbox of a field with `data-variant="checkbox"`; in `review-select` without a visible label, named by `aria-label` |
| `form` | `form` | `view`, `dialog-body` | Form with fields |
| `field-row` | `div` | `form` | Short fields side by side, for example year, track and disc number in the metadata editor |
| `form-note` | `p` | `form` | Note of a form, for example the file the metadata editor writes |
| `select` | `select` (`aria-invalid`) | `field` | Selection list |
| `field-hint` | `span` | `field` | Help text of the field |
| `field-error` | `span` | `field` | Error message of the field |
| `color-picker` | `div` (`role="radiogroup"`) | `field` | Selection of a color: an `indicator` and one `color-swatch` per color; tells the selected swatch with the runtime variables of an active element, see [Active element of a group](#active-element-of-a-group) |
| `color-swatch` | `button` (`role="radio"`, `aria-checked`) | `color-picker` | One color (`--wmp-swatch-color`) with a visually hidden `label` |
| `dialog` | `dialog` | any | Modal dialog |
| `dialog-header` | `div` | `dialog` | Title bar of the dialog |
| `dialog-title` | `h2` | `dialog-header` | Title of the dialog |
| `btn-close` | `button` | `dialog-header` | Closes the dialog |
| `dialog-body` | `div` | `dialog` | Content of the dialog |
| `dialog-footer` | `div` | `dialog` | Buttons of the dialog |
| `shortcut-overview` | `div` | `dialog-body` of the `dialog` `shortcuts` | Overview of the keyboard shortcuts (key ? or the entry `shortcuts` of the profile menu): one `shortcut-group` per group. The base lays the groups out side by side where two of them fit and in one column on a phone; the `dialog` `shortcuts` is 46rem wide |
| `shortcut-group` | `section` (`aria-labelledby`, `data-item`) | `shortcut-overview` | Shortcuts of one group (`data-item` `playback`, `volume`, `view` or `general`) with `shortcut-group-title` and `shortcut-list` |
| `shortcut-group-title` | `h3` | `shortcut-group` | Name of the group |
| `shortcut-list` | `dl` | `shortcut-group` | The shortcuts of the group |
| `shortcut-item` | `div` (`data-item`) | `shortcut-list` | One shortcut: `shortcut-description` and `shortcut-keys`; `data-item` is its id, for example `play-pause`, `seek-backward`, `seek-forward`, `previous`, `next`, `shuffle`, `repeat`, `volume-up`, `volume-down`, `mute`, `fullscreen`, `search` or `shortcuts` |
| `shortcut-description` | `dt` | `shortcut-item` | What the shortcut does |
| `shortcut-keys` | `dd` | `shortcut-item` | The key combinations of the shortcut, one `shortcut-combo` each |
| `shortcut-combo` | `kbd` | `shortcut-keys` | One key combination: its `shortcut-key`s with a `shortcut-key-separator` between them |
| `shortcut-key` | `kbd` (`data-key`) | `shortcut-combo` | One key, drawn as a key cap with `--wmp-key-color`, `--wmp-key-background`, `--wmp-key-border-color` and `--wmp-key-size`; `data-key` names it: `mod` (Ctrl, Command on Apple devices), `space`, `arrowleft`, `arrowright`, `arrowup`, `arrowdown` or the character, for example `m` or `?` |
| `shortcut-key-separator` | `span` (`aria-hidden`) | `shortcut-combo` | The + between two keys |
| `shortcut-hint` | `p` | `dialog-body` of the `dialog` `shortcuts` | Note below the overview: the shortcuts rest while a field has the focus or a dialog or menu is open |
| `context-menu` | `div` (`role="menu"`, `popover`) | any (the presets of the visualization: `stage`); a submenu (`data-kind="submenu"`) lies in its `context-menu` | Context menu, shown in the top layer. The app has one context menu for songs, albums, artists, playlists, smart playlists, queue entries and Now Playing; it opens with a right click, a long press, `btn-item-menu` and the menu key or Shift+F10. An entry with a submenu (for example `rate`) opens a nested `context-menu` with `data-kind="submenu"` beside itself. Typing letters moves to the next entry whose label starts with them. A menu of choices that a button opens can also open above the button or as a sheet at the bottom of the screen (`data-placement`); it then scrolls when it is taller than its room |
| `menu-item` | `button` (`role="menuitem"`, or `role="menuitemradio"` with `aria-checked` in choices such as the sort options; with a submenu `aria-haspopup="menu"`, `aria-expanded` and `aria-controls`) | `context-menu` | Entry with `icon` and `label`; disabled entries (`:disabled`) stay in the menu, greyed out. They can become enabled in place while the menu is open, when the state behind them changes (for example the entries of a playlist once a change is saved), so both looks need the same size |
| `menu-separator` | `hr` | `context-menu` | Separator between entries |
| `submenu-indicator` | `span` (`aria-hidden`) | `menu-item` with a submenu | Arrow at the end of the entry with the `icon` `submenu` |
| `btn-item-menu` | `button` (`aria-haspopup="menu"`, `data-variant="icon"`) | `tile-item` (after `tile`), `list-cell` with `data-column="favorite"` (after `btn-favorite`), `queue-item-actions`, `autoplay-item-actions`, `stage-actions` | "More options": opens the context menu of the song, album, artist, playlist, queue entry or of the stage below itself, with the `icon` `item-menu` and a `label`. In lists it is no tab stop (the keyboard opens the menu of the row). The base shows it only where no pointer can hover (`@media (hover: hover)` hides it) and puts it in the upper corner of a `tile-item` on the surface of the menu |
| `alert` | `div` (`role="alert"` for errors, otherwise `role="status"`) | any | Message with `icon` and `label` |
| `recap-nav` | `nav` | `view` (recap pages) | Navigation between the recap (`recap`), the statistics (`statistics`) and the shared recaps (`shared-recaps`): an `indicator` and one `recap-nav-item` per page; tells themes the current page with the runtime variables of an active element, like `admin-nav` |
| `recap-nav-item` | `a` (`aria-current="page"` for the current page, `data-item`) | `recap-nav` | One page with `icon` and `label`; state `active` for the current page |
| `recap-period` | `div` | `view` `recap` and `statistics` | Choice of the period: the `segmented-control` `recap-period` and, for a custom period, a `field-row` `custom-period` with the date fields `from` and `to` |
| `recap-layout` | `div` | `view` `recap` and `shared-recap` | The story (`recap-stage`) and the `recap-panel`; the base puts the panel beside the story on the desktop and below it on a phone |
| `recap-stage` | `div` | `recap-layout` | Holds the `recap-story`, centered |
| `recap-panel` | `div` | `recap-layout` | The `section`s `recap-design` (the `field` with the `segmented-control` `recap-design`) and `recap-share` (`recap-export`, on the own recap also the `checkbox` `share-with-profiles`) |
| `recap-story` | `section` (`aria-roledescription="carousel"`, `tabindex="0"`, `data-recap-design`, `data-decoration`) | `recap-stage` | The recap as a story in the format 9:16 and a size container (`wavemp-recap`): `recap-backdrop`, `recap-progress`, `recap-slides` and `recap-controls`. States `playing`, `paused` and, while the statistics load, `loading` (then only with `recap-backdrop`). Its colors are the recap variables of its design, see [Recap and statistics](#recap-and-statistics); the arrow keys, the space bar, a tap (left third back, elsewhere on), a swipe and holding it (pauses) control it |
| `recap-backdrop` | `span` (`aria-hidden`) | `recap-story` | Decoration behind the content, after `data-decoration` of the story: `glow` (soft lights), `glass` (a pane of glass) or `bands` (diagonal bands) |
| `recap-progress` | `ol` (`aria-hidden`) | `recap-story` | One `recap-progress-segment` per section |
| `recap-progress-segment` | `li` | `recap-progress` | A section: states `done`, `active` and `upcoming`; the share of its time that has run is `--wmp-progress-value` (0 to 1) |
| `recap-progress-fill` | `span` | `recap-progress-segment` | The part of the segment that has run |
| `recap-slides` | `div` (`aria-live`) | `recap-story` | Area of the shown section; taps and swipes on it go back and on |
| `recap-slide` | `article` (`aria-roledescription="slide"`, `data-kind`) | `recap-slides` | One section: `recap-cover`, `recap-slide-title`, `recap-figure`, `recap-figure-label`, `recap-ranking`, `chart`, `recap-facts` and `recap-caption`, each only where the section has it. `data-kind` is `intro`, `listening-time`, `top-track`, `top-tracks`, `top-artists`, `top-albums`, `top-genres`, `timeline`, `hours`, `new-artists`, `summary` or `empty` (no plays). Without `prefers-reduced-motion: reduce` the base lets its parts rise into place one after the other and the bars of its `chart` grow |
| `recap-cover` | `div` | `recap-slide` (`top-track` and `summary`) | Large `cover` of the song or artist of the section |
| `recap-slide-title` | `h2` | `recap-slide` | Title of the section |
| `recap-figure` | `p` | `recap-slide` | The large number or name of the section, for example the minutes of music, the favorite hour or the title of the song number one |
| `recap-figure-label` | `p` | `recap-slide` | Line below the figure, for example "minutes of music" or the artist of the song |
| `recap-ranking` | `ol` | `recap-slide` | Up to five entries of a ranking |
| `recap-ranking-item` | `li` (`data-rank`, 1 to 5) | `recap-ranking` | An entry: `recap-rank`, a `cover`, `recap-ranking-info` and `recap-ranking-value` |
| `recap-rank` | `span` | `recap-ranking-item` | Place of the entry |
| `recap-ranking-info` | `span` | `recap-ranking-item` | `recap-ranking-title` and `recap-ranking-subtitle` |
| `recap-ranking-title` | `span` | `recap-ranking-info` | Name of the song, artist, album or genre |
| `recap-ranking-subtitle` | `span` | `recap-ranking-info` | Second line, for example the artist or the number of songs |
| `recap-ranking-value` | `span` | `recap-ranking-item` | Number of plays |
| `recap-facts` | `dl` | `recap-slide` (`listening-time`, `summary`) | Numbers of the section, one `recap-fact` each |
| `recap-fact` | `div` (`data-item`) | `recap-facts` | A number: `label` (`dt`) and `recap-fact-value` (`dd`); `data-item` is `plays`, `tracks`, `active-days`, `minutes`, `artists`, `top-artist`, `top-track` or `top-genre` |
| `recap-fact-value` | `dd` | `recap-fact` | The value |
| `recap-caption` | `p` | `recap-slide` | Sentence at the end of the section, for example the days of the period or the busiest day |
| `recap-controls` | `div` | `recap-story` | `btn-recap-previous`, `btn-recap-pause` and `btn-recap-next`; the base gives them the colors of the design, so they stay readable on the story |
| `btn-recap-previous` | `button` (`data-variant="icon"`) | `recap-controls` | Previous section, icon slot `recap-previous`; disabled on the first section |
| `btn-recap-pause` | `button` (`data-variant="icon"`) | `recap-controls` | Pauses and plays the story (states `playing` and `paused`, icon slots `pause` and `play`) |
| `btn-recap-next` | `button` (`data-variant="icon"`) | `recap-controls` | Next section, icon slot `recap-next`; disabled on the last section |
| `recap-export` | `div` (`aria-busy`) | `section` `recap-share` | Pictures and video of the recap: the `field` with the `segmented-control` `recap-picture-format` (`story`, `square`), `view-actions` with the `button`s `save-picture`, `save-video` and, while the video is recorded, `cancel-video`, a `progress` while the video is recorded or converted, the `form-note` `video-unsupported` and an `alert` |
| `recap-share-list` | `ul` | `section` `received` and `own` of `data-view="shared-recaps"` | Shared recaps, one `recap-share-item` each |
| `recap-share-item` | `li` (`data-item` with the id) | `recap-share-list` | A shared recap: state `received` (of another profile) or `own` |
| `recap-share-info` | `span` | `recap-share-item` | `recap-share-title` and `recap-share-meta` |
| `recap-share-title` | `a` | `recap-share-info` | Profile and period (received) or period (own); opens the recap |
| `recap-share-meta` | `span` | `recap-share-info` | Days of the recap and when it was shared, one `label` each |
| `recap-share-actions` | `span` | `recap-share-item` with the state `own` | The `button` `stop-sharing` |
| `chart` | `div` (`data-count`) | `section` `timeline` and `hours` of `data-view="statistics"`, `recap-slide` | Bar chart of one series; `data-count` is the number of bars. In a `recap-slide` it takes the colors of the design |
| `chart-bars` | `ol` (`aria-label`) | `chart` | The bars on a common baseline |
| `chart-bar` | `li` (`aria-label` with label and value; `tabindex="0"` on the statistics page) | `chart-bars` | A bar: `chart-bar-fill`, on the statistics page `chart-tooltip`, and `chart-bar-label`; its share of the highest bar is `--wmp-bar-value`; state `peak` for the highest |
| `chart-bar-fill` | `span` | `chart-bar` | The visible bar, `--wmp-bar-value` times the height of the chart |
| `chart-bar-label` | `span` | `chart-bar` | Label below the axis (day, month, year or hour); state `shown` for the labelled bars (about six per chart), otherwise `hidden` |
| `chart-tooltip` | `span` | `chart-bar` | Label and value of the bar; the base shows it while the bar is hovered or focused |
| `stats-table-scroll` | `div` | `section` of a ranking of `data-view="statistics"` | Scrolls a wide `stats-table` sideways |
| `stats-table` | `table` (`aria-labelledby`) | `stats-table-scroll` | A ranking of the statistics |
| `stats-table-row` | `tr` | `stats-table` | A row; state `header` for the row of the headings |
| `stats-table-heading` | `th` (`data-column`) | `stats-table-row` | Heading of a column; state `numeric` for numbers |
| `stats-table-cell` | `td` (`data-column`) | `stats-table-row` | A cell; state `numeric` for numbers; in the column `title` a `link` to the song, artist, album or genre |

The internal component overview (`/dev/components`) uses its own parts starting with `dev-`. They are not part of the theme API.

## States

### data-state

`data-state` holds space-separated tokens; check single tokens with `[data-state~='token']`.

| Part | Tokens |
|---|---|
| `transport` | `idle`, `playing` or `paused`; additionally `loading` while a song loads or waits for data, and `expanded` while the full-screen player is open (mobile). In the full-screen player additionally `view-switch` while `player-view-switch` is open, and while it is swiped down `dragging` (it follows the finger), `settling` (it snaps back) or `dismissing` (it leaves the screen and then closes) |
| `player-media` | the shown view: `cover`, `video` or `visualization`; additionally `loading` while the chosen video loads |
| `player-view-switch` | `open` or `closed` |
| `marquee` | `fit` when the text fits its place; otherwise `overflow`, alone while it does not move (reduced motion: ellipsis), else with its phase: `waiting` (rests at its start), `scrolling` or `end` (rests at its end) |
| `transport-message` | `empty` without a notice, otherwise its kind: `unsupported` (the browser cannot play the song and the server cannot transcode it), `unavailable` (it could not be loaded) or `failed` (it failed to play) |
| `transport-time` | `empty` without a current song |
| `compact-player` | `idle`, `playing` or `paused`; additionally `loading` while a song loads or waits for data, and `video` while it shows the video of the song |
| `compact-player-media` | `video` while the video of the song is shown, otherwise `cover` |
| `compact-player-info` | `empty` while nothing plays |
| `compact-player-seek` | `disabled` while nothing plays |
| `compact-player-time` | `empty` without a current song |
| `btn-compact` | `on` while the mini player is open, otherwise `off` |
| `theme-preview` | `missing` while the preview picture of the theme cannot be loaded |
| `btn-play` | `idle`, `playing` or `paused` |
| `btn-shuffle` | `on` or `off` |
| `btn-repeat` | `off`, `all` or `one` |
| `btn-crossfade` | `on` or `off` |
| `btn-autoplay` | `on` or `off` |
| `btn-mute` | `muted` or `unmuted` |
| `btn-equalizer` | `on` or `off`: whether the equalizer of the profile is on |
| `sleep-timer` | `off`, `duration`, `end-of-track` or `end-of-queue`; additionally `fading` while the music fades out before the stop |
| `btn-sleep-timer` | `on` while a timer runs, otherwise `off` |
| `cast` | the technique `chromecast` or `airplay`; `available` or `insecure` (Chromecast without HTTPS); `disconnected`, `connecting` or `connected`; additionally `no-devices` while the browser finds no device, and `hint` while `cast-hint` is open |
| `btn-cast` | `disconnected`, `connecting` or `connected` |
| `cast-hint` | `open` or `closed` |
| `equalizer` | `on` or `off` |
| `button` `power` of `equalizer-controls` | `on` or `off`: whether the equalizer of the profile is on |
| `seek` | `disabled` while nothing plays |
| `volume` | `muted` |
| `slider` | `disabled`, `dragging` |
| `sidebar`, `sidebar-backdrop` | `open` or `closed` (drawer on mobile) |
| `sidebar` | additionally `collapsed` (desktop, only the icons) or `expanded` |
| `tab`, `bottom-nav-item`, `breadcrumb-item`, `tree-item` | `active` for the current page |
| `tree-item` | additionally `expanded` or `collapsed` for nodes with children |
| `list` | `placeholder` while the list shows placeholder content (component overview); `busy` while a change of a playlist is being saved; `has-video` while one of its songs has a video: the base then lays out the `list-column` and the `list-cell`s `title` as a grid of two columns, `--wmp-list-badge-width` for the video `badge` and the rest for the `label`, so the titles of all rows stay in line; `ungrouped` while it is a plain list (view `view-list` of the [Layout options](#layout-options)): one `list-group` without heading and details, and on the desktop no column for `list-group-info` |
| `list-row` | `selected`; `has-video` for songs with a video, additionally `video-only` for songs with a video only (no audio); `playing` for the current song of the player |
| `btn-favorite` | `on` for a favorite, otherwise `off` |
| `rating` | `rated` or `unrated` |
| `rating-star` | `on` up to the rating, otherwise `off` |
| `now-playing-actions` | `empty` while nothing plays |
| `stage` | the shown view: `cover`, `video`, `visualization` or `lyrics` |
| `stage-view` | `active` for the shown view; `loading` for the video view while the video loads or moves to the position of the music (the base keeps it invisible but rendered); `failed` for the video view while its video cannot be played |
| `visualization` | `idle` (not shown yet), `loading` (the module loads), `waiting` (waits for the music), `running` (draws), `paused` (hidden, nothing is drawn), `unsupported` (the browser cannot show it) or `failed`; additionally `fullscreen` while it is shown in full screen |
| `visualization-controls` | `loading` until the presets of the module are loaded, then `ready`; additionally `has-color-schemes` while the module offers color schemes |
| `btn-preset-picker` | `open` while the list of presets is open, otherwise `closed` |
| `btn-color-scheme-picker` | `open` while the list of color schemes is open, otherwise `closed` |
| `btn-auto-change` | `on` while the preset changes automatically, otherwise `off` |
| `video-stage` | `fullscreen` while it is shown in full screen |
| `video-ambient` | `off` (switched off by the profile), `waiting` (the video has no picture yet), `running` (draws the playing video), `paused` (keeps the last picture: the video is paused, the video view is hidden, the stage is out of the viewport or the page is in the background) or `static` (reduced motion: one color without animation) |
| `video-ambient-canvas` | `current` for the canvas with the newest picture, `previous` for the other one; none before the first picture of a video |
| `lyrics` | `synchronized` (the lyrics follow the music) or `static` |
| `lyrics-line`, `lyrics-word` | `past`, `active` or `upcoming` against the position of the music (synchronized lyrics only) |
| `queue-item` | `current` for the current song, `played` for the songs before it; additionally `autoplay` for songs autoplay added after the list |
| `queue-pane-header` | `empty` while nothing is current |
| `autoplay` | `loading` while the first songs are chosen, `ready` with songs, `empty` when no matching song is left |
| `autoplay-item` | `video-only` for songs that only have a music video |
| `placeholder-notice`, `cover` | `placeholder` |
| `now-playing-mini` | `empty` while nothing plays |
| `server-status` | `checking`, `healthy`, `degraded`, `unhealthy` or `unreachable` |
| `btn-review-inbox` | `pending` while songs are unreviewed, `empty` without (or before the number is loaded); additionally `active` on the pages of the review inbox |
| `search` | `empty` or `filled` |
| `context-menu` | `open` or `closed` |
| `badge` | the kind of hint, for example `has-video`, for songs with a video only additionally `video-only`; in `file-meta` `audio`, `video`, `image`, `lyrics`, `primary`, `lossless`, `writable` or `issue`, in a `file-choice` also `embedded` (a cover inside an audio or video file), `active` (the file the song uses), `best` (the file of the highest quality) and `missing`, in `file-selection-info` the state `selected` or `missing` together with the category `audio`, `video` or `cover`; in `user-meta` `admin` or `user`, `built-in` and `pin` (the profile has a PIN; with `icon`, slot `has-pin`); in `scan-status` `idle`, `queued` or `running`; in `run-item`, `run-meta` and `job-meta` the status of the run: `queued`, `running`, `succeeded`, `failed`, `canceled` or `interrupted`; in `transcode-meta` `audio` or `video`; in `stat-value` (hardware acceleration) `detecting`, `active`, `unavailable`, `disabled` or `failed`; in `backup-meta` what created the export: `schedule`, `manual` or `migration`; in the `stat-value` of the `stat-item` `status` of `backup-status` the status of the last run like in `job-meta`; in `btn-review-inbox` `unreviewed`; in `review-meta` `reviewed`; in `library-issue-meta` the kind of problem (`unreadable-file`, `missing-tags`, `unknown-album`, `missing-cover`, `possible-duplicate`, `video-without-audio` and `audio-without-video`) and its severity `problem` or `hint`; in a `segment` of `issue-type` `count`, the severity of the kind (`problem`, `hint`, or `all` for the segment `all`) and `empty` without problems; in the `stat-value` of `library-check-status` `queued` or `running`; in `review-tags` `missing-tag`; in `theme-meta` `outdated` or `blocked`; in `scrobble-meta` the state of the connection like in `scrobble-item`: `connected`, `not-connected`, `authorization-failed` or `unavailable` |
| `transcode-item` | `audio` or `video` |
| `profile-select` | `loading`, `ready` or `error` (the profiles could not be loaded) |
| `pin-entry` | `busy` while the PIN is checked, `error` after a wrong PIN or during a lockout |
| `pin-pad` | `disabled` while the PIN is checked |
| `view` with `data-view="settings-pin"` | `loading`, `error`, `on` (the profile has a PIN), `off` (no PIN) or `managed` (the PIN comes from `ADMIN_PIN` and cannot be changed) |
| `avatar` | `image` while it shows a picture; no state while it shows the initials |
| `avatar-editor` | `preset` or `upload`: the checked kind of picture |
| `avatar-crop` | `dragging` while the picture is dragged |
| `profile-tile` | `built-in` for the built-in admin profile, additionally `secondary` as soon as other profiles exist; `current` for the profile of the current session; `protected` for a profile with a PIN |
| `alert` | `error`, `success` or `info` |
| `field` | `invalid` while the field has an error |
| `smart-rule` | `invalid` while a value of the rule has an error |
| `button` in `smart-templates` | `applied` for the template applied last |
| `user-item` | `built-in` for the built-in admin profile |
| `source-item` | `active` or `inactive` (scanned and watched or not), plus `unavailable` while the folder is missing, `watched` while the file watcher follows it and `media-path` while it comes from `MEDIA_PATH`; the same tokens mark the `badge`s in `source-meta` |
| `admin-nav-item` | `active` for the current page |
| `scan-status`, `job-item`, `backup-status` | `idle`, `queued` or `running` |
| `backup-item` | `busy` while the export is deleted |
| `theme-item` | `busy` while the theme is replaced or deleted; `outdated` while it no longer passes the checks of the server, `blocked` while it breaks one of their security rules (see [Checks after an update](#checks-after-an-update)) |
| `theme-problems` | `outdated` or `blocked`, like its `theme-item` |
| `scrobble-item` | `connected`, `not-connected`, `authorization-failed` (the service no longer accepts the access) or `unavailable` (Last.fm without API account on the server); additionally `busy` while the service is connected or disconnected |
| `view` with `data-view="settings-scrobbling-lastfm"` | `connecting` while the token of Last.fm is handed to the server, `failed` if that did not work |
| `run-item` | `queued`, `running`, `succeeded`, `failed`, `canceled` or `interrupted` |
| `progress` | `indeterminate` while the share done is unknown |
| `load-more` | `loading` while the next page loads |
| `file-item` | `audio`, `video`, `image` or `lyrics`; additionally `primary` for the file the song plays |
| `file-selection-item` | `selected` if a category uses a file the admin selected, `missing` if a selected file is gone, `choices` if a category has more than one file |
| `file-choice` | `checked` for the selected option, `active` for the file the song uses, `best` for the file of the highest quality, `missing` for a selected file that is gone |
| `online-search` | `loading`, `ready`, `applying`, `applied`, `disabled` (the online search is switched off), `unavailable` (MusicBrainz cannot be reached), `notFound` or `error` |
| `online-match` | `selected` for the selected match |
| `online-compare-row` | for a field `missing` (the song or album has no value, the match has one), `different`, `same` or `unknown` (the match knows no value); for the cover `none`, `loading`, `ready`, `missing` (the release has no cover) or `error` |
| `review-item` | `unreviewed` or `reviewed` (reviewed since the list was loaded); additionally `selected` for a song selected for the bulk action and `missing-tags` if its file lacks tags |
| `review-select` | `disabled` while its checkbox is disabled (the song is reviewed) |
| `review-bulk` | `selected` while songs are selected, otherwise `empty` |
| `empty-state` in `data-view="admin-review-track"` | `finished` when no unreviewed song follows in the list |
| `playlist-cover` | `rotating` while its tiles change: it has more than four song covers, is in view and the page is shown |
| `playlist-cover-tile` | `changing` while a new song cover enters the tile |
| `playlist-cover-slide` | `active` for the song cover that is shown, `loading` for a new one whose picture loads (invisible), `entering` while the new one slides or fades in |
| `cover-preview` | `loading` while the cover of the chosen rule loads, `ready` while it shows one, `empty` without a cover |
| `playlist-choice` | `busy` while the songs are being added to this playlist |

### Active element of a group

Every group in which one entry is the active one gives the theme the same means to highlight it, also with an animation in CSS only:

| Group | Active entry |
|---|---|
| `tabs`, `bottom-nav` | `tab` or `bottom-nav-item` of the current page |
| `admin-nav` | `admin-nav-item` of the current page |
| `tree` | `tree-item` of the current page (among the visible items) |
| `color-picker` | selected `color-swatch` |
| `avatar-presets` | chosen `avatar-preset` |
| `segmented-control` | checked `segment` |
| `theme-picker` | checked `theme-option` |
| `file-choices` | checked `file-choice` |
| `online-matches` | selected `online-match` |

- The state is an attribute of the entry: `aria-current="page"` and `data-state="active"`, or `aria-checked="true"`.
- The group has an `indicator` as its first child: empty, `aria-hidden`, hidden by the base (`display: none`) and laid over the active entry (`position: absolute` in the group, `left: var(--wmp-indicator-x)`, `top: var(--wmp-indicator-y)`, `inline-size: var(--wmp-indicator-width)`, `block-size: var(--wmp-indicator-height)`; in a `segmented-control` the whole height). A theme shows it and decides its shape, for example a line below the entry or a sliding pane of glass behind it. The group is a containing block (`position: relative`).
- The group carries the runtime variables `--wmp-item-count`, `--wmp-active-index`, `--wmp-indicator-x`, `--wmp-indicator-y`, `--wmp-indicator-width` and `--wmp-indicator-height` (see [Runtime variables](#runtime-variables)). The app measures the box again when the active entry changes and when the group or its entries change their size. Without an active entry (`--wmp-active-index: -1`) the box has no size.
- Two things follow from the measuring, and all shipped themes handle them in CSS only. An indicator without an active entry is a box without size, so it should paint nothing outside its box: no outer `box-shadow`, `outline` or `border` (a glow can be a `filter: drop-shadow()`, which follows painted content and draws nothing for an empty box). And the variables are set for the first time after the page is drawn and again after it is rebuilt, so an indicator with an unconditional `transition` slides in from the corner of the group. Let it move only while the person works with the group (`:hover`, `:focus-within`, `:active`), as the base does with the durations of [Motion](#motion) and as in the example below; a click or an arrow key still animates it, and the screenshots of the regression stay stable.
- `rating` carries `--wmp-item-count` and `--wmp-active-index` only, `playlist-cover` only `--wmp-item-count` (the number of its song covers).

Example of a line that slides below the active tab:

```css
[data-part='tabs'] > [data-part='indicator'] {
    display: block;
    inset-block-start: auto;
    inset-block-end: 0;
    block-size: 3px;
    border-radius: 3px;
    background: linear-gradient(90deg, #8fd2ff, #ffffff);
}

[data-part='tabs']:is(:hover, :focus-within, :active) > [data-part='indicator'] {
    transition:
        left var(--wmp-motion-duration-slow) var(--wmp-motion-easing-standard),
        inline-size var(--wmp-motion-duration-slow) var(--wmp-motion-easing-standard);
}
```

### ARIA attributes and native states

| Attribute or state | Parts |
|---|---|
| `aria-current="page"` | `tab`, `bottom-nav-item`, `breadcrumb-link`, `tree-item`, `admin-nav-item` |
| `aria-current="true"` | `profile-tile` of the current profile, `queue-item` of the current song |
| `aria-busy="true"` | `profile-select` while a profile is being selected; `autoplay` while its songs are chosen; `view` while its content loads; `load-more` while the next page loads |
| `aria-expanded` | `tree-item`, `btn-menu`, `btn-sidebar-toggle`, `btn-profile`, `btn-sleep-timer`, `btn-preset-picker`, `btn-color-scheme-picker`, `now-playing-mini` (mobile: the full-screen player on the bar, `player-view-switch` in the full-screen player), `btn-cast` (without HTTPS), `menu-item` with a submenu (`true` while it is open) |
| `aria-invalid="true"` | `input`, `select` and `segmented-control` of a field with an error |
| `aria-checked` | `color-swatch`, `segment`; `menu-item` with `role="menuitemradio"`, for example the chosen sort key and direction |
| `aria-selected` | `list-row`; lists of songs allow several selected rows (`aria-multiselectable="true"` on `list`): Ctrl or Cmd with a click adds or removes a row, Shift with a click or an arrow key selects a range, Ctrl+A selects all; every selected row also has the state `selected` and the context menu applies to all of them |
| `aria-pressed` | `btn-shuffle`, `btn-repeat`, `btn-crossfade`, `btn-autoplay`, `btn-auto-change`, `btn-mute`, `btn-favorite`, `rating-star`, `btn-sleep-timer`, the `button` `power` of `equalizer-controls` and other toggle buttons |
| `aria-disabled="true"` | `tree-item`, `slider-thumb`, `btn-cast` without HTTPS (it stays focusable to show `cast-hint`); `segmented-control` and its `segment`s while the whole group is disabled, for example the ReplayGain mode while casting (the segments stay focusable; a single option that cannot be chosen is a disabled `button` instead) |
| `aria-valuemin`, `aria-valuemax`, `aria-valuenow`, `aria-valuetext` | `slider-thumb`, `progress` (`aria-valuenow` from 0 to 100, missing while indeterminate), `meter` (`aria-valuenow` from 0 to 100) |
| `:disabled` | buttons and inputs |
| `:checked` | `checkbox` |
| `[open]` | `dialog`, `disclosure` |
| `:popover-open` | `context-menu` |

The base links states to graphics: buttons use the variables `--wmp-image-<variant>-button`, `-hover`, `-active` and `-disabled` for `:hover`, `:active` and `[aria-pressed='true']`, and `:disabled`. A theme can therefore change all button states by setting variables only.

### Other attributes

| Attribute | Parts | Values |
|---|---|---|
| `data-variant` | `button` and all `btn-*` parts | `default`, `primary`, `icon`, `round` |
| `data-variant` | `field` | `checkbox` |
| `data-icon` | `icon` | Icon slot, see [Icons](#icons) |
| `data-theme` | root element | Id of the shown theme, for example `midnight` or `clear-glass` |
| `data-theme-variant` | root element | Shown variant of the theme: `light` or `dark` (see [Variants](#variants)) |
| `data-variants` | `theme-option` | Variants of the theme, separated by spaces: `light`, `dark` or `light dark` |
| `data-kind` | `theme-preview` | Variant the preview shows: `light` or `dark` |
| `data-item` | `theme-option` | Id of the theme |
| `data-field` | `field` (appearance in the settings) | `theme` (the `theme-picker`), `theme-appearance` (the `segmented-control` `theme-appearance` with the `segment`s `system`, `light` and `dark`; disabled for themes with one variant); the `section` around them has `data-item="appearance"` |
| `data-orientation` | `slider` | `horizontal`, `vertical`; also as `aria-orientation` on `slider-thumb` |
| `data-orientation` | `video-stage` | Orientation of the picture of the video as it is shown (rotation applied): `landscape`, `portrait` or `square`; missing while the video has not loaded its size |
| `data-view` | `view` | `library`, `library-search`, `library-album`, `library-artist`, `library-genre`, `library-year`, `library-track`, `home`, `now-playing`, `playlists`, `playlist`, `recently-played`, `smart-playlist`, `smart-playlist-edit` (state `create` or `edit`), `settings`, `equalizer`, `settings-pin`, `settings-scrobbling-lastfm`, `admin-library`, `admin-jobs`, `admin-cache`, `admin-transcoding`, `admin-backups`, `admin-themes`, `admin-users`, `admin-files`, `admin-track-files`, `admin-review`, `admin-review-track`, `admin-checks`, `recap`, `statistics`, `shared-recaps`, `shared-recap`, `not-found`; the recap pages also carry the state of their statistics (`idle`, `loading`, `ready`, `error`, `invalid`; on `shared-recap` `loading`, `ready`, `not-found`, `error`) |
| `data-kind` | `tile` | `album`, `artist`, `track`, `genre`, `year`, `playlist`, `smart-playlist`, `recently-played` |
| `data-kind` | `smart-rule` | Kind of the field of the rule: `text` (artist, album, genre), `number` (year, rating, play count), `duration`, `days` (last played, date added) or `flag` (favorite, audio file, music video) |
| `data-values` | `smart-rule`, `smart-rule-values` | Values the condition of the rule needs: `text`, `number`, `range` (from and to) or `none` |
| `data-kind` | `form` | `smart-playlist` for the editor of a smart playlist; the base widens it to 56rem |
| `data-field` | `field` (editor of a smart playlist) | `name`, `sort`, `direction` (segmented control), `limited` (checkbox) and `limit`; per rule `rule-field`, `rule-operator`, `rule-text`, `rule-number` and `rule-number-to` |
| `data-item` | `section`, `segmented-control` and `button` of the editor of a smart playlist | The `section`s `templates`, `rules` and `order`; the `segmented-control`s `match` (`segment`s `All`, `Any`) and `direction` (`Asc`, `Desc`); the `button`s of `smart-templates`, `remove-rule` in `smart-rule`, `add-rule`, `cancel` and `save` in `view-actions` |
| `data-kind` | `stage-view` | `cover`, `video`, `visualization`, `lyrics` |
| `data-kind` | `compact-player` | `window` (the floating window) or `inline` (in the window of the app) |
| `data-kind` | `context-menu` | `submenu` for the submenu of an entry, `visualization-presets` for the presets of the visualization, `visualization-color-schemes` for its color schemes |
| `data-placement` | `context-menu` | Where it opens: `point` (a context menu at the pointer or the element, the default), `above` (a menu of choices above the button that opens it, at least as wide as the button, at most as tall as the room above it and `--wmp-menu-list-max-height`) or `sheet` (on phones a sheet along the bottom edge of the screen, at most `--wmp-sheet-max-height` tall, over a `::backdrop` in `--wmp-backdrop-color`) |
| `data-words` | `lyrics` | `true` for synchronized lyrics with word timestamps |
| `data-index` | `lyrics-line` | Index of the line, starting at 0 |
| `data-index` | `playlist-cover-tile` | Position of the tile in the grid: `0` top start, `1` top end, `2` bottom start, `3` bottom end |
| `data-from` | `playlist-cover-slide` | Edge a new song cover enters its tile from: `start`, `end` (following the writing direction), `top` or `bottom`; missing on the slides shown from the start |
| `data-empty` | `lyrics-line` | `true` for a line without text, a part without singing |
| `data-kind` | `backup-item` | What created the export: `schedule` (the daily backup), `manual` or `migration` (before an update of the database) |
| `data-module` | `visualization` | The module that draws: `milkdrop`, `aforce` or `blackcap` |
| `data-intensity` | `visualization`, `player-visualization` | How strongly the visualization moves and reacts: `normal`, or `reduced` while the profile chose the reduced intensity or the device asks for reduced motion (`prefers-reduced-motion`); reduced also limits brightness changes further and slows Milkdrop down |
| `data-quality` | `visualization` | The quality level it draws with while it draws or pauses: `low`, `medium` or `high` (the level of the profile, or with the quality Auto the level the frame rate of the device allows); missing before it draws |
| `data-kind` | `equalizer-band` | `preamp` or `band` |
| `data-frequency` | `equalizer-band` with `data-kind="band"` | Center frequency of the band in Hz, `31`, `62`, `125`, `250`, `500`, `1000`, `2000`, `4000`, `8000`, `16000` |
| `data-section` | `view` (library) | `recently-added`, `favorites`, `artists`, `albums`, `songs`, `genres`, `years` |
| `data-value` | `rating` | Stars of the rating, `0` (none) to `5` |
| `data-column` | `list-column`, `list-cell` | `number`, `title`, `duration`, `artist`, `rating`, `favorite`; in playlists also `handle` (first column, only where entries can be moved), `album` and `detail` (in "Recently played" the time of the last play, in the favorites the time the song was marked) |
| `data-count` | `playlist-cover` | Number of song covers (1 to 12) |
| `data-count` | `pin-dots` | Number of entered digits (0 to 8) |
| `data-item` | `dialog`, `segmented-control`, `button` and `avatar-preset` of the avatar dialog | `avatar` (the `dialog`), `avatar-mode` (the `segmented-control` with the `segment`s `preset` and `upload`), the `button`s `choose-file`, `remove-avatar` and `save-avatar`, the id of an `avatar-preset` (`bubble`, `leaf`, `drop`, `sun`, `wave`, `cloud`, `star`, `fish`); on the settings page the `section` `avatar` with the `button` `change-avatar`, in `user-actions` the `button`s `edit`, `avatar` and `delete` |
| `data-field` | `form-note` (settings) | `avatar-status`: whether the profile shows a picture |
| `data-field` | `field` (avatar dialog) | `zoom` |
| `data-key` | `pin-key` | `0` to `9`, `delete`, `submit` |
| `data-key` | `shortcut-key` | The key: `mod` (Ctrl, Command on Apple devices), `alt`, `shift`, `space`, `arrowleft`, `arrowright`, `arrowup`, `arrowdown` or the character in lower case, for example `m` or `?` |
| `data-item` | `dialog`, `shortcut-group`, `shortcut-item` | `shortcuts` (the `dialog` of the overview of the keyboard shortcuts), the group of a `shortcut-group`, the id of a `shortcut-item` |
| `data-field` | `meta-item` | `album`, `artist`, `genre`, `year`, `tracks`, `albums`, `duration`, `number`, `disc`, `media`, `rating`, `kind` (on the page of a smart playlist); on the page of the files of a song also `track` (link to the song); on the page of a song of the review inbox `artist`, `album`, `review-status` (unreviewed, or when and by whom it was reviewed) and `track` |
| `data-field` | `field` (metadata editor) | `title`, `artist`, `album`, `album-artist`, `year`, `number`, `disc`, `genre` |
| `data-field` | `field` (settings) | `language`, `replay-gain` (segmented control), `equalizer` (checkbox), `crossfade`, `crossfade-albums` (checkbox), `autoplay` and `autoplay-video-only` (checkboxes), `video-ambient` (checkbox), `sleep-timer-fade`, `visualization-auto-change`, `visualization-quality` and `visualization-intensity` (segmented controls), `visualization-milkdrop-speed` |
| `data-field` | `field` (administration) | `role` in the dialog of a profile, `hardware-acceleration` on the transcoding page (segmented controls), `backup-time` and `backup-retention` on the backup page |
| `data-item` | `segmented-control` | The choice: `scope` (`segment`s `all-music`, `my-music`), `stage-view` (`cover`, `video`, `visualization`, `lyrics`), `player-view` (`cover`, `video`, `visualization`), `queue-pane-view` (`queue`, `lyrics`; in the docked `queue-pane`), `replay-gain` (`off`, `track`, `album`), `role` (`user`, `admin`), `hardware-acceleration` (`auto`, `off`), `visualization-module` (`milkdrop`, `aforce`, `blackcap`), `visualization-quality` (`auto`, `low`, `medium`, `high`), `visualization-intensity` (`normal`, `reduced`), `file-filter` (`all`, `choices`, `several-audio`, `several-video`, `several-covers`, `selected`, `missing`), `file-sort` (`path`, `title`, `file-count`), `cover-rule` (`inherit`, `album-first`, `track-first`), `cover-priority` (`album-first`, `track-first`), `review-filter` (`all`, `missing-tags`, `complete-tags`), `review-sort` (`first-seen-at`, `artist`, `album`, `title`), `issue-type` (`all`, `unreadable-file`, `missing-tags`, `unknown-album`, `missing-cover`, `possible-duplicate`, `video-without-audio` and `audio-without-video`), `issue-sort` (`path`, `artist`, `title`, `detected-at`), `recap-period` (`last-month`, `last-three-months`, `last-year`, `current-year`, `all-time`, `custom`), `recap-design` (`aurora`, `frost`, `prism`), `recap-picture-format` (`story`, `square`) |
| `data-item` | `segment` | The option, see `segmented-control` |
| `data-field` | `field` (equalizer) | `equalizer-preset` |
| `data-field` | `label` in `queue-pane-summary` | `count` (number of songs in the queue), `duration` (their total duration) |
| `data-field` | `stat-item` (settings) | `app-version` (version of the web app), `server-version` (version of the service) |
| `data-field` | `field` (video sync of a song) | `video-offset` |
| `data-field` | `field` (cover rule of an album or song, library settings) | `cover-rule` on the page of an album or song, `cover-priority` (the global rule) on the library page of the administration (segmented controls) |
| `data-field` | `field` (library settings) | `download-lyrics` (checkbox), `subsonic-downloads` (checkbox: Subsonic apps may download the original files) |
| `data-field` | `field` (online search in the library settings) | `online-metadata-enabled` (checkbox), `musicbrainz-url`, `cover-art-archive-url`, `musicbrainz-interval` |
| `data-owner` | `online-search` | `track` or `album` |
| `data-item` | `dialog`, `button`, `link`, `form-note`, `form` and `section` of the online search | `online-search` (the `dialog`), `search-online` (the `button` in `detail-actions` of a song or album for admins, icon slot `search-online`), `apply` and `search-again` (`button`s), `online-settings` (the `link` to the library settings), `online-source` (the `form-note` on the sources), `online-metadata-settings` (the `form`) and `online-metadata` (the `section` on the library page of the administration) |
| `data-item` | `form-note` (lyrics of a song) | `lyrics-status`: which lyrics the song has; state `found` or `none`. `lyrics-lookup`: when the last online lookup found no lyrics (only then); state `not-found` or `instrumental` |
| `data-item` | `form-note` (settings) | `cast-effects`: ReplayGain, equalizer and crossfade rest while a casting device plays |
| `data-field` | `field` (PIN page of the settings) | `pin`, `pin-confirmation` |
| `data-field` | `form-note` (settings and PIN page) | `pin-status`: whether the profile has a PIN |
| `data-item` | `scrobble-item`, `button` in `scrobble-actions`, `dialog` and `link` of the scrobbling services | `last-fm` and `listen-brainz` (the `scrobble-item`), `connect` and `disconnect` (the `button`s), `scrobbling-token` (the `dialog` for the token of ListenBrainz), `listenbrainz-token` (the `link` to the token page of ListenBrainz), `retry` (`button` in `view-actions` after a failed load); on the Last.fm page the `link` `back-to-settings` |
| `data-field` | `label` in `scrobble-meta`, `form-note` in `scrobble-item`, `field` of the token dialog, `form-note` of the Last.fm page | `account`, `last-scrobbled` and `queued` (the `label`s); `authorization-failed` and `unavailable` (the hints); `scrobbling-token` (the `field`); `lastfm-status` |
| `data-field` | `field` and `stat-item` of the Subsonic access (settings) | `subsonic-enabled` (checkbox); the `stat-item`s `subsonic-server` (address of the server), `subsonic-user-name`, `subsonic-password-state` (whether and when the app password was created) and, right after a new app password was created, `subsonic-new-password` (the password, shown once) |
| `data-item` | `stat-list`, `form-note` and `button` of the Subsonic access (settings) | `subsonic-sign-in` (the `stat-list`), `subsonic-password-once` (the hint that the new password is shown once, `role="status"`), the `button`s `create-subsonic-password`, `revoke-subsonic-password` and `copy-subsonic-password` in `view-actions`, `retry` after a failed load |
| `data-item` | `menu-item`, `admin-nav-item`, `stat-item`, `job-item`, `section`, `shelf`, `button` in `view-actions`, `detail-actions`, `shelf-actions`, `queue`, `queue-item-actions`, `autoplay`, `autoplay-item-actions`, `transcode-actions` and `equalizer-controls`, `link` on the settings page | Id of the entry: the menu entry (in the profile menu `settings`, `administration`, `switch-profile`; in the menu of one or more songs `play`, `play-next`, `add-to-queue`, `add-to-playlist`, `favorite`, `rate` (a submenu with the `menuitemradio` entries `rate-0`, no rating, to `rate-5`), `details`, `album`, `artist`, `watch-video`, for admins `edit-metadata` and `show-files`, and on the page of a playlist `move-up`, `move-down` and `remove-from-playlist`; in the menu of an album `play`, `shuffle`, `add-to-queue`, `add-to-playlist`, `favorite`, `artist`; of an artist `play`, `shuffle`, `favorite`; of a playlist `play`, `shuffle`, `rename`, `change-cover`, `delete`; of a smart playlist `play`, `shuffle`, `edit-rules`, `convert`, `delete`; of a queue entry `play-next`, `move-up`, `move-down`, `remove`; of Now Playing `view-cover`, `view-video`, `view-visualization`, `view-lyrics` (`menuitemradio`) and `fullscreen`; entries that do not apply stay in the menu, disabled; in the view menu of `view-switch` `view-grouped` and `view-list` (`menuitemradio`); in the sort options `sort-album-artist`, `sort-album`, `sort-title`, `sort-artist`, `sort-name`, `sort-duration`, `sort-year`, `sort-genre`, `sort-first-seen-at`, `sort-track-count`, `sort-rating`, `sort-relevance`, `direction-asc`, `direction-desc`), the page (`admin-library`, `admin-files`, `admin-review`, `admin-checks`, `admin-jobs`, `admin-cache`, `admin-transcoding`, `admin-backups`, `admin-themes`, `admin-users`), the statistic (for example `filesFound`, `tracksTotal`, `duration`; of the cache `covers`, `transcodes`, `entries`; of the transcoding `hardware`, `ffmpeg`, `cache`, `running-jobs`, `encoder`, `device`; of the backups `status`, `last-run`, `next-run`, `schedule`, `backups`, `size`; of the library check `status`, `checked-at`, `tracks`, `problems`, `hints`; of the lyrics lookup `not-found`, `instrumental`, `wait`), the job (`library-scan`, `cover-cache`, `database-backup`, `lyrics-download`, `library-check`) or the section of the search, of an artist and of the favorites (`artists`, `albums`, `tracks`), the row of the home page (`playlists`, `recently-played`, `recently-added`, `favorites`, `top-rated`), of a song (`files`, for admins `cover-rule` and `lyrics`; for songs with a video also `video-sync`), of an album (for admins `cover-rule`), of the file selection (`songs`; on the page of a song `audio`, `video`, `cover`), of the library page of the administration (`media-sources`, `online-metadata`, `lyrics-lookups`), of the cache page (`usage`, `settings`), of the settings page (`resume`, `pin`, `scrobbling`, `subsonic`, `about`) of the transcoding page (`status`, `jobs`, `settings`) of the backup page (`status`, `backups`, `settings`) and of the theme page (`theme-upload`, `themes`), the action of a page (`play`, `shuffle`, `favorite`, `edit-metadata`; on the page of a song `save-video-offset`, `download-lyrics` and `forget-lyrics-lookup`; of the lyrics lookup on the library page `forget-lyrics-lookups`; on the page of an album or song `save-cover-rule`; of a row of the home page `play`; of a playlist also `rename`, `cover`, `remove-cover`, `delete`; of a smart playlist `edit-rules`, `convert`, `delete`; on the playlist overview `create-playlist` and `create-smart-playlist`; on the theme page `upload-theme` and per theme `replace` and `delete`) of the queue (`clear-queue`; per entry `move-up`, `move-down`, `remove`), of autoplay (`turn-off-autoplay`; per song `remove`) or of a running transcode (`cancel`); in the menu of the sleep timer `sleep-off`, `sleep-15`, `sleep-30`, `sleep-45`, `sleep-60`, `sleep-90`, `sleep-120`, `sleep-end-of-track`, `sleep-end-of-queue`; the `button`s `power` and `reset` of `equalizer-controls`; the `link`s `open-equalizer` and `open-pin` on the settings page; the `link` `theme-template` (the download of the template) on the theme page; the `link` `choose-files` in the section `files` of a song and the `button` `reset-file-selection` on the page of the files of a song; the `button`s `save-pin`, `remove-pin` and `confirm-remove-pin` of the PIN page, `clear-playback-state` in the section `resume` of the settings page and `confirm-clear-playback-state` of its `dialog` `clear-playback-state`, `back` of `pin-entry-actions`, `reset-pin` of `user-actions` and `confirm-reset-pin` of its dialog; the `section`s `check-status` and `issues` of the library check, the `button` `run-check` of `library-check-status`, the `link`s `related-track`, `edit-metadata`, `search-online` and `show-files` of a problem; the `section`s `review` (the review inbox) and `metadata` (the editor on the page of a song of the inbox), the `button` `mark-selected-reviewed` of `review-bulk`, `save-and-review`, `mark-reviewed` and `skip` of `review-actions` and `previous` and `next` of `review-nav` |

| `data-recap-design` | `recap-story` | Design of the story: `aurora` (dark), `frost` (light) or `prism` (colorful), see [Recap and statistics](#recap-and-statistics) |
| `data-decoration` | `recap-story` | Decoration of the design: `glow`, `glass` or `bands` |
| `data-kind` | `recap-slide` | The section, see `recap-slide` |
| `data-rank` | `recap-ranking-item` | Place of the entry, 1 to 5 |
| `data-column` | `stats-table-heading`, `stats-table-cell` | `rank`, `title`, `artist`, `songs`, `first` (first play of a discovered artist), `plays`, `time` |
| `data-item` | `section`, `button`, `form-note` and `empty-state` of the recap pages | The `section`s `recap-design`, `recap-share`, `totals`, `timeline`, `hours`, `top-tracks`, `top-artists`, `top-albums`, `top-genres`, `new-artists`, `received` and `own`; the `button`s `save-picture`, `save-video`, `cancel-video`, `stop-sharing` and `retry`; the `form-note`s `video-unsupported`, `received-empty` and `own-empty`; the `empty-state`s `recap-error`, `statistics-error`, `statistics-empty`, `shared-recaps-error`, `shared-recap-missing` and `shared-recap-error` |
| `data-field` | `field` (recap) | `from` and `to` (custom period), `share-with-profiles` (checkbox) |
| `data-item` | `stat-item` (statistics) | `listening-time`, `plays`, `tracks`, `artists`, `albums`, `genres`, `new-artists`, `active-days` |

## Variables

All variables are set on `:root` with neutral defaults and can be overridden by the theme, also for single areas (for example `--wmp-icon-fill` inside `[data-part='header']`).

Graphics and backgrounds take any value of the CSS `background` shorthand: a color, a gradient or a root-absolute `url()` with position and size, also several layers. Example: `url('/themes/midnight/images/glass-button.svg') center / 100% 100% no-repeat`.

### Colors

| Variable | Default |
|---|---|
| `--wmp-color-accent` | `#1f6fd6` |
| `--wmp-color-accent-contrast` | `#ffffff` |
| `--wmp-color-focus` | `#1f6fd6` |
| `--wmp-color-text` | `#1b1d21` |
| `--wmp-color-text-muted` | `#5b6270` |
| `--wmp-color-text-disabled` | `#9aa0aa` |
| `--wmp-color-surface` | `#ffffff` |
| `--wmp-color-surface-raised` | `#f1f3f6` |
| `--wmp-color-border` | `#d3d8df` |
| `--wmp-color-hover` | `#e7f0fb` |
| `--wmp-color-selection` | `#cfe2fa` |
| `--wmp-color-selection-text` | `#1b1d21` |
| `--wmp-color-success` | `#2e9d4f` |
| `--wmp-color-warning` | `#c98a12` |
| `--wmp-color-danger` | `#c83737` |
| `--wmp-favorite-color` | `#d6336c` |
| `--wmp-rating-color` | `#e0a100` |
| `--wmp-backdrop-color` | `rgb(0 0 0 / 45%)` |
| `--wmp-theme-color` | `var(--wmp-color-surface-raised)` |

`--wmp-theme-color` is a plain color (no gradient or image): the color of the status bar and of the bars of the browser (`<meta name="theme-color">`), of the safe areas around the app and of the root element. It should continue the top of the header (see [Responsive layout](#responsive-layout)).

### Areas

Text color and background of each area.

| Variable | Default |
|---|---|
| `--wmp-header-color` | `var(--wmp-color-text)` |
| `--wmp-header-background` | `var(--wmp-color-surface-raised)` |
| `--wmp-toolbar-color` | `var(--wmp-color-text)` |
| `--wmp-toolbar-background` | `var(--wmp-color-surface)` |
| `--wmp-sidebar-color` | `var(--wmp-color-text)` |
| `--wmp-sidebar-background` | `var(--wmp-color-surface)` |
| `--wmp-content-color` | `var(--wmp-color-text)` |
| `--wmp-content-background` | `var(--wmp-color-surface)` |
| `--wmp-transport-color` | `var(--wmp-color-text)` |
| `--wmp-transport-background` | `var(--wmp-color-surface-raised)` |
| `--wmp-compact-player-color` | `var(--wmp-transport-color)` |
| `--wmp-compact-player-background` | `var(--wmp-transport-background)` |
| `--wmp-bottom-nav-color` | `var(--wmp-color-text)` |
| `--wmp-bottom-nav-background` | `var(--wmp-color-surface-raised)` |
| `--wmp-dialog-color` | `var(--wmp-color-text)` |
| `--wmp-dialog-background` | `var(--wmp-color-surface)` |
| `--wmp-menu-color` | `var(--wmp-color-text)` |
| `--wmp-menu-background` | `var(--wmp-color-surface)` |
| `--wmp-key-color` | `var(--wmp-dialog-color)` |
| `--wmp-key-background` | `color-mix(in srgb, var(--wmp-dialog-color) 7%, transparent)` |
| `--wmp-key-border-color` | `color-mix(in srgb, var(--wmp-dialog-color) 28%, transparent)` |
| `--wmp-profile-select-color` | `var(--wmp-color-text)` |
| `--wmp-profile-select-background` | `var(--wmp-color-surface-raised)` |

### Profiles

| Variable | Default | Used for |
|---|---|---|
| `--wmp-avatar-color` | `#ffffff` | Text color of the initials on `avatar` |
| `--wmp-profile-secondary-opacity` | `0.7` | Opacity of the built-in admin `profile-tile` with the state `secondary`; its label keeps 4.5:1 at 0.7 on the usual colors |

The background of `avatar` is the color of the profile, `--wmp-profile-color` (see [Runtime variables](#runtime-variables)). The app sets it on the `avatar` element itself, so themes use it in rules for `[data-part='avatar']`, for example `background: linear-gradient(rgb(255 255 255 / 40%), transparent 55%), var(--wmp-profile-color);`. A variable on `:root` that refers to it would not work.

### Graphics

| Variable | Default | Used for |
|---|---|---|
| `--wmp-image-logo` | `url('/logo/wavemp-logo.svg') center / contain no-repeat`, in the dark variant (`data-theme-variant="dark"`) `url('/logo/wavemp-logo-dark.svg') center / contain no-repeat` | `logo-image`; the WaveMP logo, which the app serves under `/logo/wavemp-logo.svg`, and its dark variant under `/logo/wavemp-logo-dark.svg` |
| `--wmp-image-cover-placeholder` | `var(--wmp-color-surface-raised)` | `cover` without image |
| `--wmp-progress-track-background` | `var(--wmp-color-border)` | `progress-track`, `meter-track` |
| `--wmp-progress-fill-background` | `var(--wmp-color-accent)` | `progress-fill`, `meter-fill` |
| `--wmp-image-default-button` | `var(--wmp-color-surface)` | Buttons with `data-variant="default"` |
| `--wmp-image-default-button-hover` | `var(--wmp-color-hover)` | |
| `--wmp-image-default-button-active` | `var(--wmp-color-selection)` | Also `aria-pressed="true"` |
| `--wmp-image-default-button-disabled` | `var(--wmp-color-surface-raised)` | |
| `--wmp-image-primary-button` | `var(--wmp-color-accent)` | Buttons with `data-variant="primary"` |
| `--wmp-image-primary-button-hover` | `color-mix(in srgb, var(--wmp-color-accent), #ffffff 15%)` | |
| `--wmp-image-primary-button-active` | `color-mix(in srgb, var(--wmp-color-accent), #000000 15%)` | |
| `--wmp-image-primary-button-disabled` | `var(--wmp-color-border)` | |
| `--wmp-image-icon-button` | `transparent` | Buttons with `data-variant="icon"` |
| `--wmp-image-icon-button-hover` | `var(--wmp-color-hover)` | |
| `--wmp-image-icon-button-active` | `var(--wmp-color-selection)` | |
| `--wmp-image-icon-button-disabled` | `transparent` | |
| `--wmp-image-round-button` | `var(--wmp-color-surface)` | Buttons with `data-variant="round"` (round glass buttons) |
| `--wmp-image-round-button-hover` | `var(--wmp-color-hover)` | |
| `--wmp-image-round-button-active` | `var(--wmp-color-selection)` | |
| `--wmp-image-round-button-disabled` | `var(--wmp-color-surface-raised)` | |
| `--wmp-image-play-button` | `var(--wmp-color-accent)` | `btn-play` |
| `--wmp-image-play-button-hover` | `color-mix(in srgb, var(--wmp-color-accent), #ffffff 15%)` | |
| `--wmp-image-play-button-active` | `color-mix(in srgb, var(--wmp-color-accent), #000000 15%)` | |
| `--wmp-image-play-button-disabled` | `var(--wmp-color-border)` | |
| `--wmp-image-transport-controls` | `transparent` | `transport-controls` |
| `--wmp-image-tab-active` | `var(--wmp-color-selection)` | Active `tab` |
| `--wmp-image-tab-hover` | `var(--wmp-color-hover)` | `tab` on hover |
| `--wmp-image-slider-thumb` | `var(--wmp-color-accent)` | `slider-thumb` |
| `--wmp-slider-track-background` | `var(--wmp-color-border)` | `slider-track` |
| `--wmp-slider-fill-background` | `var(--wmp-color-accent)` | `slider-fill` |

### Icons

| Variable | Default | Used for |
|---|---|---|
| `--wmp-icon-fill` | `currentColor` | Fill of the icon shape: a color or a gradient |
| `--wmp-icon-size` | `1.25rem` | Size of icons |
| `--wmp-icon-size-s` | `1rem` | Small icons (tree toggles, search, separators) |
| `--wmp-icon-size-l` | `2rem` | Large icons |

The graphic of each icon slot is a variable as well, see [Icons](#icons).

### Spacing and radii

| Variable | Default |
|---|---|
| `--wmp-space-xs` | `0.25rem` |
| `--wmp-space-s` | `0.5rem` |
| `--wmp-space-m` | `0.75rem` |
| `--wmp-space-l` | `1rem` |
| `--wmp-space-xl` | `1.5rem` |
| `--wmp-radius-s` | `0.25rem` |
| `--wmp-radius-m` | `0.5rem` |
| `--wmp-radius-l` | `0.75rem` |
| `--wmp-radius-round` | `999px` |

### Fonts

| Variable | Default |
|---|---|
| `--wmp-font-family` | `system-ui, sans-serif` |
| `--wmp-font-size-xs` | `0.75rem` |
| `--wmp-font-size-s` | `0.8125rem` |
| `--wmp-font-size-m` | `0.9375rem` |
| `--wmp-font-size-l` | `1.125rem` |
| `--wmp-font-size-xl` | `1.5rem` |
| `--wmp-font-weight-normal` | `400` |
| `--wmp-font-weight-bold` | `600` |
| `--wmp-line-height` | `1.4` |

### Sizes

| Variable | Default | Used for |
|---|---|---|
| `--wmp-header-height` | `3rem` | Minimum height of `header` |
| `--wmp-toolbar-height` | `2.75rem` | Minimum height of `toolbar` |
| `--wmp-sidebar-width` | `15rem` | Width of the sidebar column (desktop) |
| `--wmp-sidebar-collapsed-width` | `3.25rem` | Width of the sidebar column while the sidebar is collapsed (desktop) |
| `--wmp-queue-pane-width` | `18rem` | Width of the column of the docked play queue (desktop, [Layout options](#layout-options)) |
| `--wmp-transport-height` | `4.5rem` | Minimum height of `transport` (desktop) |
| `--wmp-seek-height` | `0.25rem` | Height of the progress line (mini player) |
| `--wmp-bottom-nav-height` | `3.5rem` | Minimum height of `bottom-nav-item`; an entry with a label of two lines is taller |
| `--wmp-mini-player-height` | `3.5rem` | Minimum height of the mini player (mobile) |
| `--wmp-compact-player-width` | `20rem` | Width of `compact-player` in the window of the app (`data-kind="inline"`) |
| `--wmp-compact-player-height` | `28rem` | Height of `compact-player` in the window of the app (`data-kind="inline"`) |
| `--wmp-button-height` | `2rem` | Minimum height of buttons |
| `--wmp-field-height` | `2rem` | Minimum height of inputs |
| `--wmp-round-button-size` | `2rem` | Size of round buttons |
| `--wmp-touch-target-size` | `2.75rem` | Minimum size of touch targets below 64rem (see [Touch targets](#touch-targets)); values below `2.75rem` count as `2.75rem` |
| `--wmp-menu-list-max-width` | `24rem` | Largest width of a `context-menu` that opens above its button (`data-placement="above"`), for example the presets of the visualization |
| `--wmp-menu-list-max-height` | `26rem` | Largest height of a `context-menu` that opens above its button; longer lists scroll |
| `--wmp-sheet-max-height` | `60dvh` | Largest height of a `context-menu` shown as a sheet on phones (`data-placement="sheet"`); its `menu-item`s are as high as a touch target |
| `--wmp-play-button-size` | `3rem` | Size of `btn-play` |
| `--wmp-slider-thumb-size` | `0.875rem` | Size of `slider-thumb` |
| `--wmp-equalizer-slider-height` | `10rem` | Height of the vertical `slider`s in `equalizer-band` |
| `--wmp-cover-size` | `8rem` | Size of covers in the list |
| `--wmp-cover-size-mini` | `2.5rem` | Size of the cover in `now-playing-mini` |
| `--wmp-marquee-fade-size` | `1.5rem` | How wide the cut-off end (and, at the end of the scrolling, the cut-off start) of a `marquee` fades out |
| `--wmp-cover-size-queue` | `2.5rem` | Size of the covers in `queue-item-info` and `autoplay-item-info` |
| `--wmp-cover-size-queue-pane` | `3.5rem` | Size of the cover of the current song in `queue-pane-header` |
| `--wmp-cover-size-l` | `12rem` | Size of the cover in `detail-header` |
| `--wmp-tile-width` | `9rem` | Smallest width of a column of tiles |
| `--wmp-list-row-height` | `2rem` | Minimum height of `list-row` |
| `--wmp-list-columns` | `3rem minmax(0, 2fr) 4.5rem minmax(0, 1.5fr) 6.5rem minmax(2rem, auto)` | Columns of the `list` that `list-header` and `list-row` share (number, title, duration, artist, rating, favorite). The favorite column grows with `minmax(…, auto)` where it also holds `btn-item-menu` (touch screens) |
| `--wmp-list-columns-playlist` | `2rem 3rem minmax(0, 2fr) 4.5rem minmax(0, 1.5fr) minmax(0, 1.5fr) 6.5rem minmax(2rem, auto)` | `--wmp-list-columns` of the list in `data-view="playlist"` (handle, number, title, duration, artist, album, rating, favorite) |
| `--wmp-list-columns-recently-played` | `3rem minmax(0, 2fr) 4.5rem minmax(0, 1.5fr) minmax(0, 1.5fr) 10rem 6.5rem minmax(2rem, auto)` | `--wmp-list-columns` of the list in `data-view="recently-played"` and of the songs in `data-section="favorites"` (number, title, duration, artist, album, detail, rating, favorite) |
| `--wmp-list-columns-smart-playlist` | `3rem minmax(0, 2fr) 4.5rem minmax(0, 1.5fr) minmax(0, 1.5fr) 6.5rem minmax(2rem, auto)` | `--wmp-list-columns` of the list in `data-view="smart-playlist"` (number, title, duration, artist, album, rating, favorite) |
| `--wmp-shelf-tile-width` | `var(--wmp-tile-width)` | Width of a tile in a `shelf` of the home page |
| `--wmp-list-group-info-width` | `11rem` | Width of `list-group-info` (desktop) |
| `--wmp-list-badge-width` | `var(--wmp-icon-size-s)` | Width of the column at the start of the `list-column` and the `list-cell`s `title` that holds the video `badge` while the `list` has the state `has-video` |
| `--wmp-avatar-size` | `2rem` | Size of `avatar` |
| `--wmp-avatar-size-l` | `6rem` | Size of `avatar` in `profile-tile` |
| `--wmp-swatch-size` | `2rem` | Size of `color-swatch` |
| `--wmp-progress-height` | `0.5rem` | Height of `progress-track` and `meter-track` |
| `--wmp-key-size` | `1.625rem` | Height and smallest width of `shortcut-key` |

### Now Playing

| Variable | Default | Used for |
|---|---|---|
| `--wmp-stage-transition-duration` | `0.6s` | Duration of the fade between the views of the stage (`stage-view`); the base leaves it out for `prefers-reduced-motion: reduce` |
| `--wmp-stage-view-background` | `#000000` | Background of the video, visualization and lyrics views, of the `video-stage`, of the `visualization` and of the `lyrics`, so also of the free areas around the picture of the `video` (the `video-backdrop` lies above it) |
| `--wmp-video-ambient-intensity` | `0.75` | Opacity of the `video-ambient`, so the strength of the ambient light around a video |
| `--wmp-video-ambient-blur` | `6cqmax` | Blur of the `video-ambient-canvas`es; larger values make the light softer. The `video-ambient` is a size container, so `cqmax` (a hundredth of the larger side of the stage) lets the blur grow with the stage, also in full screen |
| `--wmp-video-ambient-saturation` | `1.3` | Saturation of the `video-ambient-canvas`es (`1` keeps the colors of the picture) |
| `--wmp-video-ambient-scale` | `1.2` | Enlargement of the `video-ambient-canvas`es, so the blurred edges lie outside the stage |
| `--wmp-video-ambient-transition-duration` | `0.25s` | Duration of the fade of a new picture of the ambient light; keep it at most `0.25s`, the time between two pictures. The base leaves it out for `prefers-reduced-motion: reduce` |
| `--wmp-video-ambient-fade-duration` | `0.6s` | Duration of the fade of the `video-ambient` when it appears or disappears (a new video, switched on or off); the base leaves it out for `prefers-reduced-motion: reduce` |
| `--wmp-lyrics-color` | `#ffffff` | Text of the active `lyrics-line`, of sung `lyrics-word`s and of static lyrics |
| `--wmp-lyrics-muted-color` | `rgb(255 255 255 / 0.45)` | Text of the other lines and of words not sung yet |
| `--wmp-lyrics-active-scale` | `1.12` | Scale of the active `lyrics-line` |
| `--wmp-lyrics-transition-duration` | `0.35s` | Duration of the change of color and size of the lines; the base leaves it out for `prefers-reduced-motion: reduce` |

On mobile, the base sets touch-friendly values (`2.75rem`) for `--wmp-button-height`, `--wmp-round-button-size`, `--wmp-field-height` and `--wmp-swatch-size` on `app`, and its own `--wmp-list-columns` on `list` (in playlists with the handle); the columns `artist`, `album`, `detail` and `rating` are left out there.

### Recap and statistics

The story of the recap takes its colors from the variables of its design. The defaults are the design Aurora; the base sets the other designs on `[data-recap-design='frost']` and `[data-recap-design='prism']`, and a theme can change any design the same way. The pictures and videos of the recap read these variables from the shown story, so they look like the page.

| Variable | Default | Used for |
|---|---|---|
| `--wmp-recap-background-start` | `#0b1636` | First color of the background gradient of `recap-story` |
| `--wmp-recap-background-end` | `#2a1660` | Last color of the background gradient |
| `--wmp-recap-glow` | `#5b43d9` | Color of the decoration (`recap-backdrop`) |
| `--wmp-recap-accent` | `#5ee8d2` | `recap-figure`, `recap-rank` and the bars of the `chart` in the story |
| `--wmp-recap-text` | `#f4f7ff` | Text of the story and the progress |
| `--wmp-recap-text-muted` | `#c1c9ea` | `recap-figure-label`, `recap-caption`, subtitles, values and labels |
| `--wmp-recap-surface` | `rgb(255 255 255 / 0.12)` | Covers without picture and the pane of the decoration `glass` |
| `--wmp-recap-font-family` | `var(--wmp-font-family)` | Font of the story and of its pictures and videos |
| `--wmp-recap-story-width` | `26rem` | Largest width of `recap-story`; it also fits the height of the window |
| `--wmp-recap-radius` | `1.25rem` | Corner radius of `recap-story` |
| `--wmp-recap-shadow` | `0 1rem 2.5rem rgb(0 0 0 / 0.28)` | Shadow of `recap-story` |
| `--wmp-recap-transition-duration` | `0.6s` | Duration of the entrance of a section; the base leaves the entrance out for `prefers-reduced-motion: reduce` |
| `--wmp-chart-height` | `12rem` | Height of the bars of a `chart` on the statistics page |
| `--wmp-chart-bar-color` | `var(--wmp-color-accent)` | Color of the bars of a `chart` on the statistics page |

### Motion

The base animates the interface subtly and fast, with the durations and easings of these variables. What answers an action moves from where it comes from; what only appears fades in:

| Interaction | What the base does | Duration and easing |
|---|---|---|
| Hover and press | Background color, border color, text color, shadow and opacity of buttons (`data-variant`), `tab`, `bottom-nav-item`, `admin-nav-item`, `recap-nav-item`, `tree-item`, `menu-item`, `list-row`, `queue-item`, `tile`, `profile-tile`, `theme-option`, `file-choice`, `online-match`, `color-swatch` and `avatar-preset` fade. Pressed buttons (not `segment`s and checkboxes), `tile`s and `profile-tile`s shrink to `--wmp-motion-press-scale` | fast, standard |
| Segmented controls, tabs and the other groups of [Active element of a group](#active-element-of-a-group) | The `indicator` slides to the new active entry (`left`, `top`, `inline-size`, `block-size`, `opacity`) while the person works with the group (`:hover`, `:focus-within`, `:active`); the first measuring and changes of the layout put it in place at once. A theme only has to show the `indicator` | medium, standard |
| Menus (`context-menu`, also submenus) | Grow out of their top left corner, the point they open at (a menu above its button out of its bottom left corner), from `--wmp-motion-scale` and fade in; a sheet slides up from the bottom edge instead (with reduced motion it only fades); they fade out faster and take no pointer while they do | in: medium, enter; out: fast, exit |
| Dialogs (`dialog` and its `::backdrop`) | Grow out of the middle from `--wmp-motion-scale` and fade in, the backdrop fades; both fade out | in: slow, enter; out: medium, exit |
| Side navigation | On phones the drawer (`sidebar`) slides in from its edge and the `sidebar-backdrop` fades; on the desktop the sidebar column narrows and widens when the person uses `btn-sidebar-toggle` (a collapsed state loaded at the start applies at once) | slow; drawer in: enter, out: exit |
| Pages | The page (`[data-view]` in `content`) fades in when it opens, also when it comes back with back or forward | slow, standard |
| Entries of lists | `list-row`, `tile`, `queue-item`, `tree-item` and `profile-tile` fade in when they appear: loaded, added, loaded more or in a group that opens | medium, standard |
| Full-screen player (mobile) | Follows the finger while it is swiped down (`dragging`), then snaps back (`settling`) or leaves the screen at the speed of the finger (`dismissing`) with `translate` and `--wmp-swipe-duration`; `player-view-switch` fades in | snapping back: standard; leaving: linear; switch: medium, enter |
| Long titles (`marquee`) | A title or artist too long for its place scrolls to its end and back to its start, see `marquee` | `--wmp-marquee-duration`, linear |
| Playlist covers (`playlist-cover`) | By themselves, calmly and one tile at a time: a new song cover slides into a tile of the grid from its outer edge, over the shown one; with reduced motion it crossfades, see `playlist-cover-slide` | `--wmp-playlist-cover-slide-duration` and `--wmp-playlist-cover-slide-easing`; reduced: `--wmp-playlist-cover-fade-duration` |

| Variable | Default | Used for |
|---|---|---|
| `--wmp-motion-duration-fast` | `120ms` | Hover and press, menus closing |
| `--wmp-motion-duration-medium` | `180ms` | The `indicator`, menus opening, dialogs closing, entries of lists |
| `--wmp-motion-duration-slow` | `250ms` | Dialogs opening, the side navigation, pages |
| `--wmp-motion-duration-fade` | `100ms` | The fades that replace the movement for `prefers-reduced-motion: reduce` |
| `--wmp-motion-easing-standard` | `cubic-bezier(0.2, 0, 0, 1)` | Changes of something that stays: hover, press, the `indicator`, pages, lists |
| `--wmp-motion-easing-enter` | `cubic-bezier(0, 0, 0, 1)` | Something that opens: menus, dialogs, the drawer |
| `--wmp-motion-easing-exit` | `cubic-bezier(0.3, 0, 1, 1)` | Something that closes |
| `--wmp-motion-scale` | `0.96` | Scale a menu or dialog grows from and shrinks to |
| `--wmp-motion-press-scale` | `0.97` | Scale of a pressed button, `tile` or `profile-tile` |
| `--wmp-playlist-cover-gap` | `0px` | Space between the tiles of a `playlist-cover` |
| `--wmp-playlist-cover-slide-duration` | `1.2s` | How long a new song cover slides into its tile; calm on purpose, as nobody waits for it |
| `--wmp-playlist-cover-slide-easing` | `cubic-bezier(0.33, 0, 0, 1)` | Easing of the slide: fast at the edge, settling softly |
| `--wmp-playlist-cover-fade-duration` | `1.2s` | How long the new song cover fades in with `prefers-reduced-motion: reduce` (the base keeps this fade, as it keeps the short fades of menus and dialogs) |

- **Tuning:** set the variables on `:root`, for example slower menus and dialogs with `--wmp-motion-duration-medium: 220ms`, or a springy easing with `--wmp-motion-easing-enter: cubic-bezier(0.3, 1.35, 0.5, 1)`. Keep the durations short (roughly 120 to 250 ms), so the interface never waits for its animations.
- **Switching off:** `--wmp-motion-duration-fast`, `-medium` and `-slow` of `0s` switch the motion of the base off; `--wmp-motion-press-scale: 1` keeps pressed elements in place.
- **Own transitions:** the base sets `transition-property`, `transition-duration` and `transition-timing-function` (and `animation` for pages and lists); a `transition` or `animation` of the theme on the same part replaces them. Use the variables in them as well, so the theme stays consistent, and keep the properties of the base the theme still wants, for example `scale` for the press of buttons and tiles. A theme that sets `opacity` or `scale` on `context-menu` or `dialog` should do so only while they are open (`:popover-open`, `[open]`), otherwise it hides their fade.
- **Reduced motion:** with `prefers-reduced-motion: reduce` nothing moves or grows: the base sets `--wmp-motion-scale` and `--wmp-motion-press-scale` to `1` with a priority no theme overrides, ends every transition and animation at once and keeps only short fades of `--wmp-motion-duration-fade` for menus, dialogs, the drawer (which fades instead of sliding) and pages. Movements of the theme itself need a `@media (prefers-reduced-motion: reduce)` rule of the theme as well, as in all shipped themes.
- The screenshots of the [regression](#regression) are taken with reduced motion and with animations stopped, so the motion never shows up in them; the tiles of playlist covers do not change while they are taken.

### Runtime variables

The app sets these variables on single elements. Themes can read them, but never set them.

| Variable | Set on | Value |
|---|---|---|
| `--wmp-slider-value` | `slider` | Position of the value between 0 and 1 |
| `--wmp-tree-level` | `tree-item` | Level of the node, starting at 1 |
| `--wmp-menu-x` | `context-menu` | Horizontal position in pixels |
| `--wmp-menu-y` | `context-menu` | Vertical position in pixels |
| `--wmp-menu-max-height` | `context-menu` with `data-placement="above"` | The room above the button that opens it, in pixels: the most the menu may take |
| `--wmp-menu-anchor-width` | `context-menu` with `data-placement="above"` | Width of the button that opens it, in pixels |
| `--wmp-profile-color` | `avatar` | Color of the profile as `#rrggbb` |
| `--wmp-swatch-color` | `color-swatch` | Color of the swatch as `#rrggbb` |
| `--wmp-progress-value` | `progress`, `recap-progress-segment` | Share of the work done between 0 and 1 (0 while indeterminate); on a `recap-progress-segment` the share of the time of its section that has run |
| `--wmp-meter-value` | `meter` | Share of the range between 0 and 1 |
| `--wmp-bar-value` | `chart-bar` | Height of the bar as share of the highest bar of its chart, between 0 and 1 |
| `--wmp-agreement` | `online-match` | Agreement of the match with the song or album between 0 and 1 |
| `--wmp-video-aspect-ratio` | `video-stage` | Width divided by the height of the picture of the video, for example `1.7778` or `0.5625`; missing while the video has not loaded its size |
| `--wmp-video-ambient-color` | `video-ambient` | Average color of the picture of the video, for example `rgb(31 92 160)`, read once per video while the `video-ambient` is `static` (reduced motion); missing in the other states and without a picture |
| `--wmp-line-progress` | the active `lyrics-line` | Share of the line that has passed, between 0 and 1, updated with every frame while the music plays |
| `--wmp-word-progress` | the active `lyrics-word` | Share of the word that has passed, between 0 and 1, updated with every frame while the music plays |
| `--wmp-item-count` | `segmented-control`, `theme-picker`, `tabs`, `bottom-nav`, `admin-nav`, `recap-nav`, `tree`, `color-picker`, `avatar-presets`, `rating`, `playlist-cover`, `lyrics`, `online-matches` | Number of entries of the group: `segment`s, `theme-option`s, `tab`s, `bottom-nav-item`s, `admin-nav-item`s, visible `tree-item`s, `color-swatch`es, `avatar-preset`s, `rating-star`s, song covers of a `playlist-cover`, `lyrics-line`s or `online-match`es |
| `--wmp-active-index` | the same, except `playlist-cover` | Index of the active entry, starting at 0: the checked `segment` or `theme-option`, the `tab`, `bottom-nav-item` or `admin-nav-item` of the current page, the `tree-item` of the current page among the visible items, the selected `color-swatch`, the chosen `avatar-preset`, the `rating-star` of the rating, the current `lyrics-line` of synchronized lyrics or the selected `online-match`; `-1` without one |
| `--wmp-indicator-x` | `segmented-control`, `theme-picker`, `tabs`, `bottom-nav`, `admin-nav`, `recap-nav`, `tree`, `color-picker`, `avatar-presets`, `online-matches` | Distance of the active entry from the left edge of the group (including its scrolled distance), in pixels, for example `84.5px`; `0px` without one |
| `--wmp-indicator-y` | the same | Distance of the active entry from the top edge of the group (including its scrolled distance), in pixels; `0px` without one |
| `--wmp-indicator-width` | the same | Width of the active entry in pixels; `0px` without one or while it is hidden, for example in a collapsed node of the tree |
| `--wmp-indicator-height` | the same | Height of the active entry in pixels; `0px` without one or while it is hidden |
| `--wmp-crop-scale` | `avatar-crop` | Width of the chosen picture in widths of `avatar-crop-frame`, at least 1, for example `2.5` |
| `--wmp-crop-x` | `avatar-crop` | Distance of the left edge of the picture from the left edge of the frame, in widths of the frame (the base moves the picture by minus this value), for example `0.75` |
| `--wmp-crop-y` | `avatar-crop` | The same for the top edge, in heights of the (square) frame |
| `--wmp-cover-color` | root element | Dominant color of the cover of the current song, for example `rgb(31 92 160)`; missing without a current song or cover (see [Cover color](#cover-color)) |
| `--wmp-cover-color-contrast` | root element | `#000000` or `#ffffff`, the one with the higher contrast on `--wmp-cover-color`; missing with it |
| `--wmp-marquee-distance` | `marquee` | How far a text too long for its place moves to show its end, in pixels, for example `86px`; `0px` when it fits |
| `--wmp-marquee-duration` | `marquee` | How long it scrolls over that distance, for example `2867ms`; `0ms` when it fits |
| `--wmp-swipe-offset` | `transport` | How far the full-screen player is swiped down, in pixels; `0px` while it is not; while it leaves the screen its height |
| `--wmp-swipe-progress` | `transport` | The same as share of the height of the player, between 0 and 1 |
| `--wmp-swipe-duration` | `transport` | Duration of the movement after the finger let go (`settling` or `dismissing`), for example `180ms`: snapping back takes `250ms`, leaving the screen the rest of the way at the speed of the finger, between `80ms` and `300ms`; `0ms` with reduced motion |

## Icons

Icons are masks. Every icon consists of two elements:

```html
<span data-part="icon" data-icon="play" aria-hidden="true">
    <span data-part="icon-glyph"></span>
</span>
```

- `icon-glyph` is masked with the SVG file of the slot (`mask-image`) and filled with `--wmp-icon-fill`. Only the shape of the SVG counts, never its color; semi-transparent layers (for example the back layer of duotone icons) appear as lighter shades of the fill. The fill can be a color or a gradient.
- `--wmp-icon-fill` is inherited, so an area can fill all of its icons, for example `[data-part="sidebar"] { --wmp-icon-fill: ... }`. Buttons (`button` and `a` with `data-variant`) are the exception: the base sets `--wmp-icon-fill: currentColor` on them, so their icons take the text color of the button and stay visible on any button background, for example the white symbol on the blue primary button. A theme that wants another fill for buttons sets `--wmp-icon-fill` on the buttons themselves (for example `[data-part="transport"] [data-variant]`) or on their `icon`.
- Glow and shadow belong on the wrapper `icon`, for example `filter: drop-shadow(0 0 4px #4aa8ff)`. On `icon-glyph`, the mask would cut them off.
- A theme exchanges a graphic with the slot variable `--wmp-icon-<slot>`, for example `--wmp-icon-play: url('/themes/example/images/play.svg')`. Use longhand mask properties if a theme sets its own masks.

The defaults are icons of [Phosphor Icons](https://phosphoricons.com) (MIT) under `/icons/phosphor/<style>/`. The style follows the area: `fill` in dark areas and toolbars, `duotone` in the sidebar, `regular` for small hints in the content, in menus and dialogs. Slots marked as reserved are part of the API but not used yet.

| Slot | Variable | Default file | Area |
|---|---|---|---|
| `back` | `--wmp-icon-back` | `/icons/phosphor/fill/arrow-left-fill.svg` | Header |
| `forward` | `--wmp-icon-forward` | `/icons/phosphor/fill/arrow-right-fill.svg` | Header |
| `tab-indicator` | `--wmp-icon-tab-indicator` | `/icons/phosphor/fill/caret-down-fill.svg` | Header |
| `menu` | `--wmp-icon-menu` | `/icons/phosphor/fill/list-fill.svg` | Header |
| `home` | `--wmp-icon-home` | `/icons/phosphor/fill/house-fill.svg` | Tabs, bottom navigation |
| `now-playing` | `--wmp-icon-now-playing` | `/icons/phosphor/fill/music-notes-fill.svg` | Tabs, bottom navigation |
| `library` | `--wmp-icon-library` | `/icons/phosphor/fill/books-fill.svg` | Tabs, bottom navigation |
| `playlists` | `--wmp-icon-playlists` | `/icons/phosphor/fill/playlist-fill.svg` | Tabs, bottom navigation |
| `review-inbox` | `--wmp-icon-review-inbox` | `/icons/phosphor/fill/tray-fill.svg` | Header (review inbox, admins only) |
| `breadcrumb-root` | `--wmp-icon-breadcrumb-root` | `/icons/phosphor/fill/music-notes-fill.svg` | Toolbar |
| `breadcrumb-separator` | `--wmp-icon-breadcrumb-separator` | `/icons/phosphor/fill/caret-right-fill.svg` | Toolbar |
| `view-tiles` | `--wmp-icon-view-tiles` | `/icons/phosphor/fill/squares-four-fill.svg` | Toolbar (reserved) |
| `view-details` | `--wmp-icon-view-details` | `/icons/phosphor/fill/rows-fill.svg` | Toolbar |
| `view-grouped` | `--wmp-icon-view-grouped` | `/icons/phosphor/regular/stack.svg` | Toolbar (entry `view-grouped` of the view menu of `view-switch`) |
| `view-list` | `--wmp-icon-view-list` | `/icons/phosphor/regular/list-dashes.svg` | Toolbar (entry `view-list` of the view menu of `view-switch`) |
| `sort` | `--wmp-icon-sort` | `/icons/phosphor/fill/sort-ascending-fill.svg` | Toolbar |
| `dropdown` | `--wmp-icon-dropdown` | `/icons/phosphor/fill/caret-down-fill.svg` | Toolbar |
| `search` | `--wmp-icon-search` | `/icons/phosphor/fill/magnifying-glass-fill.svg` | Toolbar |
| `clear` | `--wmp-icon-clear` | `/icons/phosphor/fill/x-fill.svg` | Toolbar |
| `tree-playlists` | `--wmp-icon-tree-playlists` | `/icons/phosphor/duotone/playlist-duotone.svg` | Sidebar |
| `tree-create-playlist` | `--wmp-icon-tree-create-playlist` | `/icons/phosphor/duotone/list-plus-duotone.svg` | Sidebar |
| `tree-library` | `--wmp-icon-tree-library` | `/icons/phosphor/duotone/books-duotone.svg` | Sidebar |
| `tree-recently-added` | `--wmp-icon-tree-recently-added` | `/icons/phosphor/duotone/clock-counter-clockwise-duotone.svg` | Sidebar |
| `tree-artists` | `--wmp-icon-tree-artists` | `/icons/phosphor/duotone/microphone-stage-duotone.svg` | Sidebar |
| `tree-albums` | `--wmp-icon-tree-albums` | `/icons/phosphor/duotone/vinyl-record-duotone.svg` | Sidebar |
| `tree-songs` | `--wmp-icon-tree-songs` | `/icons/phosphor/duotone/music-note-duotone.svg` | Sidebar |
| `tree-genres` | `--wmp-icon-tree-genres` | `/icons/phosphor/duotone/guitar-duotone.svg` | Sidebar |
| `tree-years` | `--wmp-icon-tree-years` | `/icons/phosphor/duotone/calendar-blank-duotone.svg` | Sidebar |
| `tree-recently-played` | `--wmp-icon-tree-recently-played` | `/icons/phosphor/duotone/headphones-duotone.svg` | Sidebar |
| `tree-playlist` | `--wmp-icon-tree-playlist` | `/icons/phosphor/duotone/music-notes-simple-duotone.svg` | Sidebar (each playlist of the profile) |
| `tree-smart-playlist` | `--wmp-icon-tree-smart-playlist` | `/icons/phosphor/duotone/magic-wand-duotone.svg` | Sidebar (each smart playlist of the profile) |
| `tree-favorites` | `--wmp-icon-tree-favorites` | `/icons/phosphor/duotone/heart-duotone.svg` | Sidebar |
| `tree-recap` | `--wmp-icon-tree-recap` | `/icons/phosphor/duotone/sparkle-duotone.svg` | Sidebar (the recap) |
| `tree-statistics` | `--wmp-icon-tree-statistics` | `/icons/phosphor/duotone/chart-bar-duotone.svg` | Sidebar (the statistics below the recap) |
| `tree-shared-recaps` | `--wmp-icon-tree-shared-recaps` | `/icons/phosphor/duotone/users-three-duotone.svg` | Sidebar (the shared recaps below the recap) |
| `tree-expand` | `--wmp-icon-tree-expand` | `/icons/phosphor/regular/plus-square.svg` | Sidebar |
| `tree-collapse` | `--wmp-icon-tree-collapse` | `/icons/phosphor/regular/minus-square.svg` | Sidebar |
| `sidebar-collapse` | `--wmp-icon-sidebar-collapse` | `/icons/phosphor/duotone/caret-double-left-duotone.svg` | Sidebar |
| `sidebar-expand` | `--wmp-icon-sidebar-expand` | `/icons/phosphor/duotone/caret-double-right-duotone.svg` | Sidebar |
| `has-video` | `--wmp-icon-has-video` | `/icons/phosphor/regular/video-camera.svg` | Content |
| `info` | `--wmp-icon-info` | `/icons/phosphor/regular/info.svg` | Content |
| `cover-placeholder` | `--wmp-icon-cover-placeholder` | `/icons/phosphor/fill/music-notes-fill.svg` | Content |
| `admin-library` | `--wmp-icon-admin-library` | `/icons/phosphor/regular/folder-open.svg` | Content (administration) |
| `admin-jobs` | `--wmp-icon-admin-jobs` | `/icons/phosphor/regular/clock-clockwise.svg` | Content (administration) |
| `admin-cache` | `--wmp-icon-admin-cache` | `/icons/phosphor/regular/hard-drives.svg` | Content (administration) |
| `admin-transcoding` | `--wmp-icon-admin-transcoding` | `/icons/phosphor/regular/film-reel.svg` | Content (administration) |
| `admin-backups` | `--wmp-icon-admin-backups` | `/icons/phosphor/regular/database.svg` | Content (administration) |
| `admin-themes` | `--wmp-icon-admin-themes` | `/icons/phosphor/regular/palette.svg` | Content (administration) |
| `admin-files` | `--wmp-icon-admin-files` | `/icons/phosphor/regular/files.svg` | Content (administration) |
| `admin-review` | `--wmp-icon-admin-review` | `/icons/phosphor/regular/tray.svg` | Content (administration) |
| `admin-checks` | `--wmp-icon-admin-checks` | `/icons/phosphor/regular/stethoscope.svg` | Content (administration) |
| `tags-missing` | `--wmp-icon-tags-missing` | `/icons/phosphor/regular/warning.svg` | Content (review inbox) |
| `album` | `--wmp-icon-album` | `/icons/phosphor/fill/vinyl-record-fill.svg` | Content (library: tiles and pages without a cover) |
| `artist` | `--wmp-icon-artist` | `/icons/phosphor/fill/microphone-stage-fill.svg` | Content (library: tiles and pages without a cover) |
| `genre` | `--wmp-icon-genre` | `/icons/phosphor/fill/guitar-fill.svg` | Content (library: tiles and pages without a cover) |
| `year` | `--wmp-icon-year` | `/icons/phosphor/fill/calendar-blank-fill.svg` | Content (library: tiles and pages without a cover) |
| `playlist` | `--wmp-icon-playlist` | `/icons/phosphor/fill/playlist-fill.svg` | Content (playlists without any cover) |
| `smart-playlist` | `--wmp-icon-smart-playlist` | `/icons/phosphor/fill/magic-wand-fill.svg` | Content (`tile-badge` of smart playlists and smart playlists without any cover) |
| `recently-played` | `--wmp-icon-recently-played` | `/icons/phosphor/fill/headphones-fill.svg` | Content (tile and page of "Recently played") |
| `drag-handle` | `--wmp-icon-drag-handle` | `/icons/phosphor/regular/dots-six-vertical.svg` | Content (`drag-handle` of playlist entries) |
| `track` | `--wmp-icon-track` | `/icons/phosphor/fill/music-note-fill.svg` | Content (tiles of songs without cover) |
| `favorite` | `--wmp-icon-favorite` | `/icons/phosphor/fill/heart-fill.svg` | Content (`btn-favorite` of a favorite) |
| `favorite-off` | `--wmp-icon-favorite-off` | `/icons/phosphor/regular/heart.svg` | Content (`btn-favorite` of anything else) |
| `rating-star` | `--wmp-icon-rating-star` | `/icons/phosphor/fill/star-fill.svg` | Content (`rating-star` up to the rating) |
| `rating-star-off` | `--wmp-icon-rating-star-off` | `/icons/phosphor/regular/star.svg` | Content (the other `rating-star`s) |
| `fullscreen` | `--wmp-icon-fullscreen` | `/icons/phosphor/fill/corners-out-fill.svg` | Content (`btn-fullscreen` of the video and the visualization) |
| `exit-fullscreen` | `--wmp-icon-exit-fullscreen` | `/icons/phosphor/fill/corners-in-fill.svg` | Content (`btn-fullscreen` in full screen) |
| `preset-previous` | `--wmp-icon-preset-previous` | `/icons/phosphor/fill/caret-left-fill.svg` | Content (`btn-preset-previous` of the visualization) |
| `preset-next` | `--wmp-icon-preset-next` | `/icons/phosphor/fill/caret-right-fill.svg` | Content (`btn-preset-next` of the visualization) |
| `auto-change` | `--wmp-icon-auto-change` | `/icons/phosphor/fill/timer-fill.svg` | Content (`btn-auto-change` of the visualization) |
| `color-scheme` | `--wmp-icon-color-scheme` | `/icons/phosphor/fill/palette-fill.svg` | Content (`btn-color-scheme-picker` of the visualization) |
| `item-menu` | `--wmp-icon-item-menu` | `/icons/phosphor/fill/dots-three-outline-fill.svg` | Content (`btn-item-menu`) |
| `recap` | `--wmp-icon-recap` | `/icons/phosphor/regular/sparkle.svg` | Content (`recap-nav-item` `recap`) |
| `statistics` | `--wmp-icon-statistics` | `/icons/phosphor/regular/chart-bar.svg` | Content (`recap-nav-item` `statistics`) |
| `shared-recaps` | `--wmp-icon-shared-recaps` | `/icons/phosphor/regular/users-three.svg` | Content (`recap-nav-item` `shared-recaps`) |
| `recap-previous` | `--wmp-icon-recap-previous` | `/icons/phosphor/fill/caret-left-fill.svg` | Content (`btn-recap-previous`) |
| `recap-next` | `--wmp-icon-recap-next` | `/icons/phosphor/fill/caret-right-fill.svg` | Content (`btn-recap-next`) |
| `shuffle` | `--wmp-icon-shuffle` | `/icons/phosphor/fill/shuffle-fill.svg` | Transport |
| `repeat` | `--wmp-icon-repeat` | `/icons/phosphor/fill/repeat-fill.svg` | Transport |
| `repeat-one` | `--wmp-icon-repeat-one` | `/icons/phosphor/fill/repeat-once-fill.svg` | Transport |
| `crossfade` | `--wmp-icon-crossfade` | `/icons/phosphor/fill/intersect-fill.svg` | Transport |
| `autoplay` | `--wmp-icon-autoplay` | `/icons/phosphor/fill/infinity-fill.svg` | Transport |
| `stop` | `--wmp-icon-stop` | `/icons/phosphor/fill/stop-fill.svg` | Transport |
| `previous` | `--wmp-icon-previous` | `/icons/phosphor/fill/skip-back-fill.svg` | Transport |
| `play` | `--wmp-icon-play` | `/icons/phosphor/fill/play-fill.svg` | Transport |
| `pause` | `--wmp-icon-pause` | `/icons/phosphor/fill/pause-fill.svg` | Transport |
| `next` | `--wmp-icon-next` | `/icons/phosphor/fill/skip-forward-fill.svg` | Transport |
| `volume` | `--wmp-icon-volume` | `/icons/phosphor/fill/speaker-high-fill.svg` | Transport |
| `muted` | `--wmp-icon-muted` | `/icons/phosphor/fill/speaker-x-fill.svg` | Transport |
| `compact` | `--wmp-icon-compact` | `/icons/phosphor/fill/corners-in-fill.svg` | Transport |
| `expand-player` | `--wmp-icon-expand-player` | `/icons/phosphor/fill/corners-out-fill.svg` | Transport |
| `equalizer` | `--wmp-icon-equalizer` | `/icons/phosphor/fill/faders-fill.svg` | Transport |
| `sleep-timer` | `--wmp-icon-sleep-timer` | `/icons/phosphor/fill/moon-fill.svg` | Transport |
| `cast` | `--wmp-icon-cast` | `/icons/phosphor/fill/screencast-fill.svg` | Transport |
| `resume-device` | `--wmp-icon-resume-device` | `/icons/phosphor/fill/devices-fill.svg` | Transport |
| `collapse-player` | `--wmp-icon-collapse-player` | `/icons/phosphor/fill/caret-down-fill.svg` | Transport |
| `settings` | `--wmp-icon-settings` | `/icons/phosphor/regular/gear-six.svg` | Menus and dialogs |
| `keyboard-shortcuts` | `--wmp-icon-keyboard-shortcuts` | `/icons/phosphor/regular/keyboard.svg` | Menus and dialogs (overview of the keyboard shortcuts in the profile menu) |
| `profile` | `--wmp-icon-profile` | `/icons/phosphor/regular/user-circle.svg` | Menus and dialogs |
| `switch-profile` | `--wmp-icon-switch-profile` | `/icons/phosphor/regular/user-switch.svg` | Menus and dialogs |
| `manage-profiles` | `--wmp-icon-manage-profiles` | `/icons/phosphor/regular/users.svg` | Menus and dialogs, administration |
| `administration` | `--wmp-icon-administration` | `/icons/phosphor/regular/wrench.svg` | Menus and dialogs |
| `scan` | `--wmp-icon-scan` | `/icons/phosphor/regular/arrows-clockwise.svg` | Menus and dialogs |
| `run-job` | `--wmp-icon-run-job` | `/icons/phosphor/regular/play.svg` | Menus and dialogs |
| `backup` | `--wmp-icon-backup` | `/icons/phosphor/regular/floppy-disk.svg` | Menus and dialogs |
| `edit` | `--wmp-icon-edit` | `/icons/phosphor/regular/pencil-simple.svg` | Menus and dialogs |
| `delete` | `--wmp-icon-delete` | `/icons/phosphor/regular/trash.svg` | Menus and dialogs |
| `add` | `--wmp-icon-add` | `/icons/phosphor/regular/plus.svg` | Menus and dialogs |
| `view-video` | `--wmp-icon-view-video` | `/icons/phosphor/regular/film-strip.svg` | Content (`btn-view-video`) |
| `view-visualization` | `--wmp-icon-view-visualization` | `/icons/phosphor/regular/waveform.svg` | Content (`btn-view-visualization`, `visualization`) |
| `view-cover` | `--wmp-icon-view-cover` | `/icons/phosphor/regular/image.svg` | Content (`btn-view-cover`) |
| `view-lyrics` | `--wmp-icon-view-lyrics` | `/icons/phosphor/regular/subtitles.svg` | Content (`segment` `lyrics` of `stage-actions`) |
| `download-lyrics` | `--wmp-icon-download-lyrics` | `/icons/phosphor/regular/cloud-arrow-down.svg` | Content (`button` `download-lyrics` on the page of a song) |
| `forget-lyrics-lookup` | `--wmp-icon-forget-lyrics-lookup` | `/icons/phosphor/regular/arrow-counter-clockwise.svg` | Content (`button` `forget-lyrics-lookup` on the page of a song, `button` `forget-lyrics-lookups` on the library page of the administration) |
| `search-online` | `--wmp-icon-search-online` | `/icons/phosphor/regular/globe-simple.svg` | Content (`button` `search-online` on the page of a song or album) |
| `upload` | `--wmp-icon-upload` | `/icons/phosphor/regular/upload-simple.svg` | Content (`button` `upload-theme` and `replace` on the theme page of the administration) |
| `detail-back` | `--wmp-icon-detail-back` | `/icons/phosphor/regular/arrow-left.svg` | Menus and dialogs |
| `review-done` | `--wmp-icon-review-done` | `/icons/phosphor/regular/check-circle.svg` | Review inbox |
| `review-skip` | `--wmp-icon-review-skip` | `/icons/phosphor/regular/skip-forward.svg` | Review inbox |
| `review-previous` | `--wmp-icon-review-previous` | `/icons/phosphor/regular/caret-left.svg` | Review inbox |
| `review-next` | `--wmp-icon-review-next` | `/icons/phosphor/regular/caret-right.svg` | Review inbox |
| `go-to-album` | `--wmp-icon-go-to-album` | `/icons/phosphor/regular/vinyl-record.svg` | Menus and dialogs |
| `go-to-artist` | `--wmp-icon-go-to-artist` | `/icons/phosphor/regular/microphone-stage.svg` | Menus and dialogs |
| `watch-video` | `--wmp-icon-watch-video` | `/icons/phosphor/regular/monitor-play.svg` | Menus and dialogs |
| `show-files` | `--wmp-icon-show-files` | `/icons/phosphor/regular/files.svg` | Menus and dialogs |
| `rate` | `--wmp-icon-rate` | `/icons/phosphor/regular/star.svg` | Menus and dialogs |
| `submenu` | `--wmp-icon-submenu` | `/icons/phosphor/regular/caret-right.svg` | Menus and dialogs (`submenu-indicator`) |
| `play-now` | `--wmp-icon-play-now` | `/icons/phosphor/regular/play.svg` | Menus and dialogs |
| `play-next` | `--wmp-icon-play-next` | `/icons/phosphor/regular/queue.svg` | Menus and dialogs |
| `add-to-queue` | `--wmp-icon-add-to-queue` | `/icons/phosphor/regular/list-plus.svg` | Menus and dialogs |
| `add-to-playlist` | `--wmp-icon-add-to-playlist` | `/icons/phosphor/regular/playlist.svg` | Menus and dialogs |
| `remove-from-playlist` | `--wmp-icon-remove-from-playlist` | `/icons/phosphor/regular/minus-circle.svg` | Menus and dialogs |
| `create-smart-playlist` | `--wmp-icon-create-smart-playlist` | `/icons/phosphor/regular/magic-wand.svg` | Menus and dialogs (new smart playlist, templates) |
| `convert-playlist` | `--wmp-icon-convert-playlist` | `/icons/phosphor/regular/swap.svg` | Menus and dialogs (convert a smart playlist into a playlist) |
| `remove-rule` | `--wmp-icon-remove-rule` | `/icons/phosphor/regular/minus-circle.svg` | Content (`remove-rule` in `smart-rule`) |
| `change-cover` | `--wmp-icon-change-cover` | `/icons/phosphor/regular/image-square.svg` | Menus and dialogs |
| `add-favorite` | `--wmp-icon-add-favorite` | `/icons/phosphor/regular/heart.svg` | Menus and dialogs |
| `remove-favorite` | `--wmp-icon-remove-favorite` | `/icons/phosphor/regular/heart-break.svg` | Menus and dialogs |
| `shuffle-play` | `--wmp-icon-shuffle-play` | `/icons/phosphor/regular/shuffle.svg` | Menus and dialogs |
| `move-up` | `--wmp-icon-move-up` | `/icons/phosphor/regular/caret-up.svg` | Menus and dialogs |
| `move-down` | `--wmp-icon-move-down` | `/icons/phosphor/regular/caret-down.svg` | Menus and dialogs |
| `remove-from-queue` | `--wmp-icon-remove-from-queue` | `/icons/phosphor/regular/x.svg` | Menus and dialogs |
| `clear-queue` | `--wmp-icon-clear-queue` | `/icons/phosphor/regular/trash.svg` | Menus and dialogs |
| `close` | `--wmp-icon-close` | `/icons/phosphor/regular/x.svg` | Menus and dialogs |
| `reset` | `--wmp-icon-reset` | `/icons/phosphor/regular/arrow-counter-clockwise.svg` | Menus and dialogs |
| `has-pin` | `--wmp-icon-has-pin` | `/icons/phosphor/regular/lock.svg` | Content (`badge` `pin` of a profile on the admin page) |
| `reset-pin` | `--wmp-icon-reset-pin` | `/icons/phosphor/regular/lock-open.svg` | Menus and dialogs (removes the PIN of a profile on the admin page) |
| `change-avatar` | `--wmp-icon-change-avatar` | `/icons/phosphor/regular/user-circle.svg` | Menus and dialogs (changes the picture of a profile on the settings page, on the admin page and in the avatar dialog) |
| `save-picture` | `--wmp-icon-save-picture` | `/icons/phosphor/regular/export.svg` | Menus and dialogs (the `button` `save-picture` of the recap) |
| `save-video` | `--wmp-icon-save-video` | `/icons/phosphor/regular/video.svg` | Menus and dialogs (the `button` `save-video` of the recap) |
| `profile-lock` | `--wmp-icon-profile-lock` | `/icons/phosphor/fill/lock-fill.svg` | Profile selection (`profile-lock` of a profile with a PIN) |
| `pin-delete` | `--wmp-icon-pin-delete` | `/icons/phosphor/fill/backspace-fill.svg` | Profile selection (`pin-key` `delete`) |
| `pin-submit` | `--wmp-icon-pin-submit` | `/icons/phosphor/fill/arrow-right-fill.svg` | Profile selection (`pin-key` `submit`) |

## Rules

- `::before` and `::after` of all parts belong to the theme; the base never uses them.
- Labels are never removed. To hide a label visually, keep it for assistive technology:

    ```css
    [data-part='btn-play'] > [data-part='label'] {
        position: absolute;
        inline-size: 1px;
        block-size: 1px;
        overflow: hidden;
        clip-path: inset(50%);
        white-space: nowrap;
    }
    ```

    The base already does this for buttons with `data-variant="icon"` or `"round"`; they show their label as tooltip.
- Keep the focus outline (`:focus-visible`) visible; `--wmp-color-focus` sets its color.
- Fonts: use fonts that are installed on the device or ship font files only under a license that allows it (see [Fonts of an uploaded theme](#fonts-of-an-uploaded-theme)).
- Graphics: only own graphics or graphics under a compatible license. No logos, icons or other assets of third-party products.
- Do not set `display` on `context-menu` or `dialog` without `:popover-open` or `[open]`, otherwise they stay visible when closed.

## Preview

The internal component overview shows all parts in all states, the icon slots and the app at mobile and desktop size side by side, with a switch for the theme and its variant. It is only available in the development server:

```bash
cd source/WaveMP.Web
npm run dev
```

Then open `http://localhost:5173/dev/components`.

### Theme previews

A theme preview is a development server that shows one theme, whatever the profile chose, serves `/assets/web` live (a saved `theme.css` shows on the next reload) and passes the API to a running WaveMP instance with a library, by default the development container (`docker compose -f docker-compose.dev.yml up -d --build`, `http://localhost:8080`). Each shipped theme has its own port, so several previews run at the same time:

| Theme | Port |
|---|---|
| `midnight` | 5200 |
| `meadow` | 5201 |
| `daylight` | 5202 |
| `accent` | 5203 |
| `pinstripe` | 5204 |
| `clear-glass` | 5205 |
| `classic-9x` | 5206 |
| `skinned` | 5207 |

```bash
cd source/WaveMP.Web
npm run theme:preview -- accent                 # http://localhost:5203, until stopped
npm run theme:preview -- stop accent            # from another terminal
npm run theme:preview -- accent --service http://localhost:5080 --port 6003
```

The preview shows the variant of the color scheme of the browser (`prefers-color-scheme`); the developer tools of the browser switch it (Rendering, "Emulate CSS media feature prefers-color-scheme"). Select any profile on `/profiles`; the built-in admin also sees the administration.

## Checking a theme

The screenshot and contrast check of tests/WaveMP.E2E opens fixed views of the app with a profile of its own per theme and variant (filled with playlists, favorites and plays of the synthetic test media), takes a screenshot of each and checks the contrast:

- desktop (1280 by 800): `profiles`, `home`, `playlists` (the overview of the playlists, with the current entry on the first level of the sidebar), `album`, `artists`, `songs-menu` (the songs with an open context menu), `now-playing` (with the cover), `settings` (with the theme picker), `admin` (the transcoding page), `shortcuts` (the overview of the keyboard shortcuts),
- phone (Pixel 7): `phone-library`, `phone-now-playing`, `phone-settings`, each with the bottom navigation, and `phone-shortcuts` (the overview of the keyboard shortcuts). The regression also compares the bottom navigation of `phone-now-playing` on its own (`phone-now-playing-bottom-nav`).

The player of every view shows the same saved playback state: E2E Album, paused at the start of First Song. `phone-library` shows it as saved on another device, with the notice "Continue listening from". The menu of `songs-menu` reaches over the transport and casts its shadow on the time of the song. Before each view, the check sets this state for the profile of the theme through the API and waits until the player shows it; the pages never change it and nothing plays, so the screenshots depend neither on the views before, nor on earlier runs against the same instance, nor on how far a song got.

```bash
cd tests/WaveMP.E2E
npm ci && npx playwright install chromium     # once
npm run theme:screenshots -- --theme accent --base-url http://localhost:5203 --out ../../theme-screenshots
npm run theme:screenshots -- --theme accent --variant dark --views home,now-playing
npm run theme:screenshots -- --theme accent --preview      # also writes preview-<variant>.webp
npm run theme:contrast -- --theme accent --base-url http://localhost:5203
```

Without `--theme` all shipped themes are checked, without `--variant` all variants of a theme, without `--out` the screenshots go to `tests/WaveMP.E2E/theme-screenshots/<id>/<variant>/` (ignored by Git), next to a `contrast.txt`. The base URL is a theme preview or WaveMP itself (the profile then uses the theme from the registry).

### Contrast

Every text must reach the contrast of WCAG 2.2 AA against what is drawn behind it, also on gradients, glass and pictures: 4.5:1, large text (24px, or 18.66px bold) 3:1, icons 3:1. The check compares the color of each visible text and icon with the background at the pixels of its glyphs (a screenshot with and one without all texts and icons) and fails when fewer than 90 % of them reach the ratio; it lists each failure with its ratio, the text and the path of parts, for example:

```text
[album] 3.12:1 < 4.5:1  text "Play"  detail-info > detail-actions > button[play] > label  (rgb(255 255 255 / 100%))
```

Exempt like in WCAG are disabled controls and the logo, and also the initials of avatars (the profile chooses their color) and the placeholder glyphs of missing covers (decoration). Texts and icons filled with a gradient or picture cannot be measured; `contrast.txt` lists them for a manual check. Disabled and secondary content (for example the built-in admin with `--wmp-profile-secondary-opacity`) still needs 4.5:1 when it can be used. Text that a menu or dialog only partly covers is left out, but text in the shadow of an open menu is not: it needs the same ratio, so choose muted text colors with some margin (the time in the transport under the context menu of `songs-menu`, for example).

### Regression

`npm test` in tests/WaveMP.E2E runs the project `themes` after all other projects: every shipped theme and variant, every view except `profiles`, with the contrast check and a comparison with the baselines in `tests/WaveMP.E2E/tests/themes/baselines`. The baselines are taken in the Playwright image of the CI, so the comparison runs only in the CI (or with `E2E_VISUAL=1`) and only for views with a baseline; locally only the contrast is checked. After a change of a theme, take the baselines again in that image (needs Docker and a running WaveMP with the test media):

```bash
cd tests/WaveMP.E2E
npm run test:themes             # compare in the Playwright image of the CI
npm run test:themes:update      # take the baselines again
```

## Uploaded themes

Admins can install own themes without changing WaveMP: the page Administration > Themes uploads a theme package, and every profile can then choose the theme on the settings page. The same page downloads the template (also at `/api/v1/theme-template`): a complete example theme with a commented CSS file, a manifest and pictures to start from.

### Package

A theme package is a ZIP file with these files:

| File | Content |
|---|---|
| `manifest.json` | Description of the theme, see [Manifest](#manifest) |
| Exactly one `.css` file | The theme, saved as UTF-8; any name and folder, for example `theme.css` |
| `.svg`, `.png`, `.webp` files | The pictures the CSS uses and the previews |
| `.woff2` files | Fonts the CSS uses in `@font-face`, see [Fonts of an uploaded theme](#fonts-of-an-uploaded-theme) |

The files may lie in a single folder of the ZIP file, as when a folder is compressed. `README`, `LICENSE` and other `.md` and `.txt` files are allowed but not installed, nor are `__MACOSX`, `.DS_Store`, `Thumbs.db` and `desktop.ini`. Every other file is rejected, for example scripts, HTML, fonts in other formats than WOFF2 (TTF, OTF, WOFF), JPEG pictures and nested archives.

Paths consist of letters, digits, `.`, `_` and `-`, separated by `/`; at most five levels and 200 characters, and no two paths that differ only in upper and lower case. The default limits:

| Limit | Default | Environment variable |
|---|---|---|
| Size of the ZIP file | 10 MiB | `Themes__MaxUploadBytes` |
| Size of all files once unpacked | 32 MiB | `Themes__MaxUnpackedBytes` |
| Size of a file | 5 MiB | `Themes__MaxFileBytes` |
| Size of the CSS file | 1 MiB | `Themes__MaxStylesheetBytes` |
| Size of a font file | 2 MiB | `Themes__MaxFontBytes` |
| Number of entries | 256 | `Themes__MaxEntries` |

### Manifest

```json
{
    "id": "my-theme",
    "name": "My Theme",
    "author": "Your Name",
    "version": "1.0.0",
    "themeApi": 7,
    "variants": ["light", "dark"],
    "previews": { "light": "preview-light.svg", "dark": "preview-dark.svg" }
}
```

| Member | Rule |
|---|---|
| `id` | Lowercase letters, digits and single hyphens, at most 40 characters, not the id of a shipped theme (`midnight`, `meadow`, `daylight`, `accent`, `pinstripe`, `clear-glass`, `classic-9x`, `skinned`). Profiles store the id; a new version of the theme keeps it |
| `name` | Name in the theme picker, at most 60 characters |
| `author` | At most 60 characters |
| `version` | Version of the theme, for example `1.2.0`; letters, digits, `.`, `+` and `-`, at most 32 characters |
| `themeApi` | The version of this document the theme is written for. The server only accepts versions it supports; a rejected package names them |
| `variants` | `light`, `dark` or both, see [Variants](#variants); the first one is shown where the appearance of a profile has no say |
| `previews` | One preview picture per variant, a path in the package (SVG, PNG or WebP, ideally 16:10), shown in the theme picker |

Comments, trailing commas and further members (for example `description`) are allowed; no member may appear twice.

### CSS of an uploaded theme

The CSS file works like the CSS file of a shipped theme, see [Cascade](#cascade) and [Variants](#variants). These rules keep a theme from loading anything from elsewhere:

- Pictures and fonts are named with paths relative to the CSS file, for example `url('images/play.svg')` or `src: url('fonts/display.woff2') format('woff2')`, also in variables and in `image-set()`. The server replaces each path with the address of the installed file; a path to a file that is neither a picture nor a font of the package is rejected. `url('#id')` (a reference into the document) stays as it is.
- No addresses with a scheme (`https:`, `data:`, `blob:`, `javascript:` and so on), no `//`, no addresses that start with `/`, no queries such as `?v=1` and no paths outside the package. `url()`, `src()`, `image()` and `image-set()` cannot take their address from `var()`, `attr()` or `env()`. The server reads the CSS file like a browser, so escapes such as `u\72l(` and comments do not hide an address.
- No `@import`: everything is in the one CSS file. No `@layer`: the theme stays outside the layers of the base. No `@namespace` or `@document`. Allowed are `@charset`, `@media`, `@supports`, `@container`, `@keyframes`, `@font-face`, `@property`, `@starting-style`, `@scope`, `@counter-style`, `@font-feature-values`, `@font-palette-values`, `@view-transition` and `@page`. `@font-face` names the fonts of the package, or fonts installed on the device with `local()`.

### Pictures of an uploaded theme

SVG files are cleaned on upload: the server reads them without a document type definition and writes them again with only the elements and attributes of drawings. Comments, metadata of drawing programs and animations are removed. Rejected are scripts, event handlers such as `onload`, `foreignObject` and HTML, `DOCTYPE` and entities, and references outside the picture: an SVG file may only reference its own elements (`#id`) and embed PNG, JPEG, WebP or GIF pictures as `data:image/...;base64`. PNG and WebP files must contain a picture of their format.

### Fonts of an uploaded theme

A theme can ship its own fonts as WOFF2 files, the format every browser that runs WaveMP supports. Convert TTF and OTF files to WOFF2 first, for example with `fonttools ttLib.woff2 compress my-font.ttf`; other font formats are rejected. Name each font in an `@font-face` rule with a path relative to the CSS file:

```css
@font-face {
    font-family: 'My Theme Sans';
    src: url('fonts/my-theme-sans.woff2') format('woff2');
    font-weight: 100 900;
    font-display: swap;
}

:root {
    --wmp-font-family: 'My Theme Sans', system-ui, sans-serif;
}
```

The server checks every font on upload: the WOFF2 signature, the font format inside it (TrueType, CFF or a font collection), the length the header names, which must be the length of the file, and the size of the file (at most `Themes__MaxFontBytes`, 2 MiB by default). The browser checks the tables of the font again before it draws with it. Keep fonts small: every device loads them with the theme, so a subset with only the characters the theme needs (for example Latin) is better than a complete font. `font-display: swap` shows the text in the next font of the list until the font is loaded.

Fonts are installed and served like the pictures, from the server itself; the Content Security Policy of the web app (`font-src 'self'`) blocks fonts from anywhere else.

**License:** the theme author is responsible for the license of every font in the package. Only ship fonts whose license allows to embed them and to pass them on, for example fonts under the SIL Open Font License (OFL) or the Apache License 2.0. Many commercial fonts and fonts of operating systems do not allow that. Put the license text of each font into the package, for example as `OFL.txt` or `fonts/LICENSE.txt`: text files are allowed in a package and stay with the theme package that is passed on, but are not installed. Mention the fonts and their licenses in the README of the theme as well.

### Installation

The server keeps uploaded themes in the volume `/data`, in `themes/<id>`. Every file is named after a hash of its content and served at `/api/v1/themes/<id>/files/<name>` without a session and with a cache time of one year, so a new version of a file always has a new address. Upload a new version of a theme with its button on the theme page (`PUT /api/v1/themes/<id>`); profiles that chose the theme keep it. Deleting a theme gives these profiles the default theme.

The web app sends a Content Security Policy that allows styles, pictures, fonts and media only from the server itself. Even a theme that was changed in the folder `/data/themes` cannot load anything from elsewhere.

### Rejected packages

A rejected package lists every problem at once, with the file and the line it concerns. The codes (`ThemeErrorCode` in the OpenAPI document):

| Code | Problem |
|---|---|
| `package.corrupt` | The ZIP file is damaged, encrypted or uses a compression the server cannot read |
| `package.too_many_entries` | More entries than allowed |
| `package.too_large` | The files are larger than allowed once unpacked |
| `file.too_large` | A file is larger than allowed once unpacked |
| `file.invalid_path` | A path is absolute, leaves the package, is too deep or too long, or has other characters |
| `file.duplicate_path` | A path appears twice, also when only upper and lower case differ |
| `file.unsupported_type` | A file of a type a theme cannot contain, also a font in another format than WOFF2 |
| `manifest.missing` | No `manifest.json` |
| `manifest.invalid_json` | `manifest.json` is no valid JSON object or names a member twice |
| `manifest.missing_field` | A required member is missing |
| `manifest.invalid_field` | A member has a wrong type or value |
| `manifest.reserved_id` | The id of a shipped theme |
| `manifest.id_mismatch` | A new version names another id than the theme it replaces |
| `manifest.unsupported_api_version` | The server does not support the version of the theme API; the problem names the supported versions |
| `manifest.preview_missing` | A preview is not in the package or no picture |
| `css.missing` | No CSS file |
| `css.multiple` | More than one CSS file |
| `css.invalid_encoding` | The CSS file is not UTF-8 |
| `css.syntax_error` | A string or address is not closed properly |
| `css.import` | `@import` |
| `css.layer` | `@layer` |
| `css.at_rule_not_allowed` | Another at-rule that themes cannot use |
| `css.external_url` | An external address (a scheme or `//`) |
| `css.absolute_url` | An address that starts with `/` |
| `css.invalid_url` | An empty address, a query, a path outside the package or an address from `var()`, `attr()` or `env()` |
| `css.missing_file` | A path to a file that is neither a picture nor a font of the package |
| `svg.invalid` | No well-formed SVG document |
| `svg.entity` | A `DOCTYPE` or entities |
| `svg.script` | A script |
| `svg.event_handler` | An event handler attribute |
| `svg.foreign_object` | `foreignObject` or HTML |
| `svg.external_reference` | A reference outside the picture |
| `image.invalid` | A PNG or WebP file without a picture of its format |
| `font.invalid` | A WOFF2 file that is no WOFF2 font, or whose header names another length than the file has |
| `svg.not_cleaned` | Only for an installed theme (see [Checks after an update](#checks-after-an-update)): an SVG file still contains elements or attributes that the server now removes from SVG files |

### Checks after an update

A newer version of WaveMP may check theme packages more strictly, for example reject more in CSS or SVG files, or no longer support the theme API version a theme is written for. On every start the server therefore checks the installed themes again with its current rules, the same way as an upload; only the limits of the package are not checked again. What happens to a theme that no longer passes depends on the rules it breaks:

- **Blocked:** the theme breaks a security rule, an unsafe construct in its CSS or SVG files (`css.invalid_encoding`, `css.syntax_error`, `css.import`, `css.at_rule_not_allowed`, `css.external_url`, `css.absolute_url`, `css.invalid_url`, `svg.invalid`, `svg.entity`, `svg.script`, `svg.event_handler`, `svg.foreign_object`, `svg.external_reference` or `svg.not_cleaned`). The server no longer serves its files, it can no longer be chosen, and profiles that chose it see Midnight. They keep their choice, so they see the theme again as soon as a new version that passes is uploaded.
- **Outdated:** the theme breaks only other rules, for example it is written for a theme API version the server no longer supports, uses `@layer` or names a missing file. It is still offered to the profiles as it is.

In both cases the theme page of the administration marks the theme as blocked or outdated and lists its problems with the codes above and the paths of the files in the uploaded package (themes installed before WaveMP kept these paths name the installed files instead). Upload a new version of the theme that fixes them; the API names the result in `status` (`Current`, `Outdated` or `Blocked`) and `problems` of a theme.

## Versioning

The version of the theme API is in `data-theme-api` on the root element and at the top of this document. Changing or removing an existing part, state, attribute or variable increases the version. New parts, states, icon slots and variables do not.

The themes shipped with WaveMP name the version they are written for in the comment at the top of their `theme.css` (for example `theme API version 7`); a test keeps it equal to the current version, so a theme is checked against every change before the version in its comment is raised.

### Changelog

- **Version 7:** the controls of the visualization below the picture. Changed: `visualization-controls` no longer lies in the `visualization` over its bottom edge (shown on hover) but in the `stage`, right after the `stage-views` and only while the visualization view is shown, always visible; the base lays it out as a grid with named areas instead of a centered row that wraps, and no longer positions it absolutely. The `field` `visualization-preset` with its `select` is replaced by the button `btn-preset-picker` (the value `visualization-preset` of `data-field` is gone), which opens the presets as a `context-menu` (`data-kind="visualization-presets"`) above it or, on phones, as a sheet. The full screen of the visualization shows no controls. Themes that placed `visualization-controls` over the picture (`inset`, `translate`, `inline-size: max-content`) or styled `visualization-controls select` have to remove the positioning and style `btn-preset-picker` instead. Added: the part `btn-preset-picker` (states `open` and `closed`), the states `loading` and `ready` of `visualization-controls`, the attribute `data-placement` of `context-menu` (`point`, `above`, `sheet`) with the runtime variables `--wmp-menu-max-height` and `--wmp-menu-anchor-width`, the variables `--wmp-menu-list-max-width`, `--wmp-menu-list-max-height` and `--wmp-sheet-max-height`, the value `visualization-presets` of `data-kind`, and typing to jump to an entry in every `context-menu`. Uploaded themes written for version 5 or 6 are still accepted.
    - Additions without a version change: layout options (see [Layout options](#layout-options)). The variables `--wmp-layout-breadcrumb`, `--wmp-layout-queue-pane`, `--wmp-layout-view-options` and `--wmp-queue-pane-width`; with `--wmp-layout-breadcrumb: header` the `breadcrumb` is a child of `header` after `nav-history` (grid area `breadcrumb` of `header`, with base templates for `header:has(> [data-part='breadcrumb'])`). With `--wmp-layout-view-options: on` `view-switch` opens the view menu (the `menuitemradio` entries `view-grouped` and `view-list` with the icon slots `view-grouped` and `view-list`); the plain list has the state `ungrouped` of `list` and one `list-group` without `list-group-header` and `list-group-info`. With `--wmp-layout-queue-pane: docked` the `app` has the child `queue-pane` after `content` (grid area `queue-pane`, with a base template for `app:has(> [data-part='queue-pane'])`) with the new parts `queue-pane-header`, `queue-pane-title`, `queue-pane-subtitle` and `queue-pane-summary`, the `segmented-control` `queue-pane-view`, the state `empty` of `queue-pane-header`, the values `count` and `duration` of `data-field` on the `label`s of `queue-pane-summary` and the variable `--wmp-cover-size-queue-pane`. Themes that set none of the variables keep their DOM and layout.
    - Additions without a version change: forgetting the results of the lyrics lookup. On the library page of the administration the `section` `lyrics-lookups` with the part `lyrics-lookups` (states `loading`, `has-results` and `empty`), its `stat-item`s `not-found`, `instrumental` and `wait` and the `button` `forget-lyrics-lookups`; on the page of a song the `form-note` `lyrics-lookup` (states `not-found` and `instrumental`) and the `button` `forget-lyrics-lookup` in the `section` `lyrics`; the icon slot `forget-lyrics-lookup`.
    - Additions without a version change: blocked uploaded themes. The state `blocked` of `theme-item`, the `badge` token `blocked` in `theme-meta` and the states `outdated` and `blocked` of `theme-problems`, see [Checks after an update](#checks-after-an-update).
    - Additions without a version change: the intensity of the visualizations, the speed of Milkdrop and color schemes. On the settings page the `field` `visualization-intensity` (a `segmented-control` with the `segment`s `normal` and `reduced`) and the `field` `visualization-milkdrop-speed` (a `select`); the attribute `data-intensity` of `visualization` and `player-visualization`; the button `btn-color-scheme-picker` (states `open` and `closed`) with the icon slot `color-scheme`, the state `has-color-schemes` and the grid area `scheme` of `visualization-controls`, and the value `visualization-color-schemes` of `data-kind` on `context-menu`.
- **Version 6:** playlist covers as a grid. Changed: a `playlist-cover` is a 2x2 grid of the new `playlist-cover-tile`s (attribute `data-index`, state `changing`), and its `playlist-cover-slide`s lie in these tiles instead of directly in the `playlist-cover`; the base no longer stacks all slides of the cover and fades between them, but slides a new song cover into one tile at a time. `playlist-cover` carries only `--wmp-item-count` (no longer `--wmp-active-index`), and `data-count` goes up to 12. Added: the state `rotating` of `playlist-cover`, the states `loading` and `entering` and the attribute `data-from` of `playlist-cover-slide`, the variables `--wmp-playlist-cover-gap`, `--wmp-playlist-cover-slide-duration`, `--wmp-playlist-cover-slide-easing` and `--wmp-playlist-cover-fade-duration`. Themes that styled `playlist-cover > playlist-cover-slide` have to style `playlist-cover-tile > playlist-cover-slide`. Uploaded themes written for version 5 are still accepted.
- **Version 5:** visualizations. Changed: the `visualization` is no longer a placeholder: the state `placeholder` is gone, its `icon` and `label` moved into the new `visualization-message`, and the base lays out the `visualization` as a positioned box (no longer a grid) with the background `--wmp-stage-view-background`; themes that styled `visualization > icon` or `visualization > label` have to style `visualization-message > icon` and `visualization-message > label`; `btn-fullscreen` also lies in `visualization-controls`, so theme rules for `btn-fullscreen` that should only apply to the video need `video-stage > btn-fullscreen`. Added: the states `idle`, `loading`, `waiting`, `running`, `paused`, `unsupported`, `failed` and `fullscreen` of `visualization` and its attribute `data-module`; the parts `visualization-canvas`, `visualization-message`, `visualization-controls`, `btn-preset-previous`, `btn-preset-next` and `btn-auto-change` (states `on` and `off`, `aria-pressed`); the values `visualization-preset` and `visualization-auto-change` of `data-field`; the icon slots `preset-previous`, `preset-next` and `auto-change`.
    - Additions without a version change: the full-screen player and long titles. The parts `swipe-grip` and `player-view-switch` (states `open` and `closed`, with the `segmented-control` `player-view`) in `transport`; `player-media` (states `cover`, `video`, `visualization` and `loading`) around the `cover` of `now-playing-mini`, with `player-video` and `player-visualization` in the full-screen player; `marquee` (states `fit`, `overflow`, `waiting`, `scrolling` and `end`) inside `now-playing-title`, `now-playing-artist`, `compact-player-title` and `compact-player-artist`, which hold their text there now; the states `view-switch`, `dragging`, `settling` and `dismissing` of `transport`; the variable `--wmp-marquee-fade-size` and the runtime variables `--wmp-marquee-distance`, `--wmp-marquee-duration`, `--wmp-swipe-offset`, `--wmp-swipe-progress` and `--wmp-swipe-duration`. In the full-screen player `now-playing-mini` opens and closes `player-view-switch` (`aria-expanded` tells that now).
    - Additions without a version change: the motion system (see [Motion](#motion)). The variables `--wmp-motion-duration-fast`, `--wmp-motion-duration-medium`, `--wmp-motion-duration-slow`, `--wmp-motion-duration-fade`, `--wmp-motion-easing-standard`, `--wmp-motion-easing-enter`, `--wmp-motion-easing-exit`, `--wmp-motion-scale` and `--wmp-motion-press-scale`; the base now animates hover and press, the `indicator` of groups while the person works with them, `context-menu`s, `dialog`s, the drawer and the collapsing sidebar, pages and entries of lists. The drawer on phones slides with these variables (it used to take `0.2s`). Existing parts and states are unchanged; a theme with its own `transition` on a pressed part keeps `scale` in it to keep the press.
    - Additions without a version change: the logo as a link and touch targets on phones. The `logo` in `header` is an `a` that leads to the start page (it was a `span`; its children stay the same, the base sets `color: inherit` and no underline); the `checkbox` of a `review-item` lies in the new part `review-select` (a `label`, state `disabled`), which takes the touch; the variable `--wmp-touch-target-size` and the minimum sizes of [Touch targets](#touch-targets) below 64rem; there `--wmp-field-height` is `2.75rem` (was `2.5rem`), the `meta-item`s of `list-group-meta` stand in a wrapping row, and the `seek` of the full-screen player is as high as a touch target.
    - Additions without a version change: the video badge in lists of songs. The `badge` of a song with a video comes before the title `label` in the `list-cell` `title` (it used to follow it); the state `has-video` of `list` while one of its songs has a video, in which the base gives the `list-column` and the `list-cell`s `title` a column of the new variable `--wmp-list-badge-width` for the badge, so the titles stay in line. Themes that put space before the badge (`margin-inline-start`) should drop it.
    - Additions without a version change: the ambient light of videos. The parts `video-ambient` (states `off`, `waiting`, `running`, `paused` and `static`) between `video-backdrop` and `video` in `video-stage`, and two `video-ambient-canvas`es in it (states `current` and `previous`); the variables `--wmp-video-ambient-intensity`, `--wmp-video-ambient-blur`, `--wmp-video-ambient-saturation`, `--wmp-video-ambient-scale`, `--wmp-video-ambient-transition-duration` and `--wmp-video-ambient-fade-duration`; the runtime variable `--wmp-video-ambient-color`; the `data-field` `video-ambient` on the settings page.
    - Additions without a version change: the recap and the statistics. The pages `data-view="recap"`, `statistics`, `shared-recaps` and `shared-recap` with their states, `recap-nav` and `recap-nav-item`, `recap-period` (the `segmented-control` `recap-period` and the `field-row` `custom-period`), `recap-layout`, `recap-stage`, `recap-panel`, the story `recap-story` (attributes `data-recap-design` and `data-decoration`, states `playing`, `paused` and `loading`) with `recap-backdrop`, `recap-progress`, `recap-progress-segment`, `recap-progress-fill`, `recap-slides`, `recap-slide` (`data-kind`), `recap-cover`, `recap-slide-title`, `recap-figure`, `recap-figure-label`, `recap-ranking`, `recap-ranking-item` (`data-rank`), `recap-rank`, `recap-ranking-info`, `recap-ranking-title`, `recap-ranking-subtitle`, `recap-ranking-value`, `recap-facts`, `recap-fact`, `recap-fact-value`, `recap-caption`, `recap-controls`, `btn-recap-previous`, `btn-recap-pause` and `btn-recap-next`; `recap-export`; `recap-share-list`, `recap-share-item`, `recap-share-info`, `recap-share-title`, `recap-share-meta` and `recap-share-actions`; the bar chart `chart`, `chart-bars`, `chart-bar`, `chart-bar-fill`, `chart-bar-label` and `chart-tooltip` with the runtime variable `--wmp-bar-value`; the tables `stats-table-scroll`, `stats-table`, `stats-table-row`, `stats-table-heading` and `stats-table-cell`; `--wmp-progress-value` also on `recap-progress-segment`; the recap variables (`--wmp-recap-background-start` to `--wmp-recap-transition-duration`), `--wmp-chart-height` and `--wmp-chart-bar-color`; further values of `data-item`, `data-field` and `data-column`; the icon slots `tree-recap`, `tree-statistics`, `tree-shared-recaps`, `recap`, `statistics`, `shared-recaps`, `recap-previous`, `recap-next`, `save-picture` and `save-video`; the node `recap` of the sidebar tree with `statistics` and `shared-recaps`.
    - Additions without a version change: clearing the playback state and a compact administration navigation. On the settings page the `section` `resume` with a `form-note`, an `alert` after clearing, the `button` `clear-playback-state` and its `dialog` `clear-playback-state` with the `button` `confirm-clear-playback-state`. The base gives `admin-nav-item` the font size `--wmp-font-size-s` and the side padding `--wmp-space-s`, so all pages of `admin-nav` fit in one row next to the sidebar in a window 1280 pixels wide, also in German; themes that widen the entries should keep that. In the library, `source-filter` is also shown for genres and years.
    - Additions without a version change: the library check of the administration. The page `data-view="admin-checks"` with the `section`s `check-status` and `issues`, `library-check-status` and its states, `library-issue-filters` (the `segmented-control`s `issue-type` and `issue-sort`), `library-issue-list`, `library-issue-item` with `data-item` and its states, `library-issue-info`, `library-issue-meta`, `library-issue-title`, `library-issue-detail` and `library-issue-actions`; the optional `badge` of a `segment` (tokens `count`, `problem`, `hint`, `all`, `empty`); the states of `empty-state` on this page; the entry `admin-checks` of `admin-nav`, the job `library-check`, further values of `data-item` and `data-field`, and the icon slot `admin-checks`.
    - Additions without a version change: smart playlists. Tiles of smart playlists (`data-kind="smart-playlist"`) among the playlists with the new part `tile-badge`, their entries in the sidebar (icon slot `tree-smart-playlist`); the page `data-view="smart-playlist"` with `smart-summary`, `smart-summary-intro`, `smart-summary-rules`, `smart-summary-rule` and `smart-summary-order` and the `meta-item` `kind`; the editor `data-view="smart-playlist-edit"` (states `create` and `edit`) with `form` `data-kind="smart-playlist"`, `smart-templates`, `smart-rules`, `smart-rule` (state `invalid`, attributes `data-kind` and `data-values`) and `smart-rule-values`; new values of `data-field` and `data-item` (menu entries `edit-rules` and `convert` of a smart playlist; actions `edit-rules` and `convert` of its page, `create-smart-playlist` on the playlist overview); the variable `--wmp-list-columns-smart-playlist` and the icon slots `tree-smart-playlist`, `smart-playlist`, `create-smart-playlist`, `convert-playlist` and `remove-rule`.
    - Additions without a version change: the mini player (compact mode). `btn-compact` is no longer disabled: it opens and closes the mini player (`aria-pressed`, states `on` and `off`). The parts `compact-player` (attribute `data-kind` with `window` or `inline`, states `idle`, `playing`, `paused`, `loading` and `video`), `compact-player-panel`, `compact-player-media` (states `cover` and `video`), `compact-player-video`, `compact-player-info` (state `empty`), `compact-player-title`, `compact-player-artist`, `compact-player-seek` (state `disabled`), `compact-player-time` (state `empty`, with `time-elapsed` and `time-duration`), `compact-player-controls` (with `btn-prev`, `btn-play` and `btn-next`), `compact-player-volume` (with `btn-mute` and `volume`) and `btn-compact-close`; the variables `--wmp-compact-player-color`, `--wmp-compact-player-background`, `--wmp-compact-player-width` and `--wmp-compact-player-height`; the icon slot `expand-player`.
    - Additions without a version change: autoplay. The toggle button `btn-autoplay` (states `on` and `off`, `aria-pressed`) between `btn-crossfade` and `btn-stop` with the icon slot `autoplay`; in the mobile full-screen player the second row holds `btn-mute`, the volume and `btn-stop`, and a third row `btn-crossfade` and `btn-autoplay`; the `section` `autoplay` after the `queue-list` in `queue` (states `loading`, `ready` and `empty`, `aria-busy`) with a `section-title` (`h3`), the `button` `turn-off-autoplay`, `autoplay-hint`, `autoplay-list`, `autoplay-item` (state `video-only`), `autoplay-item-info`, `autoplay-item-title`, `autoplay-item-artist`, `autoplay-item-duration`, `autoplay-item-actions` (`button` `remove` and `btn-item-menu`) and `autoplay-status`; the state `autoplay` of `queue-item`; the `data-field`s `autoplay` and `autoplay-video-only` on the settings page.
    - Additions without a version change: context menus. One context menu for songs (in lists, on tiles of the home page and in search), albums, artists, playlists, queue entries and Now Playing (right click, long press, the menu key and Shift+F10); entries that do not apply stay in the menu, disabled; submenus (a nested `context-menu` with `data-kind="submenu"`, `menu-item` with `aria-haspopup`, `aria-expanded` and `aria-controls`, the part `submenu-indicator`); the button `btn-item-menu` in `tile-item`, in the `list-cell` `favorite`, in `queue-item-actions` and in `stage-actions`, hidden by the base where a pointer can hover; `tile-item` is a containing block (`position: relative`) and the `list-cell` `favorite` a flex row; several selected `list-row`s in lists of songs (`aria-multiselectable` on `list`); new values of `data-item` in the menus; the icon slots `item-menu`, `watch-video`, `show-files`, `rate` and `submenu`. The defaults of `--wmp-list-columns`, `--wmp-list-columns-playlist` and `--wmp-list-columns-recently-played` end in `minmax(2rem, auto)`, so the favorite column makes room for `btn-item-menu` on touch screens; themes that set these variables should do the same.
    - Additions without a version change: several media folders. On the library page of the administration the `section` of the media folders (`data-item="media-sources"`) with the `button` `add-source` in `view-actions`, `source-list`, `source-item` and its states, `source-info`, `source-name`, `source-path`, `source-meta` with the `badge` tokens `active`, `inactive`, `unavailable`, `watched` and `media-path`, and `source-actions` with the `button`s `toggle`, `edit` and `delete`; `issue-source` in `issue-item`; in the library `source-filter` after `scope-switch`.
    - Additions without a version change: outdated uploaded themes. The part `theme-problems` in `theme-item`, the state `outdated` of `theme-item`, the `badge` token `outdated` in `theme-meta` and the code `svg.not_cleaned`, see [Checks after an update](#checks-after-an-update).
    - Additions without a version change: fonts in uploaded themes. A theme package can contain WOFF2 fonts for `@font-face` (see [Fonts of an uploaded theme](#fonts-of-an-uploaded-theme)), with the limit `Themes__MaxFontBytes` and the code `font.invalid`.
    - Additions without a version change: more themes and their variants. The themes Meadow (`meadow`), Daylight (`daylight`), Accent (`accent`, light and dark), Pinstripe (`pinstripe`), Clear Glass (`clear-glass`, light and dark), Classic 9x (`classic-9x`) and Skinned (`skinned`); the attribute `data-theme-variant` on the root element (see [Variants](#variants)); the runtime variables `--wmp-cover-color` and `--wmp-cover-color-contrast` on the root element; the theme picker of the settings (`section` `appearance` with the `field`s `theme` and `theme-appearance`, the parts `theme-picker` with an `indicator` and the runtime variables of an active element, `theme-option` with `data-item` and `data-variants`, `theme-preview` with `data-kind` and the state `missing`, `theme-preview-image` and `theme-variants`); the time display `transport-time` (state `empty`) with `time-elapsed` and `time-duration` in `transport`, hidden by the base. The default of `--wmp-profile-secondary-opacity` is `0.7` instead of `0.55`, so the label of the built-in admin keeps 4.5:1. Fixes of the base: the knob of a vertical `slider` (the bands of the `equalizer`) sits at the level of its fill, measured against `--wmp-equalizer-slider-height` (before, it stayed near the bottom); `now-playing-artist`, `queue-item-artist`, `queue-item-duration` and `equalizer-scale` are no longer dimmed with `opacity`, the last three take `--wmp-color-text-muted` instead; an `equalizer` in the state `off` fades only the `slider-fill`s and `slider-thumb`s of its bands, no longer the whole `equalizer-bands`, so scale, values and band names keep their contrast.
    - Additions without a version change: the review inbox of the administration. The entry `btn-review-inbox` of `header` (states `pending`, `empty`, `active`; a `badge` `unreviewed` with the number) without a grid area of its own, the empty cell for it in the base layouts of `header`; the page `data-view="admin-review"` with `review-filters` (the `segmented-control`s `review-filter` and `review-sort`), `review-bulk`, `review-list`, `review-item`, `review-info`, `review-title`, `review-meta` and `review-tags` and their states; the page of a song `data-view="admin-review-track"` with `review-actions`, `review-nav` and the state `finished` of `empty-state`; the tokens `unreviewed`, `reviewed` and `missing-tag` of `badge`, the entry `admin-review` of `admin-nav`, further values of `data-item` and `data-field`, and the icon slots `review-inbox`, `admin-review`, `tags-missing`, `review-done`, `review-skip`, `review-previous` and `review-next`.
    - Additions without a version change: PINs of the profiles. On the profile selection the lock symbol `profile-lock` in `profile-tile` (state `protected`) and the PIN entry `pin-entry` with `pin-entry-title`, `pin-entry-actions` and the digit pad `pin-pad` (`pin-field`, `pin-dots` with `data-count`, `pin-dot`, `pin-keys`, `pin-key` with `data-key`) and their states; the PIN page `data-view="settings-pin"` with the `data-field` values `pin`, `pin-confirmation` and `pin-status`, the `section` `pin` of the settings page, the `badge` token `pin` in `user-meta`, further values of `data-item` and the icon slots `profile-lock`, `pin-delete`, `pin-submit`, `has-pin` and `reset-pin`.
    - Additions without a version change: pictures of the profiles. `avatar-image` in `avatar` and the state `image` of `avatar`; the avatar dialog (`dialog` `avatar`) with `avatar-editor` and its states, the built-in pictures `avatar-presets`, `avatar-preset` and `avatar-preset-image`, the cropping `avatar-crop` (state `dragging`), `avatar-crop-frame`, `avatar-crop-image` and `avatar-crop-preview` with the runtime variables `--wmp-crop-scale`, `--wmp-crop-x` and `--wmp-crop-y`; the `section` `avatar` of the settings page with the `form-note` `avatar-status`, further values of `data-item` and `data-field` and the icon slot `change-avatar`.
    - Additions without a version change: the visualization A-Force and the quality of the visualizations. The value `aforce` of `data-module`, the attribute `data-quality` of `visualization` (`low`, `medium`, `high`), the `segmented-control` `visualization-module` (`segment`s `milkdrop` and `aforce`) at the end of `visualization-controls`, and on the settings page the `field` `visualization-quality` (a `segmented-control` with the `segment`s `auto`, `low`, `medium` and `high`).
    - Additions without a version change: the visualization BlackCap. The value `blackcap` of `data-module` and the `segment` `blackcap` at the end of the `segmented-control` `visualization-module`.
    - Additions without a version change: the backup page of the administration (`backup-status` with its states, `backup-list`, `backup-item` with `data-kind` and the state `busy`, `backup-info`, `backup-name`, `backup-meta`, `backup-actions`, `data-view="admin-backups"`, the entry `admin-backups` of `admin-nav`, the job `database-backup`, the tokens `schedule`, `manual` and `migration` of `badge`, further values of `data-item` and `data-field`) with the icon slots `admin-backups` and `backup`.
    - Additions without a version change: the back button `btn-detail-back` of the detail pages (album, artist, genre, year, song, playlist, "Recently played") before `detail-header` and of the metadata editor in `dialog-header`, with the icon slot `detail-back`. Pages with lists stay in the background while another page is shown and keep their content and position for back and forward.
    - Additions without a version change: the file selection per song in the administration. The page `data-view="admin-files"` with `file-selection-filters` (the `segmented-control`s `file-filter` and `file-sort`), `file-selection-list`, `file-selection-item` and its states, `file-selection-info` and `file-selection-title`; the page of the files of a song `data-view="admin-track-files"` with the radio groups `file-choices` (an active element with `indicator`) and `file-choice` and their states; `file-path` and `file-meta` in these parts, the tokens `selected`, `missing`, `embedded`, `active` and `best` of `badge`, the entry `admin-files` of `admin-nav`, the `link` `choose-files` and the `button` `reset-file-selection`, with the icon slot `admin-files`.
    - Additions without a version change: lyrics. The `stage` is a size container (`wavemp-stage`) and shows the `segment`s of `stage-actions` with their icons only below `32rem`. The `segment` `lyrics` at the end of the `segmented-control` `stage-view` with the icon slot `view-lyrics`, the value `lyrics` of `data-kind` on `stage-view` and of the state of `stage`; the parts `lyrics` (states `synchronized` and `static`, `data-words`), `lyrics-lines`, `lyrics-line` (states `past`, `active` and `upcoming`, `data-index`, `data-empty`, `aria-current`), `lyrics-line-button` and `lyrics-word` (states `past`, `active` and `upcoming`); the runtime variables `--wmp-line-progress` and `--wmp-word-progress` and `--wmp-item-count` and `--wmp-active-index` on `lyrics`; the variables `--wmp-lyrics-color`, `--wmp-lyrics-muted-color`, `--wmp-lyrics-active-scale` and `--wmp-lyrics-transition-duration`. For admins the `section` `lyrics` on the page of a song with the `form-note` `lyrics-status` and the `button` `download-lyrics` (icon slot `download-lyrics`), the `field` `download-lyrics` on the library page of the administration, the job `lyrics-download` and the token `lyrics` of the `badge` of a file.
    - Additions without a version change: cover rules. For admins the `section` `cover-rule` on the page of an album and of a song with a `form` holding `cover-preview` (states `loading`, `ready`, `empty`), the `field` `cover-rule` (a `segmented-control` with the `segment`s `inherit`, `album-first` and `track-first`) and the `button` `save-cover-rule`; on the library page of the administration the `field` `cover-priority` (a `segmented-control` with the `segment`s `album-first` and `track-first`).
    - Additions without a version change: casting. In `mode-switch` the part `cast` (states `chromecast`, `airplay`, `available`, `insecure`, `disconnected`, `connecting`, `connected`, `no-devices`, `hint`) with `btn-cast` (states `disconnected`, `connecting`, `connected`) and `cast-hint` (states `open`, `closed`), the icon slot `cast`, `cast-status` in `now-playing-meta`, `data-kind="cast"` on `stage-notice`, and the `form-note` `cast-effects` on the settings page. While casting, `btn-crossfade`, `btn-equalizer`, the `segment` `visualization` of `stage-view` and the fields `replay-gain`, `equalizer`, `crossfade` and `crossfade-albums` of the settings are disabled.
    - Additions without a version change: scrobbling. On the settings page the `section` `scrobbling` with `scrobble-list`, `scrobble-item` (`data-item`, states `connected`, `not-connected`, `authorization-failed`, `unavailable` and `busy`), `scrobble-info`, `scrobble-name`, `scrobble-meta` and `scrobble-actions`, the `badge` tokens of these states in `scrobble-meta`, the token dialog `scrobbling-token`; the page `data-view="settings-scrobbling-lastfm"` (states `connecting` and `failed`) that Last.fm returns to; further values of `data-item` and `data-field`.
    - Additions without a version change: uploaded themes, see [Uploaded themes](#uploaded-themes). The theme page of the administration (`data-view="admin-themes"`, the entry `admin-themes` of `admin-nav` with its icon slot, the `section`s `theme-upload` and `themes`) with `theme-list`, `theme-item` (`data-item`, `data-variants`, state `busy`), `theme-info`, `theme-name`, `theme-meta`, `theme-actions` (`button`s `replace` and `delete`), the `theme-preview` of a `theme-item`, the `link` `theme-template`, the `button` `upload-theme` with the icon slot `upload` and the list of the problems of a rejected package (`theme-errors`, `theme-error` with the code as `data-item`, `theme-error-location`, `theme-error-message`). The theme picker of the settings page also lists the uploaded themes. The base lets the `admin-nav` wrap onto further rows instead of scrolling sideways, so all its pages fit on tablets as well.
    - Additions without a version change: the online search for album info. For admins the `button` `search-online` (icon slot `search-online`) in `detail-actions` of a song and an album opens the `dialog` `online-search` with `online-search` (attribute `data-owner`, states `loading`, `ready`, `applying`, `applied`, `disabled`, `unavailable`, `notFound`, `error`), `online-section` and `online-section-title`, the radio group `online-matches` (an active element with `indicator`) with `online-match` (state `selected`, runtime variable `--wmp-agreement`), `online-match-title`, `online-match-artist`, `online-match-album`, `online-match-release`, `online-match-detail` and `online-match-agreement`, and the table `online-compare` with `online-compare-head`, `online-compare-row` (`data-field`, `data-active`, states `missing`, `different`, `same`, `unknown` and for the cover `none`, `loading`, `ready`, `missing`, `error`), `online-compare-field`, `online-compare-current` and `online-compare-suggested`; on the library page of the administration the `section` `online-metadata` with the `form` `online-metadata-settings` and its `field`s.
    - Additions without a version change: keyboard shortcuts. The overview of the shortcuts (key ?, or the new entry `shortcuts` of the profile menu with the icon slot `keyboard-shortcuts`) is the `dialog` `shortcuts` with `shortcut-overview`, `shortcut-group`, `shortcut-group-title`, `shortcut-list`, `shortcut-item`, `shortcut-description`, `shortcut-keys`, `shortcut-combo`, `shortcut-key` (attribute `data-key`), `shortcut-key-separator` and `shortcut-hint`, with the variables `--wmp-key-color`, `--wmp-key-background`, `--wmp-key-border-color` and `--wmp-key-size`.
    - Additions without a version change: resuming playback. The part `resume-notice` in `transport` ("Continue listening from" another device, `role="status"`) with an `icon`, a `label` holding `resume-device`, and the `button`s `btn-resume` and `btn-dismiss-resume`; the icon slot `resume-device`.
    - Fixes without a version change: the bottom navigation. The mobile base lays out every `bottom-nav-item` the same way (`icon` above `label`, both centered, icons at the same height in all entries, at most two centered lines of label) and gives it a padding at the top and bottom, so an entry with a long label grows together with its highlight instead of pushing its icon out of it; the entry of Now Playing shows a short label and carries the full one as `title` and `aria-label`. The regression of the shipped themes photographs the bottom navigation of `phone-now-playing` on its own as well (`phone-now-playing-bottom-nav`).
- **Version 4:** fixes from the first test on phones and tablets. Changed: the `video` lies over the whole `video-stage` with a transparent background and always shows its picture whole and centered, also for portrait videos and in full screen; the free areas around it show the background of the `video-stage` and the new `video-backdrop`, so themes that styled the background of the `video` have to style the `video-stage` or the `video-backdrop` instead; `app` pads the safe areas at the top and at the sides (showing `--wmp-theme-color`) instead of the mobile `header`, and the transport bar on the desktop pads the safe area at the bottom; the page never scrolls, `sidebar` and `content` are containing blocks and contain their overscroll; the `list` is one grid whose `list-header`, `list-group`, `list-group-rows` and `list-row` are subgrids, so `list-group` no longer has grid areas and `list-header` no longer has a margin at its start, and the padding at the start and end of rows moved to their first and last `list-column` and `list-cell`; the `label` of the column header `favorite` is visually hidden; icons in buttons (`data-variant`) no longer inherit `--wmp-icon-fill` from their area but take the text color of the button, unless the theme sets `--wmp-icon-fill` on the button or its `icon`; mutually exclusive options are segmented controls: the buttons `btn-view-cover`, `btn-view-video` and `btn-view-visualization` are replaced by the `segment`s `cover`, `video` and `visualization` of the `segmented-control` `stage-view` in `stage-actions`, and the `button`s of `scope-switch` by the `segment`s `all-music` and `my-music` of the `segmented-control` `scope`, both checked with `aria-checked` instead of `aria-pressed`; `stage-actions` and `scope-switch` lost their `role="group"`; the ReplayGain mode on the settings page, the role in the dialog of a profile and the hardware acceleration on the transcoding page are segmented controls instead of `select`s; the `queue-list` shows only the last three played entries before the current one, and `queue-item-info` is a grid with the `cover` of the song in its first column, spanning title and artist. Added: the part `video-backdrop`, the attribute `data-orientation` on `video-stage`, the runtime variable `--wmp-video-aspect-ratio` and the variable `--wmp-theme-color`, which the app also hands to the browser as theme color; the toggle button `btn-crossfade` (states `on` and `off`, `aria-pressed`) between `btn-repeat` and `btn-stop` with the icon slot `crossfade`, in the mobile full-screen player in the second row between the volume and `btn-stop`; the parts `segmented-control` (`role="radiogroup"`, `data-item`, `aria-disabled`, `aria-invalid`, the runtime variables `--wmp-item-count`, `--wmp-active-index`, `--wmp-indicator-x` and `--wmp-indicator-width`) and `segment` (`role="radio"`, `aria-checked`) with an `indicator`, and the values `role` and `hardware-acceleration` of `data-field`; the collapsible sidebar on the desktop with the button `btn-sidebar-toggle`, the states `collapsed` and `expanded` of `sidebar`, the variable `--wmp-sidebar-collapsed-width` and the icon slots `sidebar-collapse` and `sidebar-expand`; the `cover` in `queue-item-info` with the variable `--wmp-cover-size-queue` and the attribute `start` of `queue-list`.
    - Theme-capable groups: every group with one active entry (`tabs`, `bottom-nav`, `admin-nav`, `tree`, `color-picker` and `segmented-control`) has an `indicator` as first child and carries the runtime variables of its active entry, see [Active element of a group](#active-element-of-a-group). Changed: `tabs`, `bottom-nav`, `admin-nav`, `tree` and `color-picker` are containing blocks (`position: relative`) with a new first child; the `indicator` of a `tab` is `aria-hidden`. Added: the `indicator` of these groups, the runtime variables `--wmp-item-count`, `--wmp-active-index`, `--wmp-indicator-x` and `--wmp-indicator-width` on them and the new runtime variables `--wmp-indicator-y` and `--wmp-indicator-height` (also on `segmented-control`), `--wmp-item-count` and `--wmp-active-index` on `rating` and `playlist-cover`, and the documented states `on` and `off` of the `button` `power` of `equalizer-controls`.
- **Version 3:** cover, video and visualization on the stage, with a fade. Changed: `btn-video` is replaced by `btn-view-video`, one of the three buttons `btn-view-cover`, `btn-view-video` and `btn-view-visualization`, of which the chosen view is pressed; `stage-actions` is shown for every current song, not only for songs with a video; the `cover` and the `video-stage` of the stage no longer lie directly in `stage` but each in a `stage-view` inside `stage-views`, so themes that address `stage > cover`, `stage cover` or `stage > video-stage` have to use `stage-view > cover` and `stage-view > video-stage`; the `video` is muted and follows the music. Added: the state `visualization` of `stage`, the parts `stage-views`, `stage-view` (with `data-kind` and the states `active`, `loading` and `failed`), `stage-notice`, `visualization` (state `placeholder`) and the `btn-view-*` buttons, the variables `--wmp-stage-transition-duration` and `--wmp-stage-view-background`, and for admins the `section` `video-sync` on the page of a song with a video (`field` `video-offset`, `button` `save-video-offset`). The icon slots `view-cover`, `view-video` and `view-visualization` now belong to the content.
    - Additions without a version change: the versions on the settings page, a `section` `about` with a `stat-list` whose `stat-item`s carry `data-field` `app-version` and `server-version`.
    - Additions without a version change: equalizer, crossfade and sleep timer. The page `data-view="equalizer"` below Now Playing with the parts `equalizer` (states `on` and `off`), `equalizer-controls`, `equalizer-bands`, `equalizer-band` (`data-kind` `preamp` or `band`, `data-frequency`), `equalizer-value` and `equalizer-scale`; the vertical `slider` (`data-orientation`, `aria-orientation`) with the variable `--wmp-equalizer-slider-height`; in `mode-switch` the button `btn-equalizer` (states `on` and `off`) and the sleep timer `sleep-timer` (states `off`, `duration`, `end-of-track`, `end-of-queue` and `fading`) with `btn-sleep-timer` and `sleep-timer-remaining`; on mobile the full-screen player shows `mode-switch` below the transport controls; new values of `data-field` (`equalizer`, `crossfade`, `crossfade-albums`, `sleep-timer-fade`, `equalizer-preset`) and `data-item` (`power`, `reset`, `open-equalizer` and the entries of the sleep timer menu), `section-title` on the settings page and the icon slots `equalizer`, `sleep-timer` and `reset`.
- **Version 2:** favorites, ratings and the home page. Changed: the lists of songs have the new columns `rating` (stars) and `favorite` (heart) at their end, so the defaults of `--wmp-list-columns`, `--wmp-list-columns-playlist` and `--wmp-list-columns-recently-played` have two more columns; the new part `now-playing-actions` of `transport` takes the start of the grid area `mode-switch`, which now ends at the end of its cell (`justify-self: end`). Themes that set these variables have to add the new columns; themes that rearrange `transport` have to place `now-playing-actions`. Added: the parts `btn-favorite`, `rating`, `rating-star`, `scope-switch`, `now-playing-actions`, `shelf`, `shelf-header`, `shelf-title` and `shelf-actions`, their states, the attribute `data-value`, the home page (`data-view="home"`, tab and bottom navigation entry `home`), the library section `favorites`, the tile kind `track`, the `meta-item` `rating` on the page of a song, new values of `data-item` (menu entry `favorite`; detail action `favorite`; `all-music`, `my-music`; the rows `playlists`, `recently-played`, `recently-added`, `favorites`, `top-rated` of `shelf`), the sort key `sort-rating`, the variables `--wmp-favorite-color`, `--wmp-rating-color` and `--wmp-shelf-tile-width` and the icon slots `home`, `track`, `favorite`, `favorite-off`, `rating-star`, `rating-star-off`, `add-favorite` and `remove-favorite`; `tree-favorites` is no longer reserved.
    - Additions without a version change: videos. The `stage` shows the video of songs with one (`stage-actions`, `btn-video`, `video-stage`, `video`, `btn-fullscreen`, the states `video` and `cover` of `stage` and `fullscreen` of `video-stage`) and the icon slot `exit-fullscreen`; `fullscreen` is no longer reserved and belongs to the content. The notice `unsupported` of `transport-message` now means that the server cannot transcode the song.
    - Additions without a version change: the transcoding page of the administration (`transcoding-status`, `transcode-list`, `transcode-item`, `transcode-info`, `transcode-title`, `transcode-meta`, `transcode-actions`, `data-view="admin-transcoding"`, the entry `admin-transcoding` of `admin-nav`, further values of `data-item` and the tokens of `badge` and `transcode-item`) with the icon slot `admin-transcoding`.
- **Version 1:** first version with the app shell, the base components and the theme Midnight.
    - Additions without a version change: the profile selection (`profile-select`, `profile-select-title`, `profile-select-hint`, `profile-list`, `profile-item`, `profile-tile`, `avatar`, `avatar-initials`), the profile menu `btn-profile` with the new header area `btn-profile`, the settings page (`profile-summary`, `data-view="settings"`), the profile administration (`view-actions`, `user-list`, `user-item`, `user-info`, `user-name`, `user-meta`, `user-actions`, `data-view="admin-users"`), the base components `alert`, `form`, `select`, `field-hint`, `field-error`, `color-picker` and `color-swatch`, the icon slots `switch-profile` and `manage-profiles`, the variables `--wmp-profile-select-color`, `--wmp-profile-select-background`, `--wmp-avatar-color`, `--wmp-profile-secondary-opacity`, `--wmp-avatar-size`, `--wmp-avatar-size-l` and `--wmp-swatch-size` and the runtime variables `--wmp-profile-color` and `--wmp-swatch-color`.
    - Additions without a version change: the administration (`admin-nav`, `admin-nav-item`, `section`, `section-title`, `scan-status`, `progress`, `progress-track`, `progress-fill`, `stat-list`, `stat-item`, `stat-value`, `run-list`, `run-item`, `run-meta`, `issue-list`, `issue-item`, `issue-path`, `disclosure`, `disclosure-summary`, `job-list`, `job-item`, `job-info`, `job-name`, `job-meta`, `job-actions`, `data-view="admin-library"` and `data-view="admin-jobs"`), the base component `checkbox` with `data-variant="checkbox"` on `field`, the attribute `data-item` (`menu-item` already carried it, now it is documented), the states of `admin-nav-item`, `scan-status`, `job-item`, `run-item`, `progress` and the new tokens of `badge`, the icon slots `administration`, `admin-library`, `admin-jobs`, `scan` and `run-job`, the variables `--wmp-progress-height`, `--wmp-progress-track-background` and `--wmp-progress-fill-background` and the runtime variable `--wmp-progress-value`. The profile menu entry `manage-profiles` became `administration`.
    - Additions without a version change: the library with tiles (`tile-list`, `tile-item`, `tile`, `tile-title`, `tile-subtitle`, attribute `data-kind`), detail pages (`detail-header`, `detail-info`, `detail-title`, `data-view` `library-album`, `library-artist`, `library-genre`, `library-year`, `library-track`) and the search (`data-view="library-search"`), the part `cover-image`, the button `load-more` with the state `loading`, the files of a song for admins (`file-list`, `file-item`, `file-path`, `file-meta` and their states), the state `video-only` of `list-row` and `badge` (in addition to `has-video`, which keeps its meaning), `aria-busy` on `view`, `menuitemradio` entries with `aria-checked` in the sort options, further values of `data-field` and `data-item` (also on `section`), `link` in `meta-value`, `view` and `section`, the icon slots `album`, `artist`, `genre`, `year`, `go-to-album` and `go-to-artist` and the variables `--wmp-cover-size-l` and `--wmp-tile-width`. The sort button `sort` is active where a list can be sorted.
    - Additions without a version change: the metadata editor of a song for admins in a `dialog` (`detail-actions` with the `button` `edit-metadata`, `field-row`, `form-note`, `data-field` on its `field`s, the menu entry `edit-metadata` in the menu of a song). Disabled inputs (`input:disabled`) are greyed out by the base; an open `dialog` fits the screen and scrolls its `dialog-body`.
    - Additions without a version change: the cache page of the administration (`cache-usage`, `data-view="admin-cache"`, the entry `admin-cache` of `admin-nav`, further values of `data-item`) with the base component `meter` (`meter-track`, `meter-fill`, `role="meter"` with `aria-valuenow`), which uses the variables of `progress`, the runtime variable `--wmp-meter-value` and the icon slot `admin-cache`.
    - Additions without a version change: the player. The transport bar plays (`transport-message` with its states, the state `loading` of `transport`), Now Playing shows the current song (`stage-info`, `stage-title`, `stage-subtitle`) and the queue (`queue`, `queue-list`, `queue-item` with the states `current` and `played` and `aria-current`, `queue-item-info`, `queue-item-title`, `queue-item-artist`, `queue-item-duration`, `queue-item-actions`). The reserved state `playing` of `list-row` is now set. `data-field` on the `field`s of the settings, new values of `data-item` (menu entries `play`, `play-next`, `add-to-queue`; detail actions `play`, `shuffle`; queue buttons) and the icon slots `play-now`, `play-next`, `add-to-queue`, `shuffle-play`, `move-up`, `move-down`, `remove-from-queue` and `clear-queue`.
    - Additions without a version change: the playlists. Tiles of playlists (`data-kind` `playlist` and `recently-played`) with `playlist-cover` and `playlist-cover-slide`, the pages `data-view="playlist"` and `data-view="recently-played"` with `drag-handle` and the columns `handle`, `album` and `detail`, the dialog "Add to playlist" (`playlist-choices`, `playlist-choice`, `playlist-choice-count`), the states `busy` of `list` and `playlist-choice` and `active` of `playlist-cover-slide`, the attribute `data-count`, new values of `data-item` (menu entries `add-to-playlist`, `move-up`, `move-down`, `remove-from-playlist`; actions `rename`, `cover`, `remove-cover`, `delete`, `create-playlist`) the variables `--wmp-list-columns-playlist` and `--wmp-list-columns-recently-played` and the icon slots `tree-recently-played`, `tree-playlist`, `playlist`, `recently-played`, `drag-handle`, `add-to-playlist`, `remove-from-playlist` and `change-cover`.
