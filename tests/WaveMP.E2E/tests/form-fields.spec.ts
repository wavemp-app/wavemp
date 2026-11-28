import { expect, test, type Page } from '@playwright/test'
import { sessionHeaders, send } from '../screenshots/helpers'
import { shippedThemeTargets, themeProfileName } from '../screenshots/themes'
import { adminToken, deleteUser, findAlbumId, findTrackId, uniqueSuffix } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// Safari on iOS zooms the page into a form field that gets the focus while its text is smaller than
// 16 pixels, and the page stays zoomed in afterwards. On touch devices every field a keyboard or a
// picker opens for is therefore at least 16 pixels in every shipped theme (THEMING.md, "Form fields
// on touch devices"); with a mouse the fields keep the sizes of the theme. The zoom of the page is
// never locked.

test.use({ storageState: emptyStorageState })

/** Fields a keyboard or picker opens for, as in the base rule of components.css. */
const fieldSelector = [
    'input:not([type="checkbox"], [type="radio"], [type="range"], [type="color"], [type="file"],' +
        ' [type="hidden"], [type="button"], [type="submit"], [type="reset"], [type="image"])',
    'select',
    'textarea',
].join(', ')

interface FieldSize {
    readonly field: string
    readonly size: number
}

/** The computed font size of every field of the page, hidden ones (e.g. the PIN pad) included. */
async function fieldSizes(page: Page): Promise<FieldSize[]> {
    return page.evaluate((selector) => {
        return [...document.querySelectorAll<HTMLElement>(selector)].map((field) => {
            const owner = field.closest('[data-field], [data-part]:not([data-part="input"])')
            const name =
                owner?.getAttribute('data-field') ?? owner?.getAttribute('data-part') ?? 'page'
            const type = field.getAttribute('type') ?? field.tagName.toLowerCase()
            return {
                field: `${name} ${field.tagName.toLowerCase()}[${type}]`,
                size: parseFloat(getComputedStyle(field).fontSize),
            }
        })
    }, fieldSelector)
}

/** Signs in a new admin profile with the theme; returns its id for the cleanup. */
async function signInWithTheme(
    page: Page,
    request: Parameters<typeof adminToken>[0],
    theme: string,
    variant: 'light' | 'dark',
    name: string,
): Promise<string> {
    const created = await request.post('/api/v1/users', {
        data: { name: `${name} Fields ${uniqueSuffix()}`, role: 'Admin' },
        headers: { Authorization: `Bearer ${await adminToken(request)}` },
    })
    expect(created.status(), await created.text()).toBe(201)
    const user = (await created.json()) as { id: string; name: string }
    const headers = await sessionHeaders(request, user.id)
    await send(request, headers, 'patch', '/api/v1/me/settings', {
        language: 'en',
        theme: { id: theme, appearance: variant === 'dark' ? 'Dark' : 'Light' },
    })
    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
    await expect(page.locator(':root')).toHaveAttribute('data-theme', theme)
    return user.id
}

test('keeps the zoom of the page free', async ({ page }) => {
    await page.goto('/profiles')
    const viewport = await page.locator('meta[name="viewport"]').getAttribute('content')
    expect(viewport).toContain('width=device-width')
    expect(viewport).not.toMatch(/maximum-scale|minimum-scale|user-scalable/)
})

test.describe('on a phone', () => {
    test.skip(({ isMobile }) => !isMobile, 'Phones only')

    for (const target of shippedThemeTargets()) {
        test(`draws the form fields at 16 pixels or more in ${target.name} (${target.variant})`, async ({
            page,
            request,
        }) => {
            const userId = await signInWithTheme(
                page,
                request,
                target.theme,
                target.variant,
                themeProfileName(target),
            )
            try {
                const theme = `${target.theme} ${target.variant}`
                expect(await page.evaluate(() => matchMedia('(pointer: coarse)').matches)).toBe(true)

                const album = await findAlbumId(request, 'E2E Album')
                const track = await findTrackId(request, 'First Song')
                const pages = [
                    { url: '/library/songs', ready: "[data-part='search'] [data-part='input']" },
                    { url: '/settings', ready: "[data-field='language'] [data-part='select']" },
                    { url: '/settings/pin', ready: "[data-part='input']" },
                    { url: '/playlists/smart/new', ready: "[data-part='select']" },
                    { url: '/admin/library', ready: "[data-part='input']" },
                    { url: '/admin/organize', ready: "[data-part='input']" },
                    { url: '/admin/organize/albums/new', ready: "[data-part='input']" },
                    { url: `/library/albums/${album}`, ready: "[data-part='detail-title']" },
                ]
                const kinds = new Set<string>()
                for (const { url, ready } of pages) {
                    await page.goto(url)
                    await expect(page.locator(ready).first()).toBeAttached()
                    const sizes = await fieldSizes(page)
                    for (const { field, size } of sizes) {
                        kinds.add(field.replace(/^.* /, ''))
                        expect(size, `${theme} ${url} ${field}`).toBeGreaterThanOrEqual(16)
                    }
                }

                // The text area of the dialog that uploads lyrics.
                await page.goto(`/library/tracks/${track}`)
                const lyrics = page.locator(
                    "[data-part='app'] [data-part='section'][data-item='lyrics']",
                )
                await lyrics.locator("[data-item='upload-lyrics']").click()
                const dialog = page.locator("[data-part='dialog'][data-item='lyrics-upload']")
                await expect(dialog.locator('textarea')).toBeVisible()
                for (const { field, size } of await fieldSizes(page)) {
                    kinds.add(field.replace(/^.* /, ''))
                    expect(size, `${theme} lyrics upload ${field}`).toBeGreaterThanOrEqual(16)
                }

                // Text fields, select lists and text areas were all measured.
                expect([...kinds].some((kind) => kind.startsWith('input['))).toBe(true)
                expect(kinds).toContain('select[select]')
                expect(kinds).toContain('textarea[textarea]')
            } finally {
                await deleteUser(request, userId)
            }
        })
    }
})

test.describe('on the desktop', () => {
    test.skip(({ isMobile }) => isMobile, 'Desktop only')

    // Without a touch screen the fields keep the size of the theme: Midnight and Classic 9x draw
    // them like the text around them, below 16 pixels.
    for (const theme of ['midnight', 'classic-9x']) {
        test(`keeps the size of the theme for the form fields in ${theme}`, async ({
            page,
            request,
        }) => {
            const variant = theme === 'midnight' ? 'dark' : 'light'
            const userId = await signInWithTheme(page, request, theme, variant, theme)
            try {
                expect(await page.evaluate(() => matchMedia('(pointer: coarse)').matches)).toBe(
                    false,
                )
                await page.goto('/settings')
                const select = page.locator("[data-field='language'] [data-part='select']")
                await expect(select).toBeVisible()
                const sizes = await select.evaluate((field) => ({
                    field: parseFloat(getComputedStyle(field).fontSize),
                    body: parseFloat(getComputedStyle(document.body).fontSize),
                }))
                expect(sizes.field).toBe(sizes.body)
                expect(sizes.field).toBeLessThan(16)
            } finally {
                await deleteUser(request, userId)
            }
        })
    }
})
