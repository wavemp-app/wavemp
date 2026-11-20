// Draws a section of the recap story on a canvas: for the pictures (PNG in the story format
// 1080 x 1920 or square 1080 x 1080) and the frames of the video. The section is laid out as a
// column of blocks (cover, title, figure, ranking, chart, numbers, caption) centered in the
// picture, in the colors and decoration of the design.

import type { RecapDecoration, RecapPalette } from './designs'
import type { RecapSlide } from './slides'

export type RecapPictureFormat = 'story' | 'square'

export const pictureSizes: Record<RecapPictureFormat, { width: number; height: number }> = {
    story: { width: 1080, height: 1920 },
    square: { width: 1080, height: 1080 },
}

export interface RecapFrame {
    readonly slide: RecapSlide
    readonly palette: RecapPalette
    readonly decoration: RecapDecoration
    readonly format: RecapPictureFormat
    /** Covers by track id, already loaded. */
    readonly covers: ReadonlyMap<string, CanvasImageSource>
    /** Name of the app at the bottom of the picture. */
    readonly brand: string
    /** Progress of the section between 0 and 1: entrance and growing bars; 1 for pictures. */
    readonly progress?: number
    /** Progress bar of the whole story, as on the page (videos only). */
    readonly story?: { readonly index: number; readonly count: number }
}

interface Block {
    readonly height: number
    draw(context: CanvasRenderingContext2D, top: number): void
}

const easeOut = (value: number) => 1 - (1 - Math.min(1, Math.max(0, value))) ** 3

function font(palette: RecapPalette, weight: number, size: number): string {
    return `${weight} ${Math.round(size)}px ${palette.fontFamily}`
}

/** The lines of a text within a width; the last line ends with an ellipsis if text is left. */
export function wrapText(
    context: CanvasRenderingContext2D,
    text: string,
    width: number,
    maxLines: number,
): string[] {
    const words = text.split(/\s+/).filter(Boolean)
    const lines: string[] = []
    let line = ''
    for (const word of words) {
        const candidate = line ? `${line} ${word}` : word
        if (context.measureText(candidate).width <= width || !line) {
            line = candidate
        } else {
            lines.push(line)
            line = word
        }
    }
    if (line) {
        lines.push(line)
    }

    if (lines.length <= maxLines) {
        return lines.map((entry) => ellipsize(context, entry, width))
    }

    const kept = lines.slice(0, maxLines)
    kept[maxLines - 1] = ellipsize(context, `${kept[maxLines - 1]} ${lines[maxLines]}`, width, true)
    return kept
}

function ellipsize(
    context: CanvasRenderingContext2D,
    text: string,
    width: number,
    force = false,
): string {
    if (!force && context.measureText(text).width <= width) {
        return text
    }

    let end = text.length
    while (end > 0 && context.measureText(`${text.slice(0, end).trimEnd()}…`).width > width) {
        end--
    }
    return `${text.slice(0, end).trimEnd()}…`
}

/** The largest font size up to the given one with which the text fits the width. */
function fittingSize(
    context: CanvasRenderingContext2D,
    palette: RecapPalette,
    text: string,
    weight: number,
    size: number,
    width: number,
    minimum: number,
): number {
    let current = size
    context.font = font(palette, weight, current)
    while (current > minimum && context.measureText(text).width > width) {
        current -= 4
        context.font = font(palette, weight, current)
    }
    return current
}

function roundedRect(
    context: CanvasRenderingContext2D,
    x: number,
    y: number,
    width: number,
    height: number,
    radius: number,
): void {
    const r = Math.max(0, Math.min(radius, width / 2, height / 2))
    context.beginPath()
    context.moveTo(x + r, y)
    context.arcTo(x + width, y, x + width, y + height, r)
    context.arcTo(x + width, y + height, x, y + height, r)
    context.arcTo(x, y + height, x, y, r)
    context.arcTo(x, y, x + width, y, r)
    context.closePath()
}

/** A bar with rounded data end (top) and a square base. */
function topRoundedBar(
    context: CanvasRenderingContext2D,
    x: number,
    y: number,
    width: number,
    height: number,
    radius: number,
): void {
    const r = Math.max(0, Math.min(radius, width / 2, height))
    context.beginPath()
    context.moveTo(x, y + height)
    context.lineTo(x, y + r)
    context.arcTo(x, y, x + r, y, r)
    context.arcTo(x + width, y, x + width, y + r, r)
    context.lineTo(x + width, y + height)
    context.closePath()
}

