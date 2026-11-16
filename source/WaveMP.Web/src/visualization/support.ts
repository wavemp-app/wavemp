// Whether the browser can show the visualizations: they draw with WebGL 2 and listen with Web Audio.

/** Whether WebGL 2 and Web Audio exist; the test context is released at once. */
export function supportsWebGl2Visualization(): boolean {
    if (typeof document === 'undefined' || typeof window === 'undefined') {
        return false
    }

    const audio =
        typeof window.AudioContext === 'function' ||
        typeof (window as unknown as { webkitAudioContext?: unknown }).webkitAudioContext ===
            'function'
    if (!audio) {
        return false
    }

    try {
        const canvas = document.createElement('canvas')
        const context = canvas.getContext('webgl2')
        context?.getExtension('WEBGL_lose_context')?.loseContext()
        return context !== null && context !== undefined
    } catch {
        return false
    }
}
