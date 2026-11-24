import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'
import { adminHeaders } from './helpers/api'

// Pictures of artists: the collage of album covers of an artist without a picture of its own, the
// upload of a cropped picture by an admin, the online lookup while it is switched off (the server
// contacts no service and says so) and the online lookup with answers of the API simulated in the
// browser (page.route), so no test ever reaches MusicBrainz, Wikidata, Wikimedia Commons or
// fanart.tv. The test media have no picture in an artist folder: E2E Artist shows the collage of
// its album, Loose Artist (no album with a cover) the icon of artists. The picture in the artist
// folder is covered by the integration tests of the API, which create it with ffmpeg.
test.use({ locale: 'en-US' })

interface ArtistItem {
    readonly id: string
    readonly name: string
    readonly picture?: {
        readonly kind: string
        readonly albumIds?: string[]
        readonly hasUploadedPicture?: boolean
    }
}

async function findArtist(request: APIRequestContext, name: string): Promise<ArtistItem> {
    const response = await request.get('/api/v1/artists?limit=100', {
        headers: await adminHeaders(request),
    })
    expect(response.status()).toBe(200)
    const artist = ((await response.json()).items as ArtistItem[]).find(
        (item) => item.name === name,
    )
    expect(artist, name).toBeDefined()
    return artist!
}

async function removeUploadedPicture(request: APIRequestContext, id: string): Promise<void> {
    const response = await request.delete(`/api/v1/artists/${id}/uploaded-picture`, {
        headers: await adminHeaders(request),
    })
    expect(response.status()).toBe(204)
}

function artistTile(page: Page, name: string): Locator {
    return page
        .locator("[data-part='tile'][data-kind='artist']")
        .filter({ has: page.locator("[data-part='tile-title']", { hasText: name }) })
}

function detailMedia(page: Page): Locator {
    return page.locator(
        "[data-part='detail-header'] > :is([data-part='cover'], [data-part='artist-collage'])",
    )
}

function pictureDialog(page: Page): Locator {
    return page.locator("[data-part='dialog'][data-item='artist-picture']")
}

async function openPictureDialog(page: Page, artistId: string): Promise<Locator> {
    await page.goto(`/library/artists/${artistId}`)
    await page
        .locator("[data-part='detail-actions'] [data-item='artist-picture']")
        .filter({ visible: true })
        .first()
        .click()
    const dialog = pictureDialog(page)
    await expect(dialog).toBeVisible()
    return dialog
}

/** A landscape PNG drawn in the browser: the test brings no picture files of its own. */
async function landscapePng(page: Page): Promise<Buffer> {
    const dataUrl = await page.evaluate(() => {
        const canvas = document.createElement('canvas')
        canvas.width = 480
        canvas.height = 320
        const context = canvas.getContext('2d')!
        const gradient = context.createLinearGradient(0, 0, 480, 320)
        gradient.addColorStop(0, '#d6491f')
        gradient.addColorStop(1, '#ffd37f')
        context.fillStyle = gradient
        context.fillRect(0, 0, 480, 320)
        context.fillStyle = '#ffffff'
        context.beginPath()
        context.arc(240, 160, 90, 0, Math.PI * 2)
        context.fill()
        return canvas.toDataURL('image/png')
    })
    return Buffer.from(dataUrl.slice(dataUrl.indexOf(',') + 1), 'base64')
}

test('shows the collage of album covers in the list and on the page of an artist', async ({
    page,
    request,
}) => {
    const artist = await findArtist(request, 'E2E Artist')
    expect(artist.picture?.kind).toBe('Albums')
    const count = artist.picture?.albumIds?.length ?? 0
    expect(count).toBeGreaterThan(0)

    await page.goto('/library/artists')
    const collage = artistTile(page, 'E2E Artist').locator("[data-part='artist-collage']")
    await expect(collage).toHaveAttribute('data-count', String(count))
    await expect(collage).toHaveAttribute('aria-hidden', 'true')
    const tiles = collage.locator("[data-part='artist-collage-tile']")
    await expect(tiles).toHaveCount(count)
    await expect(tiles.first()).toHaveAttribute('data-index', '0')
    await expect(tiles.first().locator("[data-part='cover-image']")).toBeVisible()

    // The collage is round by default like the pictures of artists.
    const radius = await collage.evaluate((element) => getComputedStyle(element).borderRadius)
    expect(radius).not.toBe('0px')

    await artistTile(page, 'E2E Artist').click()
    await expect(page.locator("[data-part='detail-title']")).toHaveText('E2E Artist')
    const header = detailMedia(page)
    await expect(header).toHaveAttribute('data-part', 'artist-collage')
    await expect(header.locator("[data-part='cover-image']").first()).toBeVisible()
    await expect(page.locator("[data-part='artist-credit']")).toHaveCount(0)
})