function drawCover(
    context: CanvasRenderingContext2D,
    frame: RecapFrame,
    trackId: string | null | undefined,
    x: number,
    y: number,
    size: number,
    radius: number,
): void {
    const image = trackId ? frame.covers.get(trackId) : undefined
    context.save()
    roundedRect(context, x, y, size, size, radius)
    context.clip()
    if (image) {
        context.drawImage(image, x, y, size, size)
    } else {
        context.fillStyle = frame.palette.surface
        context.fillRect(x, y, size, size)
        context.fillStyle = frame.palette.accent
        context.globalAlpha = 0.6
        context.beginPath()
        context.arc(x + size / 2, y + size / 2, size / 6, 0, Math.PI * 2)
        context.fill()
    }
    context.restore()
}

function drawBackground(context: CanvasRenderingContext2D, frame: RecapFrame): void {
    const { width, height } = pictureSizes[frame.format]
    const { palette } = frame
    const gradient = context.createLinearGradient(0, 0, width, height)
    gradient.addColorStop(0, palette.backgroundStart)
    gradient.addColorStop(1, palette.backgroundEnd)
    context.fillStyle = gradient
    context.fillRect(0, 0, width, height)

    context.save()
    switch (frame.decoration) {
        case 'glow': {
            for (const [cx, cy, radius, alpha] of [
                [width * 0.85, height * 0.12, width * 0.75, 0.55],
                [width * 0.1, height * 0.88, width * 0.7, 0.35],
            ] as const) {
                const glow = context.createRadialGradient(cx, cy, 0, cx, cy, radius)
                glow.addColorStop(0, palette.glow)
                glow.addColorStop(1, 'transparent')
                context.globalAlpha = alpha
                context.fillStyle = glow
                context.fillRect(0, 0, width, height)
            }
            break
        }
        case 'glass': {
            const inset = width * 0.05
            context.globalAlpha = 0.35
            context.fillStyle = palette.surface
            roundedRect(context, inset, inset, width - 2 * inset, height - 2 * inset, 48)
            context.fill()
            context.globalAlpha = 0.6
            context.strokeStyle = palette.glow
            context.lineWidth = 3
            context.stroke()
            const shine = context.createLinearGradient(0, 0, width, height * 0.4)
            shine.addColorStop(0, 'rgba(255, 255, 255, 0.55)')
            shine.addColorStop(0.5, 'rgba(255, 255, 255, 0)')
            context.globalAlpha = 0.7
            context.fillStyle = shine
            roundedRect(context, inset, inset, width - 2 * inset, height * 0.4, 48)
            context.fill()
            break
        }
        case 'bands': {
            context.translate(width / 2, height / 2)
            context.rotate(-Math.PI / 7)
            const band = Math.max(width, height) * 0.16
            const colors = [palette.glow, palette.accent, palette.glow]
            colors.forEach((color, index) => {
                context.globalAlpha = index === 1 ? 0.22 : 0.3
                context.fillStyle = color
                context.fillRect(-height * 1.5, (index - 1.5) * band * 1.9, height * 3, band)
            })
            break
        }
    }
    context.restore()
}

function drawStoryProgress(context: CanvasRenderingContext2D, frame: RecapFrame): void {
    if (!frame.story || frame.story.count < 1) {
        return
    }

    const { width } = pictureSizes[frame.format]
    const margin = 48
    const gap = 10
    const count = frame.story.count
    const segment = (width - 2 * margin - gap * (count - 1)) / count
    for (let index = 0; index < count; index++) {
        const x = margin + index * (segment + gap)
        const share =
            index < frame.story.index ? 1 : index === frame.story.index ? (frame.progress ?? 1) : 0
        context.globalAlpha = 0.35
        context.fillStyle = frame.palette.text
        roundedRect(context, x, 40, segment, 8, 4)
        context.fill()
        if (share > 0) {
            context.globalAlpha = 1
            roundedRect(context, x, 40, segment * share, 8, 4)
            context.fill()
        }
    }
    context.globalAlpha = 1
}

