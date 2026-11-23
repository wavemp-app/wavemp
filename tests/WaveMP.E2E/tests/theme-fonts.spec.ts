import { expect, test } from '@playwright/test'
import { sessionHeaders, send } from '../screenshots/helpers'
import { createUser, deleteUser, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The theme Midnight brings its own font, Selawik, so it looks the same on every system: the
// browser loads the WOFF2 files of the theme (font-src 'self') for the weights the app uses and
// draws the app with it, not with a font of the device.

test.use({ storageState: emptyStorageState })

let user: TestUser | undefined

test.afterEach(async ({ request }) => {
    if (user) {
        await deleteUser(request, user.id)
        user = undefined
    }
})

test('draws the theme Midnight with its own font Selawik', async ({ page, request }) => {
    user = await createUser(request, `Midnight Font ${uniqueSuffix()}`)
    const headers = await sessionHeaders(request, user.id)
    await send(request, headers, 'patch', '/api/v1/me/settings', {
        theme: { id: 'midnight', appearance: 'Dark' },
    })

    const failedFonts: string[] = []
    page.on('requestfailed', (failed) => {
        if (failed.url().endsWith('.woff2')) {
            failedFonts.push(failed.url())
        }
    })

    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
    await expect(page.locator(':root')).toHaveAttribute('data-theme', 'midnight')

    const fonts = await page.evaluate(async () => {
        const weights = ['400', '600', '700']
        await Promise.all(weights.map((weight) => document.fonts.load(`${weight} 1rem Selawik`)))
        await document.fonts.ready
        const faces = [...document.fonts].filter(
            (face) => face.family.replace(/"/g, '') === 'Selawik',
        )
        return {
            loaded: weights.filter((weight) =>
                faces.some((face) => face.weight === weight && face.status === 'loaded'),
            ),
            family: getComputedStyle(document.body).fontFamily,
        }
    })

    expect(fonts.loaded).toEqual(['400', '600', '700'])
    expect(fonts.family.split(',')[0]?.trim().replace(/"/g, '')).toBe('Selawik')
    expect(failedFonts).toEqual([])
})

// The theme Classic 9x draws the whole app with its pixel font Pixelify Sans (Regular and Bold).
test('draws the theme Classic 9x with its own font Pixelify Sans', async ({ page, request }) => {
    user = await createUser(request, `Classic Font ${uniqueSuffix()}`)
    const headers = await sessionHeaders(request, user.id)
    await send(request, headers, 'patch', '/api/v1/me/settings', {
        theme: { id: 'classic-9x', appearance: 'Light' },
    })

    const failedFonts: string[] = []
    page.on('requestfailed', (failed) => {
        if (failed.url().endsWith('.woff2')) {
            failedFonts.push(failed.url())
        }
    })

    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
    await expect(page.locator(':root')).toHaveAttribute('data-theme', 'classic-9x')

    const fonts = await page.evaluate(async () => {
        const weights = ['400', '700']
        await Promise.all(
            weights.map((weight) => document.fonts.load(`${weight} 1rem "Pixelify Sans"`)),
        )
        await document.fonts.ready
        const faces = [...document.fonts].filter(
            (face) => face.family.replace(/"/g, '') === 'Pixelify Sans',
        )
        return {
            loaded: weights.filter((weight) =>
                faces.some((face) => face.weight === weight && face.status === 'loaded'),
            ),
            family: getComputedStyle(document.body).fontFamily,
        }
    })

    expect(fonts.loaded).toEqual(['400', '700'])
    expect(fonts.family.split(',')[0]?.trim().replace(/"/g, '')).toBe('Pixelify Sans')
    expect(failedFonts).toEqual([])
})

// The theme Skinned writes only its display in the bitmap face Silkscreen; the rest of the app keeps
// the font of the system.
test('draws the display of the theme Skinned with its own font Silkscreen', async ({
    page,
    request,
}) => {
    user = await createUser(request, `Skinned Font ${uniqueSuffix()}`)
    const headers = await sessionHeaders(request, user.id)
    await send(request, headers, 'patch', '/api/v1/me/settings', {
        theme: { id: 'skinned', appearance: 'Dark' },
    })

    const failedFonts: string[] = []
    page.on('requestfailed', (failed) => {
        if (failed.url().endsWith('.woff2')) {
            failedFonts.push(failed.url())
        }
    })

    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
    await expect(page.locator(':root')).toHaveAttribute('data-theme', 'skinned')
    const title = page.locator("[data-part='transport'] [data-part='now-playing-title']").first()
    await expect(title).toBeAttached()

    const fonts = await page.evaluate(async () => {
        const weights = ['400', '700']
        await Promise.all(weights.map((weight) => document.fonts.load(`${weight} 1rem Silkscreen`)))
        await document.fonts.ready
        const faces = [...document.fonts].filter(
            (face) => face.family.replace(/"/g, '') === 'Silkscreen',
        )
        const display = document.querySelector(
            "[data-part='transport'] [data-part='now-playing-title']",
        )
        return {
            loaded: weights.filter((weight) =>
                faces.some((face) => face.weight === weight && face.status === 'loaded'),
            ),
            display: display ? getComputedStyle(display).fontFamily : '',
            body: getComputedStyle(document.body).fontFamily,
        }
    })

    expect(fonts.loaded).toEqual(['400', '700'])
    expect(fonts.display.split(',')[0]?.trim().replace(/"/g, '')).toBe('Silkscreen')
    expect(fonts.body).not.toContain('Silkscreen')
    expect(failedFonts).toEqual([])
})
