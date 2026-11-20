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
