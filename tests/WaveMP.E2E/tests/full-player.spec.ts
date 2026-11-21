import { expect, test, type CDPSession, type Locator, type Page } from '@playwright/test'
import { createUser, deleteUser, findTrackId, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The player (WMP-108): titles and artists too long for their place scroll like a marquee, the
// full-screen player on phones closes when it is swiped down (following the finger, by distance
// and by speed), and tapping its cover opens the switch between cover, video and visualization,
// the same choice as on Now Playing. Every test uses a profile of its own, as the view belongs to
// the profile. With the synthetic test media of scripts/generate-media.sh: "Loose Song" has a
// video and lasts 20 seconds, "First Song" has no video. The tests that need the same song all along
// pause it, as the queue would move on.

// Without WebGL the visualization shows its hint instead of drawing in software (see
// stage-modes.spec.ts for the reason).
test.use({
    storageState: emptyStorageState,
    locale: 'en-US',
    launchOptions: { args: ['--disable-webgl'] },
})

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

function marquee(page: Page, place: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${place}'] > [data-part='marquee']`)
}

function viewSegment(page: Page, item: string): Locator {
    return page.locator(
        `[data-part='app'] [data-part='player-view-switch'] [data-part='segment'][data-item='${item}']`,
    )
}

async function playSong(page: Page, id: string, title: string): Promise<void> {
    await page.goto(`/library/tracks/${id}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await expect(marquee(page, 'now-playing-title')).toHaveText(title)
}

/** Plays a song and pauses it at once, so it stays the current song. */
async function playPaused(page: Page, id: string, title: string): Promise<void> {
    await playSong(page, id, title)
    const transport = part(page, 'transport')
    await expect(transport).toHaveAttribute('data-state', /^playing\b/)
    await part(page, 'btn-play').filter({ visible: true }).click()
    await expect(transport).toHaveAttribute('data-state', /^paused\b/)
}

async function expandPlayer(page: Page): Promise<void> {
    const transport = part(page, 'transport')
    await part(page, 'now-playing-mini').click()
    await expect(transport).toHaveAttribute('data-state', /\bexpanded\b/)
    // The full-screen player is in place before a finger touches it.
    await expect.poll(async () => (await transport.boundingBox())?.y).toBe(0)
}

/** The horizontal offset of the text of a marquee, in pixels (negative while it scrolled). */
function marqueeOffset(text: Locator): Promise<number> {
    return text.evaluate((element) => {
        const box = element.getBoundingClientRect()
        const place = element.parentElement!.getBoundingClientRect()
        return box.left - place.left
    })
}

/** Makes the place of the title narrow, so its text no longer fits. */
async function narrowTitle(page: Page): Promise<void> {
    await page.addStyleTag({
        content: `[data-part='now-playing-title'] {
            inline-size: 2.5rem !important;
            max-inline-size: 2.5rem !important;
            justify-self: center;
        }`,
    })
}

let user: TestUser

test.beforeEach(async ({ page, request }) => {
    user = await createUser(request, `Player ${uniqueSuffix()}`)
    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
})

test.afterEach(async ({ request }) => {
    await deleteUser(request, user.id)
})

test.describe('long titles', () => {
    test('rest, scroll slowly to their end, pause there and start again', async ({
        page,
        request,
        isMobile,
    }) => {
        test.setTimeout(60_000)
        await playPaused(page, await findTrackId(request, 'Loose Song'), 'Loose Song')
        if (isMobile) {
            await expandPlayer(page)
        }

        // A title that fits stays still.
        const title = marquee(page, 'now-playing-title')
        await expect(title).toHaveAttribute('data-state', 'fit')

        await narrowTitle(page)
        await expect(title).toHaveAttribute('data-state', 'overflow waiting')
        const waitingSince = Date.now()
        expect(await marqueeOffset(title)).toBeCloseTo(0, 0)
        // Cut off, its end fades out.
        const mask = await title.evaluate(
            (element) => getComputedStyle(element.parentElement!).maskImage,
        )
        expect(mask).toContain('linear-gradient')

        // About three seconds later it scrolls, slowly and to the left.
        await expect(title).toHaveAttribute('data-state', 'overflow scrolling', {
            timeout: 6000,
        })
        expect(Date.now() - waitingSince).toBeGreaterThan(2500)
        const distance = await title.evaluate((element) =>
            Number.parseFloat(getComputedStyle(element).getPropertyValue('--wmp-marquee-distance')),
        )
        expect(distance).toBeGreaterThan(10)
        await expect.poll(() => marqueeOffset(title)).toBeLessThan(-2)

        // It rests at its end, the whole distance moved, and starts again from the beginning.
        await expect(title).toHaveAttribute('data-state', 'overflow end', {
            timeout: 8000,
        })
        expect(await marqueeOffset(title)).toBeCloseTo(-distance, 0)
        await expect(title).toHaveAttribute('data-state', 'overflow waiting', {
            timeout: 5000,
        })
        await expect.poll(() => marqueeOffset(title)).toBeCloseTo(0, 0)
    })

    test('end with an ellipsis and stay still with reduced motion', async ({
        page,
        request,
        isMobile,
    }) => {
        await page.emulateMedia({ reducedMotion: 'reduce' })
        await playPaused(page, await findTrackId(request, 'Loose Song'), 'Loose Song')
        if (isMobile) {
            await expandPlayer(page)
        }

        await narrowTitle(page)
        const title = marquee(page, 'now-playing-title')
        await expect(title).toHaveAttribute('data-state', 'overflow')
        expect(await title.evaluate((element) => getComputedStyle(element).textOverflow)).toBe(
            'ellipsis',
        )

        await page.waitForTimeout(4000)
        await expect(title).toHaveAttribute('data-state', 'overflow')
        expect(await marqueeOffset(title)).toBeCloseTo(0, 0)
    })
})

test.describe('full-screen player', () => {
    test.skip(({ isMobile }) => !isMobile, 'the full-screen player exists on phones only')

    /** Touch input of the phone through the DevTools protocol (Chromium). */
    async function touch(page: Page): Promise<CDPSession> {
        return page.context().newCDPSession(page)
    }

    /** The point in the middle of the cover of the full-screen player. */
    async function coverPoint(page: Page): Promise<{ x: number; y: number }> {
        const box = (await part(page, 'player-media').boundingBox())!
        return {
            x: Math.round(box.x + box.width / 2),
            y: Math.round(box.y + box.height / 2),
        }
    }

    async function transportTop(page: Page): Promise<number> {
        return (await part(page, 'transport').boundingBox())!.y
    }

    test('follows a slow drag and snaps back after a short one', async ({ page, request }) => {
        await playPaused(page, await findTrackId(request, 'Loose Song'), 'Loose Song')
        await expandPlayer(page)
        const transport = part(page, 'transport')
        const cdp = await touch(page)
        const start = await coverPoint(page)

        // A short, slow drag: the player follows the finger, then snaps back.
        await cdp.send('Input.dispatchTouchEvent', {
            type: 'touchStart',
            touchPoints: [start],
        })
        for (let step = 1; step <= 10; step++) {
            await cdp.send('Input.dispatchTouchEvent', {
                type: 'touchMove',
                touchPoints: [{ x: start.x, y: start.y + step * 10 }],
            })
            await page.waitForTimeout(40)
        }
        await expect(transport).toHaveAttribute('data-state', /\bdragging\b/)
        // It follows the finger (90 pixels past the start of the drag).
        expect(await transportTop(page)).toBeGreaterThan(70)
        expect(await transportTop(page)).toBeLessThan(110)
        await page.waitForTimeout(200)
        await cdp.send('Input.dispatchTouchEvent', {
            type: 'touchEnd',
            touchPoints: [],
        })

        await expect(transport).not.toHaveAttribute('data-state', /\bdragging\b/)
        await expect(transport).toHaveAttribute('data-state', /\bexpanded\b/)
        await expect.poll(() => transportTop(page)).toBe(0)
        // The drag was no tap: the view switch stayed closed.
        await expect(part(page, 'player-view-switch')).toBeHidden()

        // A long, slow drag closes it, like the arrow at the top left.
        await cdp.send('Input.dispatchTouchEvent', {
            type: 'touchStart',
            touchPoints: [start],
        })
        const viewport = page.viewportSize()!
        for (let step = 1; step <= 20; step++) {
            await cdp.send('Input.dispatchTouchEvent', {
                type: 'touchMove',
                touchPoints: [{ x: start.x, y: start.y + step * (viewport.height / 40) }],
            })
            await page.waitForTimeout(40)
        }
        await page.waitForTimeout(200)
        await cdp.send('Input.dispatchTouchEvent', {
            type: 'touchEnd',
            touchPoints: [],
        })

        await expect(transport).not.toHaveAttribute('data-state', /\bexpanded\b/)
        await expect(part(page, 'btn-collapse')).toBeHidden()
        await expect(part(page, 'now-playing-mini')).toBeFocused()
    })

    test('closes on a short fast flick', async ({ page, request }) => {
        await playPaused(page, await findTrackId(request, 'Loose Song'), 'Loose Song')
        await expandPlayer(page)
        const transport = part(page, 'transport')
        const cdp = await touch(page)
        const start = await coverPoint(page)

        // 160 pixels, less than a quarter of the screen, in a few milliseconds.
        await cdp.send('Input.dispatchTouchEvent', {
            type: 'touchStart',
            touchPoints: [start],
        })
        for (const offset of [40, 100, 160]) {
            await cdp.send('Input.dispatchTouchEvent', {
                type: 'touchMove',
                touchPoints: [{ x: start.x, y: start.y + offset }],
            })
        }
        await cdp.send('Input.dispatchTouchEvent', {
            type: 'touchEnd',
            touchPoints: [],
        })

        await expect(transport).toHaveAttribute('data-state', /\bdismissing\b/)
        // It leaves at the speed of the flick: quickly, in at most 300 milliseconds.
        const duration = await transport.evaluate((element) =>
            Number.parseFloat(getComputedStyle(element).getPropertyValue('--wmp-swipe-duration')),
        )
        expect(duration).toBeLessThanOrEqual(300)
        await expect(transport).not.toHaveAttribute('data-state', /\bexpanded\b/, {
            timeout: 2000,
        })
    })

    test('chooses cover, video and visualization with the view switch', async ({
        page,
        request,
    }) => {
        await playSong(page, await findTrackId(request, 'Loose Song'), 'Loose Song')
        await expandPlayer(page)
        const media = part(page, 'player-media')
        const viewSwitch = part(page, 'player-view-switch')
        const mini = part(page, 'now-playing-mini')

        // A new profile starts with the video, which the player shows on the cover once ready.
        await expect(viewSwitch).toBeHidden()
        await expect(media).toHaveAttribute('data-state', 'video', {
            timeout: 15_000,
        })
        await expect(part(page, 'player-video')).toBeVisible()

        // A tap on the cover opens the switch, like the buttons of Now Playing.
        await mini.tap()
        await expect(viewSwitch).toBeVisible()
        await expect(mini).toHaveAttribute('aria-expanded', 'true')
        await expect(
            viewSwitch.locator("[data-part='segmented-control'][role='radiogroup']"),
        ).toHaveAttribute('data-item', 'player-view')
        await expect(viewSegment(page, 'video')).toHaveAttribute('aria-checked', 'true')

        await viewSegment(page, 'cover').tap()
        await expect(viewSegment(page, 'cover')).toHaveAttribute('aria-checked', 'true')
        await expect(media).toHaveAttribute('data-state', 'cover')
        await expect(part(page, 'player-video')).toHaveCount(0)

        await viewSegment(page, 'visualization').tap()
        await expect(media).toHaveAttribute('data-state', 'visualization')
        await expect(part(page, 'player-visualization')).toBeVisible()
        // Without WebGL the visualization cannot draw; it says so in its state.
        await expect(part(page, 'player-visualization')).toHaveAttribute(
            'data-state',
            'unsupported',
        )

        await viewSegment(page, 'video').tap()
        await expect(media).toHaveAttribute('data-state', 'video', {
            timeout: 15_000,
        })
        await expect(part(page, 'player-visualization')).toHaveCount(0)

        // The choice is the one of Now Playing: closing the player, the stage shows the video.
        await mini.tap()
        await expect(viewSwitch).toBeHidden()
        await part(page, 'btn-collapse').tap()
        await page
            .locator("[data-part='app'] a[href='/now-playing']")
            .filter({ visible: true })
            .click()
        await expect(page).toHaveURL(/\/now-playing$/)
        await expect(
            page.locator(
                "[data-part='app'] [data-part='stage-actions'] [data-part='segment'][data-item='video']",
            ),
        ).toHaveAttribute('aria-checked', 'true')
    })

    test('offers the video only for songs with one', async ({ page, request }) => {
        await playSong(page, await findTrackId(request, 'First Song'), 'First Song')
        await expandPlayer(page)
        await part(page, 'now-playing-mini').tap()

        await expect(viewSegment(page, 'video')).toBeDisabled()
        await expect(viewSegment(page, 'cover')).toHaveAttribute('aria-checked', 'true')
        await expect(part(page, 'player-media')).toHaveAttribute('data-state', 'cover')
    })
})
