import { crc32, deflateSync } from 'node:zlib'
import { expect, test, type Locator, type Page } from '@playwright/test'
import { withoutSavedPlayback } from './helpers/player'

// Covers are loaded in the size they are shown with times the pixel density of the screen, so they
// stay sharp on a phone with a density of 3 and on a 4K screen, in lists and in the player. The
// test media has a small cover, so the covers of albums and tracks are answered with a picture of
// exactly the requested size: the width of the shown picture then tells which size the web app
// asked for. The tests run in the project "chromium" only, with the viewports and pixel densities
// of the devices.
test.beforeEach(async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'Chromium with device viewports')
    await withoutSavedPlayback(page)
    await page.route(/\/api\/v1\/(albums|tracks)\/[0-9a-f-]+\/cover\?size=\d+/, (route) => {
        const size = Number(new URL(route.request().url()).searchParams.get('size'))
        return route.fulfill({ status: 200, contentType: 'image/png', body: squarePng(size) })
    })
})

/** Largest size of the API: 2000 pixels. */
const largestCoverSize = 2000

const iPhone = {
    viewport: { width: 393, height: 852 },
    deviceScaleFactor: 3,
    isMobile: true,
    hasTouch: true,
}

/** A 4K screen (3840 x 2160) with a scale of 200 %. */
const desktop4K = { viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 2 }

const pictures = new Map<number, Buffer>()

/** A grey square PNG of the size in pixels. */
function squarePng(size: number): Buffer {
    const known = pictures.get(size)
    if (known) {
        return known
    }

    function chunk(type: string, data: Buffer): Buffer {
        const length = Buffer.alloc(4)
        length.writeUInt32BE(data.length)
        const content = Buffer.concat([Buffer.from(type, 'ascii'), data])
        const checksum = Buffer.alloc(4)
        checksum.writeUInt32BE(crc32(content))
        return Buffer.concat([length, content, checksum])
    }

    const header = Buffer.alloc(13)
    header.writeUInt32BE(size, 0)
    header.writeUInt32BE(size, 4)
    header[8] = 8 // bit depth
    header[9] = 0 // greyscale
    // One row: the filter type 0, then one byte per pixel.
    const row = Buffer.alloc(size + 1, 0x80)
    row[0] = 0
    const picture = Buffer.concat([
        Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
        chunk('IHDR', header),
        chunk('IDAT', deflateSync(Buffer.concat(Array.from({ length: size }, () => row)))),
        chunk('IEND', Buffer.alloc(0)),
    ])
    pictures.set(size, picture)
    return picture
}

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

/** The pixels a cover covers on the screen: its width times the pixel density. */
async function shownPixels(cover: Locator): Promise<number> {
    await expect(cover).toBeVisible()
    return cover.evaluate((element) =>
        Math.ceil((element as HTMLElement).offsetWidth * window.devicePixelRatio),
    )
}

/** The width of the picture a cover shows, 0 while it shows none. */
async function pictureWidth(cover: Locator): Promise<number> {
    return cover.evaluate((element) => {
        const image = element.querySelector<HTMLImageElement>("[data-part='cover-image']")
        return image?.complete ? image.naturalWidth : 0
    })
}

/** Expects the cover to show a picture with at least as many pixels as it covers on the screen. */
async function expectSharp(cover: Locator): Promise<void> {
    const pixels = Math.min(await shownPixels(cover), largestCoverSize)
    expect(pixels).toBeGreaterThan(0)
    await expect
        .poll(() => pictureWidth(cover), { message: `a picture of at least ${pixels} pixels` })
        .toBeGreaterThanOrEqual(pixels)
}

async function openAlbum(page: Page): Promise<void> {
    await page.goto('/library/albums')
    await part(page, 'tile')
        .filter({ has: page.locator("[data-part='tile-title']", { hasText: 'E2E Album' }) })
        .click()
    await expect(part(page, 'detail-title')).toHaveText('E2E Album')
}

/** Plays the album and pauses at once, so the songs of two seconds do not end during the test. */
async function playAlbum(page: Page): Promise<void> {
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await part(page, 'btn-play').click()
    await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
    await expect(part(page, 'now-playing-title')).toHaveText('First Song')
}

function cover(container: Locator): Locator {
    return container.locator("[data-part='cover']").first()
}

test.describe('on a phone with a pixel density of 3', () => {
    test.use(iPhone)

    test('shows the covers of tiles, the album and the mini player sharp', async ({ page }) => {
        await page.goto('/library/albums')
        await expectSharp(cover(part(page, 'tile').first()))

        await openAlbum(page)
        await expectSharp(cover(part(page, 'detail-header')))

        await playAlbum(page)
        await expectSharp(cover(part(page, 'now-playing-mini')))
    })

    test('loads a larger cover when the mini player becomes the full-screen player', async ({
        page,
    }) => {
        await openAlbum(page)
        await playAlbum(page)
        const playing = cover(part(page, 'now-playing-mini'))
        await expectSharp(playing)
        const mini = await shownPixels(playing)

        await part(page, 'now-playing-mini').click()
        await expect(part(page, 'transport')).toHaveAttribute('data-state', /\bexpanded\b/)

        expect(await shownPixels(playing)).toBeGreaterThan(mini * 3)
        await expectSharp(playing)
    })
})

test.describe('on a 4K screen', () => {
    test.use(desktop4K)

    test('shows the covers of tiles, the album and Now Playing sharp', async ({ page }) => {
        await page.goto('/library/albums')
        await expectSharp(cover(part(page, 'tile').first()))

        await openAlbum(page)
        await expectSharp(cover(part(page, 'detail-header')))

        await playAlbum(page)
        await page
            .locator("[data-part='app'] a[href='/now-playing']")
            .filter({ visible: true })
            .click()
        await expect(page).toHaveURL(/\/now-playing$/)
        await expectSharp(cover(page.locator("[data-part='stage-view'][data-kind='cover']")))
    })
})
