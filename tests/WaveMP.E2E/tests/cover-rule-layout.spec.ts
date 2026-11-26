import { expect, test, type Page } from '@playwright/test'
import { send, sessionHeaders } from '../screenshots/helpers'
import { shippedThemeTargets, themeProfileName } from '../screenshots/themes'
import { adminHeaders, deleteUser, findAlbumId, findTrackId, uniqueSuffix } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The section "Cover" in the metadata editor of a song and in the editor of an album on a phone 390
// px wide, in every shipped theme and in English and German: the options of the cover rules wrap
// their labels where they do not fit side by side, so the segmented controls, their labels and
// their hints stay inside the section and the dialog.

test.skip(({ isMobile }) => !isMobile, 'Mobile layout only')
test.use({ storageState: emptyStorageState, viewport: { width: 390, height: 844 } })

interface Edges {
    readonly left: number
    readonly right: number
}

interface FieldLayout {
    readonly control: Edges
    readonly hint: Edges
    readonly segments: { readonly segment: Edges; readonly label: Edges }[]
}

interface SectionLayout {
    readonly dialog: Edges
    readonly section: Edges
    readonly fields: FieldLayout[]
    readonly pageOverflow: number
}

async function sectionLayout(page: Page, fieldCount: number): Promise<SectionLayout> {
    const section = page.locator("[data-part='dialog'][open] [data-part='section'][data-item='cover-rule']")
    await expect(section).not.toHaveAttribute('aria-busy', 'true')
    await expect(section.locator("[data-part='segmented-control']")).toHaveCount(fieldCount)
    await page.evaluate(() => document.fonts.ready)
    return section.evaluate((element) => {
        const edges = (node: Element | null): Edges => {
            const rect = node!.getBoundingClientRect()
            return { left: rect.left, right: rect.right }
        }
        const root = document.documentElement
        return {
            dialog: edges(element.closest("[data-part='dialog']")),
            section: edges(element),
            fields: [...element.querySelectorAll("[data-part='field']")]
                .filter((field) => field.querySelector("[data-part='segmented-control']"))
                .map((field) => ({
                    control: edges(field.querySelector("[data-part='segmented-control']")),
                    hint: edges(field.querySelector("[data-part='field-hint']")),
                    segments: [...field.querySelectorAll("[data-part='segment']")].map((segment) => ({
                        segment: edges(segment),
                        label: edges(segment.querySelector(":scope > [data-part='label']")),
                    })),
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
    test(`fits the cover rules into the editors in ${target.name} (${target.variant})`, async ({
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

            const track = `/library/tracks/${await findTrackId(request, 'First Song')}?edit=metadata`
            const album = `/library/albums/${await findAlbumId(request, 'E2E Album')}`
            for (const language of ['en', 'de']) {
                await send(request, headers, 'patch', '/api/v1/me/settings', { language })
                for (const path of [track, album]) {
                    await page.goto(path)
                    await expect(page.locator('html')).toHaveAttribute('lang', language)
                    if (path === album) {
                        await page.locator("[data-part='detail-actions'] [data-item='edit-metadata']").click()
                    }

                    const context = `${target.theme} ${target.variant} ${language} ${path}`
                    const layout = await sectionLayout(page, path === album ? 2 : 1)
                    expect(layout.pageOverflow, context).toBeLessThanOrEqual(0)
                    expectInside(layout.section, layout.dialog, `${context}: section`)
                    for (const [fieldIndex, field] of layout.fields.entries()) {
                        const fieldContext = `${context}: field ${fieldIndex}`
                        expectInside(field.control, layout.section, `${fieldContext} options`)
                        expectInside(field.hint, layout.section, `${fieldContext} hint`)
                        for (const [index, { segment, label }] of field.segments.entries()) {
                            expectInside(segment, field.control, `${fieldContext} option ${index}`)
                            expectInside(label, segment, `${fieldContext} label ${index}`)
                        }
                    }
                }
            }
        } finally {
            await deleteUser(request, user.id)
        }
    })
}
