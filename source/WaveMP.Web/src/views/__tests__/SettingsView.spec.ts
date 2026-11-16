import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import SettingsView from '../SettingsView.vue'
import { createWaveMpI18n } from '@/i18n'
import { useHealthStore } from '@/stores/health'
import { useSessionStore } from '@/stores/session'
import { appVersion } from '@/version'

function mountView() {
    const session = useSessionStore()
    session.$patch({
        token: 'token',
        sessionId: 'session',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        profile: {
            id: 'anna',
            name: 'Anna Bell',
            initials: 'AB',
            color: '#2e7d32',
            role: 'User',
            isBuiltIn: false,
        },
        language: 'en',
    })
    const router = createRouter({
        history: createMemoryHistory(),
        routes: [
            { path: '/', name: 'settings', component: { render: () => null } },
            {
                path: '/now-playing/equalizer',
                name: 'equalizer',
                component: { render: () => null },
            },
        ],
    })
    const wrapper = mount(SettingsView, {
        global: { plugins: [createWaveMpI18n('en'), router] },
    })
    return { wrapper, session }
}

describe('SettingsView', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('shows the profile and its language', () => {
        const { wrapper } = mountView()

        expect(wrapper.get('[data-part="view"]').attributes('data-view')).toBe('settings')
        expect(wrapper.get('[data-part="profile-summary"] [data-part="label"]').text()).toBe(
            'Anna Bell',
        )
        const select = wrapper.get<HTMLSelectElement>('[data-part="select"]')
        expect(select.element.value).toBe('en')
        expect(
            wrapper.findAll('[data-field="language"] option').map((option) => option.text()),
        ).toEqual(['English', 'Deutsch'])
        const replayGain = wrapper.get('[data-field="replay-gain"] [data-part="segmented-control"]')
        expect(replayGain.attributes('role')).toBe('radiogroup')
        expect(
            replayGain
                .findAll('[data-part="segment"]')
                .map((segment) => [
                    segment.attributes('data-item'),
                    segment.text(),
                    segment.attributes('aria-checked'),
                ]),
        ).toEqual([
            ['off', 'Off', 'true'],
            ['track', 'Per song', 'false'],
            ['album', 'Per album', 'false'],
        ])
        const label = wrapper.get('[data-field="replay-gain"] > [data-part="label"]')
        expect(replayGain.attributes('aria-labelledby')).toBe(label.attributes('id'))
        expect(wrapper.get('[data-part="field-hint"]').text()).toBe(
            'Applies to this profile on all devices.',
        )
    })

    it('saves a new language at once', async () => {
        const { wrapper, session } = mountView()
        const updateLanguage = vi.spyOn(session, 'updateLanguage').mockResolvedValue()

        await wrapper.get('[data-part="select"]').setValue('de')
        await flushPromises()

        expect(updateLanguage).toHaveBeenCalledWith('de')
        expect(wrapper.find('[data-part="alert"]').exists()).toBe(false)
    })

    it('saves the ReplayGain mode at once and keeps it after an error', async () => {
        const { wrapper, session } = mountView()
        const update = vi
            .spyOn(session, 'updateReplayGain')
            .mockImplementationOnce(async (mode) => {
                session.replayGain = mode
            })
            .mockRejectedValueOnce(new TypeError('Failed to fetch'))
        const segment = (item: string) =>
            wrapper.get(`[data-field="replay-gain"] [data-part="segment"][data-item="${item}"]`)

        await segment('album').trigger('click')
        await flushPromises()
        expect(update).toHaveBeenCalledWith('Album')
        expect(session.replayGain).toBe('Album')

        await segment('track').trigger('click')
        await flushPromises()
        expect(wrapper.get('[data-part="alert"]').text()).toBe('The setting could not be saved.')
        expect(segment('album').attributes('aria-checked')).toBe('true')
        expect(segment('track').attributes('aria-checked')).toBe('false')
    })

    it('reports an error and keeps the saved language', async () => {
        const { wrapper, session } = mountView()
        vi.spyOn(session, 'updateLanguage').mockRejectedValue(new TypeError('Failed to fetch'))

        await wrapper.get('[data-part="select"]').setValue('de')
        await flushPromises()

        expect(wrapper.get('[data-part="alert"]').text()).toBe('The setting could not be saved.')
        expect(wrapper.get<HTMLSelectElement>('[data-part="select"]').element.value).toBe('en')
    })
})

