import { expect, test } from '@playwright/test'
import { adminToken, deleteUser, uniqueSuffix } from './helpers/api'
import { cutStatLabels, formatCutLabels } from './helpers/labels'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The labels of the status tiles (`stat-item`) of the administration and the settings are shown
// in full in English and in German, on the desktop and on the smartphone: a long label wraps
// instead of being cut off with an ellipsis. tests/themes checks the same in every shipped theme.

const pages = [
    { url: '/admin/library', view: 'admin-library' },
    { url: '/admin/cache', view: 'admin-cache' },
    { url: '/admin/transcoding', view: 'admin-transcoding' },
    { url: '/admin/backups', view: 'admin-backups' },
    { url: '/settings', view: 'settings' },
]

for (const language of ['en', 'de'] as const) {
    test.describe(`status tiles in the language ${language}`, () => {
        test.use({ storageState: emptyStorageState })

        let profile: { id: string; name: string } | undefined

        test.beforeEach(async ({ request }) => {
            const name = `Stat Labels ${language} ${uniqueSuffix()}`
            const created = await request.post('/api/v1/users', {
                data: { name, role: 'Admin' },
                headers: { Authorization: `Bearer ${await adminToken(request)}` },
            })
            expect(created.status(), await created.text()).toBe(201)
            const id = (await created.json()).id as string
            profile = { id, name }

            const session = await request.post('/api/v1/sessions', { data: { profileId: id } })
            expect(session.status()).toBe(201)
            const settings = await request.patch('/api/v1/me/settings', {
                data: { language },
                headers: { Authorization: `Bearer ${(await session.json()).token}` },
            })
            expect(settings.ok(), await settings.text()).toBe(true)
        })

        test.afterEach(async ({ request }) => {
            if (profile) {
                await deleteUser(request, profile.id)
            }
        })

        test('shows the labels of the status tiles in full', async ({ page }) => {
            await page.goto('/profiles')
            await profileTile(page, profile!.name).click()
            await expect(page).toHaveURL(homeUrl)
            await expect(page.locator('html')).toHaveAttribute('lang', language)

            for (const { url, view } of pages) {
                await page.goto(url)
                await expect(page.locator(`[data-view='${view}']`)).toBeVisible()
                await expect(
                    page.locator("[data-part='stat-item'] > [data-part='label']").first(),
                ).toBeVisible()
                const cut = await cutStatLabels(page)
                expect(cut, `Cut off labels on ${url}:\n${formatCutLabels(cut)}`).toEqual([])
            }
        })
    })
}
