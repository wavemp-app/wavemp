import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'
import { createUser, deleteUser, findTrackId, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The visualization on Now Playing with the synthetic test media (Loose Song: 20 seconds of audio).
// Headless Chromium has no GPU; SwiftShader gives it WebGL 2 in software, as the visualization
// needs. A browser without WebGL 2 shows a hint instead, which the first test accepts as well.
// The settings of the visualization belong to the profile, so each test selects its own profile.

test.use({
    storageState: emptyStorageState,
    locale: 'en-US',
    launchOptions: {
        args: ['--enable-unsafe-swiftshader', '--use-angle=swiftshader'],
    },
})

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
    })

    const id = await findTrackId(request, 'Loose Song')
    await page.goto(`/library/tracks/${id}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
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
    await expect.poll(async () => (await storedVisualization(request, user.id)).preset).toBe(chosen)

    // The next button follows the list.
    const following = await preset
        .locator('option')
        .nth(index + 1)
        .getAttribute('value')
    await part(page, 'btn-preset-next').click()
    await expect(preset).toHaveValue(following!)
    await expect
        .poll(async () => (await storedVisualization(request, user.id)).preset)
        .toBe(following)

    const autoChange = part(page, 'btn-auto-change')
    await expect(autoChange).toHaveAttribute('aria-pressed', 'true')
    await autoChange.click()
    await expect(autoChange).toHaveAttribute('aria-pressed', 'false')
    await expect
        .poll(async () => (await storedVisualization(request, user.id)).autoChange)
        .toBe(false)

    // Another view pauses the drawing, coming back draws again.
    await viewSegment(page, 'cover').click()
    await expect(visualization).toHaveAttribute('data-state', /\bpaused\b/)
    await expect.poll(() => storedViewMode(request, user.id)).toBe('Cover')
    await viewSegment(page, 'visualization').click()
    await expect(visualization).toHaveAttribute('data-state', /\brunning\b/)

    // After loading the app again, the visualization starts with the stored preset.
    await expect.poll(() => storedViewMode(request, user.id)).toBe('Visualization')
    await page.reload()
    await page.goto(`/library/tracks/${id}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(visualization).toHaveAttribute('data-state', /\brunning\b/, {
        timeout: 20_000,
    })
    await expect(preset).toHaveValue(following!)
    await expect(autoChange).toHaveAttribute('aria-pressed', 'false')
})

test('sets the interval of the automatic change in the settings', async ({ page, request }) => {
    await page.goto('/settings')
    const select = page.locator("[data-part='app'] [data-field='visualization-auto-change'] select")
    await expect(select).toHaveValue('30')

    await select.selectOption('120')
    await expect
        .poll(() => storedVisualization(request, user.id))
        .toMatchObject({ autoChange: true, autoChangeSeconds: 120 })

    await select.selectOption('off')
    await expect
        .poll(() => storedVisualization(request, user.id))
        .toMatchObject({ autoChange: false, autoChangeSeconds: 120 })

    await page.reload()
    await expect(select).toHaveValue('off')
})

test('rejects visualization settings out of range', async ({ request }) => {
    const headers = await sessionHeaders(request, user.id)

    const response = await request.patch('/api/v1/me/settings', {
        data: { visualization: { autoChangeSeconds: 4 } },
        headers,
    })

    expect(response.status()).toBe(400)
    expect(await response.text()).toContain('out_of_range')
    expect((await storedVisualization(request, user.id)).autoChangeSeconds).toBe(30)
})
