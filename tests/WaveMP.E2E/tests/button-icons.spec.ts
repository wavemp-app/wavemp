import { expect, test, type Locator } from '@playwright/test'
import { findAlbumId } from './helpers/api'

// Icons in buttons take the text color of the button unless the theme sets another fill, so they
// stay visible on colored buttons such as the blue "Play" of a detail page.

async function fillOf(button: Locator) {
    return button.evaluate((element) => {
        const glyph = element.querySelector("[data-part='icon'] > [data-part='icon-glyph']")!
        const style = getComputedStyle(glyph)
        return {
            image: style.backgroundImage,
            color: style.backgroundColor,
            text: getComputedStyle(element).color,
        }
    })
}

test('fills the icon of the play button of an album with the text color', async ({
    page,
    request,
}) => {
    await page.goto(`/library/albums/${await findAlbumId(request, 'E2E Album')}`)
    const play = page.locator(
        "[data-part='app'] [data-part='detail-actions'] [data-item='play'][data-variant='primary']",
    )
    await expect(play).toBeVisible()
    await expect(play.locator("[data-part='icon']")).toBeVisible()

    const fill = await fillOf(play)
    expect(fill.image).toBe('none')
    expect(fill.color).toBe(fill.text)
})

test('fills the icons of primary buttons with their text color', async ({ page }) => {
    await page.goto('/playlists')
    const create = page.locator(
        "[data-part='app'] [data-part='view-actions'] [data-item='create-playlist']",
    )
    await expect(create).toBeVisible()

    const fill = await fillOf(create)
    expect(fill.image).toBe('none')
    expect(fill.color).toBe(fill.text)
})
