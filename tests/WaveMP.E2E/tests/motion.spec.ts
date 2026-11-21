import { expect, test, type Page } from '@playwright/test'
import { homeUrl } from './helpers/navigation'
import { withoutSavedPlayback } from './helpers/player'

// The motion system (THEMING.md, "Motion"): subtle, fast transitions with the --wmp-motion-*
// variables for hover and press, the indicator of groups, menus, dialogs, the side navigation,
// pages and lists; no movement but short fades with prefers-reduced-motion. Nothing is changed in
// the library.

const openMenu = "[data-part='context-menu']:popover-open:not([data-kind='submenu'])"

/** Seconds of a CSS time, e.g. "0.18s" or "180ms". */
function seconds(value: string): number {
    const time = value.trim()
    return time.endsWith('ms') ? Number.parseFloat(time) / 1000 : Number.parseFloat(time)
}

async function rootVariable(page: Page, name: string): Promise<string> {
    return page.evaluate(
        (variable) => getComputedStyle(document.documentElement).getPropertyValue(variable).trim(),
        name,
    )
}

/** Longest duration of the running animations and transitions of the elements of a selector. */
async function runningDurations(page: Page, selector: string): Promise<number[]> {
    return page.evaluate(
        (target) =>
            document
                .getAnimations()
                .filter((animation) => {
                    const effect = animation.effect as KeyframeEffect | null
                    const element = effect?.target
                    return element instanceof Element && element.matches(target)
                })
                .map((animation) => Number(animation.effect?.getTiming().duration ?? 0)),
        selector,
    )
}

/** Records the CSS animations that start on elements of a selector from now on. */
async function recordAnimations(page: Page, selector: string): Promise<void> {
    await page.evaluate((target) => {
        const seen: string[] = []
        ;(window as unknown as { motionSeen: string[] }).motionSeen = seen
        document.addEventListener(
            'animationstart',
            (event) => {
                const element = event.target
                if (element instanceof Element && element.matches(target)) {
                    const name = element.hasAttribute('data-view')
                        ? 'view'
                        : element.getAttribute('data-part')
                    seen.push(`${name}:${event.animationName}`)
                }
            },
            true,
        )
    }, selector)
}

/** Properties of the context menu that started a transition, sorted. */
async function menuTransitions(page: Page): Promise<string[]> {
    return page.evaluate(() =>
        [...(window as unknown as { menuSeen: Set<string> }).menuSeen]
            .filter((name) => name === 'opacity' || name === 'scale')
            .sort(),
    )
}

async function recordedAnimations(page: Page): Promise<string[]> {
    return page.evaluate(() => (window as unknown as { motionSeen: string[] }).motionSeen)
}

test.beforeEach(async ({ page }) => {
    await withoutSavedPlayback(page)
})

