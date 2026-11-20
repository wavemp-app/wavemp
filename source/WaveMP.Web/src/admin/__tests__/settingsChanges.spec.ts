import { describe, expect, it } from 'vitest'
import { changedSettings } from '../settingsChanges'

const saved = {
    scanIntervalMinutes: 360,
    fileWatcherEnabled: true,
    audioExtensions: ['mp3', 'flac'],
    coverPriority: 'AlbumFirst',
}

describe('changes of a settings form', () => {
    it('has no changes while every value is as loaded', () => {
        expect(changedSettings(saved, { ...saved, audioExtensions: ['mp3', 'flac'] })).toEqual({})
    })

    it('contains only the values that differ from the loaded settings', () => {
        expect(
            changedSettings(saved, {
                ...saved,
                scanIntervalMinutes: 720,
                fileWatcherEnabled: false,
            }),
        ).toEqual({ scanIntervalMinutes: 720, fileWatcherEnabled: false })
    })

    it('counts a list as changed if an entry or the order changed', () => {
        expect(changedSettings(saved, { ...saved, audioExtensions: ['mp3'] })).toEqual({
            audioExtensions: ['mp3'],
        })
        expect(changedSettings(saved, { ...saved, audioExtensions: ['flac', 'mp3'] })).toEqual({
            audioExtensions: ['flac', 'mp3'],
        })
        expect(changedSettings(saved, { ...saved, audioExtensions: ['mp3', 'ogg'] })).toEqual({
            audioExtensions: ['mp3', 'ogg'],
        })
    })

    it('compares a missing list of the server with an empty one', () => {
        expect(changedSettings({ imageExtensions: null }, { imageExtensions: [] })).toEqual({})
        expect(changedSettings({ imageExtensions: null }, { imageExtensions: ['jpg'] })).toEqual({
            imageExtensions: ['jpg'],
        })
    })

    it('counts a value the server did not send as changed', () => {
        expect(changedSettings({ scanIntervalMinutes: 360 }, { downloadLyrics: false })).toEqual({
            downloadLyrics: false,
        })
    })

    it('sends every value while no settings are loaded', () => {
        expect(changedSettings(null, saved)).toEqual(saved)
        expect(changedSettings(undefined, saved)).toEqual(saved)
    })
})
