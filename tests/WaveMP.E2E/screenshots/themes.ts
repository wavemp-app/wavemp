import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import {
    devices,
    expect,
    type APIRequestContext,
    type Browser,
    type BrowserContextOptions,
    type Locator,
    type Page,
} from '@playwright/test'
import {
    adminToken,
    deleteUser,
    findAlbumId,
    findTrackId,
    findUserByName,
} from '../tests/helpers/api'
import { homeUrl, profileTile } from '../tests/helpers/navigation'
import { part, prepareProfile, send, sessionHeaders } from './helpers'

// The views every theme is checked and photographed with (THEMING.md, "Checking a theme"): the
// screenshots for theme designers (theme.screenshots.ts), the preview pictures of the theme
// picker and the screenshot and contrast regression of the shipped themes (tests/themes). Each
// theme and variant gets a profile of its own (an admin, so it also sees the administration),
// filled like the profile of the README screenshots, so several runs can share one WaveMP
// instance at the same time.
//
// The player of every view starts from the same saved playback state: E2E Album paused at the
// start of First Song, saved by the desktop device of the theme runs. The pages never save their
// own state and nothing plays, so no view depends on how far an earlier view played or on when its
// save reached the server.

export type ThemeVariant = 'light' | 'dark'

export interface ThemeTarget {
    readonly theme: string
    /** Name of the theme, e.g. "Clear Glass". */
    readonly name: string
    readonly variant: ThemeVariant
}

const registryFile = fileURLToPath(
    new URL('../../../source/WaveMP.Web/src/themes/themes.ts', import.meta.url),
)

/** The shipped themes and their variants, read from the registry of the web app. */
export function shippedThemeTargets(): ThemeTarget[] {
    const registry = readFileSync(registryFile, 'utf8')
    const targets: ThemeTarget[] = []
    for (const match of registry.matchAll(/builtIn\('([a-z0-9-]+)', '([^']+)', \[([^\]]+)\]\)/g)) {
        for (const variant of match[3]!.matchAll(/'(light|dark)'/g)) {
            targets.push({
                theme: match[1]!,
                name: match[2]!,
                variant: variant[1] as ThemeVariant,
            })
        }
    }
    return targets
}

/**
 * The targets named in "theme[:variant]" items, e.g. ["accent:dark", "meadow"]; all shipped ones
 * for an empty list. A theme without a variant stands for all its variants.
 */
export function selectThemeTargets(items: readonly string[]): ThemeTarget[] {
    const shipped = shippedThemeTargets()
    if (items.length === 0) {
        return shipped
    }
    return items.flatMap((item) => {
        const [theme, variant] = item.split(':')
        const matches = shipped.filter(
            (target) => target.theme === theme && (!variant || target.variant === variant),
        )
        if (matches.length === 0) {
            const known = shipped.map((target) => `${target.theme}:${target.variant}`).join(', ')
            throw new Error(`Unknown theme or variant "${item}"; known: ${known}.`)
        }
        return matches
    })
}

export function targetKey(target: ThemeTarget): string {
    return `${target.theme}-${target.variant}`
}

/** Name of the profile of a target, e.g. "Clear Glass Dark". */
export function themeProfileName(target: ThemeTarget): string {
    return `${target.name} ${target.variant === 'dark' ? 'Dark' : 'Light'}`
}

/** Color of the theme profiles, fixed so the screenshots stay the same. */
const profileColor = '#2f6fd0'

/** Key of the id of the device in the local storage of the web app (src/player/device.ts). */
const deviceIdKey = 'wavemp.device-id'

/** The device that saved the playback state of the theme profiles. */
const savingDevice = { id: 'theme-regression-desktop', name: 'Chrome · Windows' }

/** Another device of the profile, which shows the notice "Continue listening from". */
const otherDeviceId = 'theme-regression-other'

