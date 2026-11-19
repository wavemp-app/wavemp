// This device for the saved playback state: an id it keeps in the browser, so it recognizes its own
// state, and a name other devices show ("Continue listening from Firefox · Windows"). The name
// carries no words of a language, as devices of the profile may show it in another language.

/** Key of the id of the device in the local storage. */
export const deviceIdKey = 'wavemp.device-id'

const browsers: readonly (readonly [RegExp, string])[] = [
    [/Edg(?:e|A|iOS)?\//, 'Edge'],
    [/OPR\/|Opera/, 'Opera'],
    [/SamsungBrowser\//, 'Samsung Internet'],
    [/Firefox\/|FxiOS\//, 'Firefox'],
    [/Chrome\/|CriOS\//, 'Chrome'],
    [/Safari\//, 'Safari'],
]

const platforms: readonly (readonly [RegExp, string])[] = [
    [/iPhone/, 'iPhone'],
    [/iPad/, 'iPad'],
    [/Android/, 'Android'],
    [/CrOS/, 'ChromeOS'],
    [/Windows/, 'Windows'],
    [/Macintosh|Mac OS X/, 'macOS'],
    [/Linux/, 'Linux'],
]

function find(list: readonly (readonly [RegExp, string])[], value: string): string | null {
    return list.find(([pattern]) => pattern.test(value))?.[1] ?? null
}

/** The name of a device from the user agent of its browser, e.g. "Firefox · Windows". */
export function deviceName(userAgent: string): string {
    const parts = [find(browsers, userAgent), find(platforms, userAgent)].filter(
        (part): part is string => part !== null,
    )
    return parts.length > 0 ? parts.join(' · ') : 'Web'
}

/** Storage of the id; null where the browser has none or blocks it. */
export interface DeviceStorage {
    getItem(key: string): string | null
    setItem(key: string, value: string): void
}

function browserStorage(): DeviceStorage | null {
    try {
        return typeof localStorage === 'undefined' ? null : localStorage
    } catch {
        return null
    }
}

function newId(): string {
    return typeof crypto !== 'undefined' && 'randomUUID' in crypto
        ? crypto.randomUUID()
        : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`
}

/**
 * The id of this device: the stored one, otherwise a new one that is stored. Without storage the
 * id lasts as long as the page.
 */
export function deviceId(
    storage: DeviceStorage | null = browserStorage(),
    create: () => string = newId,
): string {
    try {
        const stored = storage?.getItem(deviceIdKey)
        if (stored) {
            return stored
        }
    } catch {
        // Blocked storage: a new id for this page.
    }

    const id = create().slice(0, 64)
    try {
        storage?.setItem(deviceIdKey, id)
    } catch {
        // The id lasts as long as the page.
    }

    return id
}
