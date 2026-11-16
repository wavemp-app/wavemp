import { mkdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import {
    devices,
    expect,
    test,
    type APIRequestContext,
    type Browser,
    type Locator,
    type Page,
} from '@playwright/test'
import {
    adminToken,
    createUser,
    deleteBackup,
    deleteUser,
    findAlbumId,
    findTrackId,
    findUserByName,
    listBackups,
    type TestUser,
} from '../tests/helpers/api'
import { homeUrl, profileTile } from '../tests/helpers/navigation'
import { emptyStorageState } from '../tests/helpers/storage'

// The screenshots of the README, taken with the synthetic test media (sine tones and test pictures)
// and two profiles created for them: Alex with playlists, favorites, ratings and plays, and Sam.
// They are written to assets/screenshots and replace the files there; the profiles and the export of
// the database made for the backup page are removed afterwards.

const outputDirectory = fileURLToPath(new URL('../../../assets/screenshots/', import.meta.url))
const desktop = { width: 1440, height: 900 }

test.describe.configure({ mode: 'serial' })
test.use({ storageState: emptyStorageState, viewport: desktop, deviceScaleFactor: 1 })

let alex: TestUser
let sam: TestUser
let alexHeaders: Record<string, string>

async function sessionHeaders(
    request: APIRequestContext,
    profileId: string,
): Promise<Record<string, string>> {
    const session = await request.post('/api/v1/sessions', { data: { profileId } })
    expect(session.status()).toBe(201)
    return { Authorization: `Bearer ${(await session.json()).token}` }
}

async function send(
    request: APIRequestContext,
    method: 'post' | 'put' | 'patch',
    url: string,
    data?: unknown,
): Promise<void> {
    const response = await request[method](url, { data, headers: alexHeaders })
    expect(response.ok(), `${method.toUpperCase()} ${url}: ${await response.text()}`).toBe(true)
}

/** Fills the profile Alex with what the home page shows. */
async function prepareAlex(request: APIRequestContext): Promise<void> {
    const first = await findTrackId(request, 'First Song')
    const second = await findTrackId(request, 'Second Song')
    const loose = await findTrackId(request, 'Loose Song')
    const video = await findTrackId(request, 'Video Only')
    const album = await findAlbumId(request, 'E2E Album')

    await send(request, 'post', '/api/v1/me/playlists', {
        name: 'Evening Mix',
        trackIds: [first, loose, second],
    })
    await send(request, 'post', '/api/v1/me/playlists', {
        name: 'Road Trip',
        trackIds: [loose, second, video],
    })
    await send(request, 'put', `/api/v1/me/favorites/albums/${album}`)
    await send(request, 'put', `/api/v1/me/favorites/tracks/${loose}`)
    await send(request, 'put', `/api/v1/tracks/${first}/rating`, { rating: 5 })
    await send(request, 'put', `/api/v1/tracks/${loose}/rating`, { rating: 4 })
    for (const trackId of [second, first, loose]) {
        await send(request, 'post', '/api/v1/me/history', { trackId })
    }
    await send(request, 'patch', '/api/v1/me/settings', {
        playerViewMode: 'Cover',
        visualization: { autoChange: false, quality: 'High' },
    })
}

async function save(page: Page, name: string): Promise<void> {
    // Hover effects and the text cursor stay out of the pictures.
    await page.mouse.move(0, 0)
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur())
    await page.waitForTimeout(500)
    await page.screenshot({ path: `${outputDirectory}${name}.png` })
}

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

async function selectProfile(page: Page, name: string): Promise<void> {
    await page.goto('/profiles')
    await profileTile(page, name).click()
    await expect(page).toHaveURL(homeUrl)
}

