import type { Page } from '@playwright/test'

// Automated contrast check of WCAG 2.2 level AA for the shipped themes (THEMING.md, "Contrast").
// Themes draw text on gradients, glass and pictures, where a check of CSS colors alone cannot
// tell the background. So the check measures what is drawn: it notes the color of every visible
// text and icon, takes a screenshot of the page as it is and one with all text and icons made
// transparent (the pure background), and compares the color of each text with the background at
// the pixels of its glyphs (where the two screenshots differ), with the relative luminance and
// the contrast ratio of WCAG 2:
//   - text: at least 4.5:1, large text (24px, or 18.66px bold) at least 3:1 (1.4.3),
//   - icons (graphical objects, 1.4.11): at least 3:1.
// 90 % of the glyph pixels must reach the ratio, so single pixels of a gloss or an edge do not
// count. Text that is not drawn (cut off, hidden for assistive technology) or partly covered by
// a menu or dialog is left out; disabled controls and the logo are exempt, like in WCAG, and so
// are the initials of avatars, whose color the profile chooses, not the theme, and the
// placeholder glyphs of missing covers, which are decoration. Icons filled with a gradient and texts
// with a gradient or picture as color cannot be measured; they are reported as skipped.

/** Share of the glyph pixels of a text that must reach the ratio against the background. */
export const contrastCoverage = 0.9

/** Difference (sum over r, g and b) from which a pixel belongs to a glyph. */
const glyphThreshold = 48

/** Glyph pixels a text or icon needs at least to count as drawn. */
const minimumGlyphPixels = 6

export interface ContrastFinding {
    /** Path of the element in data-part names, e.g. "header > tab > label". */
    readonly path: string
    readonly kind: 'text' | 'icon'
    /** The text (shortened), or the icon slot. */
    readonly sample: string
    readonly color: string
    /** Ratio that 90 % of the background behind it reaches. */
    readonly ratio: number
    readonly required: number
}

export interface ContrastResult {
    readonly checked: number
    readonly failures: ContrastFinding[]
    /** Texts and icons whose color cannot be measured (gradient or picture as color). */
    readonly skipped: string[]
}

interface Target {
    id: number
    kind: 'text' | 'icon'
    path: string
    sample: string
    /** Color as r, g, b, alpha (0 to 1, including the opacity of the element and its ancestors). */
    rgba: [number, number, number, number]
    /** Box in CSS pixels within the viewport. */
    rect: { x: number; y: number; width: number; height: number }
    required: number
}

