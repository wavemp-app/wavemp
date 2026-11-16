import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'
import { createUser, deleteUser, findTrackId, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The visualization on Now Playing with the synthetic test media (Loose Song: 20 seconds of audio).
// Headless Chromium has no GPU; SwiftShader gives it WebGL 2 in software, as the visualization
// needs. A browser without WebGL 2 shows a hint instead, which the first test accepts as well.
// The settings of the visualization belong to the profile, so each test selects its own profile.
// Drawing in software takes much of the CPU, so the tests of this file run one after another in one
// worker instead of drawing two visualizations at once.

test.describe.configure({ mode: 'default' })

test.use({
    storageState: emptyStorageState,
    locale: 'en-US',
    launchOptions: {
        args: ['--enable-unsafe-swiftshader', '--use-angle=swiftshader'],
    },
})

// Drawing in software keeps the page busy, so a choice may reach the server a few seconds later.
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
}

async function storedVisualization(
    request: APIRequestContext,
    profileId: string,
): Promise<StoredVisualization> {
    const response = await request.get('/api/v1/me/settings', {
        headers: await sessionHeaders(request, profileId),
    })
    expect(response.status()).toBe(200)
    return (await response.json()).visualization
}

async function storedViewMode(request: APIRequestContext, profileId: string): Promise<string> {
    const response = await request.get('/api/v1/me/settings', {
        headers: await sessionHeaders(request, profileId),
    })
    expect(response.status()).toBe(200)
    return (await response.json()).playerViewMode
}

/**
 * Lets the profile draw at the low quality: drawing in software takes much of the CPU, and other
 * tests run at the same time.
 */
async function drawAtLowQuality(request: APIRequestContext, profileId: string): Promise<void> {
    const response = await request.patch('/api/v1/me/settings', {
        headers: await sessionHeaders(request, profileId),
        data: { visualization: { quality: 'Low' } },
    })
    expect(response.status()).toBe(200)
}

/**
 * Turns the automatic change of the profile off, so the shown preset changes only when the test
 * chooses one.
 */
async function turnOffAutoChange(request: APIRequestContext, profileId: string): Promise<void> {
    const response = await request.patch('/api/v1/me/settings', {
        headers: await sessionHeaders(request, profileId),
        data: { visualization: { autoChange: false } },
    })
    expect(response.status()).toBe(200)
}

let user: TestUser

test.beforeEach(async ({ page, request }) => {
    user = await createUser(request, `Visual ${uniqueSuffix()}`)
    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
})

test.afterEach(async ({ request }) => {
    await deleteUser(request, user.id)
})

