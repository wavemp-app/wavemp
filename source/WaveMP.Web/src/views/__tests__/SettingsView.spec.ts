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
        const options = wrapper
            .findAll('[data-field="language"] option, [data-field="replay-gain"] option')
            .map((option) => option.text())
        expect(options).toEqual(['English', 'Deutsch', 'Off', 'Per song', 'Per album'])
        expect(wrapper.findAll<HTMLSelectElement>('[data-part="select"]')[1]!.element.value).toBe(
            'Off',
        )
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
        const select = () => wrapper.findAll<HTMLSelectElement>('[data-part="select"]')[1]!

        await select().setValue('Album')
        await flushPromises()
        expect(update).toHaveBeenCalledWith('Album')
        expect(session.replayGain).toBe('Album')

        await select().setValue('Track')
        await flushPromises()
        expect(wrapper.get('[data-part="alert"]').text()).toBe('The setting could not be saved.')
        expect(select().element.value).toBe('Album')
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
        session.crossfade = { seconds: 0, albumsGapless: true }
        session.sleepTimer = { fadeSeconds: 20 }
        const { wrapper } = mountView()
        await flushPromises()

        const crossfade = field<HTMLSelectElement>(wrapper, 'crossfade', 'select')
        expect(crossfade.element.value).toBe('0')
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
                seconds: change.seconds ?? session.crossfade.seconds,
                albumsGapless: change.albumsGapless ?? session.crossfade.albumsGapless,
            }
        })

        await field(wrapper, 'crossfade', 'select').setValue('6')
        await flushPromises()
        expect(update).toHaveBeenCalledWith({ seconds: 6 })
        const albums = field<HTMLInputElement>(wrapper, 'crossfade-albums', 'input')
        expect(albums.element.disabled).toBe(false)

        await albums.setValue(false)
        await flushPromises()
        expect(update).toHaveBeenLastCalledWith({ albumsGapless: false })
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
