import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { send, sessionHeaders } from '../screenshots/helpers'
import { createUser, deleteUser, findTrackId, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The recap and the statistics of a profile with a few plays: the story goes on by itself, by
// click and by keyboard; changing the period loads the statistics of the new period; a recap
// shared with the other profiles appears in their list.

test.use({ storageState: emptyStorageState })

/** A profile with three plays of today: First Song twice and Second Song once. */
async function profileWithPlays(request: APIRequestContext, label: string) {
    const user = await createUser(request, `Recap ${label} ${uniqueSuffix()}`)
    const headers = await sessionHeaders(request, user.id)
    await send(request, headers, 'patch', '/api/v1/me/settings', { language: 'en' })
    const first = await findTrackId(request, 'First Song')
    const second = await findTrackId(request, 'Second Song')
    for (const trackId of [first, second, first]) {
        await send(request, headers, 'post', '/api/v1/me/history', { trackId })
    }
    return { user, headers }
}

async function selectProfile(page: Page, user: TestUser): Promise<void> {
    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
}

/** The next answer of the statistics; with a last day, the answer for that day only. */
function statisticsRequest(page: Page, to?: string) {
    return page.waitForResponse((response) => {
        const url = new URL(response.url())
        return (
            url.pathname === '/api/v1/me/stats' &&
            response.request().method() === 'GET' &&
            (to === undefined || url.searchParams.get('to') === to)
        )
    })
}

test.describe('recap', () => {
    let users: TestUser[] = []

    test.afterEach(async ({ request }) => {
        for (const user of users) {
            await deleteUser(request, user.id)
        }
        users = []
    })

    test('tells the story of the last month', async ({ page, request }) => {
        const { user } = await profileWithPlays(request, 'Story')
        users.push(user)
        await selectProfile(page, user)

        const loaded = statisticsRequest(page)
        await page.goto('/recap')
        expect((await loaded).status()).toBe(200)

        const view = page.locator("[data-view='recap']")
        await expect(view).toHaveAttribute('data-state', 'ready')
        const story = view.locator("[data-part='recap-story']")
        await expect(story).toHaveAttribute('data-recap-design', 'aurora')
        const slide = story.locator("[data-part='recap-slide']")
        await expect(slide).toHaveAttribute('data-kind', 'intro')
        await expect(slide.locator("[data-part='recap-figure']")).toHaveText('Last month')

        // Pause, so the story waits for the clicks below.
        await story.locator("[data-part='btn-recap-pause']").click()
        await expect(story).toHaveAttribute('data-state', 'paused')

        await story.locator("[data-part='btn-recap-next']").click()
        await expect(slide).toHaveAttribute('data-kind', 'listening-time')
        await expect(slide.locator("[data-part='recap-fact'][data-item='plays']")).toContainText(
            '3',
        )

        await story.locator("[data-part='btn-recap-next']").click()
        await expect(slide).toHaveAttribute('data-kind', 'top-track')
        await expect(slide.locator("[data-part='recap-figure']")).toHaveText('First Song')
        await expect(slide.locator("[data-part='recap-caption']")).toHaveText('2 plays')

        await story.focus()
        await page.keyboard.press('ArrowLeft')
        await expect(slide).toHaveAttribute('data-kind', 'listening-time')

        // The progress shows one segment per section, the current one active.
        const segments = story.locator("[data-part='recap-progress-segment']")
        expect(await segments.count()).toBeGreaterThan(5)
        await expect(segments.nth(1)).toHaveAttribute('data-state', 'active')
        await expect(segments.nth(0)).toHaveAttribute('data-state', 'done')

        // Another design changes the colors of the story only.
        await view.locator("[data-part='segment'][data-item='prism']").click()
        await expect(story).toHaveAttribute('data-recap-design', 'prism')
        await expect(story).toHaveAttribute('data-decoration', 'bands')

        // The picture of the shown section can be saved.
        const download = page.waitForEvent('download')
        await view.locator("[data-part='button'][data-item='save-picture']").click()
        expect((await download).suggestedFilename()).toBe(
            'wavemp-recap-last-month-listening-time.png',
        )
    })

    test('loads the statistics of another period', async ({ page, request }) => {
        const { user } = await profileWithPlays(request, 'Period')
        users.push(user)
        await selectProfile(page, user)

        const loaded = statisticsRequest(page)
        await page.goto('/recap/statistics')
        const first = new URL((await loaded).url())
        expect(first.searchParams.get('from')).not.toBeNull()

        const view = page.locator("[data-view='statistics']")
        await expect(view).toHaveAttribute('data-state', 'ready')
        await expect(view.locator("[data-part='stat-item'][data-item='plays']")).toContainText('3')
        await expect(
            view.locator("[data-item='top-tracks'] [data-part='stats-table-row']"),
        ).toHaveCount(3)

        // All time: the API finds the first day itself.
        const allTime = statisticsRequest(page)
        await view.locator("[data-part='segment'][data-item='all-time']").click()
        const second = new URL((await allTime).url())
        expect(second.searchParams.get('from')).toBeNull()
        await expect(view.locator("[data-part='segment'][data-item='all-time']")).toHaveAttribute(
            'aria-checked',
            'true',
        )
        await expect(view).toHaveAttribute('data-state', 'ready')

        // A custom period that ends before it starts is not asked for.
        await view.locator("[data-part='segment'][data-item='custom']").click()
        const custom = view.locator("[data-item='custom-period']")
        await expect(custom).toBeVisible()
        await custom.locator("[data-field='from'] input").fill('2020-05-10')
        await custom.locator("[data-field='to'] input").fill('2020-05-01')
        await expect(view).toHaveAttribute('data-state', 'invalid')

        // A custom period without plays shows the empty state.
        const empty = statisticsRequest(page, '2020-05-20')
        await custom.locator("[data-field='to'] input").fill('2020-05-20')
        const third = new URL((await empty).url())
        expect(third.searchParams.get('from')).toBe('2020-05-10')
        expect(third.searchParams.get('to')).toBe('2020-05-20')
        await expect(view.locator("[data-item='statistics-empty']")).toBeVisible()

        // The recap shows the same period.
        await page.locator("[data-part='recap-nav-item'][data-item='recap']").click()
        await expect(
            page.locator("[data-view='recap'] [data-part='segment'][data-item='custom']"),
        ).toHaveAttribute('aria-checked', 'true')
        await expect(page.locator("[data-part='recap-slide']")).toHaveAttribute(
            'data-kind',
            'intro',
        )
    })

    test('is shared with the other profiles on request', async ({ page, request }) => {
        const { user } = await profileWithPlays(request, 'Owner')
        users.push(user)
        const viewer = await createUser(request, `Recap Viewer ${uniqueSuffix()}`)
        users.push(viewer)
        const viewerHeaders = await sessionHeaders(request, viewer.id)
        await selectProfile(page, user)

        await page.goto('/recap')
        await expect(page.locator("[data-view='recap']")).toHaveAttribute('data-state', 'ready')
        const toggle = page.locator("[data-field='share-with-profiles'] input")
        await expect(toggle).not.toBeChecked()
        await expect(toggle).toBeEnabled()
        await toggle.check()
        await expect(toggle).toBeChecked()

        // The other profile receives it as soon as the share is saved.
        await expect
            .poll(async () => {
                const received = await request.get('/api/v1/recap-shares?scope=Received', {
                    headers: viewerHeaders,
                })
                expect(received.status()).toBe(200)
                const shares = (await received.json()).items as { owner: { name: string } }[]
                return shares.map((share) => share.owner.name)
            })
            .toContain(user.name)

        await page.locator("[data-part='recap-nav-item'][data-item='shared-recaps']").click()
        const own = page.locator("[data-item='own'] [data-part='recap-share-item']")
        await expect(own).toHaveCount(1)
        await own.locator("[data-part='button'][data-item='stop-sharing']").click()
        await expect(page.locator("[data-item='own-empty']")).toBeVisible()
    })
})
