import { describe, expect, it, vi } from 'vitest'
import { deviceId, deviceIdKey, deviceName, type DeviceStorage } from '../device'

const agents = {
    firefoxWindows:
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0',
    chromeMac:
        'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) ' +
        'Chrome/131.0.0.0 Safari/537.36',
    edgeWindows:
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) ' +
        'Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0',
    safariIphone:
        'Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like ' +
        'Gecko) Version/18.1 Mobile/15E148 Safari/604.1',
    chromeAndroid:
        'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) ' +
        'Chrome/131.0.0.0 Mobile Safari/537.36',
}

describe('deviceName', () => {
    it('names browser and platform', () => {
        expect(deviceName(agents.firefoxWindows)).toBe('Firefox · Windows')
        expect(deviceName(agents.chromeMac)).toBe('Chrome · macOS')
        expect(deviceName(agents.edgeWindows)).toBe('Edge · Windows')
        expect(deviceName(agents.safariIphone)).toBe('Safari · iPhone')
        expect(deviceName(agents.chromeAndroid)).toBe('Chrome · Android')
    })

    it('falls back to Web for an unknown browser', () => {
        expect(deviceName('')).toBe('Web')
        expect(deviceName('curl/8.0')).toBe('Web')
    })
})

function memoryStorage(initial: Record<string, string> = {}): DeviceStorage & {
    values: Record<string, string>
} {
    const values = { ...initial }
    return {
        values,
        getItem: (key) => values[key] ?? null,
        setItem: (key, value) => {
            values[key] = value
        },
    }
}

describe('deviceId', () => {
    it('keeps the id of the device', () => {
        const storage = memoryStorage({ [deviceIdKey]: 'known' })

        expect(deviceId(storage, () => 'new')).toBe('known')
    })

    it('creates and stores a new id', () => {
        const storage = memoryStorage()
        const create = vi.fn(() => 'x'.repeat(80))

        const id = deviceId(storage, create)

        expect(id).toBe('x'.repeat(64))
        expect(storage.values[deviceIdKey]).toBe(id)
        expect(deviceId(storage, create)).toBe(id)
        expect(create).toHaveBeenCalledTimes(1)
    })

    it('works without storage or with blocked storage', () => {
        const blocked: DeviceStorage = {
            getItem: () => {
                throw new Error('blocked')
            },
            setItem: () => {
                throw new Error('blocked')
            },
        }

        expect(deviceId(null, () => 'page')).toBe('page')
        expect(deviceId(blocked, () => 'page')).toBe('page')
    })
})
