import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'
import { createUser, deleteUser, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The crossfade switch of the transport bar, the collapsible sidebar and the segmented controls.
// Crossfade, sidebar and ReplayGain belong to the profile, so every test selects a profile of its
// own. Uses the synthetic test media of scripts/generate-media.sh (E2E Album).

const iPadLandscape = { width: 1180, height: 820 }
const iPhone = { width: 390, height: 844 }

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

async function sessionHeaders(
    request: APIRequestContext,
    profileId: string,
): Promise<Record<string, string>> {
    const session = await request.post('/api/v1/sessions', { data: { profileId } })
    expect(session.status()).toBe(201)
    return { Authorization: `Bearer ${(await session.json()).token}` }
}

interface StoredSettings {
    replayGain: string
    sidebarCollapsed: boolean
    crossfade: { enabled: boolean; seconds: number; albumsGapless: boolean }
    visualization: { quality: string }
}

async function storedSettings(
    request: APIRequestContext,
    profileId: string,
): Promise<StoredSettings> {
    const response = await request.get('/api/v1/me/settings', {
        headers: await sessionHeaders(request, profileId),
    })
    expect(response.status()).toBe(200)
    return (await response.json()) as StoredSettings
}

async function updateSettings(
    request: APIRequestContext,
    profileId: string,
    data: Record<string, unknown>,
): Promise<void> {
    const response = await request.patch('/api/v1/me/settings', {
        headers: await sessionHeaders(request, profileId),
        data,
    })
    expect(response.status()).toBe(200)
}

/** Plays the first song of E2E Album and pauses it; on phones it opens the full-screen player. */
async function startAlbum(page: Page, isMobile: boolean): Promise<void> {
    await page.goto('/library/albums')
    await part(page, 'tile')
        .filter({ has: page.locator("[data-part='tile-title']", { hasText: 'E2E Album' }) })
        .click()
    await expect(part(page, 'detail-title')).toHaveText('E2E Album')
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await part(page, 'btn-play').click()
    await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
    if (isMobile) {
        await part(page, 'now-playing-mini').click()
        await expect(part(page, 'transport')).toHaveAttribute('data-state', /\bexpanded\b/)
    }
}

test.describe('with a profile of its own', () => {
    test.use({ storageState: emptyStorageState, locale: 'en-US' })

    let user: TestUser

    test.beforeEach(async ({ page, request }) => {
        user = await createUser(request, `Corrections ${uniqueSuffix()}`)
        await page.goto('/profiles')
        await profileTile(page, user.name).click()
        await expect(page).toHaveURL(homeUrl)
    })

    test.afterEach(async ({ request }) => {
        await deleteUser(request, user.id)
    })

    test('switches the crossfade in the transport bar and keeps its length', async ({
        page,
        request,
        isMobile,
    }) => {
        const crossfadeSelect = page.locator("[data-part='app'] [data-field='crossfade'] select")

        // A new profile starts without crossfade, with five seconds ready.
        await page.goto('/settings')
        await expect(crossfadeSelect).toHaveValue('off')
        await crossfadeSelect.selectOption('8')
        await expect
            .poll(async () => (await storedSettings(request, user.id)).crossfade)
            .toMatchObject({ enabled: true, seconds: 8 })

        await startAlbum(page, isMobile)
        const button = part(page, 'btn-crossfade')
        await expect(button).toBeVisible()
        await expect(button).toHaveAttribute('aria-pressed', 'true')
        await expect(button).toHaveAttribute('data-state', 'on')

        await button.click()
        await expect(button).toHaveAttribute('aria-pressed', 'false')
        await expect(button).toHaveAttribute('data-state', 'off')
        await expect
            .poll(async () => (await storedSettings(request, user.id)).crossfade)
            .toMatchObject({ enabled: false, seconds: 8 })

        await button.click()
        await expect(button).toHaveAttribute('aria-pressed', 'true')
        await expect
            .poll(async () => (await storedSettings(request, user.id)).crossfade)
            .toMatchObject({ enabled: true, seconds: 8 })

        // The switch sits between repeat and stop.
        const order = await part(page, 'transport').evaluate((transport) =>
            [...transport.querySelectorAll('[data-part^="btn-"]')]
                .map((element) => element.getAttribute('data-part'))
                .filter((name) => ['btn-repeat', 'btn-crossfade', 'btn-stop'].includes(name!)),
        )
        expect(order).toEqual(['btn-repeat', 'btn-crossfade', 'btn-stop'])

        await page.goto('/settings')
        await expect(crossfadeSelect).toHaveValue('8')
    })

    test('checks the ReplayGain mode with the keyboard', async ({ page, request }) => {
        await page.goto('/settings')
        const group = page.locator(
            "[data-part='app'] [data-field='replay-gain'] [data-part='segmented-control']",
        )
        const segment = (item: string) =>
            group.locator(`[data-part='segment'][data-item='${item}']`)

        await expect(group).toHaveAttribute('role', 'radiogroup')
        await expect(
            page.getByRole('radiogroup', { name: 'Volume leveling (ReplayGain)' }),
        ).toBeVisible()
        await expect(segment('off')).toHaveAttribute('aria-checked', 'true')
        await expect(segment('track')).toHaveAttribute('tabindex', '-1')

        await segment('off').focus()
        await page.keyboard.press('ArrowRight')
        await expect(segment('track')).toHaveAttribute('aria-checked', 'true')
        await expect(segment('track')).toBeFocused()
        await expect
            .poll(async () => (await storedSettings(request, user.id)).replayGain)
            .toBe('Track')

        await page.keyboard.press('End')
        await expect(segment('album')).toHaveAttribute('aria-checked', 'true')
        await expect
            .poll(async () => (await storedSettings(request, user.id)).replayGain)
            .toBe('Album')

        await segment('off').click()
        await expect(segment('off')).toHaveAttribute('aria-checked', 'true')
        await expect(segment('album')).toHaveAttribute('aria-checked', 'false')

        // The checked segment carries the position for the indicator of themes.
        const variables = await group.evaluate((element) => ({
            count: element.style.getPropertyValue('--wmp-item-count'),
            index: element.style.getPropertyValue('--wmp-active-index'),
            width: parseFloat(element.style.getPropertyValue('--wmp-indicator-width')),
        }))
        expect(variables).toMatchObject({ count: '3', index: '0' })
        expect(variables.width).toBeGreaterThan(0)
    })

    test('keeps quick keys and clicks in the settings while they are saved', async ({
        page,
        request,
    }) => {
        // Every save takes a while, so the next key or click comes while one is in flight. The
        // page saves one change after the other, never two at once.
        let inFlight = 0
        let mostInFlight = 0
        await page.route('**/api/v1/me/settings', async (route) => {
            if (route.request().method() !== 'PATCH') {
                await route.continue()
                return
            }

            inFlight++
            mostInFlight = Math.max(mostInFlight, inFlight)
            await new Promise((resolve) => setTimeout(resolve, 400))
            try {
                await route.continue()
            } finally {
                inFlight--
            }
        })
        await page.goto('/settings')
        const field = (name: string) => page.locator(`[data-part='app'] [data-field='${name}']`)
        const segment = (name: string, item: string) =>
            field(name).locator(`[data-part='segment'][data-item='${item}']`)

        // Arrow and End pressed one right after the other: the last key wins.
        await segment('replay-gain', 'off').focus()
        await page.keyboard.press('ArrowRight')
        await page.keyboard.press('End')
        await expect(segment('replay-gain', 'album')).toHaveAttribute('aria-checked', 'true')
        await expect(segment('replay-gain', 'album')).toBeFocused()
        await expect(
            field('replay-gain').locator("[data-part='segmented-control']"),
        ).not.toHaveAttribute('aria-disabled', 'true')

        await segment('visualization-quality', 'auto').focus()
        await page.keyboard.press('ArrowRight')
        await page.keyboard.press('ArrowRight')
        await page.keyboard.press('ArrowRight')
        await expect(segment('visualization-quality', 'high')).toHaveAttribute(
            'aria-checked',
            'true',
        )
        await expect(segment('visualization-quality', 'high')).toBeFocused()

        // Clicks while the previous click is saved.
        await segment('replay-gain', 'off').click()
        await segment('replay-gain', 'track').click()
        await expect(segment('replay-gain', 'track')).toHaveAttribute('aria-checked', 'true')

        // A menu and a checkbox of the same group, changed while the menu is saved.
        await field('crossfade').locator('select').selectOption('3')
        await field('crossfade').locator('select').selectOption('8')
        await field('crossfade-albums').locator('input').uncheck()
        await expect(field('crossfade').locator('select')).toHaveValue('8')

        await expect
            .poll(async () => {
                const settings = await storedSettings(request, user.id)
                return {
                    replayGain: settings.replayGain,
                    quality: settings.visualization.quality,
                    crossfade: settings.crossfade,
                }
            })
            .toEqual({
                replayGain: 'Track',
                quality: 'High',
                crossfade: { enabled: true, seconds: 8, albumsGapless: false },
            })
        await expect.poll(() => inFlight).toBe(0)
        expect(mostInFlight).toBe(1)

        // The page shows what the server stored.
        await page.reload()
        await expect(segment('replay-gain', 'track')).toHaveAttribute('aria-checked', 'true')
        await expect(segment('visualization-quality', 'high')).toHaveAttribute(
            'aria-checked',
            'true',
        )
        await expect(field('crossfade').locator('select')).toHaveValue('8')
        await expect(field('crossfade-albums').locator('input')).not.toBeChecked()
    })

    test.describe('in the chromium project', () => {
        test.beforeEach(({}, testInfo) => {
            test.skip(testInfo.project.name !== 'chromium', 'Desktop and device viewports')
        })

        for (const [name, viewport] of [
            ['the desktop', { width: 1280, height: 720 }],
            ['an iPad in landscape', iPadLandscape],
        ] as const) {
            test(`collapses the sidebar to its icons on ${name} and keeps it`, async ({
                page,
                request,
            }) => {
                await page.setViewportSize(viewport)
                await page.goto('/library/songs')
                const sidebar = part(page, 'sidebar')
                const toggle = part(page, 'btn-sidebar-toggle')
                const albums = page.getByRole('treeitem', { name: 'Album', exact: true })

                await expect(sidebar).toHaveAttribute('data-state', /\bexpanded\b/)
                await expect(toggle).toHaveAttribute('aria-expanded', 'true')
                const wide = (await sidebar.boundingBox())!.width

                await toggle.click()
                await expect(sidebar).toHaveAttribute('data-state', /\bcollapsed\b/)
                await expect(toggle).toHaveAttribute('aria-expanded', 'false')
                await expect
                    .poll(async () => (await sidebar.boundingBox())!.width)
                    .toBeLessThan(wide / 2)
                // The icons stay; the labels are only there for screen readers and as tooltip.
                await expect(albums.locator("[data-part='icon']").first()).toBeVisible()
                expect(
                    (await albums.locator("[data-part='label']").first().boundingBox())!.width,
                ).toBeLessThanOrEqual(1)
                await expect(albums).toHaveAttribute('title', 'Album')
                await expect
                    .poll(async () => (await storedSettings(request, user.id)).sidebarCollapsed)
                    .toBe(true)

                // The profile keeps the state after loading the app again.
                await page.reload()
                await expect(sidebar).toHaveAttribute('data-state', /\bcollapsed\b/)
                await albums.click()
                await expect(page).toHaveURL(/\/library\/albums$/)

                await toggle.click()
                await expect(sidebar).toHaveAttribute('data-state', /\bexpanded\b/)
                await expect
                    .poll(async () => (await sidebar.boundingBox())!.width)
                    .toBeCloseTo(wide, 0)
                await expect
                    .poll(async () => (await storedSettings(request, user.id)).sidebarCollapsed)
                    .toBe(false)
            })
        }

        test('leaves the sidebar of an iPhone as it is', async ({ page, request }) => {
            await updateSettings(request, user.id, { sidebarCollapsed: true })
            await page.setViewportSize(iPhone)
            await page.goto('/library/songs')

            await part(page, 'btn-menu').click()
            const sidebar = part(page, 'sidebar')
            await expect(sidebar).toHaveAttribute('data-state', 'open expanded')
            await expect(part(page, 'btn-sidebar-toggle')).toBeHidden()
            const label = page
                .getByRole('treeitem', { name: 'Album', exact: true })
                .locator("[data-part='label']")
                .first()
            await expect(label).toBeVisible()
            expect((await label.boundingBox())!.width).toBeGreaterThan(20)
        })

        test('fits the segmented controls into an iPhone', async ({ page }) => {
            await page.setViewportSize(iPhone)
            await page.goto('/settings')

            const group = part(page, 'segmented-control').first()
            await expect(group).toBeVisible()
            const box = (await group.boundingBox())!
            expect(box.x).toBeGreaterThanOrEqual(0)
            expect(box.x + box.width).toBeLessThanOrEqual(iPhone.width)
            expect(
                await page.evaluate(
                    () => document.documentElement.scrollWidth <= window.innerWidth,
                ),
            ).toBe(true)
        })
    })
})
