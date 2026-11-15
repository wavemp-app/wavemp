import { expect, test, type Page } from '@playwright/test'

// Phones and tablets with their own viewports. The safe areas of an iPhone (status bar at the top,
// home indicator at the bottom) are emulated with the DevTools protocol, so the tests run in the
// project "chromium" only.
test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'Chromium with device viewports')
})

const iPhone = { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true }
const safeArea = { top: 47, bottom: 34, left: 0, right: 0 }

async function emulateSafeArea(page: Page): Promise<void> {
    const cdp = await page.context().newCDPSession(page)
    await cdp.send('Emulation.setSafeAreaInsetsOverride', { insets: safeArea })
}

async function rgb(page: Page, color: string): Promise<string> {
    return page.evaluate((value) => {
        const probe = document.createElement('span')
        probe.style.color = value
        document.body.append(probe)
        const result = getComputedStyle(probe).color
        probe.remove()
        return result
    }, color)
}

test.describe('status bar of an iPhone', () => {
    test.use(iPhone)

    test('lets the page reach under the status bar and colors it like the header', async ({
        page,
    }) => {
        await page.goto('/library/songs')

        await expect(page.locator('meta[name="viewport"]')).toHaveAttribute(
            'content',
            /viewport-fit=cover/,
        )
        await expect(
            page.locator('meta[name="apple-mobile-web-app-status-bar-style"]'),
        ).toHaveAttribute('content', 'black-translucent')

        const themeColor = await page.evaluate(() =>
            getComputedStyle(document.documentElement).getPropertyValue('--wmp-theme-color').trim(),
        )
        expect(themeColor).not.toBe('')
        await expect(page.locator('meta[name="theme-color"]')).toHaveAttribute(
            'content',
            themeColor,
        )

        const expected = await rgb(page, themeColor)
        const backgrounds = await page.evaluate(() => ({
            root: getComputedStyle(document.documentElement).backgroundColor,
            app: getComputedStyle(document.querySelector("[data-part='app']")!).backgroundColor,
        }))
        expect(backgrounds).toEqual({ root: expected, app: expected })
    })

    test('keeps the content out of the safe areas', async ({ page }) => {
        await emulateSafeArea(page)
        await page.goto('/library/songs')
        const header = page.locator("[data-part='app'] > [data-part='header']")
        await expect(header).toBeVisible()

        const layout = await page.evaluate(() => {
            const app = document.querySelector<HTMLElement>("[data-part='app']")!
            const header = app.querySelector("[data-part='header']")!
            const bottomNav = app.querySelector("[data-part='bottom-nav']")!
            const underStatusBar = document.elementFromPoint(window.innerWidth / 2, 20)
            return {
                headerTop: header.getBoundingClientRect().top,
                underStatusBar: underStatusBar === app,
                bottomNavPadding: parseFloat(getComputedStyle(bottomNav).paddingBottom),
                bottomNavEnd: bottomNav.getBoundingClientRect().bottom,
            }
        })

        expect(layout.headerTop).toBe(safeArea.top)
        expect(layout.underStatusBar).toBe(true)
        expect(layout.bottomNavPadding).toBeGreaterThanOrEqual(safeArea.bottom)
        expect(layout.bottomNavEnd).toBeCloseTo(iPhone.viewport.height, 0)
    })

    test('keeps the transport bar of the desktop layout above the home indicator', async ({
        page,
    }) => {
        await page.setViewportSize({ width: 1180, height: 820 })
        await emulateSafeArea(page)
        await page.goto('/library/songs')
        const transport = page.locator("[data-part='app'] > [data-part='transport']")
        await expect(transport).toBeVisible()

        const padding = await transport.evaluate((element) =>
            parseFloat(getComputedStyle(element).paddingBottom),
        )
        expect(padding).toBeGreaterThanOrEqual(safeArea.bottom)
    })
})

const iPadSizes = [
    { name: 'portrait', viewport: { width: 820, height: 1180 } },
    { name: 'landscape', viewport: { width: 1180, height: 820 } },
]

const pages = [
    '/',
    '/library',
    '/library/songs',
    '/library/albums',
    '/playlists',
    '/now-playing',
    '/settings',
    '/admin/library',
    '/admin/jobs',
    '/admin/users',
]

for (const size of iPadSizes) {
    test.describe(`iPad in ${size.name}`, () => {
        test.use({ viewport: size.viewport, isMobile: true, hasTouch: true })

        test('scrolls only the areas, never the page', async ({ page }) => {
            for (const path of pages) {
                await page.goto(path)
                await expect(
                    page.locator("[data-part='app'] > [data-part='content']"),
                ).toBeVisible()
                await expect(page.locator("[data-part='view']").first()).toBeVisible()

                const scrolling = await page.evaluate(() => {
                    const root = document.documentElement
                    const scrollers = [...document.querySelectorAll<HTMLElement>('body *')].filter(
                        (element) => {
                            const style = getComputedStyle(element)
                            return (
                                /auto|scroll/.test(style.overflowX + style.overflowY) &&
                                (element.scrollWidth > element.clientWidth + 1 ||
                                    element.scrollHeight > element.clientHeight + 1)
                            )
                        },
                    )
                    window.scrollTo(200, 200)
                    return {
                        pageWidth: root.scrollWidth - root.clientWidth,
                        pageHeight: root.scrollHeight - root.clientHeight,
                        scrolled: [window.scrollX, window.scrollY],
                        nested: scrollers
                            .filter((inner) =>
                                scrollers.some((outer) => outer !== inner && outer.contains(inner)),
                            )
                            .map((element) => element.dataset.part ?? element.tagName),
                        sideways: scrollers
                            .filter((element) => element.scrollWidth > element.clientWidth + 1)
                            .map((element) => element.dataset.part ?? element.tagName),
                    }
                })

                expect(scrolling, path).toEqual({
                    pageWidth: 0,
                    pageHeight: 0,
                    scrolled: [0, 0],
                    nested: [],
                    sideways: [],
                })
            }
        })
    })
}
