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
import { expectOverviewReady, groupHeading, mockLongTrackList } from '../tests/helpers/groupJump'
import { homeUrl, profileTile } from '../tests/helpers/navigation'
import { mockLibraryPage, mockOrganize, mockOwnAlbumId } from '../tests/helpers/organizeMock'
import { part, prepareProfile, send, sessionHeaders, stillCovers } from './helpers'

// The views every theme is checked and photographed with (THEMING.md, "Checking a theme"): the
// screenshots for theme designers (theme.screenshots.ts), the preview pictures of the theme
// picker and the screenshot and contrast regression of the shipped themes (tests/themes). Each
// theme and variant gets a profile of its own (an admin, so it also sees the administration),
// filled like the profile of the README screenshots, so the workers of a run (one theme and
// variant each) and runs of other themes can share one WaveMP instance at the same time.
//
// The player of every view starts from a playback state the view defines (`ThemeView.player`):
// before the view opens, the saved state of its profile is set through the API, either E2E Album
// paused at the start of First Song, saved by the desktop device of the theme runs, or no state at
// all, and the view waits until its player shows exactly that. The pages never change the saved
// state and nothing plays, so no view depends on an earlier view, an earlier run or on when a save
// reached the server.

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

/** Id of the <link> element that loads the theme (themeLinkId in src/themes/themes.ts of the app). */
const themeLinkId = 'wavemp-theme'

/** Color of the theme profiles, fixed so the screenshots stay the same. */
const profileColor = '#2f6fd0'

/** Key of the id of the device in the local storage of the web app (src/player/device.ts). */
const deviceIdKey = 'wavemp.device-id'

/** The device that saved the playback state of the theme profiles. */
const savingDevice = { id: 'theme-regression-desktop', name: 'Chrome · Windows' }

/** Another device of the profile, which shows the notice "Continue listening from". */
const otherDeviceId = 'theme-regression-other'

/** The song the saved playback state of the views waits on, paused at its start. */
const savedSong = 'First Song'

/** The saved playback state of the views: E2E Album paused at the start of First Song. */
async function playbackStateOfViews(request: APIRequestContext): Promise<unknown> {
    const tracks = await Promise.all(
        [savedSong, 'Second Song'].map((title) => findTrackId(request, title)),
    )
    return {
        items: tracks.map((trackId) => ({ trackId })),
        currentIndex: 0,
        positionMilliseconds: 0,
        shuffle: false,
        originalOrder: [],
        repeat: 'Off',
        source: { kind: 'Album', id: await findAlbumId(request, 'E2E Album') },
        device: savingDevice,
    }
}

const playbackStateUrl = '/api/v1/me/playback-state'

/**
 * Sets the saved playback state of the profile a view starts from through the API and checks that
 * the server answers with it: the state of the views, or none for an empty player.
 */
async function preparePlayback(
    request: APIRequestContext,
    profile: ThemeProfile,
    player: ThemePlayer,
): Promise<void> {
    if (player === 'none') {
        const cleared = await request.delete(playbackStateUrl, { headers: profile.headers })
        expect(cleared.status(), await cleared.text()).toBe(204)
    } else {
        await send(request, profile.headers, 'put', playbackStateUrl, profile.playbackState)
    }

    const saved = await request.get(playbackStateUrl, { headers: profile.headers })
    if (player === 'none') {
        expect(saved.status(), await saved.text()).toBe(404)
        return
    }

    expect(saved.status(), await saved.text()).toBe(200)
    const state = await saved.json()
    expect({
        title: state.items[state.currentIndex]?.track?.title,
        position: state.positionMilliseconds,
        device: state.device?.id,
    }).toEqual({ title: savedSong, position: 0, device: savingDevice.id })
}

