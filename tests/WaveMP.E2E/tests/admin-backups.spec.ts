import { expect, test } from '@playwright/test'
import { deleteBackup, listBackups, resetBackupSettings } from './helpers/api'
import { adminNavItem } from './helpers/navigation'

// The backup page of the administration on the desktop and on the smartphone. The exports are
// created by the real pg_dump of the image against the PostgreSQL 18 database of the test setup.
// The settings are shared by all tests and one test changes them while the others run, so only
// that test expects the default values; the others accept any time and number.

test('shows the state, the exports and the settings of the backups without horizontal scrolling', async ({
    page,
}) => {
    await page.goto('/admin/backups')

    await expect(page.locator("[data-view='admin-backups']")).toBeVisible()
    await expect(adminNavItem(page, 'admin-backups')).toHaveAttribute('aria-current', 'page')
    const status = page.locator("[data-part='backup-status']")
    await expect(status).toBeVisible()
    await expect(status.locator("[data-item='schedule'] [data-part='stat-value']")).toHaveText(
        /^Daily at \d{2}:\d{2} \([^)]+\)$/,
    )
    await expect(status.locator("[data-item='next-run'] [data-part='stat-value']")).not.toHaveText(
        'None',
    )
    await expect(page.getByLabel('Time of the daily backup')).toHaveValue(/^\d{2}:\d{2}$/)
    await expect(page.getByLabel('Exports to keep')).toHaveValue(/^\d+$/)

    const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
    expect(overflow).toBeLessThanOrEqual(0)
})

test.describe('in a browser in another time zone than the server', () => {
    test.use({ timezoneId: 'Europe/Berlin' })

    test('names the time zone of the schedule and of the next backup', async ({ page }) => {
        await page.goto('/admin/backups')

        const status = page.locator("[data-part='backup-status']")
        // The schedule is in the time zone of the server, the next backup in that of the browser.
        await expect(status.locator("[data-item='schedule'] [data-part='stat-value']")).toHaveText(
            /^Daily at \d{2}:\d{2} \([^)]+\)$/,
        )
        await expect(status.locator("[data-item='next-run'] [data-part='stat-value']")).toHaveText(
            / GMT\+[12]$/,
        )
        await expect(
            page.locator("[data-field='backup-time'] [data-part='field-hint']"),
        ).toHaveText(/^In the time zone of the server: [^ ]+ \(TZ\)\.$/)
    })
})

test.describe('changes', () => {
    test.beforeEach(({}, testInfo) => {
        test.skip(
            testInfo.project.name !== 'chromium',
            'The backups are changed on one project only.',
        )
    })

    test('exports the database with pg_dump and deletes the export after asking', async ({
        page,
        request,
    }) => {
        const before = new Set((await listBackups(request)).map((backup) => backup.id))
        let created: string | undefined
        try {
            await page.goto('/admin/backups')
            await page.getByRole('button', { name: 'Back up now' }).click()

            // The new export appears once the job has finished and the page loaded it again.
            const items = page.locator("[data-part='backup-item'][data-kind='manual']")
            await expect
                .poll(
                    async () => {
                        const added = (await listBackups(request)).filter(
                            (backup) => !before.has(backup.id),
                        )
                        created = added[0]?.id
                        return added.length
                    },
                    { timeout: 60_000 },
                )
                .toBe(1)
            const backup = (await listBackups(request)).find((entry) => entry.id === created)!
            expect(backup.trigger).toBe('Manual')
            expect(backup.fileName).toMatch(/^wavemp-\d{8}T\d{6}Z-manual(-\d+)?\.dump$/)
            // A custom-format dump of a database with tables is never tiny.
            expect(backup.sizeBytes).toBeGreaterThan(1000)
            await expect(
                page.locator(
                    "[data-part='backup-status'] [data-item='status'] [data-part='badge']",
                ),
            ).toHaveAttribute('data-state', 'succeeded', { timeout: 60_000 })
            const item = items.filter({ hasText: backup.fileName })
            await expect(item).toBeVisible()

            await item.getByRole('button', { name: `Delete ${backup.fileName}` }).click()
            const dialog = page.locator("[data-part='dialog']")
            await expect(dialog.locator("[data-part='dialog-title']")).toHaveText('Delete export')
            await dialog.getByRole('button', { name: 'Delete', exact: true }).click()

            await expect(page.locator("[data-part='alert'][data-state='success']")).toHaveText(
                'The export is deleted.',
            )
            await expect(item).toHaveCount(0)
            expect((await listBackups(request)).map((entry) => entry.id)).not.toContain(created)
            created = undefined
        } finally {
            if (created) {
                await deleteBackup(request, created)
            }
        }
    })

    test('shows the defaults and saves the time and the number of kept exports', async ({
        page,
        request,
    }) => {
        try {
            await page.goto('/admin/backups')
            const time = page.getByLabel('Time of the daily backup')
            // The fields are locked until the settings are loaded, so they keep what is typed.
            await expect(time).toBeEnabled()
            await expect(time).toHaveValue('03:00')
            await expect(page.getByLabel('Exports to keep')).toHaveValue('7')
            await expect(
                page.locator(
                    "[data-part='backup-status'] [data-item='schedule'] [data-part='stat-value']",
                ),
            ).toHaveText(/^Daily at 03:00 \([^)]+\)$/)

            await time.fill('22:15')
            await page.getByLabel('Exports to keep').selectOption('14')
            await page.getByRole('button', { name: 'Save' }).click()

            await expect(page.locator("[data-part='alert'][data-state='success']")).toHaveText(
                'The settings are saved.',
            )
            await expect(
                page.locator(
                    "[data-part='backup-status'] [data-item='schedule'] [data-part='stat-value']",
                ),
            ).toHaveText(/^Daily at 22:15 \([^)]+\)$/)
            await page.reload()
            await expect(page.getByLabel('Time of the daily backup')).toHaveValue('22:15')
            await expect(page.getByLabel('Exports to keep')).toHaveValue('14')
        } finally {
            await resetBackupSettings(request)
        }
    })
})
