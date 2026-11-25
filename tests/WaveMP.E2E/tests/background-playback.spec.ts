import { expect, test } from '@playwright/test'
import { findTrackId } from './helpers/api'
import {
    controlVisibility,
    expectContinuous,
    hasToken,
    playSong,
    playsOn,
    recordedSamples,
    setHidden,
    startRecording,
    transport,
} from './helpers/background'
import { withoutSavedPlayback } from './helpers/player'

// Playback in the background on the desktop and on Android ("chromium" and "mobile"): only WebKit
// on iPhone and iPad stops the audio graph in the background (tests/webkit), so everywhere else the
// player keeps the song on the audio graph with all effects when the page is hidden, and never
// switches to the plain audio elements. The switch to the background is simulated (see
// helpers/background.ts). "Loose Song" of scripts/generate-media.sh lasts 20 seconds.

test.beforeEach(async ({ page }) => {
    await withoutSavedPlayback(page)
    await controlVisibility(page)
})

test('keeps the audio graph when the page goes to the background', async ({ page, request }) => {
    const id = await findTrackId(request, 'Loose Song')
    await playSong(page, id, 'Loose Song')
    await expect(transport(page)).toHaveAttribute('data-state', /\bplaying\b/)
    await playsOn(page)
    await startRecording(page)

    await setHidden(page, true)
    await playsOn(page)
    await setHidden(page, false)
    await playsOn(page)

    const samples = await recordedSamples(page)
    expect(samples.filter((sample) => hasToken(sample, 'plain-audio'))).toEqual([])
    expect(samples.filter((sample) => !hasToken(sample, 'playing'))).toEqual([])
    expectContinuous(samples)
})

test('shows no note about paused effects in the settings', async ({ page }) => {
    await page.goto('/settings')
    await expect(page.locator("[data-part='app'] [data-field='equalizer']")).toBeVisible()
    await expect(page.locator("[data-part='app'] [data-item='background-effects']")).toHaveCount(0)
})
