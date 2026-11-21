import { mkdirSync } from 'node:fs'
import { devices, expect, test, type Browser, type Page } from '@playwright/test'
import {
    adminToken,
    deleteBackup,
    deleteUser,
    findAlbumId,
    listBackups,
    type TestUser,
} from '../tests/helpers/api'
import { homeUrl, profileTile } from '../tests/helpers/navigation'
import { emptyStorageState } from '../tests/helpers/storage'
import {
    createProfiles,
    part,
    pause,
    play,
    prepareProfile,
    save as saveAs,
    screenshotsDirectory,
    selectProfile,
    send,
    sessionHeaders,
    showVisualization as showVisualizationOf,
    stillCovers,
} from './helpers'

// The screenshots of the README, taken with the synthetic test media (sine tones and test pictures)
// and two profiles created for them: Alex with playlists, a smart playlist, favorites, ratings and
// plays, and Sam. They are written to assets/screenshots and replace the files there; the profiles
// and the export of the database made for the backup page are removed afterwards.

const outputDirectory = screenshotsDirectory
const desktop = { width: 1440, height: 900 }

test.describe.configure({ mode: 'serial' })
test.use({ storageState: emptyStorageState, viewport: desktop, deviceScaleFactor: 1 })
test.beforeEach(({ context }) => stillCovers(context))

let alex: TestUser
let sam: TestUser
let alexHeaders: Record<string, string>

/**
 * Closes the offer to continue the playback of an earlier test, which runs in its own browser and
 * so counts as another device.
 */
async function dismissResume(page: Page): Promise<void> {
    const notice = part(page, 'resume-notice')
    await notice.waitFor({ timeout: 3000 }).catch(() => undefined)
    if (await notice.isVisible()) {
        await notice.locator("[data-part='btn-dismiss-resume']").click()
        await expect(notice).toBeHidden()
    }
}

async function save(page: Page, name: string): Promise<void> {
    await saveAs(page, `${outputDirectory}${name}`)
}

/** Shows a visualization of Loose Song with the given module and preset. */
async function showVisualization(
    page: Page,
    request: Parameters<typeof showVisualizationOf>[1],
    module: string,
    preset: string | null,
): Promise<void> {
    await showVisualizationOf(page, request, alexHeaders, alex.name, module, preset)
}

test.beforeAll(async ({ request }) => {
    mkdirSync(outputDirectory, { recursive: true })
    ;[alex, sam] = await createProfiles(request, ['Alex', 'Sam'])
    alexHeaders = await sessionHeaders(request, alex.id)
    await prepareProfile(request, alexHeaders)
    await send(request, alexHeaders, 'post', '/api/v1/me/smart-playlists', {
        name: 'Best of E2E',
        match: 'Any',
        rules: [
            { field: 'Rating', operator: 'AtLeast', number: 4 },
            { field: 'Favorite', operator: 'IsTrue' },
        ],
        sort: 'Rating',
        direction: 'Desc',
        limit: 50,
    })
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
    // Autoplay below the queue has chosen the songs that follow the album.
    await expect(part(page, 'autoplay-item').first()).toBeVisible()
    await save(page, 'now-playing')
})

test('context menu', async ({ page }) => {
    await selectProfile(page, alex.name)
    await page.goto('/library/songs')
    await dismissResume(page)
    const row = part(page, 'list-row').filter({ hasText: 'Second Song' }).first()
    await row.click({ button: 'right' })
    const menu = page.locator("[data-part='context-menu'][data-state='open']")
    await expect(menu).toBeVisible()
    // The submenu of the rating, opened like with the mouse.
    await menu.locator("[data-part='menu-item'][data-item='rate']").hover()
    await expect(page.locator("[data-part='context-menu'][data-kind='submenu']")).toBeVisible()
    await page.waitForTimeout(300)
    await page.screenshot({ path: `${outputDirectory}context-menu.png` })
})

test('smart playlist', async ({ page, request }) => {
    await selectProfile(page, alex.name)
    const smart = await request.get('/api/v1/me/smart-playlists?limit=100', {
        headers: alexHeaders,
    })
    const id = ((await smart.json()).items as { id: string; name: string }[]).find(
        (item) => item.name === 'Best of E2E',
    )!.id
    await page.goto(`/playlists/smart/${id}/edit`)
    await dismissResume(page)
    await expect(part(page, 'smart-rule')).toHaveCount(2)
    await save(page, 'smart-playlist')
})

test('mini player', async ({ page, request }) => {
    // The mini player in the window of the app, as in browsers without floating windows; in
    // Chromium it opens in a window of its own.
    await page.addInitScript(() => {
        delete (window as { documentPictureInPicture?: unknown }).documentPictureInPicture
    })
    await selectProfile(page, alex.name)
    await page.goto(`/library/albums/${await findAlbumId(request, 'E2E Album')}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await expect(part(page, 'now-playing-title').filter({ visible: true }).first()).toHaveText(
        'First Song',
    )
    await pause(page)
    await part(page, 'btn-compact').click()
    const player = page.locator("[data-part='compact-player-panel']")
    await expect(player).toBeVisible()
    await page.mouse.move(0, 0)
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur())
    await page.waitForTimeout(500)
    // Only the player with its shadow, not the empty window around it.
    const box = (await player.boundingBox())!
    const margin = 32
    await page.screenshot({
        path: `${outputDirectory}mini-player.png`,
        clip: {
            x: box.x - margin,
            y: box.y - margin,
            width: box.width + 2 * margin,
            height: box.height + 2 * margin,
        },
    })
})

test('media folders', async ({ browser }) => {
    const page = await adminPage(browser)
    await page.goto('/admin/library')
    await expect(page.locator("[data-part='source-item']").first()).toBeVisible()
    await page.locator("[data-part='section'][data-item='media-sources']").scrollIntoViewIfNeeded()
    await page.locator("[data-item='add-source']").click()
    const dialog = page.locator("dialog[data-part='dialog'][open]")
    await dialog.getByLabel('Name').fill('Music videos')
    await dialog.getByLabel('Folder in the container').fill('/videos')
    await save(page, 'admin-media-folders')
    await dialog.getByRole('button', { name: 'Cancel' }).click()
    await page.context().close()
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
        await send(request, alexHeaders, 'patch', '/api/v1/me/settings', {
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
