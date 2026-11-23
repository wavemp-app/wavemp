import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import type { Track } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import type { StageView } from '@/player/videoSync'
import { visualizationDependencies } from '@/visualization/modules'
import { defaultVisualizationSettings, type VisualizationSettings } from '@/visualization/settings'
import { FakeClock, FakeModule, fakeAnalyser } from '@/visualization/__tests__/fakes'
import NowPlayingStage from '../NowPlayingStage.vue'
import { minimumRoomAbove, presetMenuPlacement } from '../visualizationControls'

vi.mock('@/library/api', () => ({ fetchAlbumCover: vi.fn(), fetchTrackCover: vi.fn() }))

// The controls of the visualization on the stage of Now Playing (VisualizationControls), with the
// visualization drawing a fake module with the presets A, B and C.

// jsdom has no Popover API; the stand-in shows a popover by taking its attribute away.
beforeAll(() => {
    HTMLElement.prototype.showPopover = function (this: HTMLElement) {
        this.dataset.popover = this.getAttribute('popover') ?? 'auto'
        this.removeAttribute('popover')
    }
    HTMLElement.prototype.hidePopover = function (this: HTMLElement) {
        if (this.dataset.popover) {
            this.setAttribute('popover', this.dataset.popover)
            delete this.dataset.popover
        }
    }
})

afterAll(() => {
    // @ts-expect-error The stand-ins are removed again.
    delete HTMLElement.prototype.showPopover
    // @ts-expect-error The stand-ins are removed again.
    delete HTMLElement.prototype.hidePopover
})

const song: Track = {
    id: 'a',
    title: 'A',
    hasAudio: true,
    hasVideo: false,
    hasCover: false,
    artist: { id: 'band', name: 'Band' },
}

