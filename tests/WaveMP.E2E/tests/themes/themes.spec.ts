import { existsSync } from 'node:fs'
import { expect, test } from '@playwright/test'
import { checkContrast, formatContrastFailures } from '../../screenshots/contrast'
import {
    createThemeProfile,
    deleteThemeProfile,
    dynamicParts,
    shippedThemeTargets,
    targetKey,
    themeViews,
    visitThemeViews,
    type ThemeProfile,
} from '../../screenshots/themes'
import { cutStatLabels, formatCutLabels } from '../helpers/labels'

// Screenshot and contrast regression of the shipped themes: every theme and variant shows the
// views of screenshots/themes.ts on the desktop and on a phone, with a profile of its own that
// uses the theme. Each view must reach the contrast of WCAG 2.2 AA (screenshots/contrast.ts), show
// the labels of its status tiles in full and match its baseline in tests/themes/baselines.
//
// The baselines are taken in the Playwright image of the CI (fonts and rendering of Linux), so
// the screenshots are compared only there (CI) or when E2E_VISUAL=1 asks for it, and only views
// with a baseline; `npm run test:themes:update` takes the baselines again in that image. The
// contrast check runs everywhere.

const compare = !!process.env.CI || process.env.E2E_VISUAL === '1'

test.describe.configure({ mode: 'parallel' })

for (const target of shippedThemeTargets()) {
    test(`theme ${target.theme} (${target.variant})`, async ({ browser, request }, testInfo) => {
        test.setTimeout(300_000)
        const updating =
            testInfo.config.updateSnapshots === 'all' ||
            testInfo.config.updateSnapshots === 'changed'
        let profile: ThemeProfile | undefined
        try {
            profile = await createThemeProfile(request, target)
            await visitThemeViews(
                browser,
                request,
                target,
                profile,
                themeViews.filter((view) => view.stable),
                async (page, view) => {
                    await expect(page.locator(':root')).toHaveAttribute('data-theme', target.theme)
                    await expect(page.locator(':root')).toHaveAttribute(
                        'data-theme-variant',
                        target.variant,
                    )

                    const contrast = await checkContrast(page)
                    expect
                        .soft(
                            contrast.failures,
                            `Contrast of ${targetKey(target)} on ${view.name}:\n${formatContrastFailures(contrast.failures)}`,
                        )
                        .toEqual([])

                    const cut = await cutStatLabels(page)
                    expect
                        .soft(
                            cut,
                            `Cut off labels of ${targetKey(target)} on ${view.name}:\n${formatCutLabels(cut)}`,
                        )
                        .toEqual([])

                    const name = `${targetKey(target)}-${view.name}.png`
                    if (
                        updating ||
                        (compare &&
                            existsSync(
                                testInfo.snapshotPath(name, {
                                    kind: 'screenshot',
                                }),
                            ))
                    ) {
                        await expect.soft(page).toHaveScreenshot(name, {
                            animations: 'disabled',
                            caret: 'hide',
                            mask: dynamicParts(page),
                            maxDiffPixelRatio: 0.005,
                        })
                    }
                },
            )
        } finally {
            await deleteThemeProfile(request, profile)
        }
    })
}
