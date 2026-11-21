import { expect, type Page, type Request } from '@playwright/test'

/** Whether a request of the page saves the given language for the profile. */
function savesLanguage(request: Request, language: string): boolean {
    return (
        request.method() === 'PATCH' &&
        new URL(request.url()).pathname === '/api/v1/me/settings' &&
        request.postDataJSON()?.language === language
    )
}

/**
 * Chooses a language on the settings page and waits until the server stored it. The page shows the
 * new language at once and saves it in the background, so a test that loads another page right
 * after the choice could otherwise load it before the language is saved. If the language is
 * already chosen, the page saves nothing and there is nothing to wait for.
 */
export async function chooseLanguage(page: Page, language: string): Promise<void> {
    const select = page.locator("[data-field='language'] [data-part='select']")
    if ((await select.inputValue()) === language) {
        return
    }

    const saved = page.waitForResponse((response) => savesLanguage(response.request(), language))
    await select.selectOption(language)
    const response = await saved
    expect(response.ok(), `saving the language ${language}`).toBe(true)
}

/** Whether a request of the page saves the given theme for the profile. */
function savesTheme(request: Request, themeId: string): boolean {
    return (
        request.method() === 'PATCH' &&
        new URL(request.url()).pathname === '/api/v1/me/settings' &&
        request.postDataJSON()?.theme?.id === themeId
    )
}

/**
 * Chooses a theme on the settings page and waits until the server stored it. Like the language,
 * the page shows the theme at once and saves it in the background, so a reload right after the
 * choice could otherwise load the theme the server still has. If the theme is already chosen, the
 * page saves nothing and there is nothing to wait for.
 */
export async function chooseTheme(page: Page, themeId: string): Promise<void> {
    const option = page.locator(`[data-part='theme-option'][data-item='${themeId}']`)
    if ((await option.getAttribute('aria-checked')) === 'true') {
        return
    }

    const saved = page.waitForResponse((response) => savesTheme(response.request(), themeId))
    await option.click()
    const response = await saved
    expect(response.ok(), `saving the theme ${themeId}`).toBe(true)
    await expect(option).toHaveAttribute('aria-checked', 'true')
}

/**
 * Opens the settings page and waits until the app has loaded the settings of the profile again,
 * as it does on every start, so the page shows what the server saved. A choice made before that
 * answer arrives is kept as well (the answer does not replace a setting changed meanwhile).
 */
export async function openSettings(page: Page): Promise<void> {
    const loaded = page.waitForResponse(
        (response) =>
            response.request().method() === 'GET' &&
            new URL(response.url()).pathname === '/api/v1/me/settings',
    )
    await page.goto('/settings')
    expect((await loaded).ok(), 'loading the settings of the profile').toBe(true)
}