test('draws the music, changes presets and pauses on another view', async ({ page, request }) => {
    expect(await storedVisualization(request, user.id)).toEqual({
        module: 'Milkdrop',
        preset: null,
        autoChange: true,
        autoChangeSeconds: 30,
        quality: 'Auto',
    })

    await drawAtLowQuality(request, user.id)
    const id = await findTrackId(request, 'Loose Song')
    await page.goto(`/library/tracks/${id}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await repeatSong(page)
    await expect(part(page, 'now-playing-title')).toHaveText('Loose Song')
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
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
    const preset = page.locator("[data-part='app'] [data-field='visualization-preset'] select")
    await expect(preset).toBeEnabled()
    expect(await preset.locator('option').count()).toBeGreaterThanOrEqual(100)

    // A chosen preset is stored with the profile. The visualization starts with a random preset,
    // so the test chooses the third one, or the fifth if it already shows the third.
    const shown = await preset.inputValue()
    const index = (await preset.locator('option').nth(2).getAttribute('value')) === shown ? 4 : 2
    const chosen = await preset.locator('option').nth(index).getAttribute('value')
    await preset.selectOption(chosen!)
    await settled
        .poll(async () => (await storedVisualization(request, user.id)).preset)
        .toBe(chosen)

    // The next button follows the list.
    const following = await preset
        .locator('option')
        .nth(index + 1)
        .getAttribute('value')
    await part(page, 'btn-preset-next').click()
    await expect(preset).toHaveValue(following!)
    await settled
        .poll(async () => (await storedVisualization(request, user.id)).preset)
        .toBe(following)

    const autoChange = part(page, 'btn-auto-change')
    await expect(autoChange).toHaveAttribute('aria-pressed', 'true')
    await autoChange.click()
    await expect(autoChange).toHaveAttribute('aria-pressed', 'false')
    await settled
        .poll(async () => (await storedVisualization(request, user.id)).autoChange)
        .toBe(false)

    // Another view pauses the drawing, coming back draws again.
    await viewSegment(page, 'cover').click()
    await expect(visualization).toHaveAttribute('data-state', /\bpaused\b/)
    await settled.poll(() => storedViewMode(request, user.id)).toBe('Cover')
    await viewSegment(page, 'visualization').click()
    await expect(visualization).toHaveAttribute('data-state', /\brunning\b/)

    // After loading the app again, the visualization starts with the stored preset.
    await settled.poll(() => storedViewMode(request, user.id)).toBe('Visualization')
    await page.reload()
    await page.goto(`/library/tracks/${id}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await repeatSong(page)
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(visualization).toHaveAttribute('data-state', /\brunning\b/, {
        timeout: 20_000,
    })
    await expect(preset).toHaveValue(following!)
    await expect(autoChange).toHaveAttribute('aria-pressed', 'false')
})

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

function moduleSegment(page: Page, item: string): Locator {
    return page.locator(
        `[data-part='app'] [data-part='visualization-controls'] [data-item='visualization-module'] [data-part='segment'][data-item='${item}']`,
    )
}

test('draws A-Force, which moves with the music and blends its styles', async ({
    page,
    request,
}) => {
    // Both modules load and draw in software, A-Force twice.
    test.slow()
    await drawAtLowQuality(request, user.id)
    await turnOffAutoChange(request, user.id)
    const visualization = await showVisualization(page, request)
    await expect(visualization).toHaveAttribute('data-state', /\b(running|unsupported)\b/, {
        timeout: 20_000,
    })
    if ((await visualization.getAttribute('data-state'))?.includes('unsupported')) {
        test.skip(true, 'This browser has no WebGL 2')
    }

    await expect(moduleSegment(page, 'milkdrop')).toHaveAttribute('aria-checked', 'true')
    await moduleSegment(page, 'aforce').click()
    await expect(moduleSegment(page, 'aforce')).toHaveAttribute('aria-checked', 'true')
    await expect(visualization).toHaveAttribute('data-module', 'aforce')
    await expect(visualization).toHaveAttribute('data-state', /\brunning\b/, {
        timeout: 20_000,
    })
    await expect(visualization).toHaveAttribute('data-quality', 'low')
    await settled
        .poll(() => storedVisualization(request, user.id))
        .toMatchObject({ module: 'AForce', preset: null })

    // The style variants are the presets, with their names in the language of the app.
    const preset = page.locator("[data-part='app'] [data-field='visualization-preset'] select")
    await expect(preset).toBeEnabled()
    await expect(preset.locator('option')).toHaveText([
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

    // A chosen style is kept with the profile; the next button follows the list. A-Force starts
    // with a random style, and choosing the shown one changes nothing, so the test chooses Ember,
    // or Prism if it already shows Ember.
    const chosen = (await preset.inputValue()) === 'ember' ? 'prism' : 'ember'
    const following = chosen === 'ember' ? 'lagoon' : 'eclipse'
    await preset.selectOption(chosen)
    await settled
        .poll(async () => (await storedVisualization(request, user.id)).preset)
        .toBe(chosen)
    await part(page, 'btn-preset-next').click()
    await expect(preset).toHaveValue(following)
    await expect(visualization).toHaveAttribute('data-state', /\brunning\b/)

    // After loading the app again, A-Force starts with the stored style.
    await settled
        .poll(async () => (await storedVisualization(request, user.id)).preset)
        .toBe(following)
    await page.reload()
    await showVisualization(page, request)
    await expect(visualization).toHaveAttribute('data-module', 'aforce')
    await expect(visualization).toHaveAttribute('data-state', /\brunning\b/, {
        timeout: 20_000,
    })
    await expect(preset).toHaveValue(following)

    // Back to Milkdrop.
    await moduleSegment(page, 'milkdrop').click()
    await expect(visualization).toHaveAttribute('data-module', 'milkdrop')
    await settled
        .poll(() => storedVisualization(request, user.id))
        .toMatchObject({ module: 'Milkdrop', preset: null })
})

test('draws BlackCap, which moves with the music and changes its scenes', async ({
    page,
    request,
}) => {
    // Both modules load and draw in software, BlackCap twice.
    test.slow()
    await drawAtLowQuality(request, user.id)
    await turnOffAutoChange(request, user.id)
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
        .poll(() => storedVisualization(request, user.id))
        .toMatchObject({ module: 'BlackCap', preset: null })

    // The scenes are the presets, with their names in the language of the app.
    const preset = page.locator("[data-part='app'] [data-field='visualization-preset'] select")
    await expect(preset).toBeEnabled()
    await expect(preset.locator('option')).toHaveText([
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

    // A chosen scene is kept with the profile; the next button follows the list. BlackCap starts
    // with a random scene, and choosing the shown one changes nothing, so the test chooses Halo,
    // or Tide if it already shows Halo.
    const chosen = (await preset.inputValue()) === 'halo' ? 'tide' : 'halo'
    const following = chosen === 'halo' ? 'dusk' : 'orchid'
    await preset.selectOption(chosen)
    await settled
        .poll(async () => (await storedVisualization(request, user.id)).preset)
        .toBe(chosen)
    await part(page, 'btn-preset-next').click()
    await expect(preset).toHaveValue(following)
    // The scenes blend into each other while the visualization keeps drawing.
    await expect(visualization).toHaveAttribute('data-state', /\brunning\b/)
    const blending = await canvas.screenshot()
    await page.waitForTimeout(400)
    expect(blending.equals(await canvas.screenshot())).toBe(false)

    // After loading the app again, BlackCap starts with the stored scene.
    await settled
        .poll(async () => (await storedVisualization(request, user.id)).preset)
        .toBe(following)
    await page.reload()
    await showVisualization(page, request)
    await expect(visualization).toHaveAttribute('data-module', 'blackcap')
    await expect(visualization).toHaveAttribute('data-state', /\brunning\b/, {
        timeout: 20_000,
    })
    await expect(preset).toHaveValue(following)
})

test('sets the quality of the visualization in the settings', async ({ page, request }) => {
    await page.goto('/settings')
    const segment = (item: string) =>
        page.locator(
            `[data-part='app'] [data-field='visualization-quality'] [data-part='segment'][data-item='${item}']`,
        )
    await expect(segment('auto')).toHaveAttribute('aria-checked', 'true')

    await segment('low').click()
    await settled
        .poll(async () => (await storedVisualization(request, user.id)).quality)
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
    const select = page.locator("[data-part='app'] [data-field='visualization-auto-change'] select")
    await expect(select).toHaveValue('30')

    await select.selectOption('120')
    await settled
        .poll(() => storedVisualization(request, user.id))
        .toMatchObject({ autoChange: true, autoChangeSeconds: 120 })

    await select.selectOption('off')
    await settled
        .poll(() => storedVisualization(request, user.id))
        .toMatchObject({ autoChange: false, autoChangeSeconds: 120 })

    await page.reload()
    await expect(select).toHaveValue('off')
})

test('rejects visualization settings out of range', async ({ request }) => {
    const headers = await sessionHeaders(request, user.id)

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
    expect((await storedVisualization(request, user.id)).autoChangeSeconds).toBe(30)
})