test.describe('Loose Artist', () => {
    // The upload changes the picture of Loose Artist on the server: on the desktop only and one
    // test after the other, so no test sees the picture of another one.
    test.describe.configure({ mode: 'serial' })
    test.skip(({ isMobile }) => isMobile, 'Changes the library: desktop only.')

    test('shows the icon of artists without picture and album covers', async ({
        page,
        request,
    }) => {
        const artist = await findArtist(request, 'Loose Artist')
        expect(artist.picture?.kind).toBe('None')

        await page.goto(`/library/artists/${artist.id}`)
        const media = detailMedia(page)
        await expect(media).toHaveAttribute('data-kind', 'artist')
        await expect(media.locator("[data-part='icon'][data-icon='artist']")).toBeVisible()
    })

    test('uploads a cropped picture as admin, shows it and removes it again', async ({
        page,
        request,
    }) => {
        const artist = await findArtist(request, 'Loose Artist')
        if (artist.picture?.hasUploadedPicture) {
            await removeUploadedPicture(request, artist.id)
        }

        try {
            const dialog = await openPictureDialog(page, artist.id)
            const sources = dialog.locator("[data-part='picture-source']")
            await expect(sources).toHaveCount(4)
            await expect(sources.nth(1)).toHaveAttribute('data-source', 'uploaded')
            await expect(sources.nth(1)).toHaveAttribute('data-state', 'missing')

            const mode = dialog.locator(
                "[data-part='segmented-control'][data-item='artist-picture-mode']",
            )
            await expect(mode).toHaveAttribute('role', 'radiogroup')
            await expect(mode.locator("[role='radio'][data-item='upload']")).toHaveAttribute(
                'aria-checked',
                'true',
            )
            await expect(dialog.locator("[data-item='save-upload']")).toBeDisabled()

            await dialog.locator("input[type='file']").setInputFiles({
                name: 'artist.png',
                mimeType: 'image/png',
                buffer: await landscapePng(page),
            })
            const frame = dialog.locator("[data-part='avatar-crop-frame']")
            await expect(frame).toBeVisible()

            // Zoom in and move the picture with the keyboard, then upload the chosen square.
            const zoom = dialog.locator("[data-field='zoom'] [role='slider']")
            const zoomBefore = await zoom.getAttribute('aria-valuenow')
            await zoom.focus()
            await page.keyboard.press('PageUp')
            await expect(zoom).not.toHaveAttribute('aria-valuenow', zoomBefore ?? '')
            await frame.focus()
            await page.keyboard.press('ArrowLeft')
            await page.keyboard.press('ArrowUp')

            const uploaded = page.waitForResponse(
                (response) =>
                    response.url().endsWith(`/api/v1/artists/${artist.id}/uploaded-picture`) &&
                    response.request().method() === 'PUT',
            )
            await dialog.locator("[data-item='save-upload']").click()
            expect((await uploaded).status()).toBe(200)
            await expect(dialog.locator("[data-part='alert']")).toContainText(
                'The picture was uploaded.',
            )
            await expect(sources.nth(1)).toHaveAttribute('data-state', 'active')
            await expect(
                dialog.locator("[data-item='upload'] [data-part='cover'][data-kind='artist']"),
            ).toBeVisible()

            await dialog.locator("[data-part='dialog-footer'] [data-part='button']").first().click()
            await expect(dialog).toBeHidden()
            const media = detailMedia(page)
            await expect(media).toHaveAttribute('data-kind', 'artist')
            await expect(media.locator("[data-part='cover-image']")).toBeVisible()

            // The list shows the uploaded picture as well.
            await page.goto('/library/artists')
            await expect(
                artistTile(page, 'Loose Artist').locator(
                    "[data-part='cover'][data-kind='artist'] [data-part='cover-image']",
                ),
            ).toBeVisible()
            const after = await findArtist(request, 'Loose Artist')
            expect(after.picture?.kind).toBe('Uploaded')

            const again = await openPictureDialog(page, artist.id)
            await again.locator("[data-item='remove-uploaded']").click()
            await expect(again.locator("[data-part='alert']")).toContainText(
                'The uploaded picture was removed.',
            )
            await expect(again.locator("[data-part='picture-source']").nth(1)).toHaveAttribute(
                'data-state',
                'missing',
            )
        } finally {
            const left = await findArtist(request, 'Loose Artist')
            if (left.picture?.hasUploadedPicture) {
                await removeUploadedPicture(request, artist.id)
            }
        }
    })
})

