import { expect, test, type Locator, type Page } from '@playwright/test'
import { send, sessionHeaders } from '../screenshots/helpers'
import { shippedThemeTargets, targetKey } from '../screenshots/themes'
import { createUser, deleteUser, findTrackId, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// Disabled buttons and menu entries look disabled in every shipped theme and variant, their icons
// included (THEMING.md: disabled entries stay in the menu, greyed out). While a change of a
// playlist is saved, the buttons of its header that change it and the entries of the menu of a
// row that change it are disabled; in the queue, the last entry cannot move down. The visible
// label and the icon of each take the disabled text color of the theme, while the enabled buttons
// and entries next to them keep their own look.

test.use({ storageState: emptyStorageState })

/** Key of the id of the device in the local storage of the web app (src/player/device.ts). */
const deviceIdKey = 'wavemp.device-id'

const device = { id: 'disabled-looks', name: 'Chrome · Windows' }

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

interface Look {
    /** --wmp-color-text-disabled of the theme, resolved at the element. */
    disabled: string
    /** Color of the label, or null if the label is visually hidden (a button with an icon only). */
    label: string | null
    /** Fill of the icon: the color and the image (a gradient) of its glyph. */
    icon: string
    iconImage: string
}

async function lookOf(element: Locator): Promise<Look> {
    return element.evaluate((target) => {
        const probe = document.createElement('span')
        probe.style.color = 'var(--wmp-color-text-disabled)'
        target.append(probe)
        const disabled = getComputedStyle(probe).color
        probe.remove()
        const label = target.querySelector(":scope > [data-part='label']")
        const glyph = target.querySelector(
            ":scope > [data-part='icon'] > [data-part='icon-glyph']",
        )!
        return {
            disabled,
            label:
                label && label.getBoundingClientRect().width > 1
                    ? getComputedStyle(label).color
                    : null,
            icon: getComputedStyle(glyph).backgroundColor,
            iconImage: getComputedStyle(glyph).backgroundImage,
        }
    })
}

/** The label and the icon of a look: "disabled" where they take the disabled color, else theirs. */
function shownLook(look: Look): { label: string; icon: string } {
    const icon = look.iconImage === 'none' ? look.icon : look.iconImage
    return {
        label: look.label === null || look.label === look.disabled ? 'disabled' : look.label,
        icon: icon === look.disabled ? 'disabled' : icon,
    }
}

async function expectDisabledLook(element: Locator, name: string): Promise<void> {
    await expect(element).toBeDisabled()
    // The colors follow the state with the transitions of the theme.
    await expect
        .soft(async () => {
            expect(shownLook(await lookOf(element)), `look of ${name}`).toEqual({
                label: 'disabled',
                icon: 'disabled',
            })
        })
        .toPass({ timeout: 2_000 })
}

async function expectEnabledLook(element: Locator, name: string): Promise<void> {
    await expect(element).toBeEnabled()
    const look = await lookOf(element)
    if (look.label !== null) {
        expect.soft(look.label, `label of ${name}`).not.toBe(look.disabled)
    }
    if (look.iconImage === 'none') {
        expect.soft(look.icon, `icon of ${name}`).not.toBe(look.disabled)
    }
}

/** Holds back the GET requests of the playlist itself, the reload after a change, until release. */
async function holdPlaylist(page: Page, id: string): Promise<() => void> {
    let release!: () => void
    const released = new Promise<void>((resolve) => (release = resolve))
    await page.route(
        (url) => url.pathname === `/api/v1/me/playlists/${id}`,
        async (route) => {
            if (route.request().method() !== 'GET') {
                await route.fallback()
                return
            }

            await released
            await route.continue()
        },
    )
    return release
}

for (const target of shippedThemeTargets()) {
    test(`disabled buttons and menu entries look disabled in ${targetKey(target)}`, async ({
        page,
        request,
        isMobile,
    }) => {
        let user: TestUser | undefined
        let release: (() => void) | undefined
        try {
            user = await createUser(request, `Disabled ${targetKey(target)} ${uniqueSuffix()}`)
            const headers = await sessionHeaders(request, user.id)
            await send(request, headers, 'patch', '/api/v1/me/settings', {
                language: 'en',
                theme: {
                    id: target.theme,
                    appearance: target.variant === 'dark' ? 'Dark' : 'Light',
                },
            })
            const first = await findTrackId(request, 'First Song')
            const second = await findTrackId(request, 'Second Song')
            const trackIds = [first, second, first]
            const created = await request.post('/api/v1/me/playlists', {
                data: { name: 'Disabled Looks', trackIds },
                headers,
            })
            expect(created.status()).toBe(201)
            const id = (await created.json()).id as string
            // The queue of the playlist waits paused at its first entry on the device of the page.
            await send(request, headers, 'put', '/api/v1/me/playback-state', {
                items: trackIds.map((trackId) => ({ trackId })),
                currentIndex: 0,
                positionMilliseconds: 0,
                shuffle: false,
                originalOrder: [],
                repeat: 'Off',
                source: { kind: 'Playlist', id },
                device,
            })
            await page.addInitScript(([key, value]) => localStorage.setItem(key, value), [
                deviceIdKey,
                device.id,
            ] as const)

            await page.goto('/profiles')
            await profileTile(page, user.name).click()
            await expect(page).toHaveURL(homeUrl)
            await expect(page.locator(':root')).toHaveAttribute('data-theme', target.theme)
            await expect(page.locator(':root')).toHaveAttribute(
                'data-theme-variant',
                target.variant,
            )

            await page.goto(`/playlists/${id}`)
            const rows = part(page, 'list-row')
            await expect(rows).toHaveCount(3)
            const view = page.locator("[data-part='view'][data-view='playlist']")
            await expect(view).not.toHaveAttribute('aria-busy', 'true')

            const menu = page.locator(
                "[data-part='context-menu'][data-state='open']:not([data-kind])",
            )
            const openMenu = async (row: Locator) => {
                if (isMobile) {
                    await row.locator("[data-part='btn-item-menu']").tap()
                } else {
                    await row.click({ button: 'right' })
                }
                await expect(menu).toBeVisible()
            }

            // A move whose reload is held back keeps the page busy.
            release = await holdPlaylist(page, id)
            await openMenu(rows.nth(2))
            await menu.locator("[data-item='move-up']").click()
            await expect(view).toHaveAttribute('aria-busy', 'true')
            await expect(menu).toHaveCount(0)

            const actions = part(page, 'detail-actions')
            for (const item of ['rename', 'cover', 'delete']) {
                await expectDisabledLook(actions.locator(`[data-item='${item}']`), item)
            }
            await expectEnabledLook(actions.locator("[data-item='shuffle']"), 'shuffle')

            await openMenu(rows.nth(0))
            for (const item of ['move-up', 'move-down', 'remove-from-playlist']) {
                await expectDisabledLook(menu.locator(`[data-item='${item}']`), `entry ${item}`)
            }
            // The entry with the focus, without the pointer on it.
            await page.mouse.move(0, 0)
            await expectEnabledLook(menu.locator("[data-item='play-next']"), 'entry play-next')
            await page.keyboard.press('Escape')
            await expect(menu).toHaveCount(0)
            release()
            await expect(view).not.toHaveAttribute('aria-busy', 'true')

            await page.goto('/now-playing')
            const entries = part(page, 'queue-item').filter({ visible: true })
            await expect(entries).toHaveCount(3)
            await expectDisabledLook(
                entries.last().locator("[data-item='move-down']"),
                'move-down of the last queue entry',
            )
            await expectEnabledLook(
                entries.last().locator("[data-item='move-up']"),
                'move-up of the last queue entry',
            )
        } finally {
            release?.()
            if (user) {
                await deleteUser(request, user.id)
            }
        }
    })
}
