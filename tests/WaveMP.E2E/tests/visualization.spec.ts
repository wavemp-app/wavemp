import {
    devices,
    expect,
    test,
    type APIRequestContext,
    type Locator,
    type Page,
} from '@playwright/test'
import { createUser, deleteUser, findTrackId, uniqueSuffix, type TestUser } from './helpers/api'
import { drawSmall, limitDrawingRate } from './helpers/drawing'
import {
    countFlashes,
    largestChange,
    meanChange,
    recordFlashFrames,
    takeFlashFrames,
} from './helpers/flashes'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The visualization on Now Playing with the synthetic test media (Loose Song: 20 seconds of audio).
// Headless Chromium has no GPU; SwiftShader gives it WebGL 2 in software, as the visualization
// needs. A browser without WebGL 2 shows a hint instead, which the first test accepts as well.
// The settings of the visualization belong to the profile, so each test selects its own profile.
// Drawing in software takes much of the CPU, so the tests of this file run one after another in one
// worker instead of drawing two visualizations at once: the project "visualization" of
// playwright.config.ts runs this file alone with one worker, first on the desktop, then on a phone.
// One visualization at full size and rate still takes every core, and the preset it starts with is
// random: with a slow one the clicks, the server and the database wait for seconds. So
// - all tests draw at a limited rate, and the tests of the behavior on a small stage as well (see
//   helpers/drawing.ts); the test of the layout keeps the size,
// - the profiles start with the cover, because a video and its ambient light cost the same,
// - a test uses one session for the API, because creating a session writes to the database.

test.describe.configure({ mode: 'default' })

// The phone of the project "mobile" (its browser is the Chromium of the project).
const { defaultBrowserType: _phoneBrowser, ...phone } = devices['Pixel 7']

test.use({
    storageState: emptyStorageState,
    locale: 'en-US',
    launchOptions: {
        args: ['--enable-unsafe-swiftshader', '--use-angle=swiftshader'],
    },
})

// The app saves a choice in the background, and drawing in software still takes a part of the CPU,
// so a choice may reach the server a moment later.
const settled = expect.configure({ timeout: 15_000 })

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

function viewSegment(page: Page, item: string): Locator {
    return page.locator(
        `[data-part='app'] [data-part='stage-actions'] [data-part='segment'][data-item='${item}']`,
    )
}

async function sessionHeaders(
    request: APIRequestContext,
    profileId: string,
): Promise<Record<string, string>> {
    const session = await request.post('/api/v1/sessions', {
        data: { profileId },
    })
    expect(session.status()).toBe(201)
    return { Authorization: `Bearer ${(await session.json()).token}` }
}

interface StoredVisualization {
    module: string
    preset: string | null
    autoChange: boolean
    autoChangeSeconds: number
    quality: string
    colorScheme: string | null
    intensity: string
    milkdropSpeed: number
}

async function storedVisualization(
    request: APIRequestContext,
    headers: Record<string, string>,
): Promise<StoredVisualization> {
    const response = await request.get('/api/v1/me/settings', { headers })
    expect(response.status()).toBe(200)
    return (await response.json()).visualization
}

async function storedViewMode(
    request: APIRequestContext,
    headers: Record<string, string>,
): Promise<string> {
    const response = await request.get('/api/v1/me/settings', { headers })
    expect(response.status()).toBe(200)
    return (await response.json()).playerViewMode
}

/**
 * Lets the profile draw at the low quality: drawing in software takes much of the CPU, and other
 * tests run at the same time.
 */
async function drawAtLowQuality(
    request: APIRequestContext,
    headers: Record<string, string>,
): Promise<void> {
    const response = await request.patch('/api/v1/me/settings', {
        headers,
        data: { visualization: { quality: 'Low' } },
    })
    expect(response.status()).toBe(200)
}

/**
 * Turns the automatic change of the profile off, so the shown preset changes only when the test
 * chooses one.
 */
async function turnOffAutoChange(
    request: APIRequestContext,
    headers: Record<string, string>,
): Promise<void> {
    const response = await request.patch('/api/v1/me/settings', {
        headers,
        data: { visualization: { autoChange: false } },
    })
    expect(response.status()).toBe(200)
}

/** Lets Now Playing open with the cover, so nothing is drawn before a test chooses the visualization. */
async function startWithCover(
    request: APIRequestContext,
    headers: Record<string, string>,
): Promise<void> {
    const response = await request.patch('/api/v1/me/settings', {
        headers,
        data: { playerViewMode: 'Cover' },
    })
    expect(response.status()).toBe(200)
}

/**
 * Repeats the song: it is only 20 seconds long, and drawing in software can make a test take longer.
 * On phones the button is in the expanded player.
 */
