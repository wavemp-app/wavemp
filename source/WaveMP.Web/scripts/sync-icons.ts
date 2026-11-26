// Copies the Phosphor icons used by WaveMP from @phosphor-icons/core into /assets/web/icons,
// unchanged except for LF line endings and a final newline (.editorconfig), together with the
// license.
// Icons that are no longer listed are removed, so only the icons in use are shipped.
//
// Usage: npm run icons:sync (after adding an icon to the list below)

import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

type IconStyle = 'fill' | 'duotone' | 'regular'

// Phosphor icon names per style; the file is <name>-<style>.svg (regular: <name>.svg).
// Dark areas and toolbars use "fill", the sidebar "duotone", small hints, menus and dialogs
// "regular". The icon slots in src/themes/iconSlots.ts refer to these files.
const icons: Record<IconStyle, string[]> = {
    fill: [
        'arrow-left',
        'arrow-right',
        'backspace',
        'books',
        'calendar-blank',
        'caret-down',
        'caret-left',
        'caret-right',
        'corners-in',
        'corners-out',
        'devices',
        'dots-three-outline',
        'faders',
        'guitar',
        'headphones',
        'heart',
        'house',
        'infinity',
        'intersect',
        'list',
        'lock',
        'magic-wand',
        'magnifying-glass',
        'microphone-stage',
        'moon',
        'music-note',
        'music-notes',
        'palette',
        'pause',
        'play',
        'playlist',
        'repeat',
        'repeat-once',
        'rows',
        'screencast',
        'shuffle',
        'skip-back',
        'skip-forward',
        'sort-ascending',
        'speaker-high',
        'speaker-x',
        'squares-four',
        'star',
        'stop',
        'timer',
        'tray',
        'vinyl-record',
        'x',
    ],
    duotone: [
        'books',
        'calendar-blank',
        'caret-double-left',
        'caret-double-right',
        'chart-bar',
        'clock-counter-clockwise',
        'guitar',
        'headphones',
        'heart',
        'list-plus',
        'magic-wand',
        'microphone-stage',
        'music-note',
        'music-notes-simple',
        'playlist',
        'sparkle',
        'users-three',
        'vinyl-record',
    ],
    regular: [
        'arrow-counter-clockwise',
        'arrow-left',
        'arrow-right',
        'arrow-u-up-left',
        'arrows-clockwise',
        'broadcast',
        'caret-down',
        'caret-left',
        'caret-right',
        'caret-up',
        'chart-bar',
        'check-circle',
        'clock-clockwise',
        'cloud-arrow-down',
        'database',
        'desktop',
        'device-mobile',
        'device-tablet',
        'dots-six-vertical',
        'export',
        'eye',
        'files',
        'film-reel',
        'film-strip',
        'floppy-disk',
        'folder-open',
        'folder-simple-plus',
        'folders',
        'gear-six',
        'globe-simple',
        'hard-drives',
        'heart',
        'heart-break',
        'image',
        'image-square',
        'info',
        'keyboard',
        'link-break',
        'list-dashes',
        'list-plus',
        'lock',
        'lock-open',
        'magic-wand',
        'microphone-stage',
        'minus-circle',
        'minus-square',
        'monitor-play',
        'palette',
        'pencil-simple',
        'play',
        'playlist',
        'plus',
        'plus-square',
        'queue',
        'shuffle',
        'skip-forward',
        'sparkle',
        'speaker-hifi',
        'squares-four',
        'stack',
        'star',
        'stethoscope',
        'stop-circle',
        'subtitles',
        'swap',
        'television-simple',
        'trash',
        'tray',
        'tree-structure',
        'upload-simple',
        'user-circle',
        'user-switch',
        'users',
        'users-three',
        'video',
        'video-camera',
        'vinyl-record',
        'warning',
        'warning-circle',
        'watch',
        'waveform',
        'wrench',
        'x',
    ],
}

const webDirectory = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const packageDirectory = path.join(webDirectory, 'node_modules', '@phosphor-icons', 'core')
const targetDirectory = path.resolve(webDirectory, '..', '..', 'assets', 'web', 'icons', 'phosphor')

function fileName(name: string, style: IconStyle): string {
    return style === 'regular' ? `${name}.svg` : `${name}-${style}.svg`
}

function normalized(content: string): string {
    const lf = content.replaceAll('\r\n', '\n')
    return lf.endsWith('\n') ? lf : `${lf}\n`
}

if (!existsSync(packageDirectory)) {
    throw new Error('@phosphor-icons/core is missing, run npm install first.')
}

let copied = 0
let removed = 0
for (const [style, names] of Object.entries(icons) as [IconStyle, string[]][]) {
    const styleDirectory = path.join(targetDirectory, style)
    mkdirSync(styleDirectory, { recursive: true })

    const wanted = new Set(names.map((name) => fileName(name, style)))
    for (const file of wanted) {
        const source = path.join(packageDirectory, 'assets', style, file)
        writeFileSync(path.join(styleDirectory, file), normalized(readFileSync(source, 'utf8')))
        copied++
    }

    for (const file of readdirSync(styleDirectory)) {
        if (!wanted.has(file)) {
            rmSync(path.join(styleDirectory, file))
            removed++
        }
    }
}

const license = readFileSync(path.join(packageDirectory, 'LICENSE'), 'utf8')
writeFileSync(path.join(targetDirectory, 'LICENSE'), normalized(license))

console.log(`Phosphor icons: ${copied} copied, ${removed} removed (${targetDirectory})`)