describe('SettingsView playback', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    const field = <T extends Element>(
        wrapper: ReturnType<typeof mountView>['wrapper'],
        name: string,
        selector: string,
    ) => wrapper.get<T>(`[data-field="${name}"] ${selector}`)

    it('shows crossfade, album crossfade, sleep timer fade-out and equalizer of the profile', async () => {
        const session = useSessionStore()
        session.crossfade = { enabled: false, seconds: 5, albumsGapless: true }
        session.sleepTimer = { fadeSeconds: 20 }
        const { wrapper } = mountView()
        await flushPromises()

        const crossfade = field<HTMLSelectElement>(wrapper, 'crossfade', 'select')
        expect(crossfade.element.value).toBe('off')
        expect(crossfade.findAll('option').map((option) => option.text())).toHaveLength(13)
        expect(crossfade.find('option').text()).toBe('Off (without gaps)')
        const albums = field<HTMLInputElement>(wrapper, 'crossfade-albums', 'input')
        expect(albums.element.checked).toBe(true)
        expect(albums.element.disabled).toBe(true)
        const fade = field<HTMLSelectElement>(wrapper, 'sleep-timer-fade', 'select')
        expect(fade.element.value).toBe('20')
        expect(fade.findAll('option').map((option) => option.element.value)).toEqual([
            '5',
            '10',
            '15',
            '20',
            '30',
            '60',
        ])
        expect(field<HTMLInputElement>(wrapper, 'equalizer', 'input').element.checked).toBe(false)
        expect(wrapper.get('[data-item="open-equalizer"]').attributes('href')).toBe(
            '/now-playing/equalizer',
        )
    })

    it('saves the crossfade and the albums option at once', async () => {
        const { wrapper, session } = mountView()
        const update = vi.spyOn(session, 'updateCrossfade').mockImplementation(async (change) => {
            session.crossfade = {
                enabled: change.enabled ?? session.crossfade.enabled,
                seconds: change.seconds ?? session.crossfade.seconds,
                albumsGapless: change.albumsGapless ?? session.crossfade.albumsGapless,
            }
        })

        await field(wrapper, 'crossfade', 'select').setValue('6')
        await flushPromises()
        expect(update).toHaveBeenCalledWith({ enabled: true, seconds: 6 })
        const albums = field<HTMLInputElement>(wrapper, 'crossfade-albums', 'input')
        expect(albums.element.disabled).toBe(false)

        await albums.setValue(false)
        await flushPromises()
        expect(update).toHaveBeenLastCalledWith({ albumsGapless: false })

        await field(wrapper, 'crossfade', 'select').setValue('off')
        await flushPromises()
        expect(update).toHaveBeenLastCalledWith({ enabled: false })
        expect(session.crossfade).toEqual({ enabled: false, seconds: 6, albumsGapless: false })
    })

    it('follows the switch of the transport bar', async () => {
        const { wrapper, session } = mountView()
        session.crossfade = { enabled: true, seconds: 8, albumsGapless: true }
        await flushPromises()
        expect(field<HTMLSelectElement>(wrapper, 'crossfade', 'select').element.value).toBe('8')

        session.crossfade = { enabled: false, seconds: 8, albumsGapless: true }
        await flushPromises()
        expect(field<HTMLSelectElement>(wrapper, 'crossfade', 'select').element.value).toBe('off')
    })

    it('saves the fade-out and restores it after an error', async () => {
        const { wrapper, session } = mountView()
        vi.spyOn(session, 'updateSleepTimer').mockRejectedValue(new TypeError('Failed to fetch'))

        await field(wrapper, 'sleep-timer-fade', 'select').setValue('30')
        await flushPromises()

        expect(wrapper.get('[data-part="alert"]').text()).toBe('The setting could not be saved.')
        expect(field<HTMLSelectElement>(wrapper, 'sleep-timer-fade', 'select').element.value).toBe(
            '10',
        )
    })

    it('offers the intervals of the automatic change of the visualization', () => {
        const { wrapper } = mountView()

        const select = field<HTMLSelectElement>(wrapper, 'visualization-auto-change', 'select')
        expect(select.element.value).toBe('30')
        expect(select.findAll('option').map((option) => option.text())).toEqual([
            'Off (keep the preset)',
            'Every 10 seconds',
            'Every 15 seconds',
            'Every 20 seconds',
            'Every 30 seconds',
            'Every 45 seconds',
            'Every 60 seconds',
            'Every 2 minutes',
            'Every 5 minutes',
        ])
    })

    it('saves the automatic change of the visualization and keeps its interval when off', async () => {
        const { wrapper, session } = mountView()
        const update = vi
            .spyOn(session, 'updateVisualization')
            .mockImplementation(async (change) => {
                session.visualization = {
                    ...session.visualization,
                    autoChange: change.autoChange ?? session.visualization.autoChange,
                    autoChangeSeconds:
                        change.autoChangeSeconds ?? session.visualization.autoChangeSeconds,
                }
            })

        await field(wrapper, 'visualization-auto-change', 'select').setValue('120')
        await flushPromises()
        expect(update).toHaveBeenCalledWith({ autoChange: true, autoChangeSeconds: 120 })

        await field(wrapper, 'visualization-auto-change', 'select').setValue('off')
        await flushPromises()
        expect(update).toHaveBeenLastCalledWith({ autoChange: false })
        expect(session.visualization.autoChangeSeconds).toBe(120)
        expect(
            field<HTMLSelectElement>(wrapper, 'visualization-auto-change', 'select').element.value,
        ).toBe('off')
    })

    it('shows an interval of the profile that the menu does not offer', async () => {
        const { wrapper, session } = mountView()
        session.visualization = { ...session.visualization, autoChangeSeconds: 7 }
        await flushPromises()

        const select = field<HTMLSelectElement>(wrapper, 'visualization-auto-change', 'select')
        expect(select.element.value).toBe('7')
        expect(select.findAll('option')[1]?.text()).toBe('Every 7 seconds')
    })

    it('turns the equalizer on for the profile', async () => {
        const { wrapper, session } = mountView()
        const update = vi.spyOn(session, 'updateEqualizer').mockImplementation(async (change) => {
            session.equalizer = { ...session.equalizer, enabled: change.enabled ?? false }
        })

        await field(wrapper, 'equalizer', 'input').setValue(true)
        await flushPromises()

        expect(update).toHaveBeenCalledWith({ enabled: true })
        expect(field<HTMLInputElement>(wrapper, 'equalizer', 'input').element.checked).toBe(true)
        expect(wrapper.find('[data-part="alert"]').exists()).toBe(false)
    })
})

