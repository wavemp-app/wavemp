import {
    expect,
    test,
    type Browser,
    type BrowserContext,
    type Locator,
    type Page,
} from '@playwright/test'
import { createUser, deleteUser, findTrackId, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// Device sync and remote control (WMP-47) with two browsers of one profile: two browser contexts
// with empty storage, so each one is a device of its own. A plays, B is its remote once A allows
// remote control. The profile is an own one, as the devices and the playback belong to the
// profile. With the synthetic test media of scripts/generate-media.sh: "Loose Song" lasts 20
// seconds, "First Song" two. The devices are found by their state tokens: "current" is the device
// of the page itself. The spec opens its own contexts and checks the desktop transport bar, so it
// runs in the project "chromium" only.
test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'Two desktop browsers of their own')
})

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

/** The device picker of a page (a popover, so it lives in the top layer of the app). */
function picker(page: Page): Locator {
    return page.locator("[data-part='device-picker']")
}

/** The other device in the device picker of a page. */
function otherDevice(page: Page): Locator {
    return picker(page).locator("[data-part='device-item']:not([data-state~='current'])")
}

/** This device in the device picker of a page. */
function ownDevice(page: Page): Locator {
    return picker(page).locator("[data-part='device-item'][data-state~='current']")
}

async function openPicker(page: Page): Promise<void> {
    if (!(await picker(page).isVisible())) {
        await part(page, 'btn-devices').click()
    }
    await expect(picker(page)).toHaveAttribute('data-state', 'open')
}

async function closePicker(page: Page): Promise<void> {
    if (await picker(page).isVisible()) {
        await page.keyboard.press('Escape')
    }
    await expect(picker(page)).toBeHidden()
}

async function enterAs(context: BrowserContext, user: TestUser): Promise<Page> {
    const page = await context.newPage()
    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
    // Registered with the hub: the devices button knows the connection.
    await expect(part(page, 'devices')).toHaveAttribute('data-state', /\bconnected\b/)
    return page
}

async function newDevice(browser: Browser, user: TestUser): Promise<Page> {
    const context = await browser.newContext({ storageState: emptyStorageState, locale: 'en-US' })
    return enterAs(context, user)
}

function transport(page: Page): Locator {
    return part(page, 'transport')
}

/** Position of the player in seconds, as its seek slider tells it. */
async function position(page: Page): Promise<number> {
    return Number(await part(page, 'seek').getByRole('slider').getAttribute('aria-valuenow'))
}

async function volume(page: Page): Promise<number> {
    return Number(await part(page, 'volume').getByRole('slider').getAttribute('aria-valuenow'))
}

