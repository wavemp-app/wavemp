// Types of the parts of butterchurn and butterchurn-presets WaveMP uses; the packages ship none.

declare module 'butterchurn' {
    export interface ButterchurnRendererOptions {
        width?: number
        height?: number
        pixelRatio?: number
        textureRatio?: number
        meshWidth?: number
        meshHeight?: number
    }

    export interface ButterchurnVisualizer {
        connectAudio(node: AudioNode): void
        disconnectAudio(node: AudioNode): void
        loadPreset(preset: object, blendTime?: number): void
        setRendererSize(width: number, height: number, options?: ButterchurnRendererOptions): void
        /**
         * Runs one step of the preset and draws it; elapsedTime gives the seconds of the step
         * (otherwise butterchurn measures the time since the previous call).
         */
        render(options?: { elapsedTime?: number }): void
    }

    export interface Butterchurn {
        createVisualizer(
            context: BaseAudioContext,
            canvas: HTMLCanvasElement,
            options?: ButterchurnRendererOptions,
        ): ButterchurnVisualizer
    }

    const butterchurn: Butterchurn
    export default butterchurn
}

declare module 'butterchurn-presets' {
    export interface ButterchurnPresetPack {
        getPresets(): Record<string, object>
    }

    const presets: ButterchurnPresetPack
    export default presets
}