export interface ThemeProfile {
    readonly id: string
    readonly name: string
    readonly headers: Record<string, string>
    /** The saved playback state of the views (the body of PUT /api/v1/me/playback-state). */
    readonly playbackState: unknown
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
    return { id, name, headers, playbackState: await playbackStateOfViews(request) }
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
     * ("other-device"), or nothing, as if the profile had never played ("none"; also for pages
     * without the app shell and its player, such as the profile selection). The state is set
     * through the API before the view opens, and the view waits until its player shows it.
     */
    readonly player?: ThemePlayer
    /**
     * Areas of the view whose content depends on what other tests did with the instance, masked in
     * the regression like `dynamicParts`.
     */
    mask?(page: Page): Locator[]
    /** Opens the view; the page has the session of the theme profile. */
    open(page: Page, request: APIRequestContext): Promise<void>
}

/**
 * Parts whose content changes between runs (play position) or depends on what other tests did
 * with the instance (the number of songs to review); masked in the regression.
 */
export function dynamicParts(page: Page, view?: ThemeView): Locator[] {
    return [
        page.locator("[data-part='seek'], [data-part='time-elapsed']"),
        page.locator("[data-part='btn-review-inbox'] > [data-part='badge']"),
        ...(view?.mask?.(page) ?? []),
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

/**
 * Opens the organize page with the own albums and the log of the fixture (organizeMock.ts): the
 * availability, the own albums in their three states and the log with a run, a stopped run of the
 * library, an undo and an undone run.
 */
async function openOrganize(page: Page): Promise<void> {
    await mockOrganize(page)
    await page.goto('/admin/organize')
    await expect(page.locator("[data-part='own-album-item']")).toHaveCount(3)
    await expect(page.locator("[data-part='organize-entry']")).toHaveCount(4)
    await expect(page.locator("[data-part='organize-availability']")).toHaveAttribute(
        'data-state',
        'available',
    )
}

/**
 * Opens the preview of the own album "Summer Roadtrip" of the fixture from its editor: routes of
 * songs with several files, tags, a warning, a conflict and the cover written into the new folder.
 */
async function openOrganizePreview(page: Page): Promise<void> {
    await mockOrganize(page)
    await page.goto(`/admin/organize/albums/${mockOwnAlbumId}`)
    await expect(page.locator("[data-part='own-album-track']")).toHaveCount(4)
    // A click without scrolling: the editor behind the dialog stays at the top in every run.
    const previewButton = page.locator("button[data-item='preview-changes']")
    await expect(previewButton).toBeEnabled()
    await previewButton.dispatchEvent('click')
    const dialog = page.locator("dialog[data-item='organize-preview']")
    await expect(dialog.locator("[data-part='organize-item']")).toHaveCount(5)
    await expect(dialog.locator("[data-field='skip-conflicts']")).toBeVisible()
}

/**
 * Opens the editor of the layout template on the library page of the administration, scrolled to
 * it, with the live preview of the fixture. The media folders and the scans above it come from the
 * fixture as well, so the editor lies at the same place in every run.
 *
 * The page scrolls only once every section has loaded: a section above the editor that grows after
 * the scroll (the list of media folders) makes the browser keep the editor in place (scroll
 * anchoring), but at another fraction of a pixel, so its texts and borders look different.
 */
async function openOrganizeTemplate(page: Page): Promise<void> {
    await mockOrganize(page)
    await mockLibraryPage(page)
    await page.goto('/admin/library')
    const section = page.locator("[data-part='section'][data-item='file-layout']")
    await expect(section.locator("[data-part='template-preview-item']")).toHaveCount(3)
    await expect(section.locator("[data-part='template-field']")).toHaveAttribute(
        'data-state',
        'folders',
    )
    await expectLibraryPageLoaded(page)
    await scrollToStart(section)
}

/**
 * Scrolls the element to the start of its scroll area once the page has settled: the stylesheet of
 * the theme and its fonts have loaded and the layout stays the same for a few frames.
 *
 * A page first shows the default theme and switches to the theme of the profile once its settings
 * have loaded, which can take longer than the data of the view in a busy run. When the theme
 * arrives after the scroll, every part above the element changes its size and the browser keeps
 * the element in place (scroll anchoring), but at a scroll position rounded to another pixel, so
 * the element sits one pixel higher and its texts and borders differ from the baseline.
 */
async function scrollToStart(element: Locator): Promise<void> {
    const page = element.page()
    await page.waitForFunction((id) => {
        const link = document.querySelector<HTMLLinkElement>(`link#${id}`)
        // A page without the theme link (e.g. a theme preview) has its stylesheet from the start.
        return !link || link.sheet?.href === link.href
    }, themeLinkId)
    await element.evaluate(async (target) => {
        const frame = () => new Promise((resolve) => requestAnimationFrame(resolve))
        // The fonts of the theme start loading once the new styles apply to the texts.
        await frame()
        await frame()
        await document.fonts.ready
        const view = target.closest("[data-part='view']") ?? document.body
        const layout = () => {
            const box = target.getBoundingClientRect()
            return `${box.top} ${box.height} ${view.getBoundingClientRect().height}`
        }
        const stableFrames = 10
        let last = layout()
        let stable = 0
        for (let frames = 0; stable < stableFrames; frames++) {
            if (frames > 600) {
                throw new Error('The layout of the page did not settle before the scroll.')
            }
            await frame()
            const current = layout()
            stable = current === last ? stable + 1 : 0
            last = current
        }
        target.scrollIntoView({ block: 'start' })
    })
}

/**
 * Waits until every section of the library page of the administration shows what it loaded: the
 * media folders and the scans of the fixture (the summary of the last scan comes with them), and
 * no section (settings, lyrics lookups) still busy.
 */
async function expectLibraryPageLoaded(page: Page): Promise<void> {
    const view = page.locator("[data-part='view'][data-view='admin-library']")
    await expect(view.locator("[data-part='source-list'] > [data-part='source-item']")).toHaveCount(
        1,
    )
    await expect(view.locator("[data-part='run-list'] > [data-part='run-item']")).toHaveCount(3)
    await expect(view).not.toHaveAttribute('aria-busy', 'true')
    await expect(view.locator("[aria-busy='true']")).toHaveCount(0)
}

/** The other sections of the library page show numbers of the instance. */
function maskLibrarySections(page: Page): Locator[] {
    return [
        page.locator("[data-part='view'] > [data-part='section']:not([data-item='file-layout'])"),
    ]
}

/** Id of the smart playlist that mockSmartPlaylist answers for; it does not exist on the server. */
const mockSmartPlaylistId = '019a0000-0000-7000-8000-00000000e2e5'

/**
 * Answers the requests of a smart playlist that is not stored, so the other views do not list it:
 * a rule linked to E2E Album, a rule whose artist is not in the library and a rule of the rating,
 * with the songs of E2E Album.
 */
async function mockSmartPlaylist(page: Page, request: APIRequestContext): Promise<void> {
    const albumId = await findAlbumId(request, 'E2E Album')
    const trackIds = await Promise.all(
        [savedSong, 'Second Song'].map((title) => findTrackId(request, title)),
    )
    const smartPlaylist = {
        id: mockSmartPlaylistId,
        name: 'Album Night',
        match: 'Any',
        rules: [
            { field: 'Album', operator: 'Is', text: 'E2E Album', referenceId: albumId },
            { field: 'Artist', operator: 'Is', text: 'The Missing Band' },
            { field: 'Rating', operator: 'AtLeast', number: 4 },
        ],
        sort: 'Title',
        direction: 'Asc',
        limit: null,
        trackCount: trackIds.length,
        durationMilliseconds: 4000,
        cover: { kind: 'Tracks', trackIds },
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
    }
    const path = `/api/v1/me/smart-playlists/${mockSmartPlaylistId}`
    await page.route(
        (url) => url.pathname === path,
        (route) => route.fulfill({ contentType: 'application/json', json: smartPlaylist }),
    )
    await page.route(
        (url) => url.pathname === `${path}/tracks`,
        async (route) => {
            const url = new URL(route.request().url())
            url.pathname = `/api/v1/albums/${albumId}/tracks`
            await route.fulfill({ response: await route.fetch({ url: url.toString() }) })
        },
    )
}

/** Opens the smart playlist of mockSmartPlaylist with its rules in words and its songs. */
async function openSmartPlaylist(page: Page, request: APIRequestContext): Promise<void> {
    await mockSmartPlaylist(page, request)
    await page.goto(`/playlists/smart/${mockSmartPlaylistId}`)
    await expect(part(page, 'detail-title')).toHaveText('Album Night')
    await expect(part(page, 'smart-summary-note')).toBeVisible()
    await expect(part(page, 'list-row')).toHaveCount(2)
    // On the phone the rules lie below the cover and the buttons.
    await scrollToStart(part(page, 'smart-summary'))
}

/** Opens the editor of the smart playlist of mockSmartPlaylist, with the hint below the artist. */
async function openSmartPlaylistEditor(page: Page, request: APIRequestContext): Promise<void> {
    await mockSmartPlaylist(page, request)
    await page.goto(`/playlists/smart/${mockSmartPlaylistId}/edit`)
    const rules = part(page, 'smart-rule')
    await expect(rules).toHaveCount(3)
    await expect(rules.nth(1).locator("[data-part='field-hint']")).toBeVisible()
}

/** Opens the recap of the last month, paused on its second section once it has entered. */
async function openRecap(page: Page): Promise<void> {
    await page.goto('/recap')
    const story = page.locator("[data-part='recap-story']")
    await expect(story).toHaveAttribute('data-state', /playing|paused/)
    await story.locator("[data-part='btn-recap-pause']").click()
    await story.locator("[data-part='btn-recap-next']").click()
    await expect(story.locator("[data-part='recap-slide']")).toHaveAttribute(
        'data-kind',
        'listening-time',
    )
    // The clicks scrolled the story into view; the screenshot starts at the top of the page.
    await page.evaluate(() => {
        document.querySelector("[data-part='content']")?.scrollTo(0, 0)
        window.scrollTo(0, 0)
    })
    await page.waitForTimeout(1500)
}

export const themeViews: readonly ThemeView[] = [
    {
        name: 'profiles',
        device: 'desktop',
        stable: false,
        player: 'none',
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
        // With the song of the saved state in the player: the open menu reaches over the
        // transport and casts its shadow on the time of the song, which keeps its contrast too.
        name: 'songs-menu',
        device: 'desktop',
        stable: true,
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
        // The overview of the groups of a long list of songs (quick jump), opened from the heading
        // of the first group: the letter bar with the current letter and a letter without groups
        // (Q), the current album tile with the indicator and the zoomed-out list behind it. The
        // test media has too few albums for the letter bar, so the list comes from page.route.
        name: 'group-overview',
        device: 'desktop',
        stable: true,
        async open(page) {
            await mockLongTrackList(page)
            await page.goto('/library/songs')
            await groupHeading(page, 0).click()
            const overview = await expectOverviewReady(page)
            await expect(overview.locator("[data-part='letter-bar']")).toBeVisible()
            await expect(
                overview.locator("[data-part='group-tile'][data-state~='current']"),
            ).toBeFocused()
        },
    },
    {
        name: 'organize',
        device: 'desktop',
        stable: true,
        player: 'none',
        open: openOrganize,
    },
    {
        name: 'organize-preview',
        device: 'desktop',
        stable: true,
        player: 'none',
        open: openOrganizePreview,
    },
    {
        name: 'organize-template',
        device: 'desktop',
        stable: true,
        player: 'none',
        mask: maskLibrarySections,
        open: openOrganizeTemplate,
    },
    {
        // A smart playlist with a rule linked to its album and one whose artist is not in the
        // library; the editor shows the hint below the text of that rule.
        name: 'smart-playlist-edit',
        device: 'desktop',
        stable: true,
        open: openSmartPlaylistEditor,
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
        // The rules of the smart playlist in words, the missing artist with its note.
        name: 'phone-smart-playlist',
        device: 'phone',
        stable: true,
        open: openSmartPlaylist,
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
    {
        name: 'phone-organize',
        device: 'phone',
        stable: true,
        player: 'none',
        open: openOrganize,
    },
    {
        name: 'phone-organize-preview',
        device: 'phone',
        stable: true,
        player: 'none',
        open: openOrganizePreview,
    },
    {
        name: 'phone-organize-template',
        device: 'phone',
        stable: true,
        player: 'none',
        mask: maskLibrarySections,
        open: openOrganizeTemplate,
    },
    {
        // The recap of the last month, paused on the minutes of music. The numbers and the days
        // depend on when the run happens, so the regression leaves the recap pages out.
        name: 'recap',
        device: 'desktop',
        stable: false,
        open: openRecap,
    },
    {
        name: 'statistics',
        device: 'desktop',
        stable: false,
        async open(page) {
            await page.goto('/recap/statistics')
            await expect(page.locator("[data-view='statistics']")).toHaveAttribute(
                'data-state',
                'ready',
            )
            await expect(page.locator("[data-part='chart']").first()).toBeVisible()
        },
    },
    {
        name: 'phone-recap',
        device: 'phone',
        stable: false,
        open: openRecap,
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
 * Waits until the player of the page shows the playback state the view started from: First Song
 * paused at its start, loaded and without a notice of a failure, with the notice "Continue
 * listening from" for another device; or an empty player.
 */
async function expectPlayer(page: Page, player: ThemePlayer): Promise<void> {
    const transport = page.locator("[data-part='app'] [data-part='transport']")
    const notice = transport.locator("[data-part='resume-notice']")
    if (player === 'none') {
        // Pages without the app shell (the profile selection) show no player.
        if ((await page.locator("[data-part='app']").count()) === 0) {
            return
        }
        await expect(transport).toHaveAttribute('data-state', /^idle\b/)
        await expect(transport.locator("[data-part='now-playing-mini']")).toHaveAttribute(
            'data-state',
            'empty',
        )
        await expect(notice).toHaveCount(0)
        return
    }

    await expect(transport.locator("[data-part='now-playing-title']")).toHaveText(savedSong)
    // Loaded: paused at the start of the song, not loading and without a failure.
    await expect(transport).toHaveAttribute('data-state', /^paused(?! loading)\b/)
    await expect(transport.locator("[data-part='transport-message']")).toHaveAttribute(
        'data-state',
        'empty',
    )
    if (player === 'other-device') {
        await expect(notice.locator("[data-part='resume-device']")).toHaveText(savingDevice.name)
    } else {
        await expect(notice).toHaveCount(0)
    }
}

/**
 * Opens every view of a target in its own browser context per device, with the session of the
 * profile of the target, and calls back with the settled page. Before each view, the page of the
 * view before is left and the saved playback state of the view is set through the API; the pages
 * never change it, as their saves are answered without reaching the server.
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
            await stillCovers(context)
            await context.route('**/api/v1/me/playback-state', (route) =>
                route.request().method() === 'GET'
                    ? route.fallback()
                    : route.fulfill({ status: 204 }),
            )
            const page = await context.newPage()
            await enterAs(page, profile)
            for (const view of deviceViews) {
                const player = view.player ?? 'saved'
                // The page reads the id of its device and the saved state when it loads, and every
                // view loads the page.
                await page.evaluate(([key, id]) => localStorage.setItem(key, id), [
                    deviceIdKey,
                    player === 'other-device' ? otherDeviceId : savingDevice.id,
                ] as const)
                await page.goto('about:blank')
                await preparePlayback(request, profile, player)
                await view.open(page, request)
                await expectPlayer(page, player)
                await settle(page)
                await visit(page, view)
                // Requests a view answered itself (page.route) end with the view.
                await page.unrouteAll({ behavior: 'ignoreErrors' })
            }
        } finally {
            await context.close()
        }
    }
}
