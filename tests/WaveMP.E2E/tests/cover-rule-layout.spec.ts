import { expect, test, type Page } from '@playwright/test'
import { send, sessionHeaders } from '../screenshots/helpers'
import { shippedThemeTargets, themeProfileName } from '../screenshots/themes'
import { adminHeaders, deleteUser, findAlbumId, findTrackId, uniqueSuffix } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The section "Cover" of song and album pages on a phone 390 px wide, in every shipped theme and
// in English and German: the options of the cover rule wrap their labels where they do not fit
// side by side, so the segmented control, its labels and its hint stay inside the section.

test.skip(({ isMobile }) => !isMobile, 'Mobile layout only')
test.use({ storageState: emptyStorageState, viewport: { width: 390, height: 844 } })

interface Edges {
    readonly left: number
    readonly right: number
}

interface SectionLayout {
    readonly section: Edges
    readonly control: Edges
    readonly hint: Edges
    readonly segments: { readonly segment: Edges; readonly label: Edges }[]
    readonly pageOverflow: number
}

async function sectionLayout(page: Page): Promise<SectionLayout> {
    const section = page.locator("[data-part='app'] [data-part='section'][data-item='cover-rule']")
    await expect(section.locator("[data-part='form']")).not.toHaveAttribute('aria-busy', 'true')
    await expect(section.locator("[data-part='segment']")).toHaveCount(3)
    await page.evaluate(() => document.fonts.ready)
    return section.evaluate((element) => {
        const edges = (node: Element | null): Edges => {
            const rect = node!.getBoundingClientRect()
            return { left: rect.left, right: rect.right }
        }
        const root = document.documentElement
        return {
            section: edges(element),
            control: edges(element.querySelector("[data-part='segmented-control']")),
            hint: edges(element.querySelector("[data-part='field-hint']")),
            segments: [...element.querySelectorAll("[data-part='segment']")].map((segment) => ({
                segment: edges(segment),
                label: edges(segment.querySelector(":scope > [data-part='label']")),
            })),
            pageOverflow: root.scrollWidth - root.clientWidth,
        }
    })
}

function expectInside(inner: Edges, outer: Edges, context: string): void {
    expect(inner.left, context).toBeGreaterThanOrEqual(outer.left - 0.5)
    expect(inner.right, context).toBeLessThanOrEqual(outer.right + 0.5)
}

for (const target of shippedThemeTargets()) {
    test(`fits the cover rule into the section in ${target.name} (${target.variant})`, async ({
        page,
        request,
    }) => {
        const created = await request.post('/api/v1/users', {
            data: { name: `${themeProfileName(target)} Cover ${uniqueSuffix()}`, role: 'Admin' },
            headers: await adminHeaders(request),
        })
        expect(created.status(), await created.text()).toBe(201)
        const user = (await created.json()) as { id: string; name: string }
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

            const pages = [
                `/library/tracks/${await findTrackId(request, 'First Song')}`,
                `/library/albums/${await findAlbumId(request, 'E2E Album')}`,
            ]
            for (const language of ['en', 'de']) {
                await send(request, headers, 'patch', '/api/v1/me/settings', { language })
                for (const path of pages) {
                    await page.goto(path)
                    await expect(page.locator('html')).toHaveAttribute('lang', language)
                    const context = `${target.theme} ${target.variant} ${language} ${path}`
                    const layout = await sectionLayout(page)
                    expect(layout.pageOverflow, context).toBeLessThanOrEqual(0)
                    expectInside(layout.control, layout.section, `${context}: options`)
                    expectInside(layout.hint, layout.section, `${context}: hint`)
                    for (const [index, { segment, label }] of layout.segments.entries()) {
                        expectInside(segment, layout.control, `${context}: option ${index}`)
                        expectInside(label, segment, `${context}: label ${index}`)
                    }
                }
            }
        } finally {
            await deleteUser(request, user.id)
        }
    })
}
