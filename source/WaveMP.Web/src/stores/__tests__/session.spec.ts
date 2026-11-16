import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { sessionTokens } from '@/api/auth'
import { loadStoredSession, saveStoredSession, type StoredSession } from '@/session/storage'

const api = vi.hoisted(() => ({
    createSession: vi.fn(),
    getCurrentSession: vi.fn(),
    deleteCurrentSession: vi.fn(),
    getSettings: vi.fn(),
    updateSettings: vi.fn(),
}))

vi.mock('@/api/client', () => ({
    apiClient: {
        api: {
            v1: {
                sessions: {
                    post: (body: unknown) => api.createSession(body),
                    current: {
                        get: () => api.getCurrentSession(),
                        delete: () => api.deleteCurrentSession(),
                    },
                },
                me: {
                    settings: {
                        get: () => api.getSettings(),
                        patch: (body: unknown) => api.updateSettings(body),
                    },
                },
            },
        },
    },
}))

const { useSessionStore } = await import('../session')

const expiresAt = new Date(Date.now() + 365 * 24 * 60 * 60 * 1000)

function profile(id: string, role: 'Admin' | 'User' = 'User') {
    return { id, name: `Profile ${id}`, initials: 'P', color: '#1f6fd6', role, isBuiltIn: false }
}

function created(id: string, token = `token-${id}`, role: 'Admin' | 'User' = 'User') {
    return {
        token,
        session: { id: `session-${id}`, profile: profile(id, role), expiresAt },
    }
}

function stored(id: string, language: 'en' | 'de' | null = 'de'): StoredSession {
    return {
        token: `token-${id}`,
        sessionId: `session-${id}`,
        expiresAt: expiresAt.toISOString(),
        profile: profile(id),
        language,
    }
}

/** Lets pending promise callbacks run. */
async function settle(): Promise<void> {
    for (let index = 0; index < 5; index++) {
        await Promise.resolve()
    }
}