/** Collects the visible texts and icons and hides them; returns the targets. Runs in the page. */
function collectAndHide(): { targets: Target[]; skipped: string[] } {
    const targets: Target[] = []
    const skipped: string[] = []
    const hidden: Array<[HTMLElement, string]> = []
    const viewport = { width: window.innerWidth, height: window.innerHeight }

    const parse = (value: string): [number, number, number, number] | null => {
        const probe = document.createElement('canvas').getContext('2d')
        if (!probe) {
            return null
        }
        probe.fillStyle = '#000000'
        probe.fillStyle = value
        const normalized = String(probe.fillStyle)
        const hex = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(normalized)
        if (hex) {
            return [parseInt(hex[1]!, 16), parseInt(hex[2]!, 16), parseInt(hex[3]!, 16), 1]
        }
        const rgba = /^rgba?\(([^)]+)\)$/.exec(normalized)
        if (!rgba) {
            return null
        }
        const parts = rgba[1]!
            .split(/[\s,/]+/)
            .filter(Boolean)
            .map(Number)
        return [parts[0]!, parts[1]!, parts[2]!, parts[3] ?? 1]
    }

    const opacityOf = (element: Element): number => {
        let opacity = 1
        for (let node: Element | null = element; node; node = node.parentElement) {
            opacity *= Number(getComputedStyle(node).opacity)
        }
        return opacity
    }

    const pathOf = (element: Element): string => {
        const names: string[] = []
        for (let node: Element | null = element; node; node = node.parentElement) {
            const part = node.getAttribute('data-part')
            if (part && part !== 'app') {
                const item = node.getAttribute('data-item') ?? node.getAttribute('data-field')
                names.unshift(item ? `${part}[${item}]` : part)
            }
        }
        return names.slice(-4).join(' > ') || element.tagName.toLowerCase()
    }

    const exempt = (element: Element): boolean =>
        !!element.closest(
            ":disabled, [aria-disabled='true'], [data-part='logo'], [data-part='avatar'], [hidden], [inert], [data-part^='dev-'], option",
        )

    const visibleBox = (rects: DOMRect[]): Target['rect'] | null => {
        let left = Infinity
        let top = Infinity
        let right = -Infinity
        let bottom = -Infinity
        for (const rect of rects) {
            if (rect.width < 1 || rect.height < 1) {
                continue
            }
            left = Math.min(left, rect.left)
            top = Math.min(top, rect.top)
            right = Math.max(right, rect.right)
            bottom = Math.max(bottom, rect.bottom)
        }
        left = Math.max(0, left)
        top = Math.max(0, top)
        right = Math.min(viewport.width, right)
        bottom = Math.min(viewport.height, bottom)
        if (right - left < 3 || bottom - top < 3) {
            return null
        }
        return { x: left, y: top, width: right - left, height: bottom - top }
    }

    /** Whether the element is drawn on top in its whole box (not partly under a menu). */
    const onTop = (element: Element, rect: Target['rect']): boolean =>
        [
            [0.5, 0.5],
            [0.1, 0.2],
            [0.9, 0.2],
            [0.1, 0.8],
            [0.9, 0.8],
        ].every(([fx, fy]) => {
            const hit = document.elementFromPoint(
                rect.x + rect.width * fx!,
                rect.y + rect.height * fy!,
            )
            return !!hit && (hit === element || element.contains(hit) || hit.contains(element))
        })

    let id = 0
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT)
    const seen = new Set<Element>()
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        const element = node.parentElement
        if (!element || seen.has(element) || !node.textContent?.trim()) {
            continue
        }
        seen.add(element)
        const style = getComputedStyle(element)
        if (style.visibility !== 'visible' || exempt(element)) {
            continue
        }
        const range = document.createRange()
        range.selectNodeContents(element)
        const rects = [...range.getClientRects()]
        // Text hidden for assistive technology (clip-path inset(50%), 1px boxes) is not drawn.
        if (style.clipPath !== 'none' && rects.every((rect) => rect.width <= 2)) {
            continue
        }
        const rect = visibleBox(rects)
        if (!rect || !onTop(element, rect)) {
            continue
        }
        const opacity = opacityOf(element)
        if (opacity < 0.05) {
            continue
        }
        const sample = (element.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 40)
        const fill = style.getPropertyValue('-webkit-text-fill-color')
        const color = parse(fill && fill !== style.color ? fill : style.color)
        if (!color || color[3] === 0) {
            skipped.push(`${pathOf(element)}: "${sample}"`)
            continue
        }
        const size = parseFloat(style.fontSize)
        const bold = Number(style.fontWeight) >= 700
        targets.push({
            id: id++,
            kind: 'text',
            path: pathOf(element),
            sample,
            rgba: [color[0], color[1], color[2], color[3] * opacity],
            rect,
            required: size >= 24 || (bold && size >= 18.66) ? 3 : 4.5,
        })
    }

    for (const glyph of document.querySelectorAll<HTMLElement>("[data-part='icon-glyph']")) {
        const style = getComputedStyle(glyph)
        const icon = glyph.closest("[data-part='icon']")
        // The placeholder glyph of a missing cover is decoration: the title is next to it.
        const decorative = !!glyph.closest("[data-part='cover']")
        if (style.visibility !== 'visible' || exempt(glyph) || decorative) {
            continue
        }
        if (style.backgroundImage !== 'none') {
            const box = glyph.getBoundingClientRect()
            if (box.width > 0 && box.height > 0) {
                skipped.push(
                    `${pathOf(glyph)}: icon "${icon?.getAttribute('data-icon') ?? 'icon'}"`,
                )
            }
            continue
        }
        const rect = visibleBox([glyph.getBoundingClientRect()])
        const color = parse(style.backgroundColor)
        if (!rect || !color || color[3] === 0 || !onTop(glyph, rect)) {
            continue
        }
        const opacity = opacityOf(glyph)
        if (opacity < 0.05) {
            continue
        }
        targets.push({
            id: id++,
            kind: 'icon',
            path: pathOf(glyph),
            sample: icon?.getAttribute('data-icon') ?? 'icon',
            rgba: [color[0], color[1], color[2], color[3] * opacity],
            rect,
            required: 3,
        })
    }

    // Hide every text and icon for the screenshot of the background. Inline !important wins
    // against every rule of the base and the theme; transitions would delay the change.
    const hide = (element: HTMLElement, property: string, value: string) => {
        hidden.push([element, element.style.cssText])
        element.style.setProperty(property, value, 'important')
    }
    for (const element of document.querySelectorAll<HTMLElement>('body *')) {
        hidden.push([element, element.style.cssText])
        element.style.setProperty('transition', 'none', 'important')
        element.style.setProperty('color', 'transparent', 'important')
        element.style.setProperty('-webkit-text-fill-color', 'transparent', 'important')
        element.style.setProperty('text-shadow', 'none', 'important')
        element.style.setProperty('caret-color', 'transparent', 'important')
        element.style.setProperty('text-decoration-color', 'transparent', 'important')
    }
    for (const glyph of document.querySelectorAll<HTMLElement>("[data-part='icon-glyph']")) {
        hide(glyph, 'visibility', 'hidden')
    }
    const pseudo = document.createElement('style')
    pseudo.id = 'wmp-contrast-check'
    pseudo.textContent =
        '*::before, *::after { color: transparent !important; -webkit-text-fill-color: transparent !important; text-shadow: none !important; }'
    document.head.append(pseudo)
    ;(window as unknown as { __wmpContrastRestore?: () => void }).__wmpContrastRestore = () => {
        for (const [element, cssText] of hidden.reverse()) {
            element.style.cssText = cssText
        }
        pseudo.remove()
    }

    return { targets, skipped }
}

