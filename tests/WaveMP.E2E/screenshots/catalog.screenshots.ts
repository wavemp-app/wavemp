import { mkdirSync } from 'node:fs'
import { expect, test, type Page } from '@playwright/test'
import { deleteUser, findAlbumId, type TestUser } from '../tests/helpers/api'
import { profileTile } from '../tests/helpers/navigation'
import { emptyStorageState } from '../tests/helpers/storage'
import {
    createProfiles,
    part,
    prepareProfile,
    save as saveAs,
    screenshotsDirectory,
    selectProfile,
    sessionHeaders,
    showVisualization,
    stillCovers,
} from './helpers'

// The screenshots of the app catalogs (TrueNAS, Unraid, ZimaOS and CasaOS, see deploy/), taken like
// the screenshots of the README with the synthetic test media and two profiles created for them. They
// are written to assets/screenshots/catalogs in 16:9 (1920 x 1080); the CasaOS folder in deploy/
// holds copies of them (see deploy/README.md). The profiles are removed afterwards.

const outputDirectory = `${screenshotsDirectory}catalogs/`

test.describe.configure({ mode: 'serial' })
test.use({
    storageState: emptyStorageState,
    viewport: { width: 1280, height: 720 },
    deviceScaleFactor: 1.5,
})
test.beforeEach(({ context }) => stillCovers(context))

let alex: TestUser
let sam: TestUser
let alexHeaders: Record<string, string>

async function save(page: Page, name: string): Promise<void> {
    await saveAs(page, `${outputDirectory}${name}`)
}

test.beforeAll(async ({ request }) => {
    mkdirSync(outputDirectory, { recursive: true })
    ;[alex, sam] = await createProfiles(request, ['Alex', 'Sam'])
    alexHeaders = await sessionHeaders(request, alex.id)
    await prepareProfile(request, alexHeaders)
})

test.afterAll(async ({ request }) => {
    await deleteUser(request, alex.id)
    await deleteUser(request, sam.id)
})

test('home', async ({ page }) => {
    await page.goto('/profiles')
    await expect(profileTile(page, sam.name)).toBeVisible()
    await selectProfile(page, alex.name)
    await expect(page.locator("[data-part='shelf'][data-item='playlists']")).toBeVisible()
    await save(page, 'home')
})

test('now playing with a visualization', async ({ page, request }) => {
    await showVisualization(page, request, alexHeaders, alex.name, 'AForce', 'flow', 'aurora')
    await save(page, 'now-playing')
})

test('album', async ({ page, request }) => {
    await selectProfile(page, alex.name)
    await page.goto(`/library/albums/${await findAlbumId(request, 'E2E Album')}`)
    await expect(part(page, 'detail-title')).toHaveText('E2E Album')
    await save(page, 'album')
})
