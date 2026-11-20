import { computed, type Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ListeningStatistics } from '@/api/generated/models'
import { buildRecapSlides, type RecapSlide } from '@/recaps/slides'

/** The sections of the recap story for statistics, in the language of the page. */
export function useRecapSlides(
    statistics: Ref<ListeningStatistics | null | undefined>,
    owner: Ref<string>,
    periodLabel: Ref<string>,
) {
    const { t, locale } = useI18n()
    return computed<RecapSlide[]>(() =>
        statistics.value
            ? buildRecapSlides(
                  statistics.value,
                  { owner: owner.value, periodLabel: periodLabel.value },
                  { t: (key, values) => t(key, values ?? {}), locale: locale.value },
              )
            : [],
    )
}
