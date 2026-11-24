<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { LayoutTemplatePath } from '@/api/generated/models'
import { routeSegments } from '@/admin/organize'
import PathRoute from './PathRoute.vue'

/**
 * Where the layout template puts songs: one template-preview-item per song with its new path as a
 * route without origin (state track for songs of the library, sample for example songs, with
 * too-long for paths the scan could not read). A note says whether the songs are examples.
 */
const props = withDefaults(
    defineProps<{
        paths: readonly LayoutTemplatePath[]
        /** Whether the paths belong to example songs, because the library has no songs. */
        samples?: boolean
        loading?: boolean
    }>(),
    { samples: false, loading: false },
)

const { t } = useI18n()

const rows = computed(() =>
    props.paths.map((path, index) => {
        const file = path.extension
            ? `${path.baseName ?? ''}.${path.extension}`
            : (path.baseName ?? '')
        return {
            key: path.trackId ?? `sample-${index}`,
            state: [
                path.trackId ? 'track' : 'sample',
                ...(path.isTooLong ? ['too-long'] : []),
            ].join(' '),
            tooLong: !!path.isTooLong,
            route: routeSegments(null, [...(path.folders ?? []), file]),
        }
    }),
)
</script>

<template>
    <div
        data-part="template-preview"
        :data-state="samples ? 'samples' : 'tracks'"
        :aria-busy="loading ? 'true' : undefined"
    >
        <p data-part="form-note" data-item="template-preview-note">
            {{
                samples
                    ? t('organize.template.previewSamples')
                    : t('organize.template.previewTracks')
            }}
        </p>
        <ul data-part="template-preview-list" :aria-label="t('organize.template.previewLabel')">
            <li
                v-for="row in rows"
                :key="row.key"
                data-part="template-preview-item"
                :data-state="row.state"
            >
                <PathRoute :route="row.route" />
                <span v-if="row.tooLong" data-part="label" data-field="too-long">
                    {{ t('organize.conflicts.pathTooLong') }}
                </span>
            </li>
        </ul>
    </div>
</template>
