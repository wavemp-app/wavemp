<script setup lang="ts">
import { computed } from 'vue'
import { I18nT, useI18n } from 'vue-i18n'
import type { ArtistPictureCandidate, ArtistPictureCredit } from '@/api/generated/models'
import { creditPartsOf } from '@/library/artistPictures'

/**
 * Credit of a picture from an online service, as a quiet caption: the author, the license (a link
 * to its text where the service names an https address) and the source (a link to the page of the
 * picture). Author and license come from the service and are shown as text only, never as HTML;
 * links only lead to https addresses and open in a new tab without the address of WaveMP.
 */
const props = defineProps<{
    credit: ArtistPictureCredit | ArtistPictureCandidate | null | undefined
}>()

const { t } = useI18n()
const parts = computed(() => creditPartsOf(props.credit))
</script>

<template>
    <I18nT
        v-if="parts"
        :keypath="parts.key"
        tag="p"
        data-part="artist-credit"
        :data-provider="credit?.provider ?? undefined"
        scope="global"
    >
        <template #author>
            <span data-part="artist-credit-author">{{ parts.author }}</span>
        </template>
        <template #license>
            <span data-part="artist-credit-license">
                <a
                    v-if="parts.licenseUrl"
                    data-part="link"
                    :href="parts.licenseUrl"
                    target="_blank"
                    rel="noopener noreferrer"
                    >{{ parts.license }}</a
                >
                <template v-else>{{ parts.license }}</template>
            </span>
        </template>
        <template #source>
            <span data-part="artist-credit-source">
                <a
                    v-if="parts.sourceUrl"
                    data-part="link"
                    :href="parts.sourceUrl"
                    target="_blank"
                    rel="noopener noreferrer"
                    >{{ t(parts.providerKey) }}</a
                >
                <template v-else>{{ t(parts.providerKey) }}</template>
            </span>
        </template>
    </I18nT>
</template>