/** Saves the playback state every view starts from: E2E Album paused at the start of First Song. */
async function savePlaybackState(
    request: APIRequestContext,
    headers: Record<string, string>,
): Promise<void> {
    const tracks = await Promise.all(
        ['First Song', 'Second Song'].map((title) => findTrackId(request, title)),
    )
    await send(request, headers, 'put', '/api/v1/me/playback-state', {
        items: tracks.map((trackId) => ({ trackId })),
        currentIndex: 0,
        positionMilliseconds: 0,
        shuffle: false,
        originalOrder: [],
        repeat: 'Off',
        source: { kind: 'Album', id: await findAlbumId(request, 'E2E Album') },
        device: savingDevice,
    })
}

export interface ThemeProfile {
    readonly id: string
    readonly name: string
    readonly headers: Record<string, string>
}

/**
 * Creates the profile of a target (again, if an interrupted run left it behind), fills it and
 * sets its theme and appearance. The appearance matters on the WaveMP instance itself; a theme
 * preview shows its theme anyway and takes the variant from the color scheme of the browser.
 */
export async function createThemeProfile(
    request: APIRequestContext,
    target: ThemeTarget,
): Promise<ThemeProfile> {
    const name = themeProfileName(target)
    const left = await findUserByName(request, name)
    if (left) {
        await deleteUser(request, left.id)
    }
    const created = await request.post('/api/v1/users', {
        data: { name, color: profileColor, role: 'Admin' },
        headers: { Authorization: `Bearer ${await adminToken(request)}` },
    })
    expect(created.status(), await created.text()).toBe(201)
    const id = (await created.json()).id as string
    const headers = await sessionHeaders(request, id)
    await prepareProfile(request, headers)
    await send(request, headers, 'patch', '/api/v1/me/settings', {
        language: 'en',
        theme: {
            id: target.theme,
            appearance: target.variant === 'dark' ? 'Dark' : 'Light',
        },
    })
    await savePlaybackState(request, headers)
    return { id, name, headers }
}

export async function deleteThemeProfile(
    request: APIRequestContext,
    profile: ThemeProfile | undefined,
): Promise<void> {
    if (profile) {
        await deleteUser(request, profile.id)
    }
}

export type ThemeDevice = 'desktop' | 'phone'

/** Viewport and device of the screenshots: a desktop window and a phone. */
export function themeContextOptions(
    device: ThemeDevice,
    variant: ThemeVariant,
): BrowserContextOptions {
    const shared = {
        locale: 'en-US',
        timezoneId: 'UTC',
        colorScheme: variant,
        reducedMotion: 'reduce' as const,
        storageState: { cookies: [], origins: [] },
    }
    if (device === 'desktop') {
        return {
            ...shared,
            viewport: { width: 1280, height: 800 },
            deviceScaleFactor: 1,
        }
    }
    const phone = devices['Pixel 7']
    return {
        ...shared,
        viewport: phone.viewport,
        userAgent: phone.userAgent,
        deviceScaleFactor: 1,
        isMobile: true,
        hasTouch: true,
    }
}

/** What the player of a view shows, see `ThemeView.player`. */
export type ThemePlayer = 'saved' | 'other-device' | 'none'

export interface ThemeView {
    /** File name of the screenshot, e.g. "album" or "phone-library". */
    readonly name: string
    readonly device: ThemeDevice
    /**
     * Whether the view looks the same on every run with the test media. The profile selection
     * lists every profile of the instance, so the regression leaves it out.
     */
    readonly stable: boolean
    /**
     * Areas of the app (parts below `app`) photographed on their own as well, e.g. the bottom
     * navigation, so a change of them shows clearly; the file name is "<view>-<part>".
     */
    readonly details?: readonly string[]
    /**
     * What the player shows: the saved playback state as the device that saved it ("saved", the
     * default), as another device of the profile with the notice "Continue listening from"
     * ("other-device"), or nothing, as if the profile had never played ("none").
     */
    readonly player?: ThemePlayer
    /** Opens the view; the page has the session of the theme profile. */
    open(page: Page, request: APIRequestContext): Promise<void>
}