test('defines short durations and easings of the motion system', async ({ page }) => {
    await page.goto('/')
    for (const [name, min, max] of [
        ['--wmp-motion-duration-fast', 0.1, 0.25],
        ['--wmp-motion-duration-medium', 0.12, 0.25],
        ['--wmp-motion-duration-slow', 0.12, 0.25],
        ['--wmp-motion-duration-fade', 0.05, 0.15],
    ] as const) {
        const value = seconds(await rootVariable(page, name))
        expect(value, name).toBeGreaterThanOrEqual(min)
        expect(value, name).toBeLessThanOrEqual(max)
    }
    for (const name of [
        '--wmp-motion-easing-standard',
        '--wmp-motion-easing-enter',
        '--wmp-motion-easing-exit',
    ]) {
        expect(await rootVariable(page, name), name).toMatch(/^cubic-bezier\(/)
    }
    expect(Number(await rootVariable(page, '--wmp-motion-scale'))).toBeLessThan(1)
    expect(Number(await rootVariable(page, '--wmp-motion-press-scale'))).toBeLessThan(1)
})

test.describe('on the desktop', () => {
    test.beforeEach(({}, testInfo) => {
        test.skip(testInfo.project.name !== 'chromium', 'Pointer and window of the desktop')
    })

    test('fades a page and the entries of its list in', async ({ page }) => {
        await page.goto('/library/songs')
        await expect(page.locator("[data-part='list-row']").first()).toBeVisible()
        await recordAnimations(page, "[data-part='content'] > [data-view], [data-part='tile']")

        // To another page and back with the tabs of the header.
        const tabs = page.locator("[data-part='header'] [data-part='tab']")
        await tabs.first().click()
        await expect(page).toHaveURL(homeUrl)
        await expect(page.locator("[data-part='tile']").first()).toBeVisible()

        await expect
            .poll(() => recordedAnimations(page))
            .toEqual(expect.arrayContaining(['view:wmp-motion-fade-in', 'tile:wmp-motion-fade-in']))
    })

    test('grows a menu out of the point it opens at and fades it out', async ({ page }) => {
        await page.goto('/library/albums')
        const tile = page.locator("[data-part='tile']").first()
        await expect(tile).toBeVisible()
        const box = (await tile.boundingBox())!
        await page.evaluate(() => {
            const seen = new Set<string>()
            ;(window as unknown as { menuSeen: Set<string> }).menuSeen = seen
            document.addEventListener(
                'transitionrun',
                (event) => {
                    const element = event.target
                    if (
                        element instanceof Element &&
                        element.matches("[data-part='context-menu']")
                    ) {
                        seen.add(event.propertyName)
                    }
                },
                true,
            )
        })
        await page.mouse.click(box.x + 20, box.y + 20, { button: 'right' })

        const menu = page.locator(openMenu)
        await expect(menu).toBeVisible()
        // Opening: the menu grows from a little smaller and fades in, at the point of the click.
        await expect
            .poll(() => menu.evaluate((element) => getComputedStyle(element).transitionDuration))
            .toBe('0.18s')
        await expect.poll(() => menuTransitions(page)).toEqual(['opacity', 'scale'])
        await expect
            .poll(() => menu.evaluate((element) => getComputedStyle(element).scale))
            .toBe('none')
        await expect
            .poll(() => menu.evaluate((element) => getComputedStyle(element).opacity))
            .toBe('1')
        const placed = (await menu.boundingBox())!
        expect(Math.abs(placed.x - (box.x + 20))).toBeLessThanOrEqual(1)
        expect(Math.abs(placed.y - (box.y + 20))).toBeLessThanOrEqual(1)

        // Closing: it fades out faster and takes no pointer meanwhile.
        await page.keyboard.press('Escape')
        const closing = page.getByRole('menu', { name: 'Album actions', includeHidden: true })
        expect(
            await closing.evaluate((element) => {
                const style = getComputedStyle(element)
                return [style.transitionDuration, style.pointerEvents]
            }),
        ).toEqual(['0.12s', 'none'])
        await expect(closing).toBeHidden()
    })

    test('grows a dialog out of the middle and fades its backdrop', async ({ page }) => {
        await page.goto('/library/albums')
        await expect(page.locator("[data-part='tile']").first()).toBeVisible()
        await page.evaluate(() => {
            const seen: string[] = []
            ;(window as unknown as { dialogSeen: string[] }).dialogSeen = seen
            document.addEventListener(
                'transitionrun',
                (event) => {
                    if (
                        event.target instanceof Element &&
                        event.target.matches("[data-part='dialog']")
                    ) {
                        seen.push(`${event.pseudoElement || 'dialog'}:${event.propertyName}`)
                    }
                },
                true,
            )
        })
        await page.keyboard.press('?')
        const dialog = page.locator("[data-part='dialog'][open]")
        await expect(dialog).toBeVisible()
        await expect
            .poll(() =>
                page.evaluate(() => (window as unknown as { dialogSeen: string[] }).dialogSeen),
            )
            .toEqual(
                expect.arrayContaining(['dialog:opacity', 'dialog:scale', '::backdrop:opacity']),
            )
        expect(
            await dialog.evaluate((element) => getComputedStyle(element).transitionDuration),
        ).toBe('0.25s')
        await page.keyboard.press('Escape')
        await expect(dialog).toHaveCount(0)
    })

    test('slides the indicator of a segmented control while it is used', async ({ page }) => {
        await page.goto('/settings')
        const group = page.locator("[data-part='segmented-control']").first()
        await expect(group).toBeVisible()
        const indicator = group.locator("> [data-part='indicator']")
        // At rest, the indicator follows the layout at once.
        await page.mouse.move(0, 0)
        expect(
            await indicator.evaluate((element) => getComputedStyle(element).transitionDuration),
        ).toBe('0s')

        await group.hover()
        expect(
            await indicator.evaluate((element) => {
                const style = getComputedStyle(element)
                return [style.transitionProperty, style.transitionDuration]
            }),
        ).toEqual(['left, top, inline-size, block-size, opacity', '0.18s'])
    })

    test('lets a pressed tile give way a little', async ({ page }) => {
        await page.goto('/library/albums')
        const button = page.locator("[data-part='app'] [data-part='tile']").first()
        await expect(button).toBeVisible()
        const box = (await button.boundingBox())!
        await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2)
        await page.mouse.down()
        await expect
            .poll(() => button.evaluate((element) => getComputedStyle(element).scale))
            .toBe('0.97')
        await page.mouse.move(0, 0)
        await page.mouse.up()
    })

    test('lets a theme switch the motion off with durations of 0s', async ({ page }) => {
        await page.goto('/library/albums')
        await page.addStyleTag({
            content: `:root {
                --wmp-motion-duration-fast: 0s;
                --wmp-motion-duration-medium: 0s;
                --wmp-motion-duration-slow: 0s;
            }`,
        })
        const tile = page.locator("[data-part='tile']").first()
        await expect(tile).toBeVisible()
        await tile.click({ button: 'right' })
        const menu = page.locator(openMenu)
        await expect(menu).toBeVisible()
        expect(await runningDurations(page, "[data-part='context-menu']")).toEqual([])
        await page.keyboard.press('Escape')
        await expect(menu).toHaveCount(0)
    })
})

