import type { Page } from '@playwright/test'

/**
 * Starts the page with an empty player: the saved playback state of the profile (the queue the
 * player shows again after a reload) is answered as missing. The tests of the built-in admin share
 * one profile and play at the same time, so a test that needs an empty player at the start must
 * not take over what another test saved. Saves of the page still reach the server.
 */
export async function withoutSavedPlayback(page: Page): Promise<void> {
    await page.route('**/api/v1/me/playback-state', (route) =>
        route.request().method() === 'GET'
            ? route.fulfill({
                  status: 404,
                  contentType: 'application/problem+json',
                  body: JSON.stringify({
                      status: 404,
                      code: 'playback.state_not_found',
                  }),
              })
            : route.fallback(),
    )
}
