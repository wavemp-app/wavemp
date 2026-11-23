import { expect, test as setup } from '@playwright/test'
import { builtInAdminTile, homeUrl, navigationLabels } from './helpers/navigation'
import { withoutSavedPlayback } from './helpers/player'
import { chooseLanguage } from './helpers/settings'
import { adminStorageState } from './helpers/storage'

// Selects the built-in admin profile once; the tests reuse its session (storageState).
setup('select the built-in admin profile', async ({ page }) => {
    // The tests of the admin share its saved playback state: the setup starts with an empty player,
    // so it never takes that state over or replaces it.
    await withoutSavedPlayback(page)
    await page.goto('/')
    await expect(page).toHaveURL(/\/profiles$/)

    await builtInAdminTile(page).click()
    await expect(page).toHaveURL(homeUrl)

    // The tests expect English texts, also if a former run changed the language of the profile.
    await page.goto('/settings')
    // The tests that follow load their pages with the stored language, so wait until it is saved.
    await chooseLanguage(page, 'en')
    await expect(page.locator('html')).toHaveAttribute('lang', 'en')
    expect(await navigationLabels(page)).toEqual(['Home', 'Now Playing', 'Library', 'Playlists'])

    await page.context().storageState({ path: adminStorageState })
})
