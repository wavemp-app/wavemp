import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { FileSongSegment, TrackSongSegments } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'

const api = vi.hoisted(() => ({
    getTrackSongSegments: vi.fn(),
    setTrackSongSegment: vi.fn(),
    resetTrackSongSegment: vi.fn(),
    analyzeTrackSong: vi.fn(),
}))
const playerApi = vi.hoisted(() => ({
    getPlayback: vi.fn(),
    getVideoPlayback: vi.fn(),
    recordPlay: vi.fn(),
    reportNowPlaying: vi.fn(),
}))

vi.mock('@/library/api', () => api)
vi.mock('@/player/api', () => playerApi)

const SongSegmentFields = (await import('../SongSegmentFields.vue')).default
const { usePlayerStore } = await import('@/stores/player')

/** A music video with an intro of 20 s and an outro after 220 s of 250 s, found by alignment. */
function videoFile(extra: Partial<FileSongSegment> = {}): FileSongSegment {
    const segment = {
        method: 'Alignment' as const,
        confidence: 0.92,
        confident: true,
        startMilliseconds: 20_000,
        endMilliseconds: 220_000,
        pieces: [
            {
                fileStartMilliseconds: 20_000,
                songStartMilliseconds: 0,
                durationMilliseconds: 200_000,
            },
        ],
    }
    return {
        durationMilliseconds: 250_000,
        pending: false,
        status: 'Analyzed',
        detected: segment,
        effective: segment,
        manualStartMilliseconds: null,
        manualEndMilliseconds: null,
        ...extra,
    }
}

function audioFile(): FileSongSegment {
    const whole = {
        method: 'WholeFile' as const,
        confidence: 1,
        confident: true,
        startMilliseconds: 0,
        endMilliseconds: 200_000,
        pieces: [],
    }
    return { durationMilliseconds: 200_000, status: 'Analyzed', detected: whole, effective: whole }
}

function segments(video: FileSongSegment = videoFile()): TrackSongSegments {
    return { trackId: 'song', audio: audioFile(), video }
}

