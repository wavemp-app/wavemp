import { useI18n } from 'vue-i18n'
import type { MediaFile } from '@/api/generated/models'
import { useFormat } from '@/composables/useFormat'
import { durationSeconds } from '@/library/presentation'

/**
 * The technical details of a file as short texts in the current language: format, bitrate,
 * sample rate, bit depth, channels, resolution, duration, size and the date of the last change.
 */
export function useFileDetails() {
    const { t } = useI18n()
    const format = useFormat()

    return function details(file: MediaFile): string[] {
        const values: string[] = []
        const codec = file.codec ?? file.format
        if (codec) {
            values.push(codec.toUpperCase())
        }

        if (file.bitrate) {
            values.push(
                t('files.bitrate', { value: format.number(Math.round(file.bitrate / 1000)) }),
            )
        }

        if (file.sampleRate) {
            values.push(
                t('files.sampleRate', {
                    value: format.number(file.sampleRate / 1000, { maximumFractionDigits: 1 }),
                }),
            )
        }

        if (file.bitDepth) {
            values.push(t('files.bitDepth', { value: format.number(file.bitDepth) }))
        }

        if (file.channels) {
            values.push(t('files.channels', { count: format.number(file.channels) }))
        }

        if (file.width && file.height) {
            values.push(
                t('files.resolution', {
                    width: format.number(file.width, { useGrouping: false }),
                    height: format.number(file.height, { useGrouping: false }),
                }),
            )
        }

        const seconds = durationSeconds(file.durationMilliseconds)
        if (seconds !== null) {
            values.push(format.duration(seconds))
        }

        values.push(format.fileSize(file.size ?? 0))
        if (file.modifiedAt) {
            values.push(t('files.modified', { date: format.date(file.modifiedAt) }))
        }

        return values
    }
}