describe('session store', () => {
    beforeEach(() => {
        localStorage.clear()
        setActivePinia(createPinia())
        sessionTokens.current = null
        sessionTokens.onUnauthorized = null
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
        api.getSettings.mockResolvedValue({ language: 'en' })
    })

    it('restores the remembered session and listens for rejected tokens', () => {
        saveStoredSession(stored('a'))
        const session = useSessionStore()

        session.restore()

        expect(session.isAuthenticated).toBe(true)
        expect(session.profile?.id).toBe('a')
        expect(session.language).toBe('de')
        expect(sessionTokens.current).toBe('token-a')

        sessionTokens.onUnauthorized?.()

        expect(session.isAuthenticated).toBe(false)
        expect(sessionTokens.current).toBeNull()
        expect(loadStoredSession()).toBeNull()
    })

    it('starts without a session if nothing is remembered', () => {
        const session = useSessionStore()

        session.restore()

        expect(session.isAuthenticated).toBe(false)
        expect(session.isAdmin).toBe(false)
    })

    it('selects a profile, loads its language and remembers both', async () => {
        api.createSession.mockResolvedValue(created('a', 'token-a', 'Admin'))
        api.getSettings.mockResolvedValue({ language: 'de' })
        const session = useSessionStore()

        await session.select('a')

        expect(api.createSession).toHaveBeenCalledWith({ profileId: 'a' })
        expect(session.isAuthenticated).toBe(true)
        expect(session.isAdmin).toBe(true)
        expect(session.token).toBe('token-a')
        expect(session.language).toBe('de')
        expect(sessionTokens.current).toBe('token-a')
        expect(loadStoredSession()).toMatchObject({
            token: 'token-a',
            sessionId: 'session-a',
            profile: { id: 'a', role: 'Admin' },
            language: 'de',
        })
    })

    it('keeps the session if the language cannot be loaded', async () => {
        api.createSession.mockResolvedValue(created('a'))
        api.getSettings.mockRejectedValue(new TypeError('Failed to fetch'))
        const session = useSessionStore()

        await session.select('a')

        expect(session.isAuthenticated).toBe(true)
        expect(session.language).toBeNull()
    })

    it('does nothing when the current profile is selected again', async () => {
        saveStoredSession(stored('a'))
        const session = useSessionStore()
        session.restore()

        await session.select('a')

        expect(api.createSession).not.toHaveBeenCalled()
        expect(api.deleteCurrentSession).not.toHaveBeenCalled()
    })

    it('ends the session of another profile before selecting a new one', async () => {
        saveStoredSession(stored('a'))
        api.createSession.mockResolvedValue(created('b'))
        const session = useSessionStore()
        session.restore()

        await session.select('b')

        expect(api.deleteCurrentSession).toHaveBeenCalledOnce()
        expect(session.profile?.id).toBe('b')
        expect(session.token).toBe('token-b')
    })

    it('passes errors of the selection on', async () => {
        api.createSession.mockRejectedValue({ code: 'profile.not_found', responseStatusCode: 404 })
        const session = useSessionStore()

        await expect(session.select('gone')).rejects.toMatchObject({ code: 'profile.not_found' })
        expect(session.isAuthenticated).toBe(false)
    })

    it('updates profile and language from the server', async () => {
        saveStoredSession(stored('a', 'en'))
        api.getCurrentSession.mockResolvedValue({
            ...created('a').session,
            profile: { ...profile('a', 'Admin'), name: 'Renamed' },
        })
        api.getSettings.mockResolvedValue({ language: 'de' })
        const session = useSessionStore()
        session.restore()

        await session.refresh()

        expect(session.profile?.name).toBe('Renamed')
        expect(session.isAdmin).toBe(true)
        expect(session.language).toBe('de')
        expect(loadStoredSession()?.profile.name).toBe('Renamed')
    })

    it('ends the session when the server rejects the token', async () => {
        saveStoredSession(stored('a'))
        api.getCurrentSession.mockRejectedValue({
            code: 'auth.unauthorized',
            responseStatusCode: 401,
        })
        const session = useSessionStore()
        session.restore()

        await session.refresh()

        expect(session.isAuthenticated).toBe(false)
        expect(loadStoredSession()).toBeNull()
    })

    it('keeps the session when the server cannot be reached', async () => {
        saveStoredSession(stored('a'))
        api.getCurrentSession.mockRejectedValue(new TypeError('Failed to fetch'))
        const session = useSessionStore()
        session.restore()

        await session.refresh()

        expect(session.isAuthenticated).toBe(true)
        expect(loadStoredSession()).not.toBeNull()
    })

    it('saves the language on the server', async () => {
        saveStoredSession(stored('a', 'en'))
        api.updateSettings.mockResolvedValue({ language: 'de' })
        const session = useSessionStore()
        session.restore()

        await session.updateLanguage('de')

        expect(api.updateSettings).toHaveBeenCalledWith({ language: 'de' })
        expect(session.language).toBe('de')
        expect(loadStoredSession()?.language).toBe('de')
    })

    it('loads the ReplayGain mode with the settings and saves it on the server', async () => {
        api.createSession.mockResolvedValue(created('a'))
        api.getSettings.mockResolvedValue({ language: 'en', replayGain: 'Album' })
        api.updateSettings.mockResolvedValue({ language: 'en', replayGain: 'Track' })
        const session = useSessionStore()
        session.restore()
        expect(session.replayGain).toBe('Off')

        await session.select('a')
        expect(session.replayGain).toBe('Album')

        await session.updateReplayGain('Track')
        expect(api.updateSettings).toHaveBeenCalledWith({ replayGain: 'Track' })
        expect(session.replayGain).toBe('Track')

        session.clear()
        expect(session.replayGain).toBe('Off')
    })

    it('loads the view mode with the settings and refreshes it', async () => {
        api.createSession.mockResolvedValue(created('a'))
        api.getSettings.mockResolvedValue({ language: 'en', playerViewMode: 'Cover' })
        const session = useSessionStore()
        session.restore()
        expect(session.playerViewMode).toBe('Video')

        await session.select('a')
        expect(session.playerViewMode).toBe('Cover')

        api.getCurrentSession.mockResolvedValue(created('a').session)
        api.getSettings.mockResolvedValue({ language: 'en', playerViewMode: 'Visualization' })
        await session.refresh()
        expect(session.playerViewMode).toBe('Visualization')

        api.getSettings.mockResolvedValue({ language: 'en', playerViewMode: 'Lyrics' })
        await session.refresh()
        expect(session.playerViewMode).toBe('Visualization')

        session.clear()
        expect(session.playerViewMode).toBe('Video')
    })

    it('shows a new view mode at once and saves it on the server', async () => {
        saveStoredSession(stored('a'))
        let answer: (value: unknown) => void = () => {}
        api.updateSettings.mockReturnValue(new Promise((resolve) => (answer = resolve)))
        const session = useSessionStore()
        session.restore()

        const saving = session.updatePlayerViewMode('Visualization')
        expect(session.playerViewMode).toBe('Visualization')
        answer({ language: 'en', playerViewMode: 'Visualization' })
        await saving

        expect(api.updateSettings).toHaveBeenCalledWith({ playerViewMode: 'Visualization' })
        expect(session.playerViewMode).toBe('Visualization')
    })

    it('keeps the chosen view mode when it cannot be saved', async () => {
        saveStoredSession(stored('a'))
        api.updateSettings.mockRejectedValue(new TypeError('Failed to fetch'))
        const session = useSessionStore()
        session.restore()

        await session.updatePlayerViewMode('Cover')

        expect(session.playerViewMode).toBe('Cover')
    })

    it('saves quick view changes one after the other and only the latest waiting one', async () => {
        saveStoredSession(stored('a'))
        const answers: ((value: unknown) => void)[] = []
        api.updateSettings.mockImplementation(() => new Promise((resolve) => answers.push(resolve)))
        const session = useSessionStore()
        session.restore()

        const cover = session.updatePlayerViewMode('Cover')
        const video = session.updatePlayerViewMode('Video')
        const visualization = session.updatePlayerViewMode('Visualization')
        await settle()
        expect(session.playerViewMode).toBe('Visualization')
        expect(api.updateSettings).toHaveBeenCalledTimes(1)
        expect(api.updateSettings).toHaveBeenLastCalledWith({ playerViewMode: 'Cover' })

        // The answer to the older choice does not take the view back; the skipped one is not sent.
        answers[0]!({ language: 'en', playerViewMode: 'Cover' })
        await cover
        await video
        await settle()
        expect(session.playerViewMode).toBe('Visualization')
        expect(api.updateSettings).toHaveBeenCalledTimes(2)
        expect(api.updateSettings).toHaveBeenLastCalledWith({ playerViewMode: 'Visualization' })

        answers[1]!({ language: 'en', playerViewMode: 'Visualization' })
        await visualization
        expect(session.playerViewMode).toBe('Visualization')
    })

    it('does not save a waiting view mode for the next profile', async () => {
        saveStoredSession(stored('a'))
        const answers: ((value: unknown) => void)[] = []
        api.updateSettings.mockImplementation(() => new Promise((resolve) => answers.push(resolve)))
        const session = useSessionStore()
        session.restore()

        const cover = session.updatePlayerViewMode('Cover')
        const waiting = session.updatePlayerViewMode('Visualization')
        session.clear()
        answers[0]!({ language: 'en', playerViewMode: 'Cover' })
        await cover
        await waiting
        await settle()

        expect(api.updateSettings).toHaveBeenCalledTimes(1)
        expect(session.playerViewMode).toBe('Video')
    })

    it('loads the collapsed sidebar with the settings and forgets it for another profile', async () => {
        api.createSession.mockResolvedValue(created('a'))
        api.getSettings.mockResolvedValue({ language: 'en', sidebarCollapsed: true })
        const session = useSessionStore()
        session.restore()
        expect(session.sidebarCollapsed).toBe(false)

        await session.select('a')
        expect(session.sidebarCollapsed).toBe(true)

        api.getCurrentSession.mockResolvedValue(created('a').session)
        api.getSettings.mockResolvedValue({ language: 'en', sidebarCollapsed: false })
        await session.refresh()
        expect(session.sidebarCollapsed).toBe(false)

        session.sidebarCollapsed = true
        session.clear()
        expect(session.sidebarCollapsed).toBe(false)
    })

    it('collapses the sidebar at once, saves it and keeps it when it cannot be saved', async () => {
        saveStoredSession(stored('a'))
        let answer: (value: unknown) => void = () => {}
        api.updateSettings.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)))
        const session = useSessionStore()
        session.restore()

        const saving = session.updateSidebarCollapsed(true)
        expect(session.sidebarCollapsed).toBe(true)
        answer({ language: 'en', sidebarCollapsed: true })
        await saving
        expect(api.updateSettings).toHaveBeenCalledWith({ sidebarCollapsed: true })

        api.updateSettings.mockRejectedValueOnce(new TypeError('Failed to fetch'))
        await session.updateSidebarCollapsed(false)
        expect(session.sidebarCollapsed).toBe(false)
    })

    it('saves quick sidebar changes one after the other so the server keeps the last one', async () => {
        saveStoredSession(stored('a'))
        const answers: ((value: unknown) => void)[] = []
        api.updateSettings.mockImplementation(() => new Promise((resolve) => answers.push(resolve)))
        const session = useSessionStore()
        session.restore()

        const collapsing = session.updateSidebarCollapsed(true)
        const expanding = session.updateSidebarCollapsed(false)
        await settle()
        expect(api.updateSettings).toHaveBeenCalledTimes(1)

        answers[0]!({ language: 'en', sidebarCollapsed: true })
        await collapsing
        await settle()
        expect(session.sidebarCollapsed).toBe(false)
        expect(api.updateSettings).toHaveBeenCalledTimes(2)
        expect(api.updateSettings).toHaveBeenLastCalledWith({ sidebarCollapsed: false })

        answers[1]!({ language: 'en', sidebarCollapsed: false })
        await expanding
        expect(session.sidebarCollapsed).toBe(false)
    })

    it('passes errors of saving the language on', async () => {
        saveStoredSession(stored('a', 'en'))
        api.updateSettings.mockRejectedValue(new TypeError('Failed to fetch'))
        const session = useSessionStore()
        session.restore()

        await expect(session.updateLanguage('de')).rejects.toThrow()
        expect(session.language).toBe('en')
    })

    it('signs out on the server and forgets the session', async () => {
        saveStoredSession(stored('a'))
        api.deleteCurrentSession.mockResolvedValue(undefined)
        const session = useSessionStore()
        session.restore()

        await session.signOut()

        expect(api.deleteCurrentSession).toHaveBeenCalledOnce()
        expect(session.isAuthenticated).toBe(false)
        expect(sessionTokens.current).toBeNull()
        expect(loadStoredSession()).toBeNull()
    })

    it('signs out also when the server cannot be reached', async () => {
        saveStoredSession(stored('a'))
        api.deleteCurrentSession.mockRejectedValue(new TypeError('Failed to fetch'))
        const session = useSessionStore()
        session.restore()

        await session.signOut()

        expect(session.isAuthenticated).toBe(false)
    })

    describe('playback settings', () => {
        const equalizer = {
            enabled: true,
            preset: 'Rock',
            preampDb: -2,
            gainsDb: [5, 4, 3, 1, -1, -1, 1, 3, 4, 5],
            customGainsDb: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
        }

        it('loads equalizer, crossfade and sleep timer with the profile', async () => {
            api.createSession.mockResolvedValue(created('a'))
            api.getSettings.mockResolvedValue({
                language: 'en',
                equalizer,
                crossfade: { enabled: true, seconds: 6, albumsGapless: false },
                sleepTimer: { fadeSeconds: 30 },
            })
            const session = useSessionStore()

            await session.select('a')

            expect(session.equalizer).toEqual(equalizer)
            expect(session.crossfade).toEqual({ enabled: true, seconds: 6, albumsGapless: false })
            expect(session.sleepTimer).toEqual({ fadeSeconds: 30 })
        })

        it('keeps the defaults for incomplete settings', async () => {
            api.createSession.mockResolvedValue(created('a'))
            api.getSettings.mockResolvedValue({
                language: 'en',
                equalizer: { ...equalizer, preset: 'Loudness' },
                crossfade: { enabled: true, seconds: 40, albumsGapless: true },
                sleepTimer: {},
            })
            const session = useSessionStore()

            await session.select('a')

            expect(session.equalizer.enabled).toBe(false)
            expect(session.equalizer.preset).toBe('Flat')
            expect(session.crossfade).toEqual({ enabled: false, seconds: 5, albumsGapless: true })
            expect(session.sleepTimer).toEqual({ fadeSeconds: 10 })
        })

        it('updates the settings on refresh and resets them for another profile', async () => {
            saveStoredSession(stored('a'))
            api.getCurrentSession.mockResolvedValue(created('a').session)
            api.getSettings.mockResolvedValue({
                language: 'de',
                crossfade: { enabled: true, seconds: 3, albumsGapless: true },
            })
            const session = useSessionStore()
            session.restore()

            await session.refresh()
            expect(session.crossfade.seconds).toBe(3)

            session.clear()
            expect(session.crossfade.seconds).toBe(5)
        })

        it('turns the crossfade on and off at once and keeps its length', async () => {
            saveStoredSession(stored('a'))
            let answer: (value: unknown) => void = () => undefined
            api.updateSettings.mockImplementationOnce(
                () =>
                    new Promise((resolve) => {
                        answer = resolve
                    }),
            )
            const session = useSessionStore()
            session.restore()
            session.crossfade = { enabled: false, seconds: 8, albumsGapless: true }

            const saving = session.setCrossfadeEnabled(true)
            expect(session.crossfade).toEqual({ enabled: true, seconds: 8, albumsGapless: true })
            answer({ crossfade: { enabled: true, seconds: 8, albumsGapless: true } })
            await saving

            expect(api.updateSettings).toHaveBeenCalledWith({ crossfade: { enabled: true } })
            expect(session.crossfade.enabled).toBe(true)
        })

        it('turns the crossfade back when saving the switch fails', async () => {
            saveStoredSession(stored('a'))
            api.updateSettings.mockRejectedValueOnce(new Error('offline'))
            const session = useSessionStore()
            session.restore()
            session.crossfade = { enabled: true, seconds: 8, albumsGapless: true }

            await expect(session.setCrossfadeEnabled(false)).rejects.toThrow('offline')

            expect(session.crossfade).toEqual({ enabled: true, seconds: 8, albumsGapless: true })
        })

        it('saves a change of the equalizer and takes over the answer', async () => {
            saveStoredSession(stored('a'))
            api.updateSettings.mockResolvedValue({ equalizer })
            const session = useSessionStore()
            session.restore()

            await session.updateEqualizer({ enabled: true, preset: 'Rock' })

            expect(api.updateSettings).toHaveBeenCalledWith({
                equalizer: { enabled: true, preset: 'Rock' },
            })
            expect(session.equalizer).toEqual(equalizer)
        })

        it('ignores an older answer after a newer change', async () => {
            saveStoredSession(stored('a'))
            let answerFirst: (value: unknown) => void = () => undefined
            api.updateSettings
                .mockImplementationOnce(
                    () =>
                        new Promise((resolve) => {
                            answerFirst = resolve
                        }),
                )
                .mockResolvedValueOnce({
                    crossfade: { enabled: true, seconds: 8, albumsGapless: true },
                })
            const session = useSessionStore()
            session.restore()

            const first = session.updateCrossfade({ seconds: 2 })
            await session.updateCrossfade({ seconds: 8 })
            answerFirst({ crossfade: { enabled: true, seconds: 2, albumsGapless: true } })
            await first

            expect(session.crossfade.seconds).toBe(8)
        })

        it('saves the fade-out of the sleep timer and passes errors on', async () => {
            saveStoredSession(stored('a'))
            api.updateSettings.mockResolvedValueOnce({ sleepTimer: { fadeSeconds: 60 } })
            const session = useSessionStore()
            session.restore()

            await session.updateSleepTimer({ fadeSeconds: 60 })
            expect(session.sleepTimer.fadeSeconds).toBe(60)

            api.updateSettings.mockRejectedValueOnce(new TypeError('Failed to fetch'))
            await expect(session.updateSleepTimer({ fadeSeconds: 5 })).rejects.toThrow()
            expect(session.sleepTimer.fadeSeconds).toBe(60)
        })
    })

    describe('visualization settings', () => {
        const visualization = {
            module: 'Milkdrop',
            preset: 'Waves',
            autoChange: false,
            autoChangeSeconds: 120,
            quality: 'Medium',
        }

        it('loads the visualization with the profile and resets it for another profile', async () => {
            api.createSession.mockResolvedValue(created('a'))
            api.getSettings.mockResolvedValue({ language: 'en', visualization })
            const session = useSessionStore()

            await session.select('a')
            expect(session.visualization).toEqual(visualization)

            session.clear()
            expect(session.visualization).toEqual({
                module: 'Milkdrop',
                preset: null,
                autoChange: true,
                autoChangeSeconds: 30,
                quality: 'Auto',
            })
        })

        it('takes Auto for an unknown or missing quality', async () => {
            api.createSession.mockResolvedValue(created('a'))
            api.getSettings.mockResolvedValue({
                language: 'en',
                visualization: { ...visualization, quality: 'Ultra' },
            })
            const session = useSessionStore()

            await session.select('a')

            expect(session.visualization).toEqual({ ...visualization, quality: 'Auto' })
        })

        it('saves the quality of the visualization', async () => {
            saveStoredSession(stored('a'))
            api.updateSettings.mockResolvedValueOnce({
                visualization: { ...visualization, quality: 'Low' },
            })
            const session = useSessionStore()
            session.restore()

            await session.updateVisualization({ quality: 'Low' })

            expect(api.updateSettings).toHaveBeenCalledWith({ visualization: { quality: 'Low' } })
            expect(session.visualization.quality).toBe('Low')
        })

        it('keeps the defaults for incomplete visualization settings', async () => {
            api.createSession.mockResolvedValue(created('a'))
            api.getSettings.mockResolvedValue({
                language: 'en',
                visualization: { ...visualization, autoChangeSeconds: 2 },
            })
            const session = useSessionStore()

            await session.select('a')

            expect(session.visualization.autoChangeSeconds).toBe(30)
            expect(session.visualization.preset).toBeNull()
        })

        it('changes the visualization at once and takes over the answer', async () => {
            saveStoredSession(stored('a'))
            let answer: (value: unknown) => void = () => undefined
            api.updateSettings.mockImplementationOnce(
                () =>
                    new Promise((resolve) => {
                        answer = resolve
                    }),
            )
            const session = useSessionStore()
            session.restore()

            const saving = session.updateVisualization({ preset: 'Waves' })
            expect(session.visualization.preset).toBe('Waves')
            answer({ visualization: { ...visualization, autoChange: true } })
            await saving

            expect(api.updateSettings).toHaveBeenCalledWith({ visualization: { preset: 'Waves' } })
            expect(session.visualization).toEqual({ ...visualization, autoChange: true })
        })

        it('restores the visualization when saving fails and passes the error on', async () => {
            saveStoredSession(stored('a'))
            api.updateSettings.mockRejectedValueOnce(new Error('offline'))
            const session = useSessionStore()
            session.restore()

            await expect(session.updateVisualization({ autoChange: false })).rejects.toThrow(
                'offline',
            )

            expect(session.visualization.autoChange).toBe(true)
        })
    })
})
