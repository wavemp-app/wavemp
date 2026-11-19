import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { sessionTokens } from '@/api/auth'
import { apiClient } from '@/api/client'
import { createLatestValueSender } from '@/api/latestValue'
import { createRequestQueue } from '@/api/requestQueue'
import {
    PlayerViewModeObject,
    ReplayGainModeObject,
    RoleObject,
    type PlayerViewMode,
    type ReplayGainMode,
    type Session,
    type UpdateEqualizerSettings,
    type UpdateUserSettings,
    type UserSettings,
} from '@/api/generated/models'
import { isUnauthorized } from '@/api/problems'
import { canonicalLocale } from '@/i18n/locales'
import {
    applyAutoplayChange,
    defaultAutoplaySettings,
    toAutoplaySettings,
    type AutoplaySettings,
    type AutoplaySettingsChange,
} from '@/player/autoplay'
import {
    applyCrossfadeChange,
    defaultCrossfadeSettings,
    toCrossfadeSettings,
    type CrossfadeSettings,
    type CrossfadeSettingsChange,
} from '@/player/crossfade'
import {
    defaultEqualizerSettings,
    toEqualizerSettings,
    type EqualizerSettings,
} from '@/player/equalizer'
import {
    applySleepTimerChange,
    defaultSleepTimerSettings,
    toSleepTimerSettings,
    type SleepTimerSettings,
    type SleepTimerSettingsChange,
} from '@/player/sleepTimer'
import { createSettingSaver, mergeChanges } from '@/session/settingSaver'
import {
    clearStoredSession,
    loadStoredSession,
    saveStoredSession,
    toStoredProfile,
    type StoredProfile,
    type StoredSession,
} from '@/session/storage'
import {
    applyThemeChange,
    defaultThemeChoice,
    toThemeChoice,
    toUpdateThemeSettings,
    type ThemeChange,
    type ThemeChoice,
} from '@/themes/choice'
import {
    applyVisualizationChange,
    defaultVisualizationSettings,
    explicitVisualizationChange,
    toVisualizationSettings,
    type VisualizationSettings,
    type VisualizationSettingsChange,
} from '@/visualization/settings'

const replayGainModes = new Set<string>(Object.values(ReplayGainModeObject))

function toReplayGain(settings: UserSettings | null | undefined): ReplayGainMode | null {
    const mode = settings?.replayGain
    return mode && replayGainModes.has(mode) ? mode : null
}

const playerViewModes = new Set<string>(Object.values(PlayerViewModeObject))

function toPlayerViewMode(settings: UserSettings | null | undefined): PlayerViewMode | null {
    const mode = settings?.playerViewMode
    return mode && playerViewModes.has(mode) ? mode : null
}

/** The language of the profile as canonical tag; any language, also one without translations. */
function toLocale(language: string | null | undefined): string | null {
    return canonicalLocale(language)
}

/**
 * The selected profile and its session. Selecting a profile creates a session on the server; its
 * token authenticates all requests and is remembered, so the next start goes straight on. A
 * rejected token ends the session locally (App.vue then shows the profile selection); network
 * errors keep it, so the installed app also starts offline.
 */
