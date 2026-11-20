import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'
import SettingsView from '../SettingsView.vue'
import { FakeCastLayer } from '@/casting/__tests__/fakes'
import { createWaveMpI18n } from '@/i18n'
import { castDependencies } from '@/stores/cast'
import { useHealthStore } from '@/stores/health'
import { useResumeStore } from '@/stores/resume'
import { useSessionStore } from '@/stores/session'
import { appVersion } from '@/version'

vi.mock('@/player/api', () => ({
    getPlaybackState: vi.fn(() => Promise.resolve(undefined)),
    savePlaybackState: vi.fn(() => Promise.resolve()),
    clearPlaybackState: vi.fn(() => Promise.resolve()),
}))

vi.mock('@/profiles/avatarApi', () => ({
    listAvatarPresets: vi.fn(() => Promise.resolve(['bubble'])),
    fetchAvatar: vi.fn(() => Promise.resolve(undefined)),
    fetchAvatarPreset: vi.fn(() => Promise.resolve(undefined)),
}))

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
            { path: '/settings/pin', name: 'settings-pin', component: { render: () => null } },
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
        ).toEqual(['Deutsch', 'English'])
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
        expect(wrapper.get('[data-field="language"] [data-part="field-hint"]').text()).toBe(
            'Applies to this profile on all devices.',
        )
    })

    it('offers the themes as preview tiles in a radio group', () => {
        const { wrapper } = mountView()

        const picker = wrapper.get('[data-field="theme"] [data-part="theme-picker"]')
        const label = wrapper.get('[data-field="theme"] > [data-part="label"]')
        expect(picker.attributes('role')).toBe('radiogroup')
        expect(picker.attributes('aria-labelledby')).toBe(label.attributes('id'))
        expect(picker.find('[data-part="indicator"]').exists()).toBe(true)
        const options = picker.findAll('[data-part="theme-option"]')
        expect(
            options.map((option) => [
                option.attributes('data-item'),
                option.get('[data-part="label"]').text(),
                option.attributes('aria-checked'),
                option.attributes('tabindex'),
            ]),
        ).toEqual([
            ['midnight', 'Midnight', 'true', '0'],
            ['meadow', 'Meadow', 'false', '-1'],
            ['daylight', 'Daylight', 'false', '-1'],
            ['accent', 'Accent', 'false', '-1'],
            ['pinstripe', 'Pinstripe', 'false', '-1'],
            ['clear-glass', 'Clear Glass', 'false', '-1'],
        ])
        const accent = options[3]!
        expect(accent.attributes('role')).toBe('radio')
        expect(accent.attributes('data-variants')).toBe('light dark')
        expect(accent.get('[data-part="theme-variants"]').text()).toBe('Light and dark')
        expect(accent.get('[data-part="theme-preview-image"]').attributes('src')).toMatch(
            /^\/themes\/accent\/preview-(light|dark)\.webp$/,
        )
        expect(picker.attributes('style')).toContain('--wmp-active-index: 0')
    })

    it('switches the theme at once and saves it with clicks and the arrow keys', async () => {
        const { wrapper, session } = mountView()
        const updateTheme = vi.spyOn(session, 'updateTheme').mockImplementation(async (change) => {
            session.theme = { ...session.theme, ...change }
        })
        const option = (id: string) => wrapper.get(`[data-part="theme-option"][data-item="${id}"]`)

        await option('accent').trigger('click')
        await flushPromises()
        expect(updateTheme).toHaveBeenLastCalledWith({ id: 'accent' })
        expect(option('accent').attributes('aria-checked')).toBe('true')

        await option('accent').trigger('keydown', { key: 'ArrowRight' })
        await flushPromises()
        expect(updateTheme).toHaveBeenLastCalledWith({ id: 'pinstripe' })
        expect(option('pinstripe').attributes('tabindex')).toBe('0')
    })

    it('offers the appearance only for themes with a light and a dark variant', async () => {
        const { wrapper, session } = mountView()
        const updateTheme = vi.spyOn(session, 'updateTheme').mockImplementation(async (change) => {
            session.theme = { ...session.theme, ...change }
        })
        const appearance = () =>
            wrapper.get('[data-field="theme-appearance"] [data-part="segmented-control"]')

        expect(appearance().attributes('aria-disabled')).toBe('true')
        expect(wrapper.get('[data-field="theme-appearance"] [data-part="field-hint"]').text()).toBe(
            'Midnight has one variant only.',
        )

        session.theme = { id: 'clear-glass', appearance: 'system' }
        await flushPromises()
        expect(appearance().attributes('aria-disabled')).toBeUndefined()
        expect(
            appearance()
                .findAll('[data-part="segment"]')
                .map((segment) => [
                    segment.attributes('data-item'),
                    segment.attributes('aria-checked'),
                ]),
        ).toEqual([
            ['system', 'true'],
            ['light', 'false'],
            ['dark', 'false'],
        ])

        await appearance().get('[data-item="dark"]').trigger('click')
        await flushPromises()
        expect(updateTheme).toHaveBeenCalledWith({ appearance: 'dark' })
        expect(appearance().get('[data-item="dark"]').attributes('aria-checked')).toBe('true')
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

    it('reports an error and shows the saved language again', async () => {
        const { wrapper, session } = mountView()
        // Like the store: the choice shows at once and the saved language comes back on an error.
        vi.spyOn(session, 'updateLanguage').mockImplementation(async (locale) => {
            session.language = locale
            await nextTick()
            session.language = 'en'
            throw new TypeError('Failed to fetch')
        })

        await wrapper.get('[data-part="select"]').setValue('de')
        await flushPromises()

        expect(wrapper.get('[data-part="alert"]').text()).toBe('The setting could not be saved.')
        expect(wrapper.get<HTMLSelectElement>('[data-part="select"]').element.value).toBe('en')
    })

    it('keeps the fields usable while a change is saved and saves every quick choice', async () => {
        const { wrapper, session } = mountView()
        const saves: string[] = []
        // Like the store: the choice shows at once, the server has not answered yet.
        vi.spyOn(session, 'updateReplayGain').mockImplementation((mode) => {
            session.replayGain = mode
            saves.push(mode)
            return new Promise(() => undefined)
        })
        const group = wrapper.get('[data-field="replay-gain"] [data-part="segmented-control"]')
        const segment = (item: string) => group.get(`[data-part="segment"][data-item="${item}"]`)

        await segment('off').trigger('keydown', { key: 'ArrowRight' })
        expect(group.attributes('aria-disabled')).toBeUndefined()
        expect(segment('track').attributes('aria-checked')).toBe('true')
        await segment('track').trigger('keydown', { key: 'End' })
        expect(segment('album').attributes('aria-checked')).toBe('true')
        await segment('track').trigger('click')
        expect(segment('track').attributes('aria-checked')).toBe('true')

        expect(saves).toEqual(['Track', 'Album', 'Track'])
        expect(wrapper.find('[data-part="select"]:disabled').exists()).toBe(false)
        expect(wrapper.find('[data-field="equalizer"] input:disabled').exists()).toBe(false)
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

    it('greys out ReplayGain, equalizer and crossfade while a casting device plays', async () => {
        const layer = new FakeCastLayer()
        const createLayer = castDependencies.createLayer
        castDependencies.createLayer = () => layer
        try {
            const { wrapper } = mountView()
            await flushPromises()
            expect(wrapper.find('[data-item="cast-effects"]').exists()).toBe(false)
            expect(field<HTMLInputElement>(wrapper, 'equalizer', 'input').element.disabled).toBe(
                false,
            )

            layer.connect()
            await flushPromises()

            expect(wrapper.get('[data-item="cast-effects"]').text()).toContain('While casting')
            expect(field<HTMLInputElement>(wrapper, 'equalizer', 'input').element.disabled).toBe(
                true,
            )
            expect(field<HTMLSelectElement>(wrapper, 'crossfade', 'select').element.disabled).toBe(
                true,
            )
            expect(
                wrapper
                    .get('[data-field="replay-gain"] [data-part="segmented-control"]')
                    .attributes('aria-disabled'),
            ).toBe('true')
        } finally {
            castDependencies.createLayer = createLayer
        }
    })

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

    it('saves autoplay and its songs with a video only at once', async () => {
        const { wrapper, session } = mountView()
        const update = vi.spyOn(session, 'updateAutoplay').mockImplementation(async (change) => {
            session.autoplay = {
                enabled: change.enabled ?? session.autoplay.enabled,
                includeVideoOnly: change.includeVideoOnly ?? session.autoplay.includeVideoOnly,
            }
        })
        const enabled = field<HTMLInputElement>(wrapper, 'autoplay', 'input')
        const videoOnly = field<HTMLInputElement>(wrapper, 'autoplay-video-only', 'input')
        expect(enabled.element.checked).toBe(true)
        expect(videoOnly.element.checked).toBe(true)
        expect(videoOnly.element.disabled).toBe(false)

        await videoOnly.setValue(false)
        await flushPromises()
        expect(update).toHaveBeenCalledWith({ includeVideoOnly: false })

        await enabled.setValue(false)
        await flushPromises()
        expect(update).toHaveBeenLastCalledWith({ enabled: false })
        expect(
            field<HTMLInputElement>(wrapper, 'autoplay-video-only', 'input').element.disabled,
        ).toBe(true)
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

    it('saves the fade-out and shows the saved one again after an error', async () => {
        const { wrapper, session } = mountView()
        const update = vi.spyOn(session, 'updateSleepTimer').mockImplementation(async (change) => {
            session.sleepTimer = { fadeSeconds: change.fadeSeconds ?? 10 }
            await nextTick()
            session.sleepTimer = { fadeSeconds: 10 }
            throw new TypeError('Failed to fetch')
        })

        await field(wrapper, 'sleep-timer-fade', 'select').setValue('30')
        await flushPromises()

        expect(update).toHaveBeenCalledWith({ fadeSeconds: 30 })

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

    it('saves the quality of the visualization and restores it when saving fails', async () => {
        const { wrapper, session } = mountView()
        const update = vi
            .spyOn(session, 'updateVisualization')
            .mockImplementationOnce(async (change) => {
                session.visualization = {
                    ...session.visualization,
                    quality: change.quality ?? session.visualization.quality,
                }
            })
            .mockRejectedValueOnce(new TypeError('Failed to fetch'))
        const group = wrapper.get(
            '[data-field="visualization-quality"] [data-part="segmented-control"]',
        )
        const segment = (item: string) =>
            wrapper.get(
                `[data-field="visualization-quality"] [data-part="segment"][data-item="${item}"]`,
            )

        expect(group.attributes('role')).toBe('radiogroup')
        expect(group.findAll('[data-part="segment"]').map((item) => item.text())).toEqual([
            'Auto',
            'Low',
            'Medium',
            'High',
        ])
        expect(segment('auto').attributes('aria-checked')).toBe('true')

        await segment('low').trigger('click')
        await flushPromises()
        expect(update).toHaveBeenCalledWith({ quality: 'Low' })
        expect(segment('low').attributes('aria-checked')).toBe('true')

        await segment('high').trigger('click')
        await flushPromises()
        expect(wrapper.get('[data-part="alert"]').text()).toBe('The setting could not be saved.')
        expect(segment('low').attributes('aria-checked')).toBe('true')
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

describe('SettingsView PIN', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('tells whether the profile has a PIN and links to the PIN page', async () => {
        const { wrapper, session } = mountView()
        const section = wrapper.get('[data-part="section"][data-item="pin"]')

        expect(section.get('[data-field="pin-status"]').text()).toBe(
            'This profile has no PIN. Anyone can select it.',
        )
        expect(section.get('[data-item="open-pin"]').attributes('href')).toBe('/settings/pin')

        session.$patch({ profile: { ...session.profile!, hasPin: true } })
        await wrapper.vm.$nextTick()

        expect(section.get('[data-field="pin-status"]').text()).toBe(
            'This profile is protected with a PIN.',
        )
    })

    it('tells whether the profile shows a picture and opens the avatar dialog', async () => {
        const { wrapper, session } = mountView()
        const section = wrapper.get('[data-part="section"][data-item="avatar"]')

        expect(section.get('[data-field="avatar-status"]').text()).toBe(
            'This profile shows its initials.',
        )
        session.$patch({
            profile: { ...session.profile!, avatarVersion: null, avatarPresetId: null },
        })
        await section.get('[data-item="change-avatar"]').trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-part="dialog"][data-item="avatar"]').isVisible()).toBe(true)
        expect(wrapper.find('[data-part="avatar-editor"]').exists()).toBe(true)
    })

    it('clears the playback state after the confirmation', async () => {
        const { wrapper } = mountView()
        const resume = useResumeStore()
        const clear = vi.spyOn(resume, 'clear').mockResolvedValue()
        const section = wrapper.get('[data-part="section"][data-item="resume"]')
        const dialog = wrapper.get('[data-part="dialog"][data-item="clear-playback-state"]')

        expect(section.get('[data-part="section-title"]').text()).toBe('Playback state')
        await section.get('[data-item="clear-playback-state"]').trigger('click')
        await flushPromises()
        expect(clear).not.toHaveBeenCalled()
        expect(dialog.get('[data-part="dialog-title"]').text()).toBe('Clear playback state')

        await dialog.get('[data-item="confirm-clear-playback-state"]').trigger('click')
        await flushPromises()

        expect(clear).toHaveBeenCalledOnce()
        expect(section.get('[data-part="alert"]').attributes('data-state')).toBe('success')
        expect(section.get('[data-part="alert"]').text()).toContain(
            'The playback state is cleared.',
        )
    })

    it('reports when the playback state cannot be cleared', async () => {
        const { wrapper } = mountView()
        vi.spyOn(useResumeStore(), 'clear').mockRejectedValue(new Error('offline'))

        await wrapper.get('[data-item="confirm-clear-playback-state"]').trigger('click')
        await flushPromises()

        const alert = wrapper.get('[data-item="resume"] [data-part="alert"]')
        expect(alert.attributes('data-state')).toBe('error')
        expect(alert.text()).toContain('The playback state could not be cleared.')
    })
})
