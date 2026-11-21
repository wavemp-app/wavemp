import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createWaveMpI18n } from '@/i18n'
import { visualizationDependencies } from '@/visualization/modules'
import { defaultVisualizationSettings, type VisualizationSettings } from '@/visualization/settings'
import { FakeClock, FakeModule, fakeAnalyser } from '@/visualization/__tests__/fakes'
import StageVisualization from '../StageVisualization.vue'

describe('StageVisualization', () => {
    const original = { ...visualizationDependencies }
    let module: FakeModule
    let clock: FakeClock

    function mountVisualization(
        props: {
            active?: boolean
            audio?: AnalyserNode | null
            settings?: VisualizationSettings
        } = {},
    ) {
        return mount(StageVisualization, {
            props: { audio: fakeAnalyser(), ...props },
            global: { plugins: [createWaveMpI18n('en')] },
            attachTo: document.body,
        })
    }

    type Wrapper = ReturnType<typeof mountVisualization>

    /** The presets and the preset choice the component exposes for its controls. */
    function exposed(wrapper: Wrapper) {
        return wrapper.vm as unknown as {
            presetChoices: { id: string; label: string }[]
            preset: string
            selectPreset: (id: string) => void
        }
    }

    function state(wrapper: Wrapper): string | undefined {
        return wrapper.get('[data-part="visualization"]').attributes('data-state')
    }

    async function loaded(props: Parameters<typeof mountVisualization>[0] = {}) {
        const wrapper = mountVisualization(props)
        await module.finishLoad()
        await flushPromises()
        return wrapper
    }

    beforeEach(() => {
        module = new FakeModule()
        clock = new FakeClock()
        visualizationDependencies.module = () => module
        visualizationDependencies.clock = clock
        visualizationDependencies.devicePixelRatio = () => 1
    })

    afterEach(() => {
        Object.assign(visualizationDependencies, original)
        document.body.innerHTML = ''
    })

    it('loads the module and shows a hint until it draws', async () => {
        const wrapper = mountVisualization()
        await flushPromises()

        expect(state(wrapper)).toBe('loading')
        expect(wrapper.get('[data-part="visualization"]').attributes('data-module')).toBe(
            'milkdrop',
        )
        expect(wrapper.get('[data-part="visualization-message"]').text()).toBe(
            'Loading the visualization…',
        )
        expect(wrapper.get('[data-part="visualization-message"]').attributes('role')).toBe('status')
        wrapper.unmount()
    })

    it('draws the music with the preset of the profile', async () => {
        const audio = fakeAnalyser()
        const wrapper = await loaded({
            audio,
            settings: { ...defaultVisualizationSettings, preset: 'B' },
        })

        expect(state(wrapper)).toBe('running')
        expect(wrapper.find('[data-part="visualization-message"]').exists()).toBe(false)
        expect(module.renderer?.options.audio).toBe(audio)
        expect(module.renderer?.options.canvas).toBe(
            wrapper.get('[data-part="visualization-canvas"]').element,
        )
        expect(module.renderer?.showPreset).toHaveBeenCalledWith('B', 0)
        expect(exposed(wrapper).preset).toBe('B')
        expect(exposed(wrapper).presetChoices.map((choice) => choice.label)).toEqual([
            'A',
            'B',
            'C',
        ])
        // Nothing lies on the picture: the controls are a toolbar of the stage.
        expect(wrapper.find('[data-part="visualization-controls"]').exists()).toBe(false)

        clock.frame()
        expect(module.renderer?.render).toHaveBeenCalledTimes(1)
        wrapper.unmount()
    })

    it('waits for the music while the player has no output', async () => {
        const wrapper = await loaded({ audio: null })

        expect(state(wrapper)).toBe('waiting')
        expect(wrapper.get('[data-part="visualization-message"]').text()).toBe(
            'The visualization starts with the music.',
        )

        await wrapper.setProps({ audio: fakeAnalyser() })
        expect(state(wrapper)).toBe('running')
        wrapper.unmount()
    })

    it('stops drawing while another view is shown', async () => {
        const wrapper = await loaded()

        await wrapper.setProps({ active: false })

        expect(state(wrapper)).toBe('paused')
        expect(clock.frames.size).toBe(0)
        expect(clock.timers.size).toBe(0)

        await wrapper.setProps({ active: true })
        expect(state(wrapper)).toBe('running')
        expect(clock.frames.size).toBe(1)
        wrapper.unmount()
    })

    it('stops drawing while the page is hidden', async () => {
        const wrapper = await loaded()

        Object.defineProperty(document, 'visibilityState', {
            configurable: true,
            get: () => 'hidden',
        })
        document.dispatchEvent(new Event('visibilitychange'))
        await flushPromises()
        expect(state(wrapper)).toBe('paused')

        Object.defineProperty(document, 'visibilityState', {
            configurable: true,
            get: () => 'visible',
        })
        document.dispatchEvent(new Event('visibilitychange'))
        await flushPromises()
        expect(state(wrapper)).toBe('running')

        // Back to the getter of Document.prototype.
        Reflect.deleteProperty(document, 'visibilityState')
        wrapper.unmount()
    })

    it('shows a chosen preset and hands it to the profile', async () => {
        const wrapper = await loaded({ settings: { ...defaultVisualizationSettings, preset: 'A' } })

        exposed(wrapper).selectPreset('C')
        exposed(wrapper).selectPreset('unknown')

        expect(module.renderer?.showPreset).toHaveBeenLastCalledWith('C', 1)
        expect(wrapper.emitted('update')).toEqual([[{ preset: 'C' }]])
        expect(exposed(wrapper).preset).toBe('C')
        wrapper.unmount()
    })

    it('hands a chosen preset to the profile before it shows the preset', async () => {
        const wrapper = await loaded({ settings: { ...defaultVisualizationSettings, preset: 'A' } })
        let emittedBefore: unknown[] | undefined
        module.renderer?.showPreset.mockImplementation(() => {
            emittedBefore = wrapper.emitted('update')?.slice()
        })

        exposed(wrapper).selectPreset('B')

        expect(emittedBefore).toEqual([[{ preset: 'B' }]])
        wrapper.unmount()
    })

    it('changes the preset automatically at the interval of the profile', async () => {
        clock.randomValue = 0
        const wrapper = await loaded({
            settings: { ...defaultVisualizationSettings, preset: 'A', autoChangeSeconds: 15 },
        })

        expect(clock.elapse()).toEqual([15000])
        await flushPromises()

        expect(exposed(wrapper).preset).toBe('B')
        expect(wrapper.emitted('update')).toBeUndefined()
        wrapper.unmount()
    })

    it('tells when the browser cannot show the visualization', async () => {
        module.supported = false
        const wrapper = mountVisualization()
        await flushPromises()

        expect(state(wrapper)).toBe('unsupported')
        expect(wrapper.get('[data-part="visualization-message"]').text()).toBe(
            'This browser cannot show the visualization.',
        )
        wrapper.unmount()
    })

    it('tells when the visualization cannot be loaded', async () => {
        const wrapper = mountVisualization()
        await module.failLoad()
        await flushPromises()

        expect(state(wrapper)).toBe('failed')
        expect(wrapper.get('[data-part="visualization-message"]').text()).toBe(
            'The visualization could not be loaded.',
        )
        wrapper.unmount()
    })

    it('gives the module a new canvas when the output of the player changes', async () => {
        const wrapper = await loaded()
        const first = wrapper.get('[data-part="visualization-canvas"]').element

        await wrapper.setProps({ audio: fakeAnalyser() })
        await flushPromises()

        const second = wrapper.get('[data-part="visualization-canvas"]').element
        expect(second).not.toBe(first)
        expect(module.renderers).toHaveLength(2)
        expect(module.renderer?.options.canvas).toBe(second)
        expect(state(wrapper)).toBe('running')
        wrapper.unmount()
    })

    it('starts again with another module of the profile', async () => {
        const wrapper = await loaded()
        const first = module.renderer
        const other = new FakeModule()
        other.presets = [
            { id: 'aurora', name: 'Aurora', labelKey: 'nowPlaying.aforceStyles.aurora' },
            { id: 'ember', name: 'Ember', labelKey: 'nowPlaying.aforceStyles.ember' },
        ]
        visualizationDependencies.module = () => other

        await wrapper.setProps({
            settings: { ...defaultVisualizationSettings, module: 'AForce', preset: 'ember' },
        })
        await other.finishLoad()
        await flushPromises()

        expect(first?.dispose).toHaveBeenCalled()
        expect(wrapper.get('[data-part="visualization"]').attributes('data-module')).toBe('aforce')
        expect(state(wrapper)).toBe('running')
        expect(other.renderer?.showPreset).toHaveBeenCalledWith('ember', 0)
        // The styles of A-Force carry message keys and show in the language of the app.
        expect(exposed(wrapper).presetChoices).toEqual([
            { id: 'aurora', label: 'Aurora' },
            { id: 'ember', label: 'Ember' },
        ])
        wrapper.unmount()
    })

    it('starts BlackCap with the stored scene and shows the scenes in the language of the app', async () => {
        const wrapper = await loaded()
        const other = new FakeModule()
        other.presets = [
            { id: 'lumen', name: 'Lumen', labelKey: 'nowPlaying.blackcapScenes.lumen' },
            { id: 'tide', name: 'Tide', labelKey: 'nowPlaying.blackcapScenes.tide' },
        ]
        visualizationDependencies.module = () => other

        await wrapper.setProps({
            settings: { ...defaultVisualizationSettings, module: 'BlackCap', preset: 'tide' },
        })
        await other.finishLoad()
        await flushPromises()

        expect(wrapper.get('[data-part="visualization"]').attributes('data-module')).toBe(
            'blackcap',
        )
        expect(state(wrapper)).toBe('running')
        expect(other.renderer?.showPreset).toHaveBeenCalledWith('tide', 0)
        expect(exposed(wrapper).presetChoices.map((choice) => choice.label)).toEqual([
            'Lumen',
            'Tide',
        ])
        wrapper.unmount()
    })

    it('draws at the quality of the profile and tells it the theme', async () => {
        const wrapper = await loaded({
            settings: { ...defaultVisualizationSettings, quality: 'Low' },
        })

        expect(module.renderer?.options.quality).toBe('low')
        expect(wrapper.get('[data-part="visualization"]').attributes('data-quality')).toBe('low')

        await wrapper.setProps({
            settings: { ...defaultVisualizationSettings, quality: 'High' },
        })
        expect(module.renderer?.setQuality).toHaveBeenCalledWith('high')
        expect(wrapper.get('[data-part="visualization"]').attributes('data-quality')).toBe('high')
        wrapper.unmount()
    })

    it('has no quality before it draws', async () => {
        const wrapper = mountVisualization()
        await flushPromises()

        expect(
            wrapper.get('[data-part="visualization"]').attributes('data-quality'),
        ).toBeUndefined()
        wrapper.unmount()
    })

    it('releases the renderer when it closes', async () => {
        const wrapper = await loaded()
        const renderer = module.renderer

        wrapper.unmount()

        expect(renderer?.dispose).toHaveBeenCalled()
        expect(clock.frames.size).toBe(0)
    })
})
