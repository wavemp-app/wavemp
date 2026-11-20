<script setup lang="ts">
import { RouterLink, type RouteLocationRaw } from 'vue-router'

export interface StatisticsColumn {
    readonly key: string
    readonly label: string
    /** Numbers are aligned to the end of the cell. */
    readonly numeric?: boolean
}

export interface StatisticsRow {
    readonly key: string
    readonly cells: Readonly<Record<string, string>>
    /** Page the cell of the column `title` links to, e.g. the artist. */
    readonly to?: RouteLocationRaw
}

/**
 * A ranking of the statistics page as table: one row per entry, the columns with data-column
 * (rank, title, artist, plays, time …); numeric columns have the state numeric.
 */
defineProps<{
    /** Id of the heading that names the table. */
    labelledby: string
    columns: readonly StatisticsColumn[]
    rows: readonly StatisticsRow[]
}>()
</script>

<template>
    <div data-part="stats-table-scroll">
        <table data-part="stats-table" :aria-labelledby="labelledby">
            <thead>
                <tr data-part="stats-table-row" data-state="header">
                    <th
                        v-for="column in columns"
                        :key="column.key"
                        scope="col"
                        data-part="stats-table-heading"
                        :data-column="column.key"
                        :data-state="column.numeric ? 'numeric' : undefined"
                    >
                        {{ column.label }}
                    </th>
                </tr>
            </thead>
            <tbody>
                <tr v-for="row in rows" :key="row.key" data-part="stats-table-row">
                    <td
                        v-for="column in columns"
                        :key="column.key"
                        data-part="stats-table-cell"
                        :data-column="column.key"
                        :data-state="column.numeric ? 'numeric' : undefined"
                    >
                        <RouterLink
                            v-if="column.key === 'title' && row.to"
                            data-part="link"
                            :to="row.to"
                        >
                            {{ row.cells[column.key] }}
                        </RouterLink>
                        <template v-else>{{ row.cells[column.key] }}</template>
                    </td>
                </tr>
            </tbody>
        </table>
    </div>
</template>
