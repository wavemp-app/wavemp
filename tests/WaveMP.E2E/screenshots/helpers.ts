import { fileURLToPath } from 'node:url'
import { expect, type APIRequestContext, type Locator, type Page } from '@playwright/test'
import {
    createUser,
    deleteUser,
    findAlbumId,
    findTrackId,
    findUserByName,
    type TestUser,
} from '../tests/helpers/api'
import { homeUrl, profileTile } from '../tests/helpers/navigation'

// Helpers of the screenshot specs: the profiles they show and the steps to the pages they take.

/** Folder of the screenshots (assets/screenshots); the specs write into it or a folder below. */
export const screenshotsDirectory = fileURLToPath(
    new URL('../../../assets/screenshots/', import.meta.url),
)

/** Starts a session of a profile and returns the header that sends its token. */
export async function sessionHeaders(
    request: APIRequestContext,
    profileId: string,
): Promise<Record<string, string>> {
    const session = await request.post('/api/v1/sessions', { data: { profileId } })
    expect(session.status()).toBe(201)
    return { Authorization: `Bearer ${(await session.json()).token}` }
}

/** Sends a request with the session of a profile and expects it to succeed. */
export async function send(
    request: APIRequestContext,
    headers: Record<string, string>,
    method: 'post' | 'put' | 'patch',
    url: string,
    data?: unknown,
): Promise<void> {
    const response = await request[method](url, { data, headers })
    expect(response.ok(), `${method.toUpperCase()} ${url}: ${await response.text()}`).toBe(true)
}

/** Creates the profiles again, after removing the ones an interrupted run left behind. */
export async function createProfiles(
    request: APIRequestContext,
    names: string[],
): Promise<TestUser[]> {
    const users: TestUser[] = []
    for (const name of names) {
        const left = await findUserByName(request, name)
        if (left) {
            await deleteUser(request, left.id)
        }
        users.push(await createUser(request, name))
    }
    return users
}

/** Fills a profile with what the home page shows: playlists, favorites, ratings and plays. */
export async function prepareProfile(
    request: APIRequestContext,
    headers: Record<string, string>,
): Promise<void> {
    const first = await findTrackId(request, 'First Song')
    const second = await findTrackId(request, 'Second Song')
    const loose = await findTrackId(request, 'Loose Song')
    const video = await findTrackId(request, 'Video Only')
    const album = await findAlbumId(request, 'E2E Album')

    await send(request, headers, 'post', '/api/v1/me/playlists', {
        name: 'Evening Mix',
        trackIds: [first, loose, second],
    })
    await send(request, headers, 'post', '/api/v1/me/playlists', {
        name: 'Road Trip',
        trackIds: [loose, second, video],
    })
    await send(request, headers, 'put', `/api/v1/me/favorites/albums/${album}`)
    await send(request, headers, 'put', `/api/v1/me/favorites/tracks/${loose}`)
    await send(request, headers, 'put', `/api/v1/tracks/${first}/rating`, { rating: 5 })
    await send(request, headers, 'put', `/api/v1/tracks/${loose}/rating`, { rating: 4 })
    for (const trackId of [second, first, loose]) {
        await send(request, headers, 'post', '/api/v1/me/history', { trackId })
    }
    await send(request, headers, 'patch', '/api/v1/me/settings', {
        playerViewMode: 'Cover',
        visualization: { autoChange: false, quality: 'High' },
    })
}

/** Saves a screenshot of the page as <path>.png, without hover effects and text cursor. */
export async function save(page: Page, path: string): Promise<void> {
    await page.mouse.move(0, 0)
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur())
    await page.waitForTimeout(500)
    await page.screenshot({ path: `${path}.png` })
}

export function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

export async function selectProfile(page: Page, name: string): Promise<void> {
    await page.goto('/profiles')
    await profileTile(page, name).click()
    await expect(page).toHaveURL(homeUrl)
}

/** Plays a song from its page and opens Now Playing. */
export async function play(page: Page, request: APIRequestContext, title: string): Promise<void> {
    await page.goto(`/library/tracks/${await findTrackId(request, title)}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await expect(part(page, 'now-playing-title').filter({ visible: true }).first()).toHaveText(
        title,
    )
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(page).toHaveURL(/\/now-playing$/)
}

export async function pause(page: Page): Promise<void> {
    const button = part(page, 'btn-play').filter({ visible: true }).first()
    await button.click()
}

/** Shows a visualization of Loose Song on Now Playing with the given module and preset. */
export async function showVisualization(
    page: Page,
    request: APIRequestContext,
    headers: Record<string, string>,
    profile: string,
    module: string,
    preset: string | null,
): Promise<void> {
    await send(request, headers, 'patch', '/api/v1/me/settings', {
        visualization: { module, preset: preset ?? '' },
    })
    await selectProfile(page, profile)
    await play(page, request, 'Loose Song')
    await page
        .locator(
            "[data-part='app'] [data-part='stage-actions'] [data-part='segment'][data-item='visualization']",
        )
        .click()
    const visualization = part(page, 'visualization')
    await expect(visualization).toHaveAttribute('data-state', /\brunning\b/, { timeout: 30_000 })
    // Let the picture build up with the music.
    await page.waitForTimeout(4000)
}
