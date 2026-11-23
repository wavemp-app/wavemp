// A stand-in for the AnalyserNode of the browser, for tests of the analysis with real signals: it
// takes the fftSize samples that end at a position of a signal, applies the Blackman window, a
// Fourier transform and the smoothing over time, and reports the spectrum in decibels and in
// bytes like getFloatFrequencyData and getByteFrequencyData (as the Web Audio specification
// describes them).

export class TestAnalyser {
    readonly fftSize: number
    readonly frequencyBinCount: number
    readonly smoothingTimeConstant: number
    readonly minDecibels = -100
    readonly maxDecibels = -30
    private readonly window: Float64Array
    private readonly smoothed: Float64Array
    private readonly real: Float64Array
    private readonly imaginary: Float64Array

    constructor(fftSize: number, smoothingTimeConstant: number) {
        this.fftSize = fftSize
        this.frequencyBinCount = fftSize / 2
        this.smoothingTimeConstant = smoothingTimeConstant
        this.window = new Float64Array(fftSize)
        for (let index = 0; index < fftSize; index++) {
            const x = index / fftSize
            this.window[index] =
                0.42 - 0.5 * Math.cos(2 * Math.PI * x) + 0.08 * Math.cos(4 * Math.PI * x)
        }

        this.smoothed = new Float64Array(this.frequencyBinCount)
        this.real = new Float64Array(fftSize)
        this.imaginary = new Float64Array(fftSize)
    }

    /** Analyses the fftSize samples of the signal (times gain) that end before the index end. */
    analyse(signal: Float32Array, end: number, gain = 1): void {
        const size = this.fftSize
        for (let index = 0; index < size; index++) {
            const position = end - size + index
            const sample = position >= 0 && position < signal.length ? signal[position]! : 0
            this.real[index] = sample * gain * this.window[index]!
            this.imaginary[index] = 0
        }

        transform(this.real, this.imaginary)
        const keep = this.smoothingTimeConstant
        for (let bin = 0; bin < this.frequencyBinCount; bin++) {
            const magnitude = Math.hypot(this.real[bin]!, this.imaginary[bin]!) / size
            this.smoothed[bin] = keep * this.smoothed[bin]! + (1 - keep) * magnitude
        }
    }

    getFloatFrequencyData(out: Float32Array): void {
        for (let bin = 0; bin < this.frequencyBinCount; bin++) {
            out[bin] = 20 * Math.log10(this.smoothed[bin]!)
        }
    }

    getByteFrequencyData(out: Uint8Array): void {
        const scale = 255 / (this.maxDecibels - this.minDecibels)
        for (let bin = 0; bin < this.frequencyBinCount; bin++) {
            const value = scale * (20 * Math.log10(this.smoothed[bin]!) - this.minDecibels)
            out[bin] = Number.isFinite(value) ? Math.max(0, Math.min(255, Math.floor(value))) : 0
        }
    }
}

/** An in-place radix-2 Fourier transform; the length must be a power of two. */
function transform(real: Float64Array, imaginary: Float64Array): void {
    const size = real.length
    for (let index = 1, swap = 0; index < size; index++) {
        let bit = size >> 1
        for (; swap & bit; bit >>= 1) {
            swap ^= bit
        }

        swap ^= bit
        if (index < swap) {
            ;[real[index], real[swap]] = [real[swap]!, real[index]!]
            ;[imaginary[index], imaginary[swap]] = [imaginary[swap]!, imaginary[index]!]
        }
    }

    for (let length = 2; length <= size; length <<= 1) {
        const angle = (-2 * Math.PI) / length
        const stepReal = Math.cos(angle)
        const stepImaginary = Math.sin(angle)
        for (let start = 0; start < size; start += length) {
            let turnReal = 1
            let turnImaginary = 0
            for (let offset = 0; offset < length / 2; offset++) {
                const a = start + offset
                const b = a + length / 2
                const productReal = real[b]! * turnReal - imaginary[b]! * turnImaginary
                const productImaginary = real[b]! * turnImaginary + imaginary[b]! * turnReal
                real[b] = real[a]! - productReal
                imaginary[b] = imaginary[a]! - productImaginary
                real[a] = real[a]! + productReal
                imaginary[a] = imaginary[a]! + productImaginary
                const nextReal = turnReal * stepReal - turnImaginary * stepImaginary
                turnImaginary = turnReal * stepImaginary + turnImaginary * stepReal
                turnReal = nextReal
            }
        }
    }
}
