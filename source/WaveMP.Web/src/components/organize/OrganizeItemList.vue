<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    OrganizeTagFieldObject,
    type OrganizeItem,
    type OrganizeTagField,
    type OrganizeTagValues,
} from '@/api/generated/models'
import {
    conflictKey,
    itemExtensions,
    itemRoute,
    itemStates,
    itemStatusKey,
    warningKey,
} from '@/admin/organize'
import BaseIcon from '@/components/base/BaseIcon.vue'
import PathRoute from './PathRoute.vue'

/**
 * The items of an organize operation like a shipping manifest: per song its title and artist, the
 * route of its files (old path, new path), the extensions of the files that move together, the
 * tags that are written, and its conflict or warnings. The state of an item (pending, conflict,
 * warning, done, failed, skipped, rolled-back, running) is in data-state; conflicts are the loud
 * part of the list.
 */
const props = defineProps<{
    items: readonly OrganizeItem[]
    /** Accessible name of the list. */
    label: string
    /** Whether the status of each item is shown (the log of a run, not a preview). */
    showStatus?: boolean
}>()

const { t } = useI18n()

interface TagRow {
    readonly field: string
    readonly label: string
    readonly value: string
}

const fieldOrder: readonly OrganizeTagField[] = [
    OrganizeTagFieldObject.Album,
    OrganizeTagFieldObject.AlbumArtist,
    OrganizeTagFieldObject.TrackNumber,
    OrganizeTagFieldObject.TrackCount,
    OrganizeTagFieldObject.DiscNumber,
    OrganizeTagFieldObject.Year,
    OrganizeTagFieldObject.Genre,
]

function tagValue(values: OrganizeTagValues, field: OrganizeTagField): string | null {
    switch (field) {
        case OrganizeTagFieldObject.Album:
            return values.album ?? null
        case OrganizeTagFieldObject.AlbumArtist:
            return values.albumArtists?.length ? values.albumArtists.join(', ') : null
        case OrganizeTagFieldObject.TrackNumber:
            return values.trackNumber ? String(values.trackNumber) : null
        case OrganizeTagFieldObject.TrackCount:
            return values.trackCount ? String(values.trackCount) : null
        case OrganizeTagFieldObject.DiscNumber:
            return values.discNumber ? String(values.discNumber) : null
        case OrganizeTagFieldObject.Year:
            return values.year ? String(values.year) : null
        case OrganizeTagFieldObject.Genre:
            return values.genres?.length ? values.genres.join(', ') : null
        default:
            return null
    }
}

function tagRows(item: OrganizeItem): TagRow[] {
    const changes = item.tagChanges
    const fields = changes?.fields ?? []
    if (!changes || fields.length === 0) {
        return []
    }

    return fieldOrder
        .filter((field) => fields.includes(field))
        .map((field) => ({
            field,
            label: t(`organize.tags.${field.charAt(0).toLowerCase()}${field.slice(1)}`),
            value: tagValue(changes, field) ?? t('organize.tags.removed'),
        }))
}

const rows = computed(() =>
    props.items.map((item) => ({
        item,
        key: item.id ?? `${item.sequence}`,
        states: itemStates(item).join(' '),
        route: itemRoute(item),
        extensions: itemExtensions(item),
        tags: tagRows(item),
        title:
            item.displayTitle ||
            (item.kind === 'AlbumCover'
                ? t('organize.items.albumCover')
                : item.kind === 'FolderFiles'
                  ? t('organize.items.folderFiles')
                  : t('organize.items.untitled')),
    })),
)
</script>

<template>
    <ol data-part="organize-items" :aria-label="label">
        <li
            v-for="row in rows"
            :key="row.key"
            data-part="organize-item"
            :data-kind="row.item.kind ?? undefined"
            :data-state="row.states"
        >
            <div data-part="organize-item-header">
                <span data-part="organize-item-title">{{ row.title }}</span>
                <span v-if="row.item.displayArtist" data-part="label" data-field="artist">
                    {{ row.item.displayArtist }}
                </span>
                <span
                    v-for="extension in row.extensions"
                    :key="extension"
                    data-part="badge"
                    :data-state="`file ${extension}`"
                >
                    <span data-part="label">{{ extension }}</span>
                </span>
                <span
                    v-if="showStatus"
                    data-part="badge"
                    :data-state="`status ${row.states.split(' ')[0]}`"
                >
                    <span data-part="label">{{ t(itemStatusKey(row.item.status)) }}</span>
                </span>
            </div>
            <PathRoute :route="row.route" />
            <ul
                v-if="row.tags.length > 0"
                data-part="organize-tags"
                :aria-label="t('organize.tags.label')"
            >
                <li
                    v-for="tag in row.tags"
                    :key="tag.field"
                    data-part="organize-tag"
                    :data-field="tag.field"
                >
                    <span data-part="label" data-field="name">{{ tag.label }}</span>
                    <span data-part="label" data-field="value">{{ tag.value }}</span>
                </li>
            </ul>
            <p v-if="row.item.conflict" data-part="organize-problem" data-state="conflict">
                <BaseIcon name="organize-conflict" />
                <span data-part="label">{{ t(conflictKey(row.item.conflict)) }}</span>
            </p>
            <p
                v-for="warning in row.item.warnings ?? []"
                :key="warning"
                data-part="organize-problem"
                data-state="warning"
                :data-item="warning"
            >
                <BaseIcon name="organize-warning" />
                <span data-part="label">{{ t(warningKey(warning)) }}</span>
            </p>
        </li>
    </ol>
</template>
