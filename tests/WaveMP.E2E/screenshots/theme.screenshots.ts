import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { test, type Page } from '@playwright/test'
import { checkContrast, formatContrastFailures, type ContrastFinding } from './contrast'
import {
    createThemeProfile,
    deleteThemeProfile,
    selectThemeTargets,
    targetKey,
    themeViews,
    visitThemeViews,
    type ThemeProfile,
    type ThemeTarget,
} from './themes'

// Screenshots and contrast check of themes for theme designers (THEMING.md, "Checking a theme"),
// started with `npm run theme:screenshots` (screenshots/theme-run.ts), which sets:
//   WAVEMP_THEME_TARGETS  "theme[:variant]" items separated by commas; empty for all themes
//   WAVEMP_THEME_TASKS    "screenshots", "contrast" and/or "preview", separated by commas
//   WAVEMP_THEME_OUT      folder of the screenshots (one folder per theme and variant below it)
//   WAVEMP_THEME_VIEWS    names of the views, separated by commas; empty for all views
//   E2E_BASE_URL          WaveMP or a theme preview (npm run theme:preview in the web app)

const targets = selectThemeTargets(list(process.env.WAVEMP_THEME_TARGETS))
const tasks = new Set(list(process.env.WAVEMP_THEME_TASKS ?? 'screenshots,contrast'))
const outputDirectory = path.resolve(
    process.env.WAVEMP_THEME_OUT ??
        fileURLToPath(new URL('../theme-screenshots/', import.meta.url)),
)
const viewNames = list(process.env.WAVEMP_THEME_VIEWS)
const views = themeViews.filter((view) => viewNames.length === 0 || viewNames.includes(view.name))
const themesDirectory = fileURLToPath(new URL('../../../assets/web/themes/', import.meta.url))

function list(value: string | undefined): string[] {
    return (value ?? '')
        .split(',')
        .map((item) => item.trim())
        .filter(Boolean)
}

/**
 * The preview picture of the theme picker: the artist overview, scaled down, as WebP. It shows the
 * chrome of the theme and its own cover placeholders instead of the colorful test covers.
 */
async function writePreview(page: Page, target: ThemeTarget, png: Buffer): Promise<void> {
    const webp = await page.evaluate(async (data) => {
        const image = new Image()
        image.src = `data:image/png;base64,${data}`
        await image.decode()
        const canvas = document.createElement('canvas')
        canvas.width = 480
        canvas.height = Math.round((480 * image.naturalHeight) / image.naturalWidth)
        const context = canvas.getContext('2d')!
        context.imageSmoothingQuality = 'high'
        context.drawImage(image, 0, 0, canvas.width, canvas.height)
        return canvas.toDataURL('image/webp', 0.85).split(',')[1]!
    }, png.toString('base64'))
    const file = path.join(themesDirectory, target.theme, `preview-${target.variant}.webp`)
    writeFileSync(file, Buffer.from(webp, 'base64'))
    console.log(`Wrote ${path.relative(process.cwd(), file)}`)
}

test.describe.configure({ mode: 'parallel' })

for (const target of targets) {
    test(`${target.theme} (${target.variant})`, async ({ browser, request }) => {
        test.setTimeout(300_000)
        const directory = path.join(outputDirectory, target.theme, target.variant)
        mkdirSync(directory, { recursive: true })
        const failures: Array<ContrastFinding & { view: string }> = []
        const skipped: string[] = []
        let profile: ThemeProfile | undefined
        try {
            profile = await createThemeProfile(request, target)
            await visitThemeViews(browser, request, target, profile, views, async (page, view) => {
                const png = await page.screenshot({
                    animations: 'disabled',
                    caret: 'hide',
                })
                if (tasks.has('screenshots')) {
                    writeFileSync(path.join(directory, `${view.name}.png`), png)
                }
                if (tasks.has('preview') && view.name === 'artists') {
                    await writePreview(page, target, png)
                }
                if (tasks.has('contrast')) {
                    const result = await checkContrast(page)
                    failures.push(
                        ...result.failures.map((failure) => ({
                            ...failure,
                            view: view.name,
                        })),
                    )
                    skipped.push(...result.skipped.map((entry) => `${view.name}: ${entry}`))
                }
            })
        } finally {
            await deleteThemeProfile(request, profile)
        }

        if (tasks.has('contrast')) {
            const report = [
                `Contrast of ${targetKey(target)} (WCAG 2.2 AA): ${failures.length} failure(s)`,
                ...failures.map(
                    (failure) => `[${failure.view}] ${formatContrastFailures([failure])}`,
                ),
                ...(skipped.length > 0
                    ? ['', 'Not measurable (gradient or picture as color):', ...skipped]
                    : []),
            ].join('\n')
            writeFileSync(path.join(directory, 'contrast.txt'), `${report}\n`)
            console.log(report)
            if (failures.length > 0) {
                throw new Error(`Contrast too low in ${targetKey(target)}:\n${report}`)
            }
        }
    })
}