/**
 * Compares the targets with the background behind their glyphs: the pixels in which the page with
 * text and icons (shown) differs from the page without them (background). Runs in the page.
 * Returns null as ratio for a target that is not drawn at all (e.g. text cut off by overflow).
 */
async function measure(args: {
    shown: string
    background: string
    after: string
    targets: Target[]
    coverage: number
    glyphThreshold: number
    minimumGlyphPixels: number
}): Promise<Array<{ id: number; ratio: number | null }>> {
    const load = async (png: string) => {
        const image = new Image()
        image.src = `data:image/png;base64,${png}`
        await image.decode()
        const canvas = document.createElement('canvas')
        canvas.width = image.naturalWidth
        canvas.height = image.naturalHeight
        const context = canvas.getContext('2d', { willReadFrequently: true })!
        context.drawImage(image, 0, 0)
        return { context, width: image.naturalWidth }
    }
    const shown = await load(args.shown)
    const background = await load(args.background)
    const after = await load(args.after)
    const scale = background.width / window.innerWidth

    const linear = (value: number) => {
        const c = value / 255
        return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
    }
    const luminance = (r: number, g: number, b: number) =>
        0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b)

    return args.targets.map((target) => {
        const x = Math.floor(target.rect.x * scale)
        const y = Math.floor(target.rect.y * scale)
        const width = Math.max(1, Math.floor(target.rect.width * scale))
        const height = Math.max(1, Math.floor(target.rect.height * scale))
        const data = background.context.getImageData(x, y, width, height).data
        const drawn = shown.context.getImageData(x, y, width, height).data
        const redrawn = after.context.getImageData(x, y, width, height).data
        const [r, g, b, alpha] = target.rgba
        const ratios: number[] = []
        for (let row = 0; row < height; row += 1) {
            for (let column = 0; column < width; column += 1) {
                const index = (row * width + column) * 4
                const difference =
                    Math.abs(drawn[index]! - data[index]!) +
                    Math.abs(drawn[index + 1]! - data[index + 1]!) +
                    Math.abs(drawn[index + 2]! - data[index + 2]!)
                // Pixels that changed on their own between the screenshots (a badge that
                // appeared, a moving progress) are no glyphs.
                const change =
                    Math.abs(drawn[index]! - redrawn[index]!) +
                    Math.abs(drawn[index + 1]! - redrawn[index + 1]!) +
                    Math.abs(drawn[index + 2]! - redrawn[index + 2]!)
                if (difference < args.glyphThreshold || change >= args.glyphThreshold) {
                    continue
                }
                const br = data[index]!
                const bg = data[index + 1]!
                const bb = data[index + 2]!
                // A translucent text color is drawn over the background.
                const fr = r * alpha + br * (1 - alpha)
                const fg = g * alpha + bg * (1 - alpha)
                const fb = b * alpha + bb * (1 - alpha)
                const first = luminance(fr, fg, fb)
                const second = luminance(br, bg, bb)
                ratios.push((Math.max(first, second) + 0.05) / (Math.min(first, second) + 0.05))
            }
        }
        if (ratios.length < args.minimumGlyphPixels) {
            return { id: target.id, ratio: null }
        }
        ratios.sort((first, second) => first - second)
        const ratio = ratios[Math.floor(ratios.length * (1 - args.coverage))]!
        return { id: target.id, ratio: Math.round(ratio * 100) / 100 }
    })
}

