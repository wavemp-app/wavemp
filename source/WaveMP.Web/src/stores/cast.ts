import { defineStore } from 'pinia'
import { computed, markRaw, ref } from 'vue'
import { createBrowserCastLayer, type CastLayer } from '@/casting/layer'
import {
    noCastStatus,
    type CastAvailability,
    type CastConnection,
    type CastFormats,
    type CastStatus,
    type CastTechnology,
} from '@/casting/types'
import { browserAudioFormats } from '@/player/formats'
import { browserVideoFormats } from '@/player/videoFormats'

/** Parts of the browser the store uses; tests replace them with a simulated device. */
export const castDependencies = {
    createLayer: (): CastLayer =>
        createBrowserCastLayer((): CastFormats => ({
            audio: browserAudioFormats(),
            video: browserVideoFormats(),
        })),
}

/**
 * Casting of this browser: the technique (Chromecast or AirPlay), whether it is available, the
 * connected device and its volume. The store wraps the casting layer for the UI; the player store
 * switches its playback to the device while one is connected.
 */
export const useCastStore = defineStore('cast', () => {
    const layer = markRaw(castDependencies.createLayer())
    const status = ref<CastStatus>(layer.status ?? noCastStatus)

    layer.onChange(() => {
        status.value = layer.status
    })
    void layer.start().then(() => {
        status.value = layer.status
    })

    const technology = computed<CastTechnology | null>(() => status.value.technology)
    const availability = computed<CastAvailability>(() => status.value.availability)
    const connection = computed<CastConnection>(() => status.value.connection)
    const deviceName = computed(() => status.value.deviceName)
    const devicesFound = computed(() => status.value.devicesFound)
    const volume = computed(() => status.value.volume)
    const muted = computed(() => status.value.muted)
    /** Whether the cast button shows: the browser can cast, maybe only over HTTPS. */
    const visible = computed(() => availability.value !== 'unsupported')
    /** Whether a device plays the music. */
    const connected = computed(
        () => availability.value === 'available' && connection.value === 'connected',
    )

    /**
     * Opens the device selection of the browser or the operating system; also the place to stop
     * casting. Must run within the click. The preview is the stream of the current track, which
     * AirPlay needs to offer its devices.
     */
    function requestSession(preview?: string | null): void {
        if (availability.value === 'available') {
            void layer.requestSession(preview)
        }
    }

    function endSession(): void {
        layer.endSession()
    }

    function setVolume(value: number): void {
        layer.setVolume(value)
    }

    function setMuted(value: boolean): void {
        layer.setMuted(value)
    }

    return {
        layer,
        status,
        technology,
        availability,
        connection,
        deviceName,
        devicesFound,
        volume,
        muted,
        visible,
        connected,
        requestSession,
        endSession,
        setVolume,
        setMuted,
    }
})