/** The blocks of a section, laid out for the width of the content. */
function layout(context: CanvasRenderingContext2D, frame: RecapFrame, width: number): Block[] {
    const { slide, palette, format } = frame
    const square = format === 'square'
    const scale = square ? 0.74 : 1
    const center = pictureSizes[format].width / 2
    const left = center - width / 2
    const blocks: Block[] = []
    const growth = easeOut((frame.progress ?? 1) / 0.6)

    const centeredText = (
        text: string | undefined,
        weight: number,
        size: number,
        color: string,
        maxLines: number,
        gapAfter: number,
    ) => {
        if (!text) {
            return
        }
        context.font = font(palette, weight, size)
        const lines = wrapText(context, text, width, maxLines)
        const lineHeight = size * 1.18
        blocks.push({
            height: lines.length * lineHeight + gapAfter,
            draw(target, top) {
                target.font = font(palette, weight, size)
                target.fillStyle = color
                target.textAlign = 'center'
                target.textBaseline = 'top'
                lines.forEach((line, index) =>
                    target.fillText(line, center, top + index * lineHeight),
                )
            },
        })
    }

    const showsCover =
        slide.coverTrackId !== undefined &&
        (slide.kind === 'top-track' || (slide.kind === 'summary' && !square))
    if (showsCover) {
        const size = (slide.kind === 'summary' ? 300 : square ? 360 : 600) * scale
        blocks.push({
            height: size + 64 * scale,
            draw(target, top) {
                target.save()
                target.shadowColor = 'rgba(0, 0, 0, 0.35)'
                target.shadowBlur = 48
                target.shadowOffsetY = 16
                drawCover(target, frame, slide.coverTrackId, center - size / 2, top, size, 36)
                target.restore()
            },
        })
    }

    centeredText(slide.title, 700, 66 * scale, palette.text, 3, 40 * scale)

    if (slide.figure) {
        const isName = slide.kind === 'top-track' || slide.kind === 'intro'
        const weight = 800
        const size = fittingSize(
            context,
            palette,
            slide.figure,
            weight,
            (isName ? 112 : 210) * scale,
            width,
            isName ? 64 * scale : 96 * scale,
        )
        centeredText(slide.figure, weight, size, palette.accent, isName ? 2 : 1, 12 * scale)
    }

    centeredText(slide.figureLabel, 500, 46 * scale, palette.textMuted, 2, 36 * scale)

    if (slide.items && slide.items.length > 0) {
        const items = slide.items.slice(0, square ? 4 : 5)
        const rowHeight = 132 * scale
        const coverSize = 100 * scale
        blocks.push({
            height: items.length * rowHeight + 24 * scale,
            draw(target, top) {
                items.forEach((item, index) => {
                    const y = top + index * rowHeight
                    const middle = y + coverSize / 2
                    target.textBaseline = 'middle'
                    target.textAlign = 'left'
                    target.font = font(palette, 800, 52 * scale)
                    target.fillStyle = palette.accent
                    target.fillText(String(item.rank), left, middle)
                    const coverX = left + 72 * scale
                    drawCover(target, frame, item.coverTrackId, coverX, y, coverSize, 18)
                    const textX = coverX + coverSize + 32 * scale
                    target.font = font(palette, 500, 32 * scale)
                    const valueWidth = target.measureText(item.value).width
                    const textWidth = left + width - textX - valueWidth - 24 * scale
                    target.textAlign = 'right'
                    target.fillStyle = palette.textMuted
                    target.fillText(item.value, left + width, middle)
                    target.textAlign = 'left'
                    target.font = font(palette, 700, 42 * scale)
                    target.fillStyle = palette.text
                    target.fillText(
                        ellipsize(target, item.title, textWidth),
                        textX,
                        middle - 22 * scale,
                    )
                    target.font = font(palette, 500, 32 * scale)
                    target.fillStyle = palette.textMuted
                    target.fillText(
                        ellipsize(target, item.subtitle, textWidth),
                        textX,
                        middle + 24 * scale,
                    )
                })
            },
        })
    }

    if (slide.bars && slide.bars.length > 0) {
        const bars = slide.bars
        const chartHeight = (square ? 380 : 620) * scale
        const labelSize = 28 * scale
        blocks.push({
            height: chartHeight + labelSize * 2.4 + 24 * scale,
            draw(target, top) {
                const gap = bars.length > 40 ? 3 : 6
                const barWidth = (width - gap * (bars.length - 1)) / bars.length
                const base = top + chartHeight
                target.globalAlpha = 0.5
                target.fillStyle = palette.textMuted
                target.fillRect(left, base, width, 2)
                bars.forEach((bar, index) => {
                    const x = left + index * (barWidth + gap)
                    const height = Math.max(bar.ratio > 0 ? 6 : 0, bar.ratio * chartHeight * growth)
                    target.globalAlpha = bar.peak ? 1 : 0.55
                    target.fillStyle = palette.accent
                    topRoundedBar(
                        target,
                        x,
                        base - height,
                        barWidth,
                        height,
                        Math.min(10, barWidth / 2),
                    )
                    target.fill()
                })
                target.globalAlpha = 1
                target.font = font(palette, 500, labelSize)
                target.fillStyle = palette.textMuted
                target.textBaseline = 'top'
                let lastRight = -Infinity
                bars.forEach((bar, index) => {
                    if (!bar.labelled) {
                        return
                    }
                    const middle = left + index * (barWidth + gap) + barWidth / 2
                    const labelWidth = target.measureText(bar.label).width
                    const x = Math.min(
                        left + width - labelWidth,
                        Math.max(left, middle - labelWidth / 2),
                    )
                    if (x > lastRight + 12) {
                        target.textAlign = 'left'
                        target.fillText(bar.label, x, base + 16 * scale)
                        lastRight = x + labelWidth
                    }
                })
            },
        })
    }

    if (slide.facts && slide.facts.length > 0) {
        const facts = slide.facts
        const columns = 2
        const rowHeight = 156 * scale
        const columnWidth = width / columns
        const rows = Math.ceil(facts.length / columns)
        blocks.push({
            height: rows * rowHeight + 16 * scale,
            draw(target, top) {
                facts.forEach((fact, index) => {
                    const x = left + (index % columns) * columnWidth
                    const y = top + Math.floor(index / columns) * rowHeight
                    target.textAlign = 'left'
                    target.textBaseline = 'top'
                    target.font = font(palette, 800, 58 * scale)
                    target.fillStyle = palette.text
                    target.fillText(ellipsize(target, fact.value, columnWidth - 24), x, y)
                    target.font = font(palette, 500, 32 * scale)
                    target.fillStyle = palette.textMuted
                    target.fillText(
                        ellipsize(target, fact.label, columnWidth - 24),
                        x,
                        y + 72 * scale,
                    )
                })
            },
        })
    }

    centeredText(slide.caption, 500, 40 * scale, palette.textMuted, 3, 0)
    return blocks
}