test.describe('remote control', () => {
    // No profile in the projects the spec skips: the hooks run there as well.
    let user: TestUser | undefined
    const contexts: BrowserContext[] = []

    test.beforeEach(async ({ request }) => {
        user = await createUser(request, `Remote ${uniqueSuffix()}`)
    })

    test.afterEach(async ({ request }) => {
        await Promise.all(contexts.splice(0).map((context) => context.close()))
        if (user) {
            await deleteUser(request, user.id)
            user = undefined
        }
    })

    test('controls a device that allows it and hands playback over and back', async ({
        browser,
        request,
    }) => {
        test.slow()
        if (!user) {
            throw new Error('The profile of the test was not created.')
        }
        const looseSong = await findTrackId(request, 'Loose Song')

        const a = await newDevice(browser, user)
        contexts.push(a.context())
        const b = await newDevice(browser, user)
        contexts.push(b.context())

        // A plays Loose Song and pauses it, so it stays the current song.
        await a.goto(`/library/tracks/${looseSong}`)
        await part(a, 'detail-actions').locator("[data-item='play']").click()
        await expect(transport(a)).toHaveAttribute('data-state', /^playing\b/)
        await part(a, 'btn-play').click()
        await expect(transport(a)).toHaveAttribute('data-state', /^paused\b/)

        // B gives A a name in the settings; both see each other.
        await b.goto('/settings')
        const known = b.locator(
            "[data-part='app'] [data-part='known-device']:not([data-state~='current'])",
        )
        await expect(known).toHaveCount(1)
        await expect(known).toHaveAttribute('data-state', /\bonline\b/)
        const name = known.locator("[data-field='device-name'] input")
        await name.fill('Desk')
        await name.press('Enter')
        await expect(known).toHaveAttribute('data-state', /\brenamed\b/)

        await openPicker(a)
        await expect(picker(a).locator("[data-part='device-item']")).toHaveCount(2)
        await expect(ownDevice(a).locator("[data-part='label']")).toHaveText('Desk')
        await expect(ownDevice(a)).toHaveAttribute('aria-checked', 'true')
        await expect(part(a, 'btn-devices')).toHaveAttribute('data-state', 'local')

        // Without the permission of A, B sees it greyed out and stays on its own.
        await openPicker(b)
        await expect(picker(b).locator("[data-part='device-item']")).toHaveCount(2)
        await expect(otherDevice(b).locator("[data-part='label']")).toHaveText('Desk')
        await expect(otherDevice(b)).toHaveAttribute('aria-disabled', 'true')
        await expect(otherDevice(b)).toHaveAttribute('data-state', /\bblocked\b/)
        await expect(otherDevice(b).locator("[data-part='device-meta']")).toHaveText('Paused')
        await expect(part(b, 'btn-devices')).toHaveAttribute('data-state', 'local')
        await closePicker(b)

        // A allows remote control: B becomes its remote and shows what A plays.
        const toggle = picker(a).locator("[data-part='remote-control-toggle']")
        await expect(toggle).toHaveAttribute('aria-checked', 'false')
        await toggle.click()
        await expect(toggle).toHaveAttribute('aria-checked', 'true')
        await closePicker(a)

        await expect(part(b, 'btn-devices')).toHaveAttribute('data-state', 'remote')
        await expect(transport(b)).toHaveAttribute('data-state', /\bremote\b/)
        await expect(part(b, 'remote-status')).toHaveText('Playing on Desk')
        await expect(part(b, 'now-playing-title')).toHaveText('Loose Song')
        await openPicker(b)
        await expect(otherDevice(b)).not.toHaveAttribute('aria-disabled', 'true')
        await expect(otherDevice(b)).toHaveAttribute('data-state', /\ballowed\b/)
        await expect(otherDevice(b)).toHaveAttribute('aria-checked', 'true')
        await closePicker(b)

        // Play and pause.
        await part(b, 'btn-play').click()
        await expect(transport(a)).toHaveAttribute('data-state', /^playing\b/)
        await expect(transport(b)).toHaveAttribute('data-state', /^playing\b/)
        await part(b, 'btn-play').click()
        await expect(transport(a)).toHaveAttribute('data-state', /^paused\b/)
        await expect(transport(b)).toHaveAttribute('data-state', /^paused\b/)

        // Seek: to the start, then ten seconds on.
        const seek = part(b, 'seek').getByRole('slider')
        await seek.focus()
        await b.keyboard.press('Home')
        await expect.poll(() => position(a)).toBe(0)
        await expect.poll(() => position(b)).toBe(0)
        await b.keyboard.press('PageUp')
        await expect.poll(() => position(a)).toBe(10)
        await expect.poll(() => position(b)).toBe(10)

        // Volume.
        const start = await volume(a)
        await part(b, 'volume').getByRole('slider').focus()
        await b.keyboard.press('ArrowLeft')
        await expect.poll(() => volume(a)).toBeCloseTo(Math.max(0, start - 0.05), 2)
        await expect.poll(() => volume(b)).toBeCloseTo(Math.max(0, start - 0.05), 2)

        // The queue: B adds First Song from its library, then skips to it.
        await b.goto('/library/songs')
        const firstSong = part(b, 'list-row').filter({ hasText: 'First Song' }).first()
        await firstSong.click({ button: 'right' })
        await b.locator("[data-part='context-menu'] [data-item='add-to-queue']").click()
        await expect(part(b, 'btn-devices')).toHaveAttribute('data-state', 'remote')
        await part(b, 'btn-next').click()
        await expect(part(a, 'now-playing-title')).toHaveText('First Song')
        await expect(part(b, 'now-playing-title')).toHaveText('First Song')

        // An entry of the queue of A, chosen on B, plays on A.
        await b.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
        const entries = b.locator("[data-part='app'] [data-part='queue'] [data-part='queue-item']")
        await expect(entries).toHaveCount(2)
        await entries
            .filter({ hasText: 'Loose Song' })
            .locator("[data-part='queue-item-info']")
            .click()
        await expect(part(a, 'now-playing-title')).toHaveText('Loose Song')
        await expect(transport(a)).toHaveAttribute('data-state', /^playing\b/)
        await part(b, 'btn-play').click()
        await expect(transport(a)).toHaveAttribute('data-state', /^paused\b/)

        // B takes playback over at the position of A; A pauses.
        const paused = await position(a)
        await openPicker(b)
        await ownDevice(b).click()
        await expect(part(b, 'btn-devices')).toHaveAttribute('data-state', 'local')
        await expect(ownDevice(b)).toHaveAttribute('aria-checked', 'true')
        await closePicker(b)
        await expect(part(b, 'now-playing-title')).toHaveText('Loose Song')
        await expect(transport(b)).toHaveAttribute('data-state', /^paused\b/)
        await expect.poll(() => position(b)).toBeGreaterThanOrEqual(paused - 1)
        expect(await position(b)).toBeLessThanOrEqual(paused + 1)
        await expect(transport(a)).toHaveAttribute('data-state', /^paused\b/)

        // B plays and hands playback back to A, which goes on near the same position.
        await part(b, 'btn-play').click()
        await expect(transport(b)).toHaveAttribute('data-state', /^playing\b/)
        await expect.poll(() => position(b)).toBeGreaterThanOrEqual(paused + 1)
        const before = await position(b)
        await openPicker(b)
        await otherDevice(b).click()
        await expect(transport(a)).toHaveAttribute('data-state', /^playing\b/)
        await expect(part(a, 'now-playing-title')).toHaveText('Loose Song')
        const after = await position(a)
        expect(after).toBeGreaterThanOrEqual(before - 1)
        expect(after).toBeLessThanOrEqual(before + 4)
        await expect(part(b, 'btn-devices')).toHaveAttribute('data-state', 'remote')
        await expect(otherDevice(b)).toHaveAttribute('aria-checked', 'true')
        await closePicker(b)
        await part(b, 'btn-play').click()
        await expect(transport(a)).toHaveAttribute('data-state', /^paused\b/)

        // A reload ends the permission: A is greyed out on B again, and B plays on its own.
        await a.reload()
        await expect(part(a, 'devices')).toHaveAttribute('data-state', /\bconnected\b/)
        await openPicker(a)
        await expect(
            picker(a).locator("[data-part='remote-control-toggle']"),
        ).toHaveAttribute('aria-checked', 'false')
        await closePicker(a)
        await openPicker(b)
        await expect(otherDevice(b)).toHaveAttribute('aria-disabled', 'true')
        await expect(otherDevice(b)).toHaveAttribute('data-state', /\bblocked\b/)
        await expect(part(b, 'btn-devices')).toHaveAttribute('data-state', 'local')
    })
})
