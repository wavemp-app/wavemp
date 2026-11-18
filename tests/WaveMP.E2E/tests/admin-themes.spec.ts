import { expect, test, type APIRequestContext } from '@playwright/test'
import { adminHeaders, createUser, deleteUser, uniqueSuffix } from './helpers/api'
import { adminNavItem, homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The theme page of the administration: the template is downloaded, uploaded as a theme package
// through the page, chosen by a profile under the Content Security Policy of the web app and
// deleted again. The theme of the template has the id "my-theme".

const templateId = 'my-theme'

async function downloadTemplate(request: APIRequestContext): Promise<Buffer> {
    const response = await request.get('/api/v1/theme-template')
    expect(response.status()).toBe(200)
    expect(response.headers()['content-type']).toBe('application/zip')
    return response.body()
}

async function deleteTemplateTheme(request: APIRequestContext): Promise<void> {
    const response = await request.delete(`/api/v1/themes/${templateId}`, {
        headers: await adminHeaders(request),
    })
    expect([204, 404]).toContain(response.status())
}

test('shows the upload and the template without horizontal scrolling', async ({ page }) => {
    await page.goto('/admin/themes')

    await expect(page.locator("[data-view='admin-themes']")).toBeVisible()
    await expect(adminNavItem(page, 'admin-themes')).toHaveAttribute('aria-current', 'page')
    await expect(page.locator("[data-item='theme-template']")).toHaveAttribute(
        'href',
        '/api/v1/theme-template',
    )
    await expect(page.getByRole('button', { name: 'Upload theme' })).toBeVisible()

    const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
    expect(overflow).toBeLessThanOrEqual(0)
})

test.describe('changes', () => {
    test.beforeEach(({}, testInfo) => {
        test.skip(
            testInfo.project.name !== 'chromium',
            'The themes are changed on one project only.',
        )
    })

    test('installs the template, lets a profile choose it and deletes it after asking', async ({
        page,
        browser,
        request,
    }) => {
        await deleteTemplateTheme(request)
        const template = await downloadTemplate(request)
        const user = await createUser(request, `Theo ${uniqueSuffix()} Themer`)
        try {
            await page.goto('/admin/themes')
            await page.locator("[data-item='theme-upload'] input[type='file']").setInputFiles({
                name: 'wavemp-theme-template.zip',
                mimeType: 'application/zip',
                buffer: template,
            })
            await expect(page.locator("[data-part='alert']")).toHaveText(
                'The theme My Theme is installed. Every profile can now choose it in the settings.',
            )
            const item = page.locator(`[data-part='theme-item'][data-item='${templateId}']`)
            await expect(item.locator("[data-part='theme-name']")).toHaveText('My Theme')
            await expect(item.locator("[data-part='theme-preview-image']")).toHaveAttribute(
                'src',
                new RegExp(`^/api/v1/themes/${templateId}/files/[0-9a-f]{32}\\.svg$`),
            )

            // Another profile chooses the theme; its CSS and pictures load under the policy.
            const context = await browser.newContext({
                storageState: emptyStorageState,
            })
            try {
                const userPage = await context.newPage()
                await userPage.addInitScript(() => {
                    const violations: string[] = []
                    ;(window as unknown as { cspViolations: string[] }).cspViolations = violations
                    document.addEventListener('securitypolicyviolation', (event) =>
                        violations.push(`${event.violatedDirective} ${event.blockedURI}`),
                    )
                })
                await userPage.goto('/')
                await profileTile(userPage, user.name).click()
                await expect(userPage).toHaveURL(homeUrl)
                await userPage.goto('/settings')
                const option = userPage.locator(
                    `[data-part='theme-option'][data-item='${templateId}']`,
                )
                await option.click()
                await expect(option).toHaveAttribute('aria-checked', 'true')
                await expect(userPage.locator(':root')).toHaveAttribute('data-theme', templateId)
                await expect
                    .poll(() =>
                        userPage.evaluate(
                            () =>
                                [...document.styleSheets].filter((sheet) =>
                                    sheet.href?.includes('/api/v1/themes/my-theme/files/'),
                                ).length,
                        ),
                    )
                    .toBe(1)

                // After a reload the theme is there before the list of themes is loaded again.
                await userPage.reload()
                await expect(userPage.locator(':root')).toHaveAttribute('data-theme', templateId)
                expect(
                    await userPage.evaluate(
                        () => (window as unknown as { cspViolations: string[] }).cspViolations,
                    ),
                ).toEqual([])
            } finally {
                await context.close()
            }

            await item.locator("[data-item='delete']").click()
            await expect(page.locator("[data-part='dialog-title']")).toHaveText('Delete theme')
            await page
                .locator("[data-part='dialog-footer']")
                .getByRole('button', { name: 'Delete' })
                .click()
            await expect(page.locator("[data-part='alert']")).toHaveText(
                'The theme is deleted. Profiles that used it now see the default theme.',
            )
            await expect(item).toHaveCount(0)
        } finally {
            await deleteTemplateTheme(request)
            await deleteUser(request, user.id)
        }
    })

    test('lists why a package is rejected', async ({ page }) => {
        await page.goto('/admin/themes')
        await page.locator("[data-item='theme-upload'] input[type='file']").setInputFiles({
            name: 'theme.zip',
            mimeType: 'application/zip',
            buffer: Buffer.from('PK\u0005\u0006' + '\u0000'.repeat(18), 'latin1'),
        })

        await expect(page.locator("[data-part='alert']")).toHaveText(
            'The theme was not installed. Fix these problems and upload it again:',
        )
        await expect(page.locator("[data-part='theme-error']").first()).toHaveAttribute(
            'data-item',
            /^(manifest\.missing|css\.missing)$/,
        )
    })
})