describe('SettingsView versions', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    const version = (wrapper: ReturnType<typeof mountView>['wrapper'], field: string) =>
        wrapper.get(
            `[data-part="section"][data-item="about"] [data-part="stat-item"][data-field="${field}"]`,
        )

    it('shows the version of the web app and of the server', () => {
        useHealthStore().version = '1.0.42'
        const { wrapper } = mountView()

        const section = wrapper.get('[data-part="section"][data-item="about"]')
        const title = section.get('[data-part="section-title"]')
        expect(title.text()).toBe('About WaveMP')
        expect(section.attributes('aria-labelledby')).toBe(title.attributes('id'))
        expect(version(wrapper, 'app-version').get('[data-part="label"]').text()).toBe(
            'App version',
        )
        expect(version(wrapper, 'app-version').get('[data-part="stat-value"]').text()).toBe(
            appVersion,
        )
        expect(version(wrapper, 'server-version').get('[data-part="label"]').text()).toBe(
            'Server version',
        )
        expect(version(wrapper, 'server-version').get('[data-part="stat-value"]').text()).toBe(
            '1.0.42',
        )
    })

    it('reports an unknown server version while the server does not answer', () => {
        const { wrapper } = mountView()

        expect(version(wrapper, 'server-version').get('[data-part="stat-value"]').text()).toBe(
            'Unknown',
        )
    })

    it('uses the CI version or a development version of the VersionPrefix', () => {
        // Without WAVEMP_VERSION (local builds, pull requests) the build is <major>.<minor>.0-dev.
        const ciVersion = process.env.WAVEMP_VERSION?.trim()
        if (ciVersion) {
            expect(appVersion).toBe(ciVersion)
        } else {
            expect(appVersion).toMatch(/^\d+\.\d+\.0-dev$/)
        }
    })
})