test.describe('with reduced motion', () => {
    test.use({ reducedMotion: 'reduce' })

    test('moves nothing and keeps only short fades, whatever the theme sets', async ({
        page,
    }, testInfo) => {
        test.skip(testInfo.project.name !== 'chromium', 'One run is enough')
        await page.goto('/library/albums')
        // A theme cannot bring the movement back.
        await page.addStyleTag({
            content: ':root { --wmp-motion-scale: 0.5; --wmp-motion-press-scale: 0.5; }',
        })
        expect(await rootVariable(page, '--wmp-motion-scale')).toBe('1')
        expect(await rootVariable(page, '--wmp-motion-press-scale')).toBe('1')

        // Entries of lists and buttons change at once.
        const tile = page.locator("[data-part='tile']").first()
        await expect(tile).toBeVisible()
        expect(
            await tile.evaluate((element) => {
                const style = getComputedStyle(element)
                return [style.transitionDuration, style.animationDuration]
            }),
        ).toEqual(['1e-05s', '1e-05s'])

        // Menus and pages only fade, briefly.
        await tile.click({ button: 'right' })
        const menu = page.locator(openMenu)
        await expect(menu).toBeVisible()
        expect(
            await menu.evaluate((element) => {
                const style = getComputedStyle(element)
                return [style.transitionDuration, style.scale]
            }),
        ).toEqual(['0.1s', '1'])
        expect(
            await page
                .locator("[data-part='content'] > [data-view]")
                .evaluate((element) => getComputedStyle(element).animationDuration),
        ).toBe('0.1s')
        await page.keyboard.press('Escape')
    })
})

test.describe('on a phone', () => {
    test.beforeEach(({}, testInfo) => {
        test.skip(testInfo.project.name !== 'mobile', 'Drawer of the phone')
    })

    test('slides the drawer in from its edge and fades its backdrop', async ({ page }) => {
        await page.goto('/library/albums')
        const sidebar = page.locator("[data-part='app'] > [data-part='sidebar']")
        await page.locator("[data-part='btn-menu']").click()
        await expect(sidebar).toHaveAttribute('data-state', /\bopen\b/)
        expect(
            await sidebar.evaluate((element) => getComputedStyle(element).transitionDuration),
        ).toMatch(/^0\.25s/)
        const backdrop = page.locator("[data-part='app'] > [data-part='sidebar-backdrop']")
        await expect
            .poll(() => backdrop.evaluate((element) => getComputedStyle(element).opacity))
            .toBe('1')
        await expect.poll(async () => (await sidebar.boundingBox())?.x).toBe(0)
    })
})
