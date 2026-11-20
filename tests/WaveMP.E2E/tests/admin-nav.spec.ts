import { expect, test, type Locator } from '@playwright/test'
import { send, sessionHeaders } from '../screenshots/helpers'
import { shippedThemeTargets, themeProfileName } from '../screenshots/themes'
import { adminToken, deleteUser, uniqueSuffix } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The navigation of the administration fits in one line on a desktop window of 1280 pixels, with
// the sidebar open, in every shipped theme and in English and German: no entry wraps into a second
// line.

test.skip(({ isMobile }) => isMobile, 'Desktop layout only')
test.use({ storageState: emptyStorageState, viewport: { width: 1280, height: 800 } })

/** The top edges of the entries, rounded; one value means one line. */
async function linesOf(navigation: Locator): Promise<{ tops: number[]; labels: string[] }> {
    return navigation.evaluate((nav) => {
        const items = [...nav.querySelectorAll(":scope > [data-part='admin-nav-item']")]
        return {
            tops: [...new Set(items.map((item) => Math.round(item.getBoundingClientRect().top)))],
            labels: items.map((item) => item.textContent?.trim() ?? ''),
        }
    })
}

for (const target of shippedThemeTargets()) {
    test(`shows the administration navigation in one line in ${target.name} (${target.variant})`, async ({
        page,
        request,
    }) => {
        const name = `${themeProfileName(target)} Admin Nav ${uniqueSuffix()}`
        const created = await request.post('/api/v1/users', {
            data: { name, role: 'Admin' },
            headers: { Authorization: `Bearer ${await adminToken(request)}` },
        })
        expect(created.status(), await created.text()).toBe(201)
        const id = (await created.json()).id as string
        try {
            const headers = await sessionHeaders(request, id)
            await send(request, headers, 'patch', '/api/v1/me/settings', {
                language: 'en',
                theme: {
                    id: target.theme,
                    appearance: target.variant === 'dark' ? 'Dark' : 'Light',
                },
            })
            await page.goto('/profiles')
            await profileTile(page, name).click()
            await expect(page).toHaveURL(homeUrl)
            await expect(page.locator(':root')).toHaveAttribute('data-theme', target.theme)

            for (const language of ['en', 'de'] as const) {
                await send(request, headers, 'patch', '/api/v1/me/settings', { language })
                await page.goto('/admin/transcoding')
                await expect(page.locator('html')).toHaveAttribute('lang', language)
                await expect(
                    page.locator("[data-part='sidebar'] [data-part='tree']").first(),
                ).toBeVisible()
                const navigation = page.locator("[data-part='admin-nav']")
                await expect(navigation.locator("[aria-current='page']")).toBeVisible()
                await page.evaluate(() => document.fonts.ready)
                const { tops, labels } = await linesOf(navigation)
                expect(labels, language).toHaveLength(10)
                expect(
                    tops,
                    `${target.theme} ${target.variant} ${language}: ${labels.join(', ')}`,
                ).toHaveLength(1)
            }
        } finally {
            await deleteUser(request, id)
        }
    })
}
