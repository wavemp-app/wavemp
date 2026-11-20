<script setup lang="ts">
import type { RecapBar } from '@/recaps/slides'

/**
 * Bar chart of one series, e.g. the plays per day or per hour. Each bar is a list item with its
 * value as accessible name; its height is the runtime variable --wmp-bar-value (0 to 1, the
 * highest bar is 1). The highest bar has the state peak. Labels below the axis show for the bars
 * marked labelled. Interactive charts (the statistics page) make the bars focusable and show their
 * value in a tooltip on hover and focus.
 */
withDefaults(
    defineProps<{
        bars: readonly RecapBar[]
        label: string
        interactive?: boolean
    }>(),
    { interactive: false },
)
</script>

<template>
    <div data-part="chart" :data-count="bars.length">
        <ol data-part="chart-bars" :aria-label="label">
            <li
                v-for="bar in bars"
                :key="bar.key"
                data-part="chart-bar"
                :data-state="bar.peak ? 'peak' : undefined"
                :tabindex="interactive ? 0 : undefined"
                :aria-label="bar.description"
                :style="{ '--wmp-bar-value': bar.ratio }"
            >
                <span data-part="chart-bar-fill" aria-hidden="true"></span>
                <span v-if="interactive" data-part="chart-tooltip" aria-hidden="true">
                    {{ bar.description }}
                </span>
                <span
                    data-part="chart-bar-label"
                    :data-state="bar.labelled ? 'shown' : 'hidden'"
                    aria-hidden="true"
                >
                    {{ bar.label }}
                </span>
            </li>
        </ol>
    </div>
</template>
