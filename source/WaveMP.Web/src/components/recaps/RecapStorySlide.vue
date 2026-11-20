<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import LibraryCover from '@/components/library/LibraryCover.vue'
import StatisticsChart from './StatisticsChart.vue'
import type { RecapSlide } from '@/recaps/slides'

/**
 * One section of the recap story: title, a large figure with its label, the cover of the song,
 * artist or album it is about, a ranking, a chart or the numbers of the summary, and a caption.
 * data-kind names the section; every element is a part of its own, so a design or theme can
 * animate them one after the other.
 */
defineProps<{ slide: RecapSlide }>()

const { t } = useI18n()
</script>

<template>
    <article data-part="recap-slide" :data-kind="slide.kind">
        <div v-if="slide.coverTrackId !== undefined" data-part="recap-cover">
            <LibraryCover kind="track" :id="slide.coverTrackId" :available="!!slide.coverTrackId" />
        </div>
        <h2 data-part="recap-slide-title">{{ slide.title }}</h2>
        <p v-if="slide.figure" data-part="recap-figure">{{ slide.figure }}</p>
        <p v-if="slide.figureLabel" data-part="recap-figure-label">{{ slide.figureLabel }}</p>
        <ol v-if="slide.items?.length" data-part="recap-ranking">
            <li
                v-for="item in slide.items"
                :key="item.key"
                data-part="recap-ranking-item"
                :data-rank="item.rank"
            >
                <span data-part="recap-rank">{{ item.rank }}</span>
                <LibraryCover
                    kind="track"
                    :id="item.coverTrackId"
                    :available="!!item.coverTrackId"
                />
                <span data-part="recap-ranking-info">
                    <span data-part="recap-ranking-title">{{ item.title }}</span>
                    <span v-if="item.subtitle" data-part="recap-ranking-subtitle">
                        {{ item.subtitle }}
                    </span>
                </span>
                <span data-part="recap-ranking-value">{{ item.value }}</span>
            </li>
        </ol>
        <StatisticsChart
            v-if="slide.bars?.length"
            :bars="slide.bars"
            :label="t('recap.chartLabel', { title: slide.title })"
        />
        <dl v-if="slide.facts?.length" data-part="recap-facts">
            <div
                v-for="fact in slide.facts"
                :key="fact.key"
                data-part="recap-fact"
                :data-item="fact.key"
            >
                <dt data-part="label">{{ fact.label }}</dt>
                <dd data-part="recap-fact-value">{{ fact.value }}</dd>
            </div>
        </dl>
        <p v-if="slide.caption" data-part="recap-caption">{{ slide.caption }}</p>
    </article>
</template>