/**
 * Checks the contrast of everything visible in the viewport of the page. The page is unchanged
 * afterwards (the hidden texts and icons are shown again).
 */
export async function checkContrast(page: Page): Promise<ContrastResult> {
    await page.mouse.move(0, 0)
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur?.())
    const shot = async () =>
        (await page.screenshot({ animations: 'disabled', caret: 'hide' })).toString('base64')
    const shown = await shot()
    const { targets, skipped } = await page.evaluate(collectAndHide)
    let background: string
    try {
        await page.evaluate(() => new Promise(requestAnimationFrame))
        background = await shot()
    } finally {
        await page.evaluate(() =>
            (window as unknown as { __wmpContrastRestore?: () => void }).__wmpContrastRestore?.(),
        )
    }
    await page.evaluate(() => new Promise(requestAnimationFrame))
    const after = await shot()

    const ratios = await page.evaluate(measure, {
        shown,
        background,
        after,
        targets,
        coverage: contrastCoverage,
        glyphThreshold,
        minimumGlyphPixels,
    })
    const byId = new Map(ratios.map((entry) => [entry.id, entry.ratio]))
    const failures = targets
        .map((target) => ({ target, ratio: byId.get(target.id) ?? null }))
        .filter(
            (entry): entry is { target: Target; ratio: number } =>
                entry.ratio !== null && entry.ratio < entry.target.required,
        )
        .map(({ target, ratio }) => ({
            path: target.path,
            kind: target.kind,
            sample: target.sample,
            color: `rgb(${target.rgba.slice(0, 3).join(' ')} / ${Math.round(target.rgba[3] * 100)}%)`,
            ratio,
            required: target.required,
        }))

    return { checked: targets.length, failures, skipped }
}

/** A readable report of the failures, one line each. */
export function formatContrastFailures(failures: readonly ContrastFinding[]): string {
    return failures
        .map(
            (failure) =>
                `${failure.ratio.toFixed(2)}:1 < ${failure.required}:1  ${failure.kind} "${failure.sample}"  ${failure.path}  (${failure.color})`,
        )
        .join('\n')
}