describe('VisualizationControls', () => {
    const original = { ...visualizationDependencies }
    let module: FakeModule

    beforeEach(() => {
        module = new FakeModule()
        visualizationDependencies.module = () => module
        visualizationDependencies.clock = new FakeClock()
        visualizationDependencies.devicePixelRatio = () => 1
    })

    afterEach(() => {
        Object.assign(visualizationDependencies, original)
        document.body.innerHTML = ''
    })

    function mountStage(view: StageView, visualization: Partial<VisualizationSettings> = {}) {
        return mount(NowPlayingStage, {
            props: {
                track: song,
                mode: view === 'visualization' ? 'Visualization' : 'Cover',
                view,
                audio: fakeAnalyser(),
                visualization: { ...defaultVisualizationSettings, ...visualization },
            },
            global: { plugins: [createPinia(), createWaveMpI18n('en')] },
            attachTo: document.body,
        })
    }

    type Wrapper = ReturnType<typeof mountStage>

    async function loaded(visualization: Partial<VisualizationSettings> = {}) {
        const wrapper = mountStage('visualization', visualization)
        await module.finishLoad()
        await flushPromises()
        return wrapper
    }

    function controls(wrapper: Wrapper) {
        return wrapper.get('[data-part="visualization-controls"]')
    }

    function presetMenu(): HTMLElement {
        return document.querySelector(
            '[data-part="context-menu"][data-kind="visualization-presets"]',
        ) as HTMLElement
    }

    /** Places the button of the list at a height of the viewport and records scrolling into view. */
    function placePicker(wrapper: Wrapper, top: number) {
        const picker = wrapper.get('[data-part="btn-preset-picker"]').element as HTMLElement
        picker.getBoundingClientRect = () =>
            ({ left: 40, top, right: 340, bottom: top + 32, width: 300, height: 32 }) as DOMRect
        const scrolled = vi.fn()
        picker.scrollIntoView = scrolled
        return scrolled
    }

    function entry(id: string): HTMLButtonElement {
        return presetMenu().querySelector(`[data-item="${id}"]`) as HTMLButtonElement
    }

    it('lies below the views of the stage, not on the picture, only with the visualization', async () => {
        const wrapper = await loaded()

        const toolbar = controls(wrapper)
        expect(toolbar.attributes('role')).toBe('group')
        expect(toolbar.attributes('aria-label')).toBe('Visualization')
        expect(toolbar.element.previousElementSibling?.getAttribute('data-part')).toBe(
            'stage-views',
        )
        expect(
            wrapper
                .find('[data-part="visualization"] [data-part="visualization-controls"]')
                .exists(),
        ).toBe(false)
        // The buttons in their fixed order.
        expect(
            toolbar
                .findAll(':scope > [data-part]')
                .map((element) => element.attributes('data-part')),
        ).toEqual([
            'btn-preset-previous',
            'btn-preset-picker',
            'btn-preset-next',
            'btn-auto-change',
            'btn-fullscreen',
            'segmented-control',
        ])
        // The presets lie beside the toolbar, so they take nothing over from its look.
        expect(toolbar.element.nextElementSibling?.getAttribute('data-kind')).toBe(
            'visualization-presets',
        )

        await wrapper.setProps({ view: 'cover', mode: 'Cover' })
        expect(wrapper.find('[data-part="visualization-controls"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('disables the preset controls until the presets are loaded', async () => {
        const wrapper = mountStage('visualization')
        await flushPromises()

        expect(controls(wrapper).attributes('data-state')).toBe('loading')
        for (const name of ['btn-preset-previous', 'btn-preset-picker', 'btn-preset-next']) {
            expect(wrapper.get(`[data-part="${name}"]`).attributes('disabled')).toBeDefined()
        }

        await module.finishLoad()
        await flushPromises()
        expect(controls(wrapper).attributes('data-state')).toBe('ready')
        expect(
            wrapper.get('[data-part="btn-preset-picker"]').attributes('disabled'),
        ).toBeUndefined()
        wrapper.unmount()
    })

    it('names the shown preset on the button of the list', async () => {
        const wrapper = await loaded({ preset: 'B' })

        const picker = wrapper.get('[data-part="btn-preset-picker"]')
        expect(picker.get('[data-part="label"]').text()).toBe('B')
        expect(picker.attributes('aria-label')).toBe('Preset: B')
        expect(picker.attributes('aria-haspopup')).toBe('menu')
        expect(picker.attributes('aria-expanded')).toBe('false')
        expect(picker.attributes('data-state')).toBe('closed')
        expect(picker.find('[data-icon="dropdown"]').exists()).toBe(true)
        wrapper.unmount()
    })

    it('steps to the previous and the next preset and hands them to the profile', async () => {
        const wrapper = await loaded({ preset: 'A' })

        await wrapper.get('[data-part="btn-preset-next"]').trigger('click')
        await wrapper.get('[data-part="btn-preset-previous"]').trigger('click')
        await wrapper.get('[data-part="btn-preset-previous"]').trigger('click')

        expect(wrapper.emitted('updateVisualization')).toEqual([
            [{ preset: 'B' }],
            [{ preset: 'A' }],
            [{ preset: 'C' }],
        ])
        expect(module.renderer?.showPreset).toHaveBeenLastCalledWith('C', 1)
        expect(wrapper.get('[data-part="btn-preset-picker"] [data-part="label"]').text()).toBe('C')
        wrapper.unmount()
    })

    it('opens the presets above the button with the shown one chosen and shows a chosen one', async () => {
        const wrapper = await loaded({ preset: 'B' })
        const picker = wrapper.get('[data-part="btn-preset-picker"]')
        const scrolled = placePicker(wrapper, 600)

        await picker.trigger('click')
        await flushPromises()

        const menu = presetMenu()
        expect(menu.getAttribute('data-state')).toBe('open')
        expect(menu.getAttribute('data-placement')).toBe('above')
        expect(menu.getAttribute('aria-label')).toBe('Presets')
        expect(picker.attributes('aria-expanded')).toBe('true')
        expect(picker.attributes('data-state')).toBe('open')
        expect(
            [...menu.querySelectorAll('[data-part="menu-item"]')].map((item) => [
                item.textContent,
                item.getAttribute('role'),
                item.getAttribute('aria-checked'),
            ]),
        ).toEqual([
            ['A', 'menuitemradio', 'false'],
            ['B', 'menuitemradio', 'true'],
            ['C', 'menuitemradio', 'false'],
        ])
        expect(document.activeElement).toBe(entry('B'))
        expect(menu.getAttribute('style')).toContain('--wmp-menu-anchor-width: 300px')
        expect(scrolled).not.toHaveBeenCalled()

        entry('C').click()
        await flushPromises()

        expect(wrapper.emitted('updateVisualization')).toEqual([[{ preset: 'C' }]])
        expect(module.renderer?.showPreset).toHaveBeenLastCalledWith('C', 1)
        expect(menu.getAttribute('data-state')).toBe('closed')
        wrapper.unmount()
    })

    it('brings the button down first when there is too little room above it', async () => {
        const wrapper = await loaded({ preset: 'B' })
        const scrolled = placePicker(wrapper, 100)

        await wrapper.get('[data-part="btn-preset-picker"]').trigger('click')
        await flushPromises()

        expect(scrolled).toHaveBeenCalledWith({ block: 'end', inline: 'nearest' })
        expect(presetMenu().getAttribute('data-placement')).toBe('above')
        wrapper.unmount()
    })

    it('closes the presets when the button is used again', async () => {
        const wrapper = await loaded({ preset: 'B' })
        placePicker(wrapper, 600)
        const picker = wrapper.get('[data-part="btn-preset-picker"]')

        await picker.trigger('click')
        await flushPromises()
        await picker.trigger('click')
        await flushPromises()

        expect(presetMenu().getAttribute('data-state')).toBe('closed')
        expect(picker.attributes('aria-expanded')).toBe('false')
        wrapper.unmount()
    })

    it('does nothing when the shown preset is chosen again', async () => {
        const wrapper = await loaded({ preset: 'B' })
        placePicker(wrapper, 600)

        await wrapper.get('[data-part="btn-preset-picker"]').trigger('click')
        await flushPromises()
        entry('B').click()
        await flushPromises()

        expect(wrapper.emitted('updateVisualization')).toBeUndefined()
        wrapper.unmount()
    })

    it('turns the automatic change on and off', async () => {
        const wrapper = await loaded()
        const button = wrapper.get('[data-part="btn-auto-change"]')

        expect(button.attributes('aria-pressed')).toBe('true')
        expect(button.attributes('data-state')).toBe('on')
        await button.trigger('click')
        expect(wrapper.emitted('updateVisualization')).toEqual([[{ autoChange: false }]])

        await wrapper.setProps({
            visualization: { ...defaultVisualizationSettings, autoChange: false },
        })
        expect(button.attributes('aria-pressed')).toBe('false')
        expect(button.attributes('data-state')).toBe('off')
        await button.trigger('click')
        expect(wrapper.emitted('updateVisualization')?.[1]).toEqual([{ autoChange: true }])
        wrapper.unmount()
    })

    it('offers the modules as a segmented control and hands a choice to the profile', async () => {
        const wrapper = await loaded()
        const group = wrapper.get(
            '[data-part="visualization-controls"] > [data-part="segmented-control"]',
        )
        const segments = group.findAll('[data-part="segment"]')

        expect(group.attributes('role')).toBe('radiogroup')
        expect(group.attributes('data-item')).toBe('visualization-module')
        expect(segments.map((segment) => segment.attributes('data-item'))).toEqual([
            'milkdrop',
            'aforce',
            'blackcap',
        ])
        expect(segments.map((segment) => segment.text())).toEqual([
            'Milkdrop',
            'A-Force',
            'BlackCap',
        ])
        expect(segments[0]!.attributes('aria-checked')).toBe('true')

        await segments[0]!.trigger('click')
        expect(wrapper.emitted('updateVisualization')).toBeUndefined()
        await segments[1]!.trigger('click')
        await segments[2]!.trigger('click')
        expect(wrapper.emitted('updateVisualization')).toEqual([
            [{ module: 'AForce' }],
            [{ module: 'BlackCap' }],
        ])
        wrapper.unmount()
    })

    describe('with color schemes', () => {
        function schemeMenu(): HTMLElement | null {
            return document.querySelector(
                '[data-part="context-menu"][data-kind="visualization-color-schemes"]',
            )
        }

        beforeEach(() => {
            module.colorSchemes = ['Red', 'Green', 'Blue'].map((name) => ({
                id: name.toLowerCase(),
                name,
            }))
        })

        it('offers them in a second list beside the module choice', async () => {
            const wrapper = await loaded({ colorScheme: 'green' })
            const toolbar = controls(wrapper)

            expect(toolbar.attributes('data-state')).toBe('ready has-color-schemes')
            expect(
                toolbar
                    .findAll(':scope > [data-part]')
                    .map((element) => element.attributes('data-part')),
            ).toEqual([
                'btn-preset-previous',
                'btn-preset-picker',
                'btn-preset-next',
                'btn-auto-change',
                'btn-fullscreen',
                'btn-color-scheme-picker',
                'segmented-control',
            ])
            const picker = wrapper.get('[data-part="btn-color-scheme-picker"]')
            expect(picker.get('[data-part="icon"]').attributes('data-icon')).toBe('color-scheme')
            expect(picker.get('[data-part="label"]').text()).toBe('Green')
            expect(picker.attributes('aria-label')).toBe('Color scheme: Green')
            expect(picker.attributes('title')).toBe('Color scheme')
            expect(picker.attributes('aria-haspopup')).toBe('menu')
            expect(picker.attributes('data-state')).toBe('closed')
            expect(schemeMenu()?.getAttribute('aria-label')).toBe('Color schemes')
            wrapper.unmount()
        })

        it('opens the color schemes above their button and hands a choice to the profile', async () => {
            const wrapper = await loaded({ colorScheme: 'green' })
            const picker = wrapper.get('[data-part="btn-color-scheme-picker"]')
            const element = picker.element as HTMLElement
            element.getBoundingClientRect = () =>
                ({
                    left: 400,
                    top: 600,
                    right: 520,
                    bottom: 632,
                    width: 120,
                    height: 32,
                }) as DOMRect

            await picker.trigger('click')
            await flushPromises()

            const menu = schemeMenu()!
            expect(menu.getAttribute('data-state')).toBe('open')
            expect(menu.getAttribute('data-placement')).toBe('above')
            expect(picker.attributes('aria-expanded')).toBe('true')
            expect(
                [...menu.querySelectorAll('[data-part="menu-item"]')].map((item) => [
                    item.textContent,
                    item.getAttribute('aria-checked'),
                ]),
            ).toEqual([
                ['Red', 'false'],
                ['Green', 'true'],
                ['Blue', 'false'],
            ])

            ;(menu.querySelector('[data-item="green"]') as HTMLButtonElement).click()
            await flushPromises()
            expect(wrapper.emitted('updateVisualization')).toBeUndefined()

            await picker.trigger('click')
            await flushPromises()
            ;(menu.querySelector('[data-item="blue"]') as HTMLButtonElement).click()
            await flushPromises()

            expect(wrapper.emitted('updateVisualization')).toEqual([[{ colorScheme: 'blue' }]])
            expect(module.renderer?.showColorScheme).toHaveBeenCalledWith('blue', 1.5)
            expect(module.renderer?.showPreset).toHaveBeenCalledTimes(1)
            expect(wrapper.get('[data-part="btn-color-scheme-picker"]').text()).toContain('Blue')
            wrapper.unmount()
        })
    })

    it('offers no color schemes for modules without them', async () => {
        const wrapper = await loaded()

        expect(controls(wrapper).attributes('data-state')).toBe('ready')
        expect(wrapper.find('[data-part="btn-color-scheme-picker"]').exists()).toBe(false)
        expect(document.querySelector('[data-kind="visualization-color-schemes"]')).toBeNull()
        wrapper.unmount()
    })

    it('offers no full screen where the browser has none', async () => {
        const wrapper = await loaded()

        const button = wrapper.get(
            '[data-part="visualization-controls"] [data-part="btn-fullscreen"]',
        )
        expect(button.attributes('disabled')).toBeDefined()
        expect(button.attributes('aria-label') ?? button.text()).toContain('Full screen')
        wrapper.unmount()
    })
})

describe('presetMenuPlacement', () => {
    const button = (top: number) => ({ left: 100, top, right: 400, bottom: top + 32 })

    it('opens the presets as a sheet on phones', () => {
        expect(presetMenuPlacement(true, button(20))).toEqual({
            placement: 'sheet',
            scrollIntoView: false,
        })
    })

    it('opens the presets above the button elsewhere', () => {
        expect(presetMenuPlacement(false, button(500))).toEqual({
            placement: 'above',
            scrollIntoView: false,
        })
    })

    it('scrolls the button down first when the room above it is too small', () => {
        expect(presetMenuPlacement(false, button(minimumRoomAbove))).toEqual({
            placement: 'above',
            scrollIntoView: true,
        })
        expect(presetMenuPlacement(false, button(minimumRoomAbove + 8)).scrollIntoView).toBe(false)
    })
})