/**
 * Parts whose content changes between runs (play position) or depends on what other tests did
 * with the instance (the number of songs to review); masked in the regression.
 */
export function dynamicParts(page: Page): Locator[] {
    return [
        page.locator("[data-part='seek'], [data-part='time-elapsed']"),
        page.locator("[data-part='btn-review-inbox'] > [data-part='badge']"),
    ]
}

async function openAlbum(page: Page, request: APIRequestContext): Promise<void> {
    await page.goto(`/library/albums/${await findAlbumId(request, 'E2E Album')}`)
    await expect(part(page, 'detail-title')).toHaveText('E2E Album')
}

/** Opens the overview of the keyboard shortcuts with its key on the home page. */
async function openShortcuts(page: Page): Promise<void> {
    await page.goto('/')
    await expect(page.locator("[data-part='shelf'][data-item='playlists']")).toBeVisible()
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur())
    await page.keyboard.press('?')
    await expect(page.locator("[data-part='dialog'][data-item='shortcuts']")).toBeVisible()
}

/**
 * Opens Now Playing with the cover from the album, with the saved playback state: First Song waits
 * paused at its start. Nothing plays, as the songs last two seconds only.
 */
async function openNowPlaying(page: Page, request: APIRequestContext): Promise<void> {
    await openAlbum(page, request)
    await expect(part(page, 'now-playing-title').filter({ visible: true }).first()).toHaveText(
        'First Song',
    )
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(page).toHaveURL(/\/now-playing$/)
    // Autoplay below the queue has chosen its songs (Video Only of the same artist).
    await expect(part(page, 'autoplay-item').filter({ visible: true }).first()).toBeVisible()
}

export const themeViews: readonly ThemeView[] = [
    {
        name: 'profiles',
        device: 'desktop',
        stable: false,
        async open(page) {
            await page.goto('/profiles')
            await expect(page.locator("[data-part='profile-tile']").first()).toBeVisible()
        },
    },
    {
        name: 'home',
        device: 'desktop',
        stable: true,
        async open(page) {
            await page.goto('/')
            await expect(page.locator("[data-part='shelf'][data-item='playlists']")).toBeVisible()
        },
    },
    {
        // The overview of the playlists, whose entry "Playlists" is the current one on the first
        // level of the sidebar tree.
        name: 'playlists',
        device: 'desktop',
        stable: true,
        async open(page) {
            await page.goto('/playlists')
            await expect(
                page.locator("[data-part='tile'][data-kind='playlist']").first(),
            ).toBeVisible()
            await expect(
                page.locator(
                    "[data-part='sidebar'] [data-part='tree-item'][aria-level='1'][aria-current='page']",
                ),
            ).toBeVisible()
        },
    },
    {
        name: 'album',
        device: 'desktop',
        stable: true,
        open: openAlbum,
    },
    {
        name: 'artists',
        device: 'desktop',
        stable: true,
        async open(page) {
            await page.goto('/library/artists')
            await expect(page.locator("[data-part='tile']").first()).toBeVisible()
        },
    },
    {
        // Without a song in the player: the open menu reaches over the transport, and its shadow
        // would fall on the time of the song.
        name: 'songs-menu',
        device: 'desktop',
        stable: true,
        player: 'none',
        async open(page) {
            await page.goto('/library/songs')
            const row = part(page, 'list-row').filter({ hasText: 'Second Song' }).first()
            await row.click({ button: 'right' })
            await expect(
                page.locator("[data-part='context-menu'][data-state='open']"),
            ).toBeVisible()
        },
    },
    {
        name: 'now-playing',
        device: 'desktop',
        stable: true,
        open: openNowPlaying,
    },
    {
        name: 'settings',
        device: 'desktop',
        stable: true,
        async open(page) {
            await page.goto('/settings')
            await expect(page.locator("[data-part='theme-option']").first()).toBeVisible()
        },
    },
    {
        name: 'admin',
        device: 'desktop',
        stable: true,
        async open(page) {
            await page.goto('/admin/transcoding')
            await expect(page.locator("[data-part='section'][data-item='settings']")).toBeVisible()
        },
    },
    {
        name: 'shortcuts',
        device: 'desktop',
        stable: true,
        open: openShortcuts,
    },
    {
        name: 'phone-library',
        device: 'phone',
        stable: true,
        player: 'other-device',
        async open(page) {
            await page.goto('/library/albums')
            await expect(page.locator("[data-part='tile']").first()).toBeVisible()
            await expect(part(page, 'bottom-nav')).toBeVisible()
        },
    },
    {
        name: 'phone-now-playing',
        device: 'phone',
        stable: true,
        details: ['bottom-nav'],
        open: openNowPlaying,
    },
    {
        name: 'phone-settings',
        device: 'phone',
        stable: true,
        async open(page) {
            await page.goto('/settings')
            await expect(page.locator("[data-part='theme-option']").first()).toBeVisible()
        },
    },
    {
        name: 'phone-shortcuts',
        device: 'phone',
        stable: true,
        open: openShortcuts,
    },
]