async function repeatSong(page: Page): Promise<void> {
    const repeat = part(page, 'btn-repeat')
    const expand = !(await repeat.isVisible())
    if (expand) {
        await part(page, 'now-playing-mini').click()
    }

    for (let click = 0; click < 3 && (await repeat.getAttribute('data-state')) !== 'one'; click++) {
        await repeat.click()
    }

    await expect(repeat).toHaveAttribute('data-state', 'one')
    if (expand) {
        await part(page, 'btn-collapse').click()
    }
}

/** Plays Loose Song and shows the visualization view of Now Playing. */
async function showVisualization(page: Page, request: APIRequestContext): Promise<Locator> {
    const id = await findTrackId(request, 'Loose Song')
    await page.goto(`/library/tracks/${id}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await repeatSong(page)
    await expect(part(page, 'now-playing-title')).toHaveText('Loose Song')
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(page).toHaveURL(/\/now-playing$/)
    await viewSegment(page, 'visualization').click()
    return part(page, 'visualization')
}

/** The list of presets (a menu beside the controls; its entries are in the page while it is closed). */
function presetMenu(page: Page): Locator {
    return page.locator(
        "[data-part='app'] [data-part='context-menu'][data-kind='visualization-presets']",
    )
}

function presetEntries(page: Page): Locator {
    return presetMenu(page).locator("[data-part='menu-item']")
}

/** The entry of the shown preset. */
function shownPreset(page: Page): Locator {
    return presetMenu(page).locator("[data-part='menu-item'][aria-checked='true']")
}

/** Opens the list of presets with its button and chooses an entry. */
async function choosePreset(page: Page, entry: Locator): Promise<void> {
    await part(page, 'btn-preset-picker').click()
    await expect(presetMenu(page)).toHaveAttribute('data-state', 'open')
    await entry.click()
    await expect(presetMenu(page)).toHaveAttribute('data-state', 'closed')
}

/** The list of color schemes (a menu beside the controls, for modules with color schemes). */
function colorSchemeMenu(page: Page): Locator {
    return page.locator(
        "[data-part='app'] [data-part='context-menu'][data-kind='visualization-color-schemes']",
    )
}

function colorSchemeEntries(page: Page): Locator {
    return colorSchemeMenu(page).locator("[data-part='menu-item']")
}

/** The entry of the shown color scheme. */
function shownColorScheme(page: Page): Locator {
    return colorSchemeMenu(page).locator("[data-part='menu-item'][aria-checked='true']")
}

/** Opens the list of color schemes with its button and chooses the one with the id. */
async function chooseColorScheme(page: Page, id: string): Promise<void> {
    await part(page, 'btn-color-scheme-picker').click()
    await expect(colorSchemeMenu(page)).toHaveAttribute('data-state', 'open')
    await colorSchemeMenu(page).locator(`[data-item='${id}']`).click()
    await expect(colorSchemeMenu(page)).toHaveAttribute('data-state', 'closed')
    await expect(shownColorScheme(page)).toHaveAttribute('data-item', id)
}

function moduleSegment(page: Page, item: string): Locator {
    return page.locator(
        `[data-part='app'] [data-part='visualization-controls'] [data-item='visualization-module'] [data-part='segment'][data-item='${item}']`,
    )
}

/**
 * The Milkdrop presets that flashed most at their full speed in a scan of all presets with real
 * music, counted on the picture of butterchurn before the flash guard: 10, 5 and 5 general flashes
 * in one second (5 to 10 red ones); the guard left at most 2.
 */
const flashingPresets = [
    'flexi - what is the matrix',
    'Unchained & Rovastar - Wormhole Pillars (Hall of Shadows mix)',
    'Aderrasi - Storm of the Eye (Thunder) - mash0000 - quasi pseudo meta concentrics',
]

let user: TestUser
/** The headers of a session of the profile, for the requests of the test to the API. */
let headers: Record<string, string>

test.beforeEach(async ({ page, request }) => {
    user = await createUser(request, `Visual ${uniqueSuffix()}`)
    headers = await sessionHeaders(request, user.id)
    await startWithCover(request, headers)
    await limitDrawingRate(page)
    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
})

test.afterEach(async ({ request }) => {
    await deleteUser(request, user.id)
})

/** The tests of the visualization, run on the desktop and on a phone. */
function visualizationTests(): void {
    test('draws the music, changes presets and pauses on another view', async ({
        page,
        request,
    }) => {
        await drawSmall(page)
        expect(await storedVisualization(request, headers)).toEqual({
            module: 'Milkdrop',
            preset: null,
            autoChange: true,
            autoChangeSeconds: 30,
            quality: 'Auto',
            colorScheme: null,
            intensity: 'Normal',
            milkdropSpeed: 50,
        })

        await drawAtLowQuality(request, headers)
        const id = await findTrackId(request, 'Loose Song')
        await page.goto(`/library/tracks/${id}`)
        await part(page, 'detail-actions').locator("[data-item='play']").click()
        await repeatSong(page)
        await expect(part(page, 'now-playing-title')).toHaveText('Loose Song')
        await page
            .locator("[data-part='app'] a[href='/now-playing']")
            .filter({ visible: true })
            .click()
        await expect(page).toHaveURL(/\/now-playing$/)

        await viewSegment(page, 'visualization').click()
        const visualization = part(page, 'visualization')
        await expect(visualization).toHaveAttribute('data-module', 'milkdrop')
        await expect(visualization).toHaveAttribute('data-state', /\b(running|unsupported)\b/, {
            timeout: 20_000,
        })
        if ((await visualization.getAttribute('data-state'))?.includes('unsupported')) {
            await expect(part(page, 'visualization-message')).toHaveText(
                'This browser cannot show the visualization.',
            )
            test.skip(true, 'This browser has no WebGL 2')
        }

        await expect(part(page, 'visualization-canvas')).toBeVisible()
        await expect(part(page, 'visualization-message')).toHaveCount(0)
        await expect(part(page, 'btn-preset-picker')).toBeEnabled()
        const entries = presetEntries(page)
        expect(await entries.count()).toBeGreaterThanOrEqual(100)

        // A chosen preset is stored with the profile. The visualization starts with a random preset,
        // so the test chooses the third one, or the fifth if it already shows the third.
        const shown = await shownPreset(page).getAttribute('data-item')
        const index = (await entries.nth(2).getAttribute('data-item')) === shown ? 4 : 2
        const chosen = await entries.nth(index).getAttribute('data-item')
        await choosePreset(page, entries.nth(index))
        await expect(shownPreset(page)).toHaveAttribute('data-item', chosen!)
        await settled
            .poll(async () => (await storedVisualization(request, headers)).preset)
            .toBe(chosen)

        // The next button follows the list.
        const following = await entries.nth(index + 1).getAttribute('data-item')
        await part(page, 'btn-preset-next').click()
        await expect(shownPreset(page)).toHaveAttribute('data-item', following!)
        await settled
            .poll(async () => (await storedVisualization(request, headers)).preset)
            .toBe(following)

        const autoChange = part(page, 'btn-auto-change')
        await expect(autoChange).toHaveAttribute('aria-pressed', 'true')
        await autoChange.click()
        await expect(autoChange).toHaveAttribute('aria-pressed', 'false')
        await settled
            .poll(async () => (await storedVisualization(request, headers)).autoChange)
            .toBe(false)

        // Another view pauses the drawing, coming back draws again. The profile started with the
        // cover, so the stored view has to change before the switch back says that it was saved.
        await settled.poll(() => storedViewMode(request, headers)).toBe('Visualization')
        await viewSegment(page, 'cover').click()
        await expect(visualization).toHaveAttribute('data-state', /\bpaused\b/)
        await settled.poll(() => storedViewMode(request, headers)).toBe('Cover')
        await viewSegment(page, 'visualization').click()
        await expect(visualization).toHaveAttribute('data-state', /\brunning\b/)

        // After loading the app again, the visualization starts with the stored preset.
        await settled.poll(() => storedViewMode(request, headers)).toBe('Visualization')
        await page.reload()
        await page.goto(`/library/tracks/${id}`)
        await part(page, 'detail-actions').locator("[data-item='play']").click()
        await repeatSong(page)
        await page
            .locator("[data-part='app'] a[href='/now-playing']")
            .filter({ visible: true })
            .click()
        await expect(visualization).toHaveAttribute('data-state', /\brunning\b/, {
            timeout: 20_000,
        })
        await expect(shownPreset(page)).toHaveAttribute('data-item', following!)
        await expect(autoChange).toHaveAttribute('aria-pressed', 'false')
    })

    test('draws A-Force, which moves with the music, changes its modes and keeps its color scheme', async ({
        page,
        request,
    }) => {
        // Both modules load and draw in software, A-Force twice, and the test watches the picture
        // for a while.
        test.slow()
        await drawSmall(page)
        await recordFlashFrames(page)
        await drawAtLowQuality(request, headers)
        const settings = await request.patch('/api/v1/me/settings', {
            headers,
            data: { visualization: { autoChange: false, autoChangeSeconds: 5 } },
        })
        expect(settings.status()).toBe(200)
        const visualization = await showVisualization(page, request)
        await expect(visualization).toHaveAttribute('data-state', /\b(running|unsupported)\b/, {
            timeout: 20_000,
        })
        if ((await visualization.getAttribute('data-state'))?.includes('unsupported')) {
            test.skip(true, 'This browser has no WebGL 2')
        }

        await expect(moduleSegment(page, 'milkdrop')).toHaveAttribute('aria-checked', 'true')
        await expect(part(page, 'btn-color-scheme-picker')).toHaveCount(0)
        await moduleSegment(page, 'aforce').click()
        await expect(moduleSegment(page, 'aforce')).toHaveAttribute('aria-checked', 'true')
        await expect(visualization).toHaveAttribute('data-module', 'aforce')
        await expect(visualization).toHaveAttribute('data-state', /\brunning\b/, {
            timeout: 20_000,
        })
        await expect(visualization).toHaveAttribute('data-quality', 'low')
        await settled
            .poll(() => storedVisualization(request, headers))
            .toMatchObject({ module: 'AForce', preset: null, colorScheme: null })

        // The modes are the presets, with their names in the language of the app; the color
        // schemes are a list of their own, starting with the first one.
        await expect(part(page, 'btn-preset-picker')).toBeEnabled()
        await expect(part(page, 'btn-preset-picker')).toHaveAttribute('title', 'Mode')
        await expect(presetEntries(page)).toHaveText(['Flow', 'Rings', 'Warp', 'Ridges', 'Helix'])
        const schemePicker = part(page, 'btn-color-scheme-picker')
        await expect(schemePicker).toBeVisible()
        await expect(schemePicker.locator("[data-part='label']")).toHaveText('Aurora')
        await expect(colorSchemeEntries(page)).toHaveText([
            'Aurora',
            'Nebula',
            'Ember',
            'Lagoon',
            'Prism',
            'Eclipse',
        ])

        // The picture moves: two shots of the canvas a moment apart differ.
        const canvas = part(page, 'visualization-canvas')
        const first = await canvas.screenshot()
        await page.waitForTimeout(400)
        const second = await canvas.screenshot()
        expect(first.equals(second)).toBe(false)

        // It moves more with the music than in silence: the change from frame to frame of the
        // rendered picture is larger while the song plays than while it is paused.
        await page.waitForTimeout(1000)
        await takeFlashFrames(page)
        await page.waitForTimeout(3000)
        const playing = meanChange(await takeFlashFrames(page))
        const play = part(page, 'btn-play').filter({ visible: true }).first()
        await play.click()
        await expect(play).toHaveAttribute('data-state', 'paused')
        await page.waitForTimeout(2500)
        await takeFlashFrames(page)
        await page.waitForTimeout(3000)
        const silent = meanChange(await takeFlashFrames(page))
        test.info().annotations.push({
            type: 'frame change',
            description: `playing ${playing.toFixed(5)}, paused ${silent.toFixed(5)}`,
        })
        expect(playing).toBeGreaterThan(silent * 1.2)
        await play.click()
        await expect(play).toHaveAttribute('data-state', 'playing')

        // A chosen mode is kept with the profile; the next button follows the list. A-Force starts
        // with a random mode, and choosing the shown one changes nothing, so the test chooses Warp,
        // or Ridges if it already shows Warp.
        const chosen =
            (await shownPreset(page).getAttribute('data-item')) === 'warp' ? 'ridges' : 'warp'
        const following = chosen === 'warp' ? 'ridges' : 'helix'
        await choosePreset(page, presetMenu(page).locator(`[data-item='${chosen}']`))
        await settled
            .poll(async () => (await storedVisualization(request, headers)).preset)
            .toBe(chosen)
        await part(page, 'btn-preset-next').click()
        await expect(shownPreset(page)).toHaveAttribute('data-item', following)
        await expect(visualization).toHaveAttribute('data-state', /\brunning\b/)

        // A chosen color scheme is kept with the profile, apart from the mode.
        await chooseColorScheme(page, 'ember')
        await expect(schemePicker.locator("[data-part='label']")).toHaveText('Ember')
        await settled
            .poll(() => storedVisualization(request, headers))
            .toMatchObject({ preset: following, colorScheme: 'ember' })

        // Quick steps through the modes and the color schemes stay below three flashes a second.
        await takeFlashFrames(page)
        for (let click = 0; click < 10; click++) {
            await part(page, 'btn-preset-next').click()
            await page.waitForTimeout(300)
        }

        for (const scheme of ['prism', 'eclipse', 'ember']) {
            await chooseColorScheme(page, scheme)
        }

        await page.waitForTimeout(1500)
        const frames = await takeFlashFrames(page)
        expect(frames.length).toBeGreaterThan(40)
        expect(largestChange(frames)).toBeGreaterThan(0)
        const flashes = countFlashes(frames)
        expect(flashes.general).toBeLessThanOrEqual(3)
        expect(flashes.red).toBeLessThanOrEqual(3)

        // The automatic change changes the mode and keeps the color scheme.
        const autoChange = part(page, 'btn-auto-change')
        await autoChange.click()
        await expect(autoChange).toHaveAttribute('aria-pressed', 'true')
        const before = await shownPreset(page).getAttribute('data-item')
        await settled
            .poll(() => shownPreset(page).getAttribute('data-item'), { timeout: 20_000 })
            .not.toBe(before)
        await expect(schemePicker.locator("[data-part='label']")).toHaveText('Ember')
        await autoChange.click()
        await expect(autoChange).toHaveAttribute('aria-pressed', 'false')
        await settled
            .poll(() => storedVisualization(request, headers))
            .toMatchObject({ autoChange: false, colorScheme: 'ember' })

        // After loading the app again, A-Force starts with the stored mode and color scheme.
        const stored = (await storedVisualization(request, headers)).preset
        await page.reload()
        await showVisualization(page, request)
        await expect(visualization).toHaveAttribute('data-module', 'aforce')
        await expect(visualization).toHaveAttribute('data-state', /\brunning\b/, {
            timeout: 20_000,
        })
        await expect(shownPreset(page)).toHaveAttribute('data-item', stored!)
        await expect(schemePicker.locator("[data-part='label']")).toHaveText('Ember')
        await expect(shownColorScheme(page)).toHaveAttribute('data-item', 'ember')

        // Back to Milkdrop, which forgets mode and color scheme and has no color schemes.
        await moduleSegment(page, 'milkdrop').click()
        await expect(visualization).toHaveAttribute('data-module', 'milkdrop')
        await expect(part(page, 'btn-color-scheme-picker')).toHaveCount(0)
        await settled
            .poll(() => storedVisualization(request, headers))
            .toMatchObject({ module: 'Milkdrop', preset: null, colorScheme: null })
    })

    test('draws BlackCap, which moves with the music, changes its modes and keeps its color scheme', async ({
        page,
        request,
    }) => {
        // Both modules load and draw in software, BlackCap twice, and the test watches the picture
        // for a while.
        test.slow()
        await drawSmall(page)
        await recordFlashFrames(page)
        await drawAtLowQuality(request, headers)
        const settings = await request.patch('/api/v1/me/settings', {
            headers,
            data: { visualization: { autoChange: false, autoChangeSeconds: 5 } },
        })
        expect(settings.status()).toBe(200)
        const visualization = await showVisualization(page, request)
        await expect(visualization).toHaveAttribute('data-state', /\b(running|unsupported)\b/, {
            timeout: 20_000,
        })
        if ((await visualization.getAttribute('data-state'))?.includes('unsupported')) {
            test.skip(true, 'This browser has no WebGL 2')
        }

        // A running state means the shaders of BlackCap compiled; a failed one would show a message.
        await moduleSegment(page, 'blackcap').click()
        await expect(moduleSegment(page, 'blackcap')).toHaveAttribute('aria-checked', 'true')
        await expect(visualization).toHaveAttribute('data-module', 'blackcap')
        await expect(visualization).toHaveAttribute('data-state', /\brunning\b/, {
            timeout: 20_000,
        })
        await expect(part(page, 'visualization-message')).toHaveCount(0)
        await expect(visualization).toHaveAttribute('data-quality', 'low')
        await settled
            .poll(() => storedVisualization(request, headers))
            .toMatchObject({ module: 'BlackCap', preset: null, colorScheme: null })

        // The modes are the presets, with their names in the language of the app; the color
        // schemes are a list of their own, starting with the first one.
        await expect(part(page, 'btn-preset-picker')).toBeEnabled()
        await expect(part(page, 'btn-preset-picker')).toHaveAttribute('title', 'Mode')
        await expect(presetEntries(page)).toHaveText([
            'Drift',
            'Orbit',
            'Cluster',
            'Ribbon',
            'Bloom',
        ])
        const schemePicker = part(page, 'btn-color-scheme-picker')
        await expect(schemePicker).toBeVisible()
        await expect(schemePicker.locator("[data-part='label']")).toHaveText('Lumen')
        await expect(colorSchemeEntries(page)).toHaveText([
            'Lumen',
            'Tide',
            'Orchid',
            'Mercury',
            'Halo',
            'Dusk',
        ])

        // The picture moves: two shots of the canvas a moment apart differ.
        const canvas = part(page, 'visualization-canvas')
        const first = await canvas.screenshot()
        await page.waitForTimeout(400)
        const second = await canvas.screenshot()
        expect(first.equals(second)).toBe(false)

        // It is no static bubble: the change from frame to frame of the rendered picture is larger
        // while the song plays than while it is paused.
        await page.waitForTimeout(1000)
        await takeFlashFrames(page)
        await page.waitForTimeout(3000)
        const playing = meanChange(await takeFlashFrames(page))
        const play = part(page, 'btn-play').filter({ visible: true }).first()
        await play.click()
        await expect(play).toHaveAttribute('data-state', 'paused')
        await page.waitForTimeout(2500)
        await takeFlashFrames(page)
        await page.waitForTimeout(3000)
        const silent = meanChange(await takeFlashFrames(page))
        test.info().annotations.push({
            type: 'frame change',
            description: `playing ${playing.toFixed(5)}, paused ${silent.toFixed(5)}`,
        })
        expect(playing).toBeGreaterThan(silent * 1.2)
        await play.click()
        await expect(play).toHaveAttribute('data-state', 'playing')

        // A chosen mode is kept with the profile; the next button follows the list. BlackCap starts
        // with a random mode, and choosing the shown one changes nothing, so the test chooses
        // Cluster, or Orbit if it already shows Cluster.
        const chosen =
            (await shownPreset(page).getAttribute('data-item')) === 'cluster' ? 'orbit' : 'cluster'
        const following = chosen === 'cluster' ? 'ribbon' : 'cluster'
        await choosePreset(page, presetMenu(page).locator(`[data-item='${chosen}']`))
        await settled
            .poll(async () => (await storedVisualization(request, headers)).preset)
            .toBe(chosen)
        await part(page, 'btn-preset-next').click()
        await expect(shownPreset(page)).toHaveAttribute('data-item', following)
        // The modes blend into each other while the visualization keeps drawing.
        await expect(visualization).toHaveAttribute('data-state', /\brunning\b/)
        const blending = await canvas.screenshot()
        await page.waitForTimeout(400)
        expect(blending.equals(await canvas.screenshot())).toBe(false)

        // A chosen color scheme is kept with the profile, apart from the mode.
        await chooseColorScheme(page, 'halo')
        await expect(schemePicker.locator("[data-part='label']")).toHaveText('Halo')
        await settled
            .poll(() => storedVisualization(request, headers))
            .toMatchObject({ preset: following, colorScheme: 'halo' })

        // Quick steps through the modes and the color schemes stay below three flashes a second.
        await takeFlashFrames(page)
        for (let click = 0; click < 10; click++) {
            await part(page, 'btn-preset-next').click()
            await page.waitForTimeout(300)
        }

        for (const scheme of ['mercury', 'dusk', 'halo']) {
            await chooseColorScheme(page, scheme)
        }

        await page.waitForTimeout(1500)
        const frames = await takeFlashFrames(page)
        expect(frames.length).toBeGreaterThan(40)
        expect(largestChange(frames)).toBeGreaterThan(0)
        const flashes = countFlashes(frames)
        expect(flashes.general).toBeLessThanOrEqual(3)
        expect(flashes.red).toBeLessThanOrEqual(3)

        // The automatic change changes the mode and keeps the color scheme.
        const autoChange = part(page, 'btn-auto-change')
        await autoChange.click()
        await expect(autoChange).toHaveAttribute('aria-pressed', 'true')
        const before = await shownPreset(page).getAttribute('data-item')
        await settled
            .poll(() => shownPreset(page).getAttribute('data-item'), { timeout: 20_000 })
            .not.toBe(before)
        await expect(schemePicker.locator("[data-part='label']")).toHaveText('Halo')
        await autoChange.click()
        await expect(autoChange).toHaveAttribute('aria-pressed', 'false')
        await settled
            .poll(() => storedVisualization(request, headers))
            .toMatchObject({ autoChange: false, colorScheme: 'halo' })

        // After loading the app again, BlackCap starts with the stored mode and color scheme.
        const stored = (await storedVisualization(request, headers)).preset
        await page.reload()
        await showVisualization(page, request)
        await expect(visualization).toHaveAttribute('data-module', 'blackcap')
        await expect(visualization).toHaveAttribute('data-state', /\brunning\b/, {
            timeout: 20_000,
        })
        await expect(shownPreset(page)).toHaveAttribute('data-item', stored!)
        await expect(schemePicker.locator("[data-part='label']")).toHaveText('Halo')
        await expect(shownColorScheme(page)).toHaveAttribute('data-item', 'halo')
    })

    test('lays the controls below the picture and opens the presets the same way every time', async ({
        page,
        request,
    }) => {
        await drawAtLowQuality(request, headers)
        await turnOffAutoChange(request, headers)
        const visualization = await showVisualization(page, request)
        const controls = part(page, 'visualization-controls')
        await expect(controls).toBeVisible()
        await expect(part(page, 'btn-preset-picker')).toBeEnabled({
            timeout: 20_000,
        })

        // Nothing lies on the picture: the toolbar starts below it and is as wide as it.
        const picture = (await visualization.boundingBox())!
        const bar = (await controls.boundingBox())!
        expect(bar.y).toBeGreaterThanOrEqual(picture.y + picture.height)
        expect(Math.abs(bar.width - picture.width)).toBeLessThanOrEqual(1)
        await expect(visualization.locator("[data-part='visualization-controls']")).toHaveCount(0)

        // The buttons keep their places whatever the preset is called.
        const places = async () =>
            Promise.all(
                ['btn-preset-previous', 'btn-preset-next', 'btn-auto-change', 'btn-fullscreen'].map(
                    async (name) =>
                        (await controls.locator(`[data-part='${name}']`).boundingBox())!.x,
                ),
            )
        const before = await places()
        await part(page, 'btn-preset-next').click()
        await part(page, 'btn-preset-next').click()
        expect(await places()).toEqual(before)

        // The presets open above their button on the desktop, as a sheet at the bottom on phones;
        // either way they stay on the screen. Below 64rem (1024 pixels) the mobile layout applies.
        const phone = (page.viewportSize()?.width ?? 0) < 1024
        for (let round = 0; round < 2; round++) {
            await part(page, 'btn-preset-picker').click()
            const menu = presetMenu(page)
            await expect(menu).toHaveAttribute('data-state', 'open')
            await expect(menu).toHaveAttribute('data-placement', phone ? 'sheet' : 'above')
            await expect(shownPreset(page)).toBeFocused()
            // Let it grow to its size (motion system).
            await page.waitForTimeout(400)
            const box = (await menu.boundingBox())!
            const viewport = page.viewportSize()!
            expect(box.y).toBeGreaterThanOrEqual(0)
            expect(box.y + box.height).toBeLessThanOrEqual(viewport.height + 1)
            if (phone) {
                expect(Math.round(box.y + box.height)).toBe(viewport.height)
            } else {
                const button = (await part(page, 'btn-preset-picker').boundingBox())!
                expect(box.y + box.height).toBeLessThanOrEqual(button.y)
            }
            await page.keyboard.press('Escape')
            await expect(menu).toHaveAttribute('data-state', 'closed')
        }
    })

    test('keeps Milkdrop below three flashes a second, also while its presets change fast', async ({
        page,
        request,
    }) => {
        // Every preset loads and draws in software, and the test watches the picture for half a
        // minute.
        test.slow()
        await drawSmall(page)
        await recordFlashFrames(page)
        await drawAtLowQuality(request, headers)
        const settings = await request.patch('/api/v1/me/settings', {
            headers,
            data: {
                visualization: {
                    preset: flashingPresets[0],
                    autoChange: false,
                    autoChangeSeconds: 5,
                    milkdropSpeed: 100,
                },
            },
        })
        expect(settings.status()).toBe(200)
        const visualization = await showVisualization(page, request)
        await expect(visualization).toHaveAttribute('data-state', /\b(running|unsupported)\b/, {
            timeout: 20_000,
        })
        if ((await visualization.getAttribute('data-state'))?.includes('unsupported')) {
            test.skip(true, 'This browser has no WebGL 2')
        }

        await expect(shownPreset(page)).toHaveAttribute('data-item', flashingPresets[0]!)
        await takeFlashFrames(page)

        // The presets that flashed most in a scan with real music, at their full speed.
        for (const preset of flashingPresets) {
            if ((await shownPreset(page).getAttribute('data-item')) !== preset) {
                await choosePreset(page, presetMenu(page).locator(`[data-item="${preset}"]`))
            }

            await page.waitForTimeout(4000)
        }

        // The automatic change at its shortest interval.
        await part(page, 'btn-auto-change').click()
        await expect(part(page, 'btn-auto-change')).toHaveAttribute('aria-pressed', 'true')
        const automatic = await shownPreset(page).getAttribute('data-item')
        await settled
            .poll(() => shownPreset(page).getAttribute('data-item'), {
                timeout: 20_000,
            })
            .not.toBe(automatic)

        // Quick steps through the presets by hand, one every 300 milliseconds.
        for (let click = 0; click < 10; click++) {
            await part(page, 'btn-preset-next').click()
            await page.waitForTimeout(300)
        }

        await page.waitForTimeout(2000)
        const frames = await takeFlashFrames(page)
        // The recording holds frames of all of it, and the picture moved.
        expect(frames.length).toBeGreaterThan(100)
        expect(largestChange(frames)).toBeGreaterThan(0)
        const flashes = countFlashes(frames)
        expect(flashes.general).toBeLessThanOrEqual(3)
        expect(flashes.red).toBeLessThanOrEqual(3)
    })

    test('draws at the reduced intensity while the device asks for reduced motion', async ({
        page,
        request,
    }) => {
        await drawSmall(page)
        await drawAtLowQuality(request, headers)
        await turnOffAutoChange(request, headers)
        await page.emulateMedia({ reducedMotion: 'reduce' })
        const visualization = await showVisualization(page, request)

        await expect(visualization).toHaveAttribute('data-intensity', 'reduced')
        await page.emulateMedia({ reducedMotion: 'no-preference' })
        await expect(visualization).toHaveAttribute('data-intensity', 'normal')

        // The reduced intensity of the profile holds without the wish of the device as well.
        const response = await request.patch('/api/v1/me/settings', {
            headers,
            data: { visualization: { intensity: 'Reduced' } },
        })
        expect(response.status()).toBe(200)
        await page.reload()
        await showVisualization(page, request)
        await expect(visualization).toHaveAttribute('data-intensity', 'reduced')
    })

    test('sets the quality of the visualization in the settings', async ({ page, request }) => {
        await drawSmall(page)
        await page.goto('/settings')
        const segment = (item: string) =>
            page.locator(
                `[data-part='app'] [data-field='visualization-quality'] [data-part='segment'][data-item='${item}']`,
            )
        await expect(segment('auto')).toHaveAttribute('aria-checked', 'true')

        await segment('low').click()
        await settled
            .poll(async () => (await storedVisualization(request, headers)).quality)
            .toBe('Low')
        await page.reload()
        await expect(segment('low')).toHaveAttribute('aria-checked', 'true')

        // The visualization draws at the chosen level.
        const visualization = await showVisualization(page, request)
        await expect(visualization).toHaveAttribute('data-state', /\b(running|unsupported)\b/, {
            timeout: 20_000,
        })
        if ((await visualization.getAttribute('data-state'))?.includes('unsupported')) {
            test.skip(true, 'This browser has no WebGL 2')
        }

        await expect(visualization).toHaveAttribute('data-quality', 'low')
    })

    test('sets the interval of the automatic change in the settings', async ({ page, request }) => {
        await page.goto('/settings')
        const select = page.locator(
            "[data-part='app'] [data-field='visualization-auto-change'] select",
        )
        await expect(select).toHaveValue('30')

        await select.selectOption('120')
        await settled
            .poll(() => storedVisualization(request, headers))
            .toMatchObject({ autoChange: true, autoChangeSeconds: 120 })

        await select.selectOption('off')
        await settled
            .poll(() => storedVisualization(request, headers))
            .toMatchObject({ autoChange: false, autoChangeSeconds: 120 })

        await page.reload()
        await expect(select).toHaveValue('off')
    })

    test('sets the intensity and the Milkdrop speed in the settings', async ({ page, request }) => {
        await page.goto('/settings')
        const segment = (item: string) =>
            page.locator(
                `[data-part='app'] [data-field='visualization-intensity'] [data-part='segment'][data-item='${item}']`,
            )
        const speed = page.locator(
            "[data-part='app'] [data-field='visualization-milkdrop-speed'] select",
        )
        await expect(segment('normal')).toHaveAttribute('aria-checked', 'true')
        await expect(speed).toHaveValue('50')

        await segment('reduced').click()
        await settled
            .poll(async () => (await storedVisualization(request, headers)).intensity)
            .toBe('Reduced')
        await speed.selectOption('25')
        await settled
            .poll(async () => (await storedVisualization(request, headers)).milkdropSpeed)
            .toBe(25)

        await page.reload()
        await expect(segment('reduced')).toHaveAttribute('aria-checked', 'true')
        await expect(speed).toHaveValue('25')
    })

    test('rejects visualization settings out of range', async ({ request }) => {
        const quality = await request.patch('/api/v1/me/settings', {
            data: { visualization: { quality: 'Ultra' } },
            headers,
        })
        expect(quality.status()).toBe(400)
        expect(await quality.text()).toContain('invalid_format')

        const response = await request.patch('/api/v1/me/settings', {
            data: { visualization: { autoChangeSeconds: 4 } },
            headers,
        })

        expect(response.status()).toBe(400)
        expect(await response.text()).toContain('out_of_range')
        expect((await storedVisualization(request, headers)).autoChangeSeconds).toBe(30)

        const speed = await request.patch('/api/v1/me/settings', {
            data: { visualization: { milkdropSpeed: 101 } },
            headers,
        })
        expect(speed.status()).toBe(400)
        expect(await speed.text()).toContain('out_of_range')
        const intensity = await request.patch('/api/v1/me/settings', {
            data: { visualization: { intensity: 'Extreme' } },
            headers,
        })
        expect(intensity.status()).toBe(400)
        expect(await intensity.text()).toContain('invalid_format')
        expect(await storedVisualization(request, headers)).toMatchObject({
            intensity: 'Normal',
            milkdropSpeed: 50,
        })
    })
}

test.describe('desktop', () => {
    visualizationTests()
})

test.describe('phone', () => {
    test.use(phone)
    visualizationTests()
})