/** Plays a song from its page and opens Now Playing. */
async function play(page: Page, request: APIRequestContext, title: string): Promise<void> {
    await page.goto(`/library/tracks/${await findTrackId(request, title)}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await expect(part(page, 'now-playing-title').filter({ visible: true }).first()).toHaveText(
        title,
    )
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(page).toHaveURL(/\/now-playing$/)
}

async function pause(page: Page): Promise<void> {
    const button = part(page, 'btn-play').filter({ visible: true }).first()
    await button.click()
}

/** Shows a visualization of Loose Song with the given module and preset. */
async function showVisualization(
    page: Page,
    request: APIRequestContext,
    module: string,
    preset: string | null,
): Promise<void> {
    await send(request, 'patch', '/api/v1/me/settings', {
        visualization: { module, preset: preset ?? '' },
    })
    await selectProfile(page, alex.name)
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

test.beforeAll(async ({ request }) => {
    mkdirSync(outputDirectory, { recursive: true })
    // Profiles left over from an interrupted run.
    for (const name of ['Alex', 'Sam']) {
        const left = await findUserByName(request, name)
        if (left) {
            await deleteUser(request, left.id)
        }
    }
    alex = await createUser(request, 'Alex')
    sam = await createUser(request, 'Sam')
    alexHeaders = await sessionHeaders(request, alex.id)
    await prepareAlex(request)
})

test.afterAll(async ({ request }) => {
    await deleteUser(request, alex.id)
    await deleteUser(request, sam.id)
})

test('profile selection', async ({ page }) => {
    await page.goto('/profiles')
    await expect(profileTile(page, 'Alex')).toBeVisible()
    await expect(profileTile(page, 'Sam')).toBeVisible()
    await save(page, 'profiles')
})

test('home', async ({ page }) => {
    await selectProfile(page, alex.name)
    await expect(page.locator("[data-part='shelf'][data-item='playlists']")).toBeVisible()
    await expect(page.locator("[data-part='shelf'][data-item='top-rated']")).toBeVisible()
    await save(page, 'home')
})

test('album', async ({ page, request }) => {
    await selectProfile(page, alex.name)
    await page.goto(`/library/albums/${await findAlbumId(request, 'E2E Album')}`)
    await expect(part(page, 'detail-title')).toHaveText('E2E Album')
    await save(page, 'album')
})

test('now playing', async ({ page, request }) => {
    await selectProfile(page, alex.name)
    await page.goto(`/library/albums/${await findAlbumId(request, 'E2E Album')}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await expect(part(page, 'now-playing-title').filter({ visible: true }).first()).toHaveText(
        'First Song',
    )
    await pause(page)
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(part(page, 'queue')).toBeVisible()
    await save(page, 'now-playing')
})

test('visualization A-Force', async ({ page, request }) => {
    await showVisualization(page, request, 'AForce', 'aurora')
    await save(page, 'visualization-a-force')
})

test('visualization BlackCap', async ({ page, request }) => {
    await showVisualization(page, request, 'BlackCap', 'orchid')
    await save(page, 'visualization-blackcap')
})

test('visualization Milkdrop', async ({ page, request }) => {
    await showVisualization(page, request, 'Milkdrop', null)
    await save(page, 'visualization-milkdrop')
})

test('administration', async ({ browser, request }) => {
    const before = new Set((await listBackups(request)).map((backup) => backup.id))
    const started = await request.post('/api/v1/backups', {
        headers: { Authorization: `Bearer ${await adminToken(request)}` },
    })
    expect(started.status()).toBe(202)
    let created: string | undefined
    await expect
        .poll(
            async () => {
                created = (await listBackups(request)).find((backup) => !before.has(backup.id))?.id
                return created
            },
            { timeout: 60_000 },
        )
        .toBeDefined()

    try {
        const page = await adminPage(browser)
        await page.goto('/admin/backups')
        await expect(page.locator("[data-part='backup-item']").first()).toBeVisible()
        await save(page, 'admin-backups')
        await page.context().close()
    } finally {
        await deleteBackup(request, created!)
    }
})

async function adminPage(browser: Browser): Promise<Page> {
    const context = await browser.newContext({
        viewport: desktop,
        deviceScaleFactor: 1,
        locale: 'en-US',
    })
    const page = await context.newPage()
    await page.goto('/profiles')
    await page.locator("[data-part='profile-tile'][data-state~='built-in']").click()
    await expect(page).toHaveURL(homeUrl)
    return page
}

test.describe('phone', () => {
    const phone = devices['Pixel 7']

    test.use({
        viewport: phone.viewport,
        userAgent: phone.userAgent,
        deviceScaleFactor: 2,
        isMobile: true,
        hasTouch: true,
    })

    test('library on the phone', async ({ page }) => {
        await selectProfile(page, alex.name)
        await page.goto('/library/albums')
        await expect(page.locator("[data-part='tile']").first()).toBeVisible()
        await save(page, 'phone-library')
    })

    test('now playing on the phone', async ({ page, request }) => {
        await send(request, 'patch', '/api/v1/me/settings', {
            visualization: { module: 'AForce', preset: 'nebula' },
        })
        await selectProfile(page, alex.name)
        await play(page, request, 'Loose Song')
        await page
            .locator(
                "[data-part='app'] [data-part='stage-actions'] [data-part='segment'][data-item='visualization']",
            )
            .click()
        await expect(part(page, 'visualization')).toHaveAttribute('data-state', /\brunning\b/, {
            timeout: 30_000,
        })
        await page.waitForTimeout(4000)
        await save(page, 'phone-now-playing')
    })
})
