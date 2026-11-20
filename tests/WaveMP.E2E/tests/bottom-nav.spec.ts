import { expect, test, type Locator, type Page } from '@playwright/test'
import { sessionHeaders, send } from '../screenshots/helpers'
import { shippedThemeTargets, themeProfileName } from '../screenshots/themes'
import { createUser, deleteUser, uniqueSuffix } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The bottom navigation of the phone in every shipped theme, in English and German: all entries
// have the same structure and layout (icon above label, both centered, at the same height), a
// label takes at most two centered lines, and the highlight of the current entry holds its icon
// and label, also with a long label. Now Playing shows its short label there and keeps the full
// one as tooltip and accessible name. The screenshots of the bar are part of the theme regression
// (tests/themes, "phone-bottom-nav").

test.skip(({ isMobile }) => !isMobile, 'Mobile layout only')
test.use({ storageState: emptyStorageState })

interface Box {
    readonly x: number
    readonly y: number
    readonly width: number
    readonly height: number
}

interface EntryLayout {
    readonly item: Box
    readonly icon: Box
    readonly label: Box
    /** Height of the label in the layout, without a transform of the theme (e.g. a scale). */
    readonly labelHeight: number
    readonly lineHeight: number
    readonly textAlign: string
    readonly children: string[]
}

interface BarLayout {
    readonly entries: EntryLayout[]
    /** Box of the highlight of the current entry, if the theme draws one around it. */
    readonly highlight: Box | null
}

/** Boxes of the entries, their icons and labels, and of the highlight of the current entry. */
async function barLayout(bar: Locator): Promise<BarLayout> {
    return bar.evaluate((nav) => {
        const box = (element: Element) => {
            const rect = element.getBoundingClientRect()
            return { x: rect.x, y: rect.y, width: rect.width, height: rect.height }
        }
        const entries = [...nav.querySelectorAll(":scope > [data-part='bottom-nav-item']")].map(
            (item) => {
                const label = item.querySelector(":scope > [data-part='label']")!
                const style = getComputedStyle(label)
                return {
                    item: box(item),
                    icon: box(item.querySelector(":scope > [data-part='icon']")!),
                    label: box(label),
                    labelHeight: (label as HTMLElement).offsetHeight,
                    lineHeight: Number.parseFloat(style.lineHeight),
                    textAlign: style.textAlign,
                    children: [...item.children].map((child) => child.getAttribute('data-part') ?? ''),
                }
            },
        )
        const indicator = nav.querySelector(":scope > [data-part='indicator']")!
        const active = nav.querySelector(":scope > [aria-current='page']")!
        const shown = getComputedStyle(indicator).display !== 'none' && indicator.getClientRects().length > 0
        // A line along an edge is no highlight around the entry; a well or a lens is.
        const highlight =
            shown && box(indicator).height >= box(active).height / 2 ? box(indicator) : null
        return { entries, highlight }
    })
}

function center(box: Box): number {
    return box.x + box.width / 2
}

function inside(inner: Box, outer: Box): boolean {
    const tolerance = 0.5
    return (
        inner.x >= outer.x - tolerance &&
        inner.y >= outer.y - tolerance &&
        inner.x + inner.width <= outer.x + outer.width + tolerance &&
        inner.y + inner.height <= outer.y + outer.height + tolerance
    )
}

