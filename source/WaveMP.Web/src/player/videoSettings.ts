// Settings of the videos of the Now Playing stage for a profile, as the API carries them in the
// group video of the user settings. Pure functions, so the store and its tests share the rules.

/** The video settings of a profile. */
export interface VideoSettings {
    /** Whether videos show the ambient light in the free area around their picture. */
    readonly ambientMode: boolean
}

/** On, like on the server, until the settings are loaded. */
export const defaultVideoSettings: VideoSettings = { ambientMode: true }

/** The video settings from an answer of the server, or null if they are incomplete. */
export function toVideoSettings(value: unknown): VideoSettings | null {
    const settings = value as Partial<Record<keyof VideoSettings, unknown>> | null | undefined
    if (!settings || typeof settings.ambientMode !== 'boolean') {
        return null
    }

    return { ambientMode: settings.ambientMode }
}

/** A change of the video settings, as the API takes it: missing or null values stay unchanged. */
export interface VideoSettingsChange {
    readonly ambientMode?: boolean | null
}

/** The settings after a change, as the server applies it. */
export function applyVideoSettingsChange(
    settings: VideoSettings,
    change: VideoSettingsChange,
): VideoSettings {
    return { ambientMode: change.ambientMode ?? settings.ambientMode }
}