async function mountFields() {
    const wrapper = mount(SongSegmentFields, {
        props: { trackId: 'song' },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountFields>>
type Fields = { save: () => Promise<boolean>; dirty: boolean }

function fields(wrapper: Wrapper): Fields {
    return wrapper.vm as unknown as Fields
}

function input(wrapper: Wrapper, kind: string, field: string) {
    return wrapper.get(`[data-kind="${kind}"] [data-field="${field}"] input`)
}

describe('SongSegmentFields', () => {
    let play: ReturnType<typeof vi.spyOn>

    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of [...Object.values(api), ...Object.values(playerApi)]) {
            mock.mockReset()
        }
        api.getTrackSongSegments.mockResolvedValue(segments())
        play = vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue(undefined)
        vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => undefined)
        vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => undefined)
    })

    afterEach(() => {
        vi.restoreAllMocks()
    })

    it('shows per file where the song lies, how it was found and how sure the analysis is', async () => {
        const wrapper = await mountFields()

        expect(api.getTrackSongSegments).toHaveBeenCalledWith('song')
        const video = wrapper.get('[data-part="song-segment"][data-kind="video"]')
        expect(video.attributes('data-state')).toBe('sure')
        expect(video.get('[data-part="song-segment-status"]').text()).toBe(
            'Found by comparing it with the audio file, 92 % sure',
        )
        const piece = video.get('[data-part="song-strip-piece"]')
        expect(piece.attributes('style')).toContain('--wmp-piece-start: 0.08')
        expect(piece.attributes('style')).toContain('--wmp-piece-length: 0.8')
        expect(video.get('[data-part="song-strip-legend"]').text()).toBe(
            'Song from 20.0 s to 220.0 s of 250.0 s',
        )
        const audio = wrapper.get('[data-part="song-segment"][data-kind="audio"]')
        expect(audio.attributes('data-state')).toBe('whole-file')
        expect(
            (input(wrapper, 'video', 'song-start').element as HTMLInputElement).placeholder,
        ).toBe('20')
        wrapper.unmount()
    })

    it('marks an unsure analysis and a video that does not match', async () => {
        api.getTrackSongSegments.mockResolvedValue(
            segments(
                videoFile({
                    detected: { method: 'Structure', confidence: 0.41 },
                    effective: { method: 'Structure', confidence: 0.41, confident: false },
                }),
            ),
        )
        const wrapper = await mountFields()
        const status = wrapper.get('[data-kind="video"] [data-part="song-segment-status"]')
        expect(status.attributes('data-state')).toBe('unsure')
        expect(status.text()).toBe('Found by the pauses in the sound, only 41 % sure: please check')
        expect(status.find('[data-icon="song-segment-notice"]').exists()).toBe(true)
        wrapper.unmount()

        api.getTrackSongSegments.mockResolvedValue(
            segments(videoFile({ effective: { mismatch: true } })),
        )
        const mismatch = await mountFields()
        expect(mismatch.get('[data-kind="video"]').attributes('data-state')).toBe('mismatch')
        mismatch.unmount()
    })

    it('saves a corrected start and end in milliseconds and reports the new segments', async () => {
        const corrected = segments(
            videoFile({
                manualStartMilliseconds: 18_500,
                manualEndMilliseconds: null,
                effective: { manual: true, confident: true, startMilliseconds: 18_500 },
            }),
        )
        api.setTrackSongSegment.mockResolvedValue(corrected)
        const wrapper = await mountFields()

        await input(wrapper, 'video', 'song-start').setValue('18,5')
        expect(fields(wrapper).dirty).toBe(true)
        expect(await fields(wrapper).save()).toBe(true)

        expect(api.setTrackSongSegment).toHaveBeenCalledWith('song', 'video', 18_500, null)
        expect(api.setTrackSongSegment).toHaveBeenCalledOnce()
        expect(wrapper.emitted('saved')).toEqual([[corrected]])
        expect(wrapper.get('[data-kind="video"]').attributes('data-state')).toBe('corrected')
        expect((input(wrapper, 'video', 'song-start').element as HTMLInputElement).value).toBe(
            '18.5',
        )
        expect(fields(wrapper).dirty).toBe(false)
        wrapper.unmount()
    })

    it('saves nothing when nothing changed', async () => {
        const wrapper = await mountFields()

        expect(await fields(wrapper).save()).toBe(true)

        expect(api.setTrackSongSegment).not.toHaveBeenCalled()
        expect(api.resetTrackSongSegment).not.toHaveBeenCalled()
        wrapper.unmount()
    })

    it('removes a correction when both fields are emptied', async () => {
        api.getTrackSongSegments.mockResolvedValue(
            segments(videoFile({ manualStartMilliseconds: 18_500, effective: { manual: true } })),
        )
        api.resetTrackSongSegment.mockResolvedValue(segments())
        const wrapper = await mountFields()

        await wrapper.get('[data-kind="video"] [data-item="use-analysis"]').trigger('click')
        expect(await fields(wrapper).save()).toBe(true)

        expect(api.resetTrackSongSegment).toHaveBeenCalledWith('song', 'video')
        wrapper.unmount()
    })

    it('shows text that is no number at its field without a request', async () => {
        const wrapper = await mountFields()

        await input(wrapper, 'video', 'song-end').setValue('3:40')
        expect(await fields(wrapper).save()).toBe(false)

        expect(api.setTrackSongSegment).not.toHaveBeenCalled()
        expect(wrapper.get('[data-kind="video"] [data-field="song-end"]').text()).toContain(
            'Enter seconds as a number, for example 12.5.',
        )
        wrapper.unmount()
    })

    it('shows the range the server accepts at the rejected field', async () => {
        api.setTrackSongSegment.mockRejectedValue({
            code: 'request.invalid',
            errors: [{ field: 'endMilliseconds', code: 'out_of_range', min: 1_000, max: 250_000 }],
        })
        const wrapper = await mountFields()

        await input(wrapper, 'video', 'song-end').setValue('400')
        expect(await fields(wrapper).save()).toBe(false)

        expect(wrapper.get('[data-kind="video"] [data-field="song-end"]').text()).toContain(
            'Enter seconds from 1 to 250.',
        )
        expect(wrapper.find('[data-part="alert"][data-tone="error"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('queues a new analysis and keeps the fields the admin is editing', async () => {
        api.analyzeTrackSong.mockResolvedValue(undefined)
        const wrapper = await mountFields()
        await input(wrapper, 'video', 'song-start').setValue('12')

        await wrapper.get('[data-item="analyze-song"]').trigger('click')
        await flushPromises()

        expect(api.analyzeTrackSong).toHaveBeenCalledWith('song')
        expect(api.getTrackSongSegments).toHaveBeenCalledTimes(2)
        expect(wrapper.text()).toContain('The song is analyzed again in the background.')
        expect((input(wrapper, 'video', 'song-start').element as HTMLInputElement).value).toBe('12')
        wrapper.unmount()
    })

    it('plays the start of the song in the video as the fields have it', async () => {
        playerApi.getPlayback.mockResolvedValue({
            trackId: 'song',
            method: 'DirectPlay',
            protocol: 'File',
            streamUrl: '/video-sound/song',
            sound: 'Video',
        })
        const player = usePlayerStore()
        const pause = vi.spyOn(player, 'pause')
        const wrapper = await mountFields()
        await input(wrapper, 'video', 'song-start').setValue('30')

        const button = wrapper.get('[data-kind="video"] [data-item="preview-song-start"]')
        await button.trigger('click')
        await flushPromises()

        expect(playerApi.getPlayback).toHaveBeenCalledWith('song', expect.any(Array), 'Video')
        expect(play).toHaveBeenCalledOnce()
        expect(button.attributes('aria-pressed')).toBe('true')
        expect(wrapper.find('[data-kind="video"] [data-part="song-strip-playhead"]').exists()).toBe(
            true,
        )
        expect(pause).not.toHaveBeenCalled()

        await button.trigger('click')
        expect(button.attributes('aria-pressed')).toBe('false')
        expect(wrapper.find('[data-part="song-strip-playhead"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('shows an error when the segments cannot be loaded', async () => {
        api.getTrackSongSegments.mockRejectedValue(new Error('offline'))
        const wrapper = await mountFields()

        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'Start and end of the song could not be loaded.',
        )
        wrapper.unmount()
    })
})