/** Expects the same layout in every entry and the current one inside its highlight. */
async function expectAlignedEntries(bar: Locator, context: string): Promise<void> {
    const { entries, highlight } = await barLayout(bar)
    expect(entries, context).toHaveLength(4)
    const [first] = entries
    for (const [index, entry] of entries.entries()) {
        const name = `${context}, entry ${index}`
        expect(entry.children, name).toEqual(['icon', 'label'])
        expect(entry.textAlign, name).toBe('center')
        // Icon above the label, both centered in the entry.
        expect(Math.abs(center(entry.icon) - center(entry.item)), name).toBeLessThanOrEqual(1)
        expect(Math.abs(center(entry.label) - center(entry.item)), name).toBeLessThanOrEqual(1)
        expect(entry.icon.y + entry.icon.height, name).toBeLessThanOrEqual(entry.label.y + 0.5)
        // The same height of icon and label in all entries, and no more than two lines.
        expect(Math.abs(entry.icon.y - first!.icon.y), name).toBeLessThanOrEqual(1)
        expect(Math.abs(entry.label.y - first!.label.y), name).toBeLessThanOrEqual(1)
        expect(entry.labelHeight, name).toBeLessThanOrEqual(2 * entry.lineHeight + 1)
        expect(inside(entry.icon, entry.item), name).toBe(true)
        expect(inside(entry.label, entry.item), name).toBe(true)
    }
    if (highlight) {
        const active = entries[1]!
        expect(inside(active.icon, highlight), `${context}: icon in the highlight`).toBe(true)
        expect(inside(active.label, highlight), `${context}: label in the highlight`).toBe(true)
    }
}

function bottomNav(page: Page): Locator {
    return page.locator("[data-part='app'] > [data-part='bottom-nav']")
}

// The German short label holds a soft hyphen, so it breaks as "Wieder-gabe" where it needs two
// lines.
const languages = [
    { language: 'en', short: 'Playing', full: 'Now Playing' },
    { language: 'de', short: 'Wieder­gabe', full: 'Aktuelle Wiedergabe' },
] as const

for (const target of shippedThemeTargets()) {
    test(`lays out the bottom navigation in ${target.name} (${target.variant})`, async ({
        page,
        request,
    }) => {
        const user = await createUser(request, `${themeProfileName(target)} Bar ${uniqueSuffix()}`)
        try {
            const headers = await sessionHeaders(request, user.id)
            await send(request, headers, 'patch', '/api/v1/me/settings', {
                language: 'en',
                theme: {
                    id: target.theme,
                    appearance: target.variant === 'dark' ? 'Dark' : 'Light',
                },
            })
            await page.goto('/profiles')
            await profileTile(page, user.name).click()
            await expect(page).toHaveURL(homeUrl)
            await expect(page.locator(':root')).toHaveAttribute('data-theme', target.theme)

            for (const { language, short, full } of languages) {
                await send(request, headers, 'patch', '/api/v1/me/settings', { language })
                await page.goto('/now-playing')
                await expect(page.locator('html')).toHaveAttribute('lang', language)
                const bar = bottomNav(page)
                const current = bar.locator("[aria-current='page']")
                await expect(current.locator("[data-part='label']")).toHaveText(short)
                await expect(current).toHaveAttribute('title', full)
                await expect(bar.getByRole('link', { name: full, exact: true })).toHaveAttribute(
                    'aria-current',
                    'page',
                )
                await page.evaluate(() => document.fonts.ready)
                await expectAlignedEntries(bar, `${target.theme} ${target.variant} ${language}`)
            }

            // A label too long for one line in any language: two centered lines, the bar and the
            // highlight grow with them, and the icon stays at the height of the other icons.
            const bar = bottomNav(page)
            const before = (await bar.boundingBox())!.height
            await bar
                .locator("[aria-current='page'] > [data-part='label']")
                .evaluate((label) => {
                    label.textContent = 'Aktuelle Wiedergabe und Warteschlangenverwaltung'
                })
            await expect.poll(async () => (await bar.boundingBox())!.height).toBeGreaterThan(before)
            // The highlight follows the size of the entry once the theme has measured it again.
            await expect
                .poll(() =>
                    bar.evaluate((nav) => {
                        const active = nav.querySelector("[aria-current='page']")!
                        const height = active.getBoundingClientRect().height
                        return Math.abs(
                            Number.parseFloat(
                                (nav as HTMLElement).style.getPropertyValue('--wmp-indicator-height'),
                            ) - height,
                        )
                    }),
                )
                .toBeLessThanOrEqual(1)
            await expectAlignedEntries(bar, `${target.theme} ${target.variant} long label`)
        } finally {
            await deleteUser(request, user.id)
        }
    })
}