export const useSessionStore = defineStore('session', () => {
    const token = ref<string | null>(null)
    const sessionId = ref<string | null>(null)
    const expiresAt = ref<string | null>(null)
    const profile = ref<StoredProfile | null>(null)
    const language = ref<string | null>(null)
    /** Theme of the profile; the remembered one, otherwise the default theme until loaded. */
    const theme = ref<ThemeChoice>(defaultThemeChoice)
    /** ReplayGain mode of the profile; Off until the settings are loaded. */
    const replayGain = ref<ReplayGainMode>(ReplayGainModeObject.Off)
    /** What Now Playing shows for the profile; the video until the settings are loaded. */
    const playerViewMode = ref<PlayerViewMode>(PlayerViewModeObject.Video)
    /** Whether the desktop sidebar of the profile shows only its icons; expanded until loaded. */
    const sidebarCollapsed = ref(false)
    /** Equalizer of the profile; off until the settings are loaded. */
    const equalizer = ref<EqualizerSettings>(defaultEqualizerSettings)
    /** Crossfade of the profile; gapless until the settings are loaded. */
    const crossfade = ref<CrossfadeSettings>(defaultCrossfadeSettings)
    /** Autoplay of the profile; on until the settings are loaded. */
    const autoplay = ref<AutoplaySettings>(defaultAutoplaySettings)
    /** Sleep timer settings of the profile. */
    const sleepTimer = ref<SleepTimerSettings>(defaultSleepTimerSettings)
    /** Visualization of Now Playing of the profile. */
    const visualization = ref<VisualizationSettings>(defaultVisualizationSettings)
    /** Increases with every change of the equalizer, so older answers do not overwrite newer ones. */
    const requests = { equalizer: 0 }
    /**
     * Changes of the settings reach the server one after the other, also of different settings, in
     * the order they were made, like the settings page did while it waited for every save.
     */
    const settingsQueue = createRequestQueue()

    function patchSettings(body: UpdateUserSettings): Promise<UserSettings | undefined> {
        return settingsQueue.run(() => apiClient.api.v1.me.settings.patch(body))
    }

    /**
     * The settings of the settings page show a choice at once and save it one request after the
     * other, only the latest choice (changes of a group merged), so quick changes are neither lost
     * nor reach the server out of order (see createSettingSaver).
     */
    const languageSaver = createSettingSaver({
        get: () => language.value,
        set: (value: string | null) => {
            language.value = value
            persist()
        },
        apply: (_: string | null, locale: string) => locale,
        request: (locale: string) => patchSettings({ language: locale }),
        read: (settings) => toLocale(settings?.language),
    })
    const themeSaver = createSettingSaver({
        get: () => theme.value,
        set: (value: ThemeChoice) => {
            theme.value = value
            persist()
        },
        apply: applyThemeChange,
        merge: mergeChanges,
        request: (change: ThemeChange) => patchSettings({ theme: toUpdateThemeSettings(change) }),
        read: (settings) => toThemeChoice(settings?.theme),
    })
    const replayGainSaver = createSettingSaver({
        get: () => replayGain.value,
        set: (value: ReplayGainMode) => {
            replayGain.value = value
        },
        apply: (_: ReplayGainMode, mode: ReplayGainMode) => mode,
        request: (mode: ReplayGainMode) => patchSettings({ replayGain: mode }),
        read: toReplayGain,
    })
    const crossfadeSaver = createSettingSaver({
        get: () => crossfade.value,
        set: (value: CrossfadeSettings) => {
            crossfade.value = value
        },
        apply: applyCrossfadeChange,
        merge: mergeChanges,
        request: (change: CrossfadeSettingsChange) => patchSettings({ crossfade: { ...change } }),
        read: (settings) => toCrossfadeSettings(settings?.crossfade),
    })
    const autoplaySaver = createSettingSaver({
        get: () => autoplay.value,
        set: (value: AutoplaySettings) => {
            autoplay.value = value
        },
        apply: applyAutoplayChange,
        merge: mergeChanges,
        request: (change: AutoplaySettingsChange) => patchSettings({ autoplay: { ...change } }),
        read: (settings) => toAutoplaySettings(settings?.autoplay),
    })
    const sleepTimerSaver = createSettingSaver({
        get: () => sleepTimer.value,
        set: (value: SleepTimerSettings) => {
            sleepTimer.value = value
        },
        apply: applySleepTimerChange,
        merge: mergeChanges,
        request: (change: SleepTimerSettingsChange) => patchSettings({ sleepTimer: { ...change } }),
        read: (settings) => toSleepTimerSettings(settings?.sleepTimer),
    })
    const visualizationSaver = createSettingSaver({
        get: () => visualization.value,
        set: (value: VisualizationSettings) => {
            visualization.value = value
        },
        apply: applyVisualizationChange,
        merge: mergeChanges,
        request: (change: VisualizationSettingsChange) =>
            patchSettings({ visualization: { ...change } }),
        read: (settings) => toVisualizationSettings(settings?.visualization),
    })
    /**
     * Save view mode and sidebar one request after the other and only the latest choice, so quick
     * changes cannot reach the server out of order and leave an older value stored.
     */
    const playerViewModeSender = createLatestValueSender((mode: PlayerViewMode) =>
        patchSettings({ playerViewMode: mode }),
    )
    const sidebarCollapsedSender = createLatestValueSender((collapsed: boolean) =>
        patchSettings({ sidebarCollapsed: collapsed }),
    )

    const isAuthenticated = computed(
        () =>
            token.value !== null &&
            profile.value !== null &&
            expiresAt.value !== null &&
            Date.parse(expiresAt.value) > Date.now(),
    )
    const isAdmin = computed(
        () => isAuthenticated.value && profile.value?.role === RoleObject.Admin,
    )
    const isBuiltInAdmin = computed(() => isAuthenticated.value && !!profile.value?.isBuiltIn)

    function apply(session: StoredSession | null): void {
        token.value = session?.token ?? null
        sessionId.value = session?.sessionId ?? null
        expiresAt.value = session?.expiresAt ?? null
        profile.value = session?.profile ?? null
        language.value = session?.language ?? null
        resetProfileSettings()
        theme.value = session?.theme ?? defaultThemeChoice
        resetPlaybackSettings()
        sessionTokens.current = token.value
    }

    /**
     * ReplayGain, view mode and sidebar back to their defaults; a waiting choice (also of the
     * language) is dropped.
     */
    function resetProfileSettings(): void {
        replayGain.value = ReplayGainModeObject.Off
        playerViewMode.value = PlayerViewModeObject.Video
        sidebarCollapsed.value = false
        theme.value = defaultThemeChoice
        languageSaver.reset()
        themeSaver.reset()
        replayGainSaver.reset()
        playerViewModeSender.reset()
        sidebarCollapsedSender.reset()
    }

    function resetPlaybackSettings(): void {
        equalizer.value = defaultEqualizerSettings
        crossfade.value = defaultCrossfadeSettings
        autoplay.value = defaultAutoplaySettings
        sleepTimer.value = defaultSleepTimerSettings
        visualization.value = defaultVisualizationSettings
        requests.equalizer++
        crossfadeSaver.reset()
        autoplaySaver.reset()
        sleepTimerSaver.reset()
        visualizationSaver.reset()
    }

    /** Takes over the playback settings of an answer; incomplete groups keep their values. */
    function applyPlaybackSettings(settings: UserSettings | null | undefined): void {
        equalizer.value = toEqualizerSettings(settings?.equalizer) ?? equalizer.value
        crossfade.value = toCrossfadeSettings(settings?.crossfade) ?? crossfade.value
        autoplay.value = toAutoplaySettings(settings?.autoplay) ?? autoplay.value
        sleepTimer.value = toSleepTimerSettings(settings?.sleepTimer) ?? sleepTimer.value
        visualization.value =
            toVisualizationSettings(settings?.visualization) ?? visualization.value
    }

    function persist(): void {
        if (token.value && sessionId.value && expiresAt.value && profile.value) {
            saveStoredSession({
                token: token.value,
                sessionId: sessionId.value,
                expiresAt: expiresAt.value,
                profile: profile.value,
                language: language.value,
                theme: theme.value,
            })
        }
    }

    /** Forgets the session locally, e.g. after the server rejected its token. */
    function clear(): void {
        apply(null)
        clearStoredSession()
    }

    /** Takes over the remembered session; called once on start, before the first navigation. */
    function restore(): void {
        apply(loadStoredSession())
        sessionTokens.onUnauthorized = clear
    }

    function applySession(session: Session | null | undefined): boolean {
        const snapshot = toStoredProfile(session?.profile)
        if (!snapshot || !session?.id || !session.expiresAt) {
            return false
        }

        sessionId.value = session.id
        expiresAt.value = session.expiresAt.toISOString()
        profile.value = snapshot
        return true
    }

    /**
     * Selects a profile, with its PIN if it has one. Another selected profile is signed out first;
     * selecting the current profile again changes nothing. Errors (e.g. profile.not_found,
     * profile.pin_invalid or profile.pin_locked) are passed on.
     */
    async function select(profileId: string, pin?: string): Promise<void> {
        if (isAuthenticated.value && profile.value?.id === profileId) {
            return
        }

        if (isAuthenticated.value) {
            await signOut()
        }

        const created = await apiClient.api.v1.sessions.post(
            pin ? { profileId, pin } : { profileId },
        )
        if (!created?.token) {
            throw new Error('The server returned no session token.')
        }

        token.value = created.token
        sessionTokens.current = created.token
        language.value = null
        resetProfileSettings()
        resetPlaybackSettings()
        if (!applySession(created.session)) {
            clear()
            throw new Error('The server returned an incomplete session.')
        }

        try {
            const settings = await apiClient.api.v1.me.settings.get()
            language.value = toLocale(settings?.language)
            theme.value = toThemeChoice(settings?.theme) ?? defaultThemeChoice
            replayGain.value = toReplayGain(settings) ?? ReplayGainModeObject.Off
            playerViewMode.value = toPlayerViewMode(settings) ?? PlayerViewModeObject.Video
            sidebarCollapsed.value = settings?.sidebarCollapsed ?? false
            applyPlaybackSettings(settings)
        } catch {
            // The language follows with the next refresh; until then the browser language applies.
        }

        persist()
    }

    /** Updates profile and language from the server; a rejected token ends the session. */
    async function refresh(): Promise<void> {
        const requestToken = token.value
        if (!requestToken) {
            return
        }

        try {
            const [session, settings] = await Promise.all([
                apiClient.api.v1.sessions.current.get(),
                apiClient.api.v1.me.settings.get(),
            ])
            if (token.value !== requestToken) {
                return
            }

            applySession(session)
            language.value = toLocale(settings?.language) ?? language.value
            theme.value = toThemeChoice(settings?.theme) ?? theme.value
            replayGain.value = toReplayGain(settings) ?? replayGain.value
            playerViewMode.value = toPlayerViewMode(settings) ?? playerViewMode.value
            sidebarCollapsed.value = settings?.sidebarCollapsed ?? sidebarCollapsed.value
            applyPlaybackSettings(settings)
            persist()
        } catch (error) {
            if (isUnauthorized(error) && token.value === requestToken) {
                clear()
            }
        }
    }

    /**
     * Changes the language of the profile at once and saves it on the server. If saving fails, the
     * saved language comes back, unless a newer one was chosen meanwhile. Errors are passed on.
     */
    function updateLanguage(locale: string): Promise<void> {
        return languageSaver.save(locale)
    }

    /**
     * Shows another theme or appearance at once (no reload) and saves it for the profile like
     * updateLanguage. Errors are passed on.
     */
    function updateTheme(change: ThemeChange): Promise<void> {
        return themeSaver.save(change)
    }

    /** Changes the ReplayGain mode of the profile at once and saves it like updateLanguage. */
    function updateReplayGain(mode: ReplayGainMode): Promise<void> {
        return replayGainSaver.save(mode)
    }

    /**
     * Shows the view mode at once and saves it for the profile on the server. Quick changes are
     * saved one after the other, skipping all but the latest waiting one, so the server keeps the
     * last choice. If saving fails (e.g. offline), the choice stays for this device until the next
     * refresh.
     */
    async function updatePlayerViewMode(mode: PlayerViewMode): Promise<void> {
        const requestToken = token.value
        playerViewMode.value = mode
        try {
            const outcome = await playerViewModeSender.send(mode)
            if (
                outcome.status === 'sent' &&
                outcome.latest &&
                token.value === requestToken &&
                playerViewMode.value === mode
            ) {
                playerViewMode.value = toPlayerViewMode(outcome.result) ?? mode
            }
        } catch {
            // The local choice applies; the server keeps the previous one.
        }
    }

    /**
     * Collapses or expands the desktop sidebar at once and saves it for the profile on the server,
     * one change after the other like updatePlayerViewMode. If saving fails (e.g. offline), the
     * choice stays for this device until the next refresh.
     */
    async function updateSidebarCollapsed(collapsed: boolean): Promise<void> {
        const requestToken = token.value
        sidebarCollapsed.value = collapsed
        try {
            const outcome = await sidebarCollapsedSender.send(collapsed)
            if (
                outcome.status === 'sent' &&
                outcome.latest &&
                token.value === requestToken &&
                sidebarCollapsed.value === collapsed
            ) {
                sidebarCollapsed.value = outcome.result?.sidebarCollapsed ?? collapsed
            }
        } catch {
            // The local choice applies; the server keeps the previous one.
        }
    }

    /**
     * Saves changes of the equalizer for the profile and takes over the answer, unless the profile
     * changed or a newer change was sent meanwhile. Errors are passed on.
     */
    async function updateEqualizer(change: UpdateEqualizerSettings): Promise<void> {
        const requestToken = token.value
        const request = ++requests.equalizer
        const settings = await patchSettings({ equalizer: change })
        if (token.value === requestToken && request === requests.equalizer) {
            equalizer.value = toEqualizerSettings(settings?.equalizer) ?? equalizer.value
        }
    }

    /**
     * Turns the crossfade of the profile on or off at once and saves it; the length stays. Saved
     * like updateCrossfade. Errors are passed on.
     */
    function setCrossfadeEnabled(enabled: boolean): Promise<void> {
        return crossfadeSaver.save({ enabled })
    }

    /**
     * Changes the crossfade of the profile at once and saves it. Changes made while one is saved
     * follow merged into one; if saving fails, the saved settings come back, unless a newer change
     * was made meanwhile. Errors are passed on.
     */
    function updateCrossfade(change: CrossfadeSettingsChange): Promise<void> {
        return crossfadeSaver.save(change)
    }

    /**
     * Changes the autoplay settings of the profile at once and saves them like updateCrossfade.
     * Errors are passed on.
     */
    function updateAutoplay(change: AutoplaySettingsChange): Promise<void> {
        return autoplaySaver.save(change)
    }

    /** Turns autoplay of the profile on or off at once and saves it like updateAutoplay. */
    function setAutoplayEnabled(enabled: boolean): Promise<void> {
        return autoplaySaver.save({ enabled })
    }

    /** Changes the sleep timer settings of the profile at once and saves them like updateCrossfade. */
    function updateSleepTimer(change: SleepTimerSettingsChange): Promise<void> {
        return sleepTimerSaver.save(change)
    }

    /** Changes the visualization of the profile at once and saves it like updateCrossfade. */
    function updateVisualization(change: VisualizationSettingsChange): Promise<void> {
        return visualizationSaver.save(explicitVisualizationChange(visualization.value, change))
    }

    /** Ends the session on the server (if reachable) and forgets it. */
    async function signOut(): Promise<void> {
        if (token.value) {
            try {
                await apiClient.api.v1.sessions.current.delete()
            } catch {
                // Offline or already ended: forgetting the token is enough.
            }
        }

        clear()
    }

    return {
        token,
        sessionId,
        expiresAt,
        profile,
        language,
        theme,
        replayGain,
        playerViewMode,
        sidebarCollapsed,
        equalizer,
        crossfade,
        autoplay,
        sleepTimer,
        visualization,
        isAuthenticated,
        isAdmin,
        isBuiltInAdmin,
        restore,
        select,
        refresh,
        updateLanguage,
        updateTheme,
        updateReplayGain,
        updatePlayerViewMode,
        updateSidebarCollapsed,
        updateEqualizer,
        updateCrossfade,
        setCrossfadeEnabled,
        updateAutoplay,
        setAutoplayEnabled,
        updateSleepTimer,
        updateVisualization,
        signOut,
        clear,
    }
})
