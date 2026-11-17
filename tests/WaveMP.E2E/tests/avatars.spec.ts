import { expect, test, type Locator, type Page } from '@playwright/test'
import { createUser, deleteUser, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileButton, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// Pictures of the profiles: upload with cropping, a built-in picture and removing it again. The
// test selects its own profile, so it starts without the shared admin session.
test.use({ storageState: emptyStorageState, locale: 'en-US' })

let user: TestUser

test.beforeEach(async ({ request }) => {
    user = await createUser(request, `Ava ${uniqueSuffix()} Picture`)
})

test.afterEach(async ({ request }) => {
    await deleteUser(request, user.id)
})

/** A landscape PNG drawn in the browser: the test brings no picture files of its own. */
async function landscapePng(page: Page): Promise<Buffer> {
    const dataUrl = await page.evaluate(() => {
        const canvas = document.createElement('canvas')
        canvas.width = 480
        canvas.height = 320
        const context = canvas.getContext('2d')!
        const gradient = context.createLinearGradient(0, 0, 480, 320)
        gradient.addColorStop(0, '#1f6fd6')
        gradient.addColorStop(1, '#7fd3ff')
        context.fillStyle = gradient
        context.fillRect(0, 0, 480, 320)
        context.fillStyle = '#ffffff'
        context.beginPath()
        context.arc(240, 160, 90, 0, Math.PI * 2)
        context.fill()
        return canvas.toDataURL('image/png')
    })
    return Buffer.from(dataUrl.slice(dataUrl.indexOf(',') + 1), 'base64')
}

function avatarDialog(page: Page): Locator {
    return page.locator("[data-part='dialog'][data-item='avatar']")
}

async function openAvatarDialog(page: Page): Promise<Locator> {
    await page.goto('/settings')
    await page.locator("[data-item='change-avatar']").click()
    const dialog = avatarDialog(page)
    await expect(dialog).toBeVisible()
    return dialog
}

async function selectProfile(page: Page): Promise<void> {
    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
}

test('uploads a cropped picture, chooses a built-in one and removes it', async ({ page }) => {
    await selectProfile(page)
    await expect(profileButton(page).locator("[data-part='avatar-initials']")).toHaveText('AP')

    // Upload: choose a file, move it in the frame and save.
    let dialog = await openAvatarDialog(page)
    await dialog.locator("[data-item='avatar-mode'] [data-item='upload']").click()
    await dialog.locator("input[type='file']").setInputFiles({
        name: 'avatar.png',
        mimeType: 'image/png',
        buffer: await landscapePng(page),
    })
    const frame = dialog.locator("[data-part='avatar-crop-frame']")
    await expect(frame).toBeVisible()
    await frame.focus()
    await page.keyboard.press('ArrowLeft')
    await dialog.locator("[data-item='save-avatar']").click()
    await expect(dialog).toBeHidden()

    const headerAvatar = profileButton(page).locator("[data-part='avatar']")
    await expect(headerAvatar).toHaveAttribute('data-state', 'image')
    await expect(headerAvatar.locator("[data-part='avatar-image']")).toBeVisible()
    await expect(page.locator("[data-field='avatar-status']")).toHaveText(
        'This profile shows a picture.',
    )

    // The profile selection shows it as well, without a session of its own.
    await page.goto('/profiles')
    await expect(
        profileTile(page, user.name).locator("[data-part='avatar'][data-state='image']"),
    ).toBeVisible()

    // A built-in picture replaces the upload.
    dialog = await openAvatarDialog(page)
    await expect(dialog.locator("[data-part='avatar-editor']")).toHaveAttribute(
        'data-state',
        'upload',
    )
    await dialog.locator("[data-item='avatar-mode'] [data-item='preset']").click()
    await dialog.locator("[data-part='avatar-preset'][data-item='leaf']").click()
    await dialog.locator("[data-item='save-avatar']").click()
    await expect(dialog).toBeHidden()
    dialog = await openAvatarDialog(page)
    await expect(dialog.locator("[data-part='avatar-preset'][data-item='leaf']")).toHaveAttribute(
        'aria-checked',
        'true',
    )

    // Removing it shows the initials again.
    await dialog.locator("[data-item='remove-avatar']").click()
    await expect(dialog).toBeHidden()
    await expect(profileButton(page).locator("[data-part='avatar-initials']")).toHaveText('AP')
    await expect(page.locator("[data-field='avatar-status']")).toHaveText(
        'This profile shows its initials.',
    )
})

test('delivers the picture in the requested size with an ETag', async ({ page, request }) => {
    await selectProfile(page)
    const dialog = await openAvatarDialog(page)
    await dialog.locator("[data-part='avatar-preset'][data-item='sun']").click()
    await dialog.locator("[data-item='save-avatar']").click()
    await expect(dialog).toBeHidden()

    const picture = await request.get(`/api/v1/users/${user.id}/avatar?size=96`)
    expect(picture.status()).toBe(200)
    expect(picture.headers()['content-type']).toBe('image/webp')
    const etag = picture.headers()['etag']
    expect(etag).toBeTruthy()

    const again = await request.get(`/api/v1/users/${user.id}/avatar?size=96`, {
        headers: { 'If-None-Match': etag! },
    })
    expect(again.status()).toBe(304)
})