test.describe('online lookup', () => {
    test('says that the online lookup is off and refuses it', async ({
        page,
        request,
    }) => {
        const headers = await adminHeaders(request)
        const settings = await request.get('/api/v1/settings/artist-pictures', { headers })
        expect(settings.status()).toBe(200)
        const current = await settings.json()
        // The lookup is off by default; this test never switches it on.
        expect(current.onlineLookupEnabled).toBe(false)
        expect(current).not.toHaveProperty('fanartTvApiKey')

        const artist = await findArtist(request, 'E2E Artist')
        const dialog = await openPictureDialog(page, artist.id)
        await dialog.locator("[role='radio'][data-item='online']").click()
        await expect(
            dialog.locator("[data-part='form-note'][data-item='online-privacy']"),
        ).toBeVisible()

        const refused = page.waitForResponse((response) =>
            response.url().includes(`/api/v1/artists/${artist.id}/musicbrainz-matches`),
        )
        await dialog.locator("[data-item='find-online']").click()
        const answer = await refused
        expect(answer.status()).toBe(409)
        expect((await answer.json()).code).toBe('artist.picture_lookup_disabled')

        await expect(dialog.locator("[data-part='alert']")).toContainText(
            'Online lookup is off. Switch it on under Administration, Library.',
        )
        await expect(
            dialog.locator("[data-part='link'][data-item='artist-picture-settings']"),
        ).toBeVisible()
        await expect(
            dialog.locator("[data-part='online-section'][data-item='matches']"),
        ).toHaveCount(0)
    })

    test('takes over a picture found online and shows its credit', async ({
        page,
        request,
        baseURL,
    }) => {
        const artist = await findArtist(request, 'E2E Artist')
        const base = `**/api/v1/artists/${artist.id}`
        const musicBrainzArtistId = '5b11f4ce-a62d-471e-81fc-a69a8278c7da'
        const png = await landscapePng(page)
        const requests: string[] = []
        page.on('request', (sent) => {
            if (!sent.url().startsWith(new URL(baseURL!).origin)) {
                requests.push(sent.url())
            }
        })

        // The answers of WaveMP, simulated in the browser: no request reaches the server, so the
        // server contacts no service and stores nothing.
        await page.route(`${base}/musicbrainz-matches?**`, (route) =>
            route.fulfill({
                json: {
                    items: [
                        {
                            musicBrainzArtistId,
                            name: 'E2E Artist',
                            sortName: 'Artist, E2E',
                            disambiguation: 'synthetic test band',
                            type: 'Group',
                            country: 'DE',
                            lifeSpanBegin: '1991',
                            score: 100,
                            isRemembered: false,
                        },
                    ],
                    offset: 0,
                    limit: 10,
                    total: 1,
                },
            }),
        )
        const credit = {
            author: '<b>Jane</b> Doe',
            license: 'CC BY-SA 4.0',
            licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0/',
            sourceUrl: 'https://commons.wikimedia.org/wiki/File:E2E_Artist.jpg',
        }
        await page.route(`${base}/online-picture-candidates?**`, (route) =>
            route.fulfill({
                json: {
                    items: [
                        {
                            id: 'a1b2c3d4e5f60718293a4b5c6d7e8f90',
                            provider: 'Wikimedia',
                            musicBrainzArtistId,
                            width: 1280,
                            height: 853,
                            ...credit,
                        },
                    ],
                    offset: 0,
                    limit: 50,
                    total: 1,
                },
            }),
        )
        await page.route(`${base}/online-picture-candidates/*/preview?**`, (route) =>
            route.fulfill({ body: png, contentType: 'image/png' }),
        )
        let takeOver: Record<string, unknown> | null = null
        await page.route(`${base}/online-picture`, async (route) => {
            if (route.request().method() !== 'PUT') {
                return route.fallback()
            }

            takeOver = route.request().postDataJSON()
            return route.fulfill({
                json: {
                    ...artist,
                    picture: {
                        kind: 'Online',
                        version: 'o-e2e',
                        albumIds: [],
                        hasLocalPicture: false,
                        hasUploadedPicture: false,
                        hasOnlinePicture: true,
                        onlineCredit: { provider: 'Wikimedia', ...credit },
                        musicBrainzArtistId,
                    },
                },
            })
        })
        await page.route(`${base}/online-picture?**`, (route) =>
            route.fulfill({ body: png, contentType: 'image/png' }),
        )
        await page.route(`${base}/picture?**`, (route) =>
            route.fulfill({ body: png, contentType: 'image/png' }),
        )

        const dialog = await openPictureDialog(page, artist.id)
        await dialog.locator("[role='radio'][data-item='online']").click()
        await dialog.locator("[data-item='find-online']").click()

        const match = dialog.locator("[data-part='artist-match']")
        await expect(match).toHaveCount(1)
        await expect(match).toHaveAttribute('aria-checked', 'true')
        await expect(match.locator("[data-part='artist-match-name']")).toHaveText('E2E Artist')

        const candidate = dialog.locator("[data-part='picture-candidate']")
        await expect(candidate).toHaveCount(1)
        await expect(candidate).toHaveAttribute('data-provider', 'Wikimedia')
        await expect(candidate.locator("[data-part='cover-image']")).toBeVisible()
        // Texts of the services are text, never markup.
        await expect(candidate.locator('b')).toHaveCount(0)

        const crop = dialog.locator("[data-part='online-section'][data-item='crop']")
        await expect(crop).toHaveAttribute('data-state', 'ready')
        await expect(crop.locator("[data-part='avatar-crop-frame']")).toBeVisible()

        await dialog.locator("[data-item='take-over']").click()
        await expect(dialog.locator("[data-part='alert']")).toContainText(
            'The online picture was taken over.',
        )
        expect(takeOver).toMatchObject({ candidateId: 'a1b2c3d4e5f60718293a4b5c6d7e8f90' })
        await dialog.locator("[data-part='dialog-footer'] [data-part='button']").first().click()
        await expect(dialog).toBeHidden()

        const media = detailMedia(page)
        await expect(media).toHaveAttribute('data-kind', 'artist')
        await expect(media.locator("[data-part='cover-image']")).toBeVisible()
        const caption = page.locator("[data-part='artist-credit']")
        await expect(caption).toHaveAttribute('data-provider', 'Wikimedia')
        await expect(caption).toHaveText('Photo: <b>Jane</b> Doe, CC BY-SA 4.0, Wikimedia Commons')
        await expect(caption.locator('b')).toHaveCount(0)
        const license = caption.locator("[data-part='artist-credit-license'] a")
        await expect(license).toHaveAttribute('href', credit.licenseUrl)
        await expect(license).toHaveAttribute('rel', 'noopener noreferrer')
        await expect(caption.locator("[data-part='artist-credit-source'] a")).toHaveAttribute(
            'href',
            credit.sourceUrl,
        )

        // The browser itself never contacts the services.
        expect(
            requests.filter((url) => !url.startsWith('data:') && !url.startsWith('blob:')),
        ).toEqual([])
        // The server stored nothing.
        expect((await findArtist(request, 'E2E Artist')).picture?.kind).toBe('Albums')
    })
})
