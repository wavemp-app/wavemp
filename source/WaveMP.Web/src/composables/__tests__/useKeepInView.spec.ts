import { afterEach, describe, expect, it } from 'vitest'
import { scrollIntoContainer } from '../useKeepInView'

function box(element: HTMLElement, top: number, height: number): HTMLElement {
    element.getBoundingClientRect = () =>
        ({ top, bottom: top + height, left: 0, right: 100, width: 100, height }) as DOMRect
    return element
}

function setup(overflow: string, top: number, height = 40) {
    const container = box(document.createElement('div'), 100, 200)
    container.style.overflowY = overflow
    container.scrollTop = 50
    const element = box(document.createElement('div'), top, height)
    container.append(element)
    // Only elements in the document have a computed style.
    document.body.append(container)
    return { container, element }
}

describe('scrollIntoContainer', () => {
    afterEach(() => {
        document.body.replaceChildren()
    })

    it('scrolls up to an element above the visible area', () => {
        const { container, element } = setup('auto', 60)
        scrollIntoContainer(container, element)
        expect(container.scrollTop).toBe(10)
    })

    it('scrolls down just far enough to show an element below the visible area', () => {
        const { container, element } = setup('auto', 290)
        scrollIntoContainer(container, element)
        expect(container.scrollTop).toBe(80)
    })

    it('shows the start of an element taller than the container', () => {
        const { container, element } = setup('scroll', 150, 400)
        scrollIntoContainer(container, element)
        expect(container.scrollTop).toBe(100)
    })

    it('leaves a visible element where it is', () => {
        const { container, element } = setup('auto', 150)
        scrollIntoContainer(container, element)
        expect(container.scrollTop).toBe(50)
    })

    it('does not scroll a container that does not scroll itself, or a foreign element', () => {
        const page = setup('visible', 400)
        scrollIntoContainer(page.container, page.element)
        expect(page.container.scrollTop).toBe(50)

        const { container } = setup('auto', 150)
        scrollIntoContainer(container, box(document.createElement('div'), 400, 40))
        expect(container.scrollTop).toBe(50)
    })
})