/** The area of the app shown in a detail screenshot of a view. */
export function detailLocator(page: Page, detail: string): Locator {
    return page.locator(`[data-part='app'] > [data-part='${detail}']`)
}

/** Selects the profile on the profile selection, so the page has its session. */
export async function enterAs(page: Page, profile: ThemeProfile): Promise<void> {
    await page.goto('/profiles')
    await profileTile(page, profile.name).click()
    await expect(page).toHaveURL(homeUrl)
}

/** Lets fonts, pictures and the theme settle and removes hover and focus. */
export async function settle(page: Page): Promise<void> {
    await page.waitForLoadState('networkidle')
    await page.mouse.move(0, 0)
    await page.evaluate(async () => {
        await document.fonts.ready
        await Promise.all(
            [...document.images]
                .filter((image) => !image.complete)
                .map(
                    (image) =>
                        new Promise((resolve) => {
                            image.addEventListener('load', resolve, {
                                once: true,
                            })
                            image.addEventListener('error', resolve, {
                                once: true,
                            })
                        }),
                ),
        )
    })
    await page.waitForTimeout(400)
}

/**
 * Opens every view of a target in its own browser context per device, with the session of the
 * profile of the target, and calls back with the settled page. The pages keep the saved playback
 * state as it is: their saves are answered without reaching the server.
 */
export async function visitThemeViews(
    browser: Browser,
    request: APIRequestContext,
    target: ThemeTarget,
    profile: ThemeProfile,
    views: readonly ThemeView[],
    visit: (page: Page, view: ThemeView) => Promise<void>,
): Promise<void> {
    for (const device of ['desktop', 'phone'] as const) {
        const deviceViews = views.filter((view) => view.device === device)
        if (deviceViews.length === 0) {
            continue
        }
        const context = await browser.newContext(themeContextOptions(device, target.variant))
        try {
            let player: ThemePlayer = 'saved'
            await context.route('**/api/v1/me/playback-state', (route) => {
                if (route.request().method() === 'PUT') {
                    return route.fulfill({ status: 204 })
                }
                return player === 'none'
                    ? route.fulfill({
                          status: 404,
                          contentType: 'application/problem+json',
                          body: JSON.stringify({ status: 404, code: 'playback.state_not_found' }),
                      })
                    : route.fallback()
            })
            const page = await context.newPage()
            await enterAs(page, profile)
            for (const view of deviceViews) {
                // The page reads the id of its device and the saved state when it loads, and every
                // view loads the page.
                player = view.player ?? 'saved'
                await page.evaluate(([key, id]) => localStorage.setItem(key, id), [
                    deviceIdKey,
                    player === 'other-device' ? otherDeviceId : savingDevice.id,
                ] as const)
                await view.open(page, request)
                await settle(page)
                await visit(page, view)
            }
        } finally {
            await context.close()
        }
    }
}
