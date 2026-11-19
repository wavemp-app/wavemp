import { defineStore } from 'pinia'
import { computed, ref, shallowRef } from 'vue'
import {
    documentPictureInPictureOf,
    miniPlayerModeFor,
    miniPlayerWindowSize,
    mirrorDocument,
    type DocumentPictureInPicture,
    type MiniPlayerMode,
} from '@/player/miniPlayer'

/** Parts of the browser the mini player uses; tests replace them. */
export const miniPlayerDependencies = {
    /** The Document Picture-in-Picture API, or null where the browser lacks it. */
    pictureInPicture: (): DocumentPictureInPicture | null => documentPictureInPictureOf(),
    /** The document of the app, whose styles the floating window takes over. */
    document: (): Document => document,
}

/** Id of the element in the floating window the mini player is shown in. */
export const miniPlayerRootId = 'wavemp-mini-player'

/**
 * The mini player (see src/player/miniPlayer.ts): whether it is shown, and where. open() shows it
 * as floating window where the browser can and as compact view in the window of the app
 * otherwise, or when the browser refuses the window. The floating window closes with its own close
 * button, with close() and when the app goes; the music always plays in the window of the app.
 */
export const useMiniPlayerStore = defineStore('miniPlayer', () => {
    const mode = ref<MiniPlayerMode>('closed')
    /** The element of the floating window the mini player is rendered into. */
    const target = shallowRef<HTMLElement | null>(null)
    /** Whether the floating window is being requested. */
    const opening = ref(false)

    let floating: Window | null = null
    let stopMirror: (() => void) | null = null

    const isOpen = computed(() => mode.value !== 'closed')

    /** Forgets the floating window once it is closed. */
    function onFloatingClosed(): void {
        stopMirror?.()
        stopMirror = null
        floating = null
        target.value = null
        if (mode.value === 'window') {
            mode.value = 'closed'
        }
    }

    /** Requests the floating window and prepares it; null if the browser refuses it. */
    async function requestWindow(api: DocumentPictureInPicture): Promise<Window | null> {
        let opened: Window
        try {
            opened = await api.requestWindow({ ...miniPlayerWindowSize })
        } catch {
            return null
        }

        const app = miniPlayerDependencies.document()
        const doc = opened.document
        doc.title = app.title
        stopMirror = mirrorDocument(app, doc)
        const root = doc.createElement('div')
        root.id = miniPlayerRootId
        doc.body.append(root)
        opened.addEventListener('pagehide', onFloatingClosed, { once: true })
        floating = opened
        target.value = root
        return opened
    }

    /**
     * Opens the mini player. The floating window has to be requested right after a click of the
     * user (the browser asks for that), so this is called from the click handler.
     */
    async function open(): Promise<void> {
        if (mode.value !== 'closed' || opening.value) {
            floating?.focus()
            return
        }

        const api = miniPlayerDependencies.pictureInPicture()
        if (api) {
            opening.value = true
            try {
                const opened = await requestWindow(api)
                mode.value = miniPlayerModeFor(opened !== null)
            } finally {
                opening.value = false
            }
        } else {
            mode.value = miniPlayerModeFor(false)
        }
    }

    /** Closes the mini player: the floating window or the compact view. */
    function close(): void {
        if (floating) {
            const closing = floating
            onFloatingClosed()
            closing.close()
        }

        mode.value = 'closed'
    }

    /** Opens the mini player, or closes it while it is open. */
    function toggle(): Promise<void> {
        if (isOpen.value) {
            close()
            return Promise.resolve()
        }

        return open()
    }

    return { mode, target, opening, isOpen, open, close, toggle }
})
