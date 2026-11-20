import type { Page } from '@playwright/test'

/**
 * Lets the video elements of the page load like in Safari on the iPhone and iPad, which loads the
 * data of a media element only once it is played (it ignores preload there): a source set before
 * the first play waits until the element is played and only then loads. Chromium, the browser of
 * the tests, otherwise loads the metadata of a video at once.
 */
export async function loadVideosOnlyWhenPlayed(page: Page): Promise<void> {
    await page.addInitScript(() => {
        const prototype = HTMLVideoElement.prototype
        const media = HTMLMediaElement.prototype
        const src = Object.getOwnPropertyDescriptor(media, 'src')!
        const { play } = media
        const { getAttribute, removeAttribute } = Element.prototype
        const waiting = new WeakMap<Element, string>()
        const played = new WeakSet<Element>()

        Object.defineProperty(prototype, 'src', {
            configurable: true,
            get(this: HTMLVideoElement) {
                return waiting.get(this) ?? src.get!.call(this)
            },
            set(this: HTMLVideoElement, value: string) {
                if (played.has(this)) {
                    src.set!.call(this, value)
                } else {
                    waiting.set(this, value)
                }
            },
        })
        prototype.play = function (this: HTMLVideoElement) {
            played.add(this)
            const value = waiting.get(this)
            if (value !== undefined) {
                waiting.delete(this)
                src.set!.call(this, value)
            }
            return play.call(this)
        }
        prototype.getAttribute = function (this: HTMLVideoElement, name: string) {
            return name === 'src' && waiting.has(this)
                ? waiting.get(this)!
                : getAttribute.call(this, name)
        }
        prototype.removeAttribute = function (this: HTMLVideoElement, name: string) {
            if (name === 'src') {
                waiting.delete(this)
            }
            removeAttribute.call(this, name)
        }
    })
}