/** Draws one frame of a section on a canvas of the size of the format. */
export function drawRecapFrame(context: CanvasRenderingContext2D, frame: RecapFrame): void {
    const { width, height } = pictureSizes[frame.format]
    const square = frame.format === 'square'
    context.save()
    context.clearRect(0, 0, width, height)
    drawBackground(context, frame)
    drawStoryProgress(context, frame)

    const margin = square ? 96 : 104
    const contentWidth = width - 2 * margin
    const blocks = layout(context, frame, contentWidth)
    const total = blocks.reduce((sum, block) => sum + block.height, 0)
    const footer = square ? 96 : 150
    const top = Math.max(square ? 72 : 140, (height - footer - total) / 2)

    const entrance = easeOut((frame.progress ?? 1) / 0.2)
    context.globalAlpha = entrance
    context.translate(0, (1 - entrance) * 48)
    let y = top
    for (const block of blocks) {
        context.save()
        block.draw(context, y)
        context.restore()
        y += block.height
    }
    context.restore()

    context.save()
    context.font = font(frame.palette, 700, square ? 30 : 36)
    context.fillStyle = frame.palette.textMuted
    context.textAlign = 'center'
    context.textBaseline = 'alphabetic'
    context.fillText(frame.brand, width / 2, height - (square ? 44 : 72))
    context.restore()
}

/** A canvas of the format with the section drawn on it. */
export function renderRecapPicture(frame: RecapFrame): HTMLCanvasElement {
    const { width, height } = pictureSizes[frame.format]
    const canvas = document.createElement('canvas')
    canvas.width = width
    canvas.height = height
    const context = canvas.getContext('2d')
    if (!context) {
        throw new Error('The browser cannot draw on a canvas.')
    }
    drawRecapFrame(context, { ...frame, progress: 1 })
    return canvas
}

export function canvasToBlob(canvas: HTMLCanvasElement, type = 'image/png'): Promise<Blob> {
    return new Promise((resolve, reject) => {
        canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('No picture.'))), type)
    })
}
