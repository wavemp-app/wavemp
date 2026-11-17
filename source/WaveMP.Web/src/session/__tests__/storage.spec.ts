import { beforeEach, describe, expect, it } from 'vitest'
import {
    clearStoredSession,
    loadStoredSession,
    saveStoredSession,
    sessionStorageKey,
    toStoredProfile,
    type StoredSession,
} from '../storage'

const now = new Date('2026-11-11T10:00:00Z')

const session: StoredSession = {
    token: 'token',
    sessionId: '0193a5b2-0000-7000-8000-000000000001',
    expiresAt: '2027-11-11T10:00:00.000Z',
    profile: {
        id: '0193a5b2-0000-7000-8000-000000000002',
        name: 'Anna',
        initials: 'A',
        color: '#1f6fd6',
        role: 'User',
        isBuiltIn: false,
        hasPin: true,
        avatarVersion: '18f3a2b4c5d6e7',
        avatarPresetId: 'leaf',
    },
    language: 'de',
}

class MemoryStorage implements Storage {
    private values = new Map<string, string>()

    get length(): number {
        return this.values.size
    }

    clear(): void {
        this.values.clear()
    }

    getItem(key: string): string | null {
        return this.values.get(key) ?? null
    }

    key(index: number): string | null {
        return [...this.values.keys()][index] ?? null
    }

    removeItem(key: string): void {
        this.values.delete(key)
    }

    setItem(key: string, value: string): void {
        this.values.set(key, value)
    }
}

class FailingStorage extends MemoryStorage {
    override getItem(): string | null {
        throw new DOMException('denied', 'SecurityError')
    }

    override setItem(): void {
        throw new DOMException('full', 'QuotaExceededError')
    }

    override removeItem(): void {
        throw new DOMException('denied', 'SecurityError')
    }
}

describe('session storage', () => {
    let storage: MemoryStorage

    beforeEach(() => {
        storage = new MemoryStorage()
    })

    it('saves, loads and clears a session', () => {
        saveStoredSession(session, storage)

        expect(storage.getItem(sessionStorageKey)).not.toBeNull()
        expect(loadStoredSession(now, storage)).toEqual(session)

        clearStoredSession(storage)

        expect(loadStoredSession(now, storage)).toBeNull()
    })

    it('uses a versioned key', () => {
        expect(sessionStorageKey).toBe('wavemp.session.v1')
    })

    it('returns null without a stored session', () => {
        expect(loadStoredSession(now, storage)).toBeNull()
        expect(loadStoredSession(now, null)).toBeNull()
    })

    it('forgets invalid JSON', () => {
        storage.setItem(sessionStorageKey, '{not json')

        expect(loadStoredSession(now, storage)).toBeNull()
    })

    it('forgets sessions with missing or invalid fields', () => {
        const invalid = [
            { ...session, token: '' },
            { ...session, expiresAt: 'tomorrow' },
            { ...session, profile: { ...session.profile, role: 'Owner' } },
            { ...session, profile: { ...session.profile, isBuiltIn: 'no' } },
            { ...session, profile: undefined },
        ]

        for (const value of invalid) {
            storage.setItem(sessionStorageKey, JSON.stringify(value))
            expect(loadStoredSession(now, storage)).toBeNull()
            expect(storage.getItem(sessionStorageKey)).toBeNull()
        }
    })

    it('treats profiles stored before PINs existed as profiles without a PIN', () => {
        const { hasPin: _, ...older } = session.profile
        storage.setItem(sessionStorageKey, JSON.stringify({ ...session, profile: older }))

        expect(loadStoredSession(now, storage)?.profile.hasPin).toBe(false)
    })

    it('treats profiles stored before avatars existed as profiles without an avatar', () => {
        const { avatarVersion: _, avatarPresetId: __, ...older } = session.profile
        storage.setItem(sessionStorageKey, JSON.stringify({ ...session, profile: older }))

        const profile = loadStoredSession(now, storage)?.profile
        expect(profile?.avatarVersion).toBeNull()
        expect(profile?.avatarPresetId).toBeNull()
    })

    it('takes the avatar of a profile of the API', () => {
        const { avatarVersion: _, avatarPresetId: __, ...fromApi } = session.profile

        expect(
            toStoredProfile({ ...fromApi, avatar: { version: 'abc', presetId: null } }),
        ).toMatchObject({ avatarVersion: 'abc', avatarPresetId: null })
        expect(
            toStoredProfile({ ...fromApi, avatar: { version: 'abc', presetId: 'sun' } }),
        ).toMatchObject({ avatarVersion: 'abc', avatarPresetId: 'sun' })
        expect(toStoredProfile({ ...fromApi, avatar: null })).toMatchObject({
            avatarVersion: null,
            avatarPresetId: null,
        })
    })

    it('forgets expired sessions', () => {
        saveStoredSession({ ...session, expiresAt: '2026-11-11T09:59:59.000Z' }, storage)

        expect(loadStoredSession(now, storage)).toBeNull()
        expect(storage.getItem(sessionStorageKey)).toBeNull()
    })

    it('keeps any language, also one without translations', () => {
        storage.setItem(sessionStorageKey, JSON.stringify({ ...session, language: 'pt-br' }))

        expect(loadStoredSession(now, storage)?.language).toBe('pt-BR')
    })

    it('keeps the session but drops an invalid language', () => {
        storage.setItem(sessionStorageKey, JSON.stringify({ ...session, language: 'not a tag' }))

        expect(loadStoredSession(now, storage)?.language).toBeNull()
    })

    it('survives a storage that throws', () => {
        const failing = new FailingStorage()

        expect(() => saveStoredSession(session, failing)).not.toThrow()
        expect(loadStoredSession(now, failing)).toBeNull()
        expect(() => clearStoredSession(failing)).not.toThrow()
    })
})
