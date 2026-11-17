<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import BaseSegmentedField from '@/components/base/BaseSegmentedField.vue'
import type { SegmentOption } from '@/components/base/BaseSegmentedControl.vue'
import BaseSelect, { type SelectOption } from '@/components/base/BaseSelect.vue'
import ProfileSummary from '@/components/profiles/ProfileSummary.vue'
import {
    ReplayGainModeObject,
    VisualizationQualityObject,
    type ReplayGainMode,
    type VisualizationQuality,
} from '@/api/generated/models'
import { useFormat } from '@/composables/useFormat'
import { isSupportedLocale, type SupportedLocale } from '@/i18n/locales'
import { maxCrossfadeSeconds, type CrossfadeSettings } from '@/player/crossfade'
import { useEqualizerStore } from '@/stores/equalizer'
import { useHealthStore } from '@/stores/health'
import { useSessionStore } from '@/stores/session'
import { appVersion } from '@/version'
import { autoChangeChoices, type VisualizationSettings } from '@/visualization/settings'

/** Settings of the current profile; changes are saved at once. */
const { t, locale } = useI18n({ useScope: 'global' })
const session = useSessionStore()
const equalizer = useEqualizerStore()
const health = useHealthStore()
const format = useFormat()

const aboutTitleId = useId()
const pinTitleId = useId()

/** Versions of this web app and of the service (from the health check of the app shell). */
const versions = computed(() => [
    { field: 'app-version', labelKey: 'settings.appVersion', value: appVersion },
    {
        field: 'server-version',
        labelKey: 'settings.serverVersion',
        value: health.version ?? t('settings.versionUnknown'),
    },
])

// Every language in its own language, so it can be found without understanding the current one.
const languageOptions = computed<SelectOption[]>(() => [
    { value: 'en', label: t('settings.languages.en') },
    { value: 'de', label: t('settings.languages.de') },
])

const replayGainOptions = computed<SegmentOption[]>(() => [
    { value: ReplayGainModeObject.Off, item: 'off', label: t('settings.replayGainModes.off') },
    {
        value: ReplayGainModeObject.Track,
        item: 'track',
        label: t('settings.replayGainModes.track'),
    },
    {
        value: ReplayGainModeObject.Album,
        item: 'album',
        label: t('settings.replayGainModes.album'),
    },
])
const replayGainModes = new Set<string>(Object.values(ReplayGainModeObject))

const language = ref<string>(session.language ?? locale.value)
const replayGain = ref<string>(session.replayGain)
const saving = ref(false)
const error = ref<string | null>(null)

watch(
    () => session.language,
    (value) => {
        if (value && !saving.value) {
            language.value = value
        }
    },
)

watch(
    () => session.replayGain,
    (value) => {
        if (!saving.value) {
            replayGain.value = value
        }
    },
)

async function changeReplayGain(value: string): Promise<void> {
    if (!replayGainModes.has(value) || value === session.replayGain) {
        return
    }

    const previous = session.replayGain
    saving.value = true
    error.value = null
    try {
        await session.updateReplayGain(value as ReplayGainMode)
    } catch {
        error.value = t('settings.saveError')
        replayGain.value = previous
    } finally {
        saving.value = false
    }
}

async function changeLanguage(value: string): Promise<void> {
    if (!isSupportedLocale(value) || value === session.language) {
        return
    }

    const previous: SupportedLocale | null = session.language
    saving.value = true
    error.value = null
    try {
        await session.updateLanguage(value)
    } catch {
        error.value = t('settings.saveError')
        language.value = previous ?? locale.value
    } finally {
        saving.value = false
    }
}
/** Value of the crossfade menu: "off" while the crossfade is off, otherwise its seconds. */
const crossfadeOff = 'off'

function crossfadeValue(settings: CrossfadeSettings): string {
    return settings.enabled && settings.seconds > 0 ? String(settings.seconds) : crossfadeOff
}

const crossfadeOptions = computed<SelectOption[]>(() => [
    { value: crossfadeOff, label: t('settings.crossfadeOff') },
    ...Array.from({ length: maxCrossfadeSeconds }, (_, index) => ({
        value: String(index + 1),
        label: t('settings.crossfadeSeconds', { seconds: format.number(index + 1) }),
    })),
])

/** Seconds of the fade-out the menu offers; another value of the profile is added. */
const sleepTimerFadeChoices = [5, 10, 15, 30, 60]
const sleepTimerFadeOptions = computed<SelectOption[]>(() =>
    [...new Set([...sleepTimerFadeChoices, session.sleepTimer.fadeSeconds])]
        .sort((a, b) => a - b)
        .map((seconds) => ({
            value: String(seconds),
            label: t('settings.sleepTimerFadeSeconds', { seconds: format.number(seconds) }),
        })),
)

const crossfade = ref(crossfadeValue(session.crossfade))
const crossfadeAlbums = ref(session.crossfade.albumsGapless)
const sleepTimerFade = ref(String(session.sleepTimer.fadeSeconds))

watch(
    () => session.crossfade,
    (value) => {
        if (!saving.value) {
            crossfade.value = crossfadeValue(value)
            crossfadeAlbums.value = value.albumsGapless
        }
    },
)

watch(
    () => session.sleepTimer,
    (value) => {
        if (!saving.value) {
            sleepTimerFade.value = String(value.fadeSeconds)
        }
    },
)

/** Value of the menu of the automatic change: "off" while it is off, otherwise its seconds. */
const autoChangeOff = 'off'

function autoChangeValue(settings: VisualizationSettings): string {
    return settings.autoChange ? String(settings.autoChangeSeconds) : autoChangeOff
}

function autoChangeLabel(seconds: number): string {
    return seconds >= 120 && seconds % 60 === 0
        ? t('settings.visualizationAutoChangeMinutes', { minutes: format.number(seconds / 60) })
        : t('settings.visualizationAutoChangeSeconds', { seconds: format.number(seconds) })
}

/** Intervals the menu offers; another value of the profile is added. */
const autoChangeOptions = computed<SelectOption[]>(() => [
    { value: autoChangeOff, label: t('settings.visualizationAutoChangeOff') },
    ...[...new Set([...autoChangeChoices, session.visualization.autoChangeSeconds])]
        .sort((a, b) => a - b)
        .map((seconds) => ({ value: String(seconds), label: autoChangeLabel(seconds) })),
])

const autoChange = ref(autoChangeValue(session.visualization))

const qualityOptions = computed<SegmentOption[]>(() => [
    {
        value: VisualizationQualityObject.Auto,
        item: 'auto',
        label: t('settings.visualizationQualities.auto'),
    },
    {
        value: VisualizationQualityObject.Low,
        item: 'low',
        label: t('settings.visualizationQualities.low'),
    },
    {
        value: VisualizationQualityObject.Medium,
        item: 'medium',
        label: t('settings.visualizationQualities.medium'),
    },
    {
        value: VisualizationQualityObject.High,
        item: 'high',
        label: t('settings.visualizationQualities.high'),
    },
])
const qualities = new Set<string>(Object.values(VisualizationQualityObject))

const quality = ref<string>(session.visualization.quality)

watch(
    () => session.visualization,
    (value) => {
        if (!saving.value) {
            autoChange.value = autoChangeValue(value)
            quality.value = value.quality
        }
    },
)

const equalizerEnabled = computed(() => equalizer.settings.enabled)

/** Saves a change of the profile and shows an error if it fails; reset restores the field. */
async function save(change: () => Promise<void>, reset: () => void): Promise<void> {
    saving.value = true
    error.value = null
    try {
        await change()
    } catch {
        error.value = t('settings.saveError')
        reset()
    } finally {
        saving.value = false
    }
}

async function changeEqualizer(enabled: boolean): Promise<void> {
    error.value = null
    equalizer.setEnabled(enabled)
    await equalizer.flush()
    if (equalizer.saveError) {
        equalizer.dismissError()
        error.value = t('settings.saveError')
    }
}

/**
 * "Off" turns the crossfade off and keeps its length (the switch in the transport bar turns it on
 * again); a length turns it on with that length.
 */
async function changeCrossfade(value: string): Promise<void> {
    const previous = crossfadeValue(session.crossfade)
    const seconds = Number(value)
    if (value === previous || (value !== crossfadeOff && !Number.isInteger(seconds))) {
        return
    }

    await save(
        () =>
            session.updateCrossfade(
                value === crossfadeOff ? { enabled: false } : { enabled: true, seconds },
            ),
        () => {
            crossfade.value = previous
        },
    )
}

async function changeCrossfadeAlbums(value: boolean): Promise<void> {
    if (value === session.crossfade.albumsGapless) {
        return
    }

    const previous = session.crossfade.albumsGapless
    await save(
        () => session.updateCrossfade({ albumsGapless: value }),
        () => {
            crossfadeAlbums.value = previous
        },
    )
}

/**
 * "Off" turns the automatic change off and keeps its interval (the button of the visualization turns
 * it on again); an interval turns it on with that interval.
 */
async function changeAutoChange(value: string): Promise<void> {
    const previous = autoChangeValue(session.visualization)
    const seconds = Number(value)
    if (value === previous || (value !== autoChangeOff && !Number.isInteger(seconds))) {
        return
    }

    await save(
        () =>
            session.updateVisualization(
                value === autoChangeOff
                    ? { autoChange: false }
                    : { autoChange: true, autoChangeSeconds: seconds },
            ),
        () => {
            autoChange.value = previous
        },
    )
}

async function changeQuality(value: string): Promise<void> {
    if (!qualities.has(value) || value === session.visualization.quality) {
        return
    }

    const previous = session.visualization.quality
    await save(
        () => session.updateVisualization({ quality: value as VisualizationQuality }),
        () => {
            quality.value = previous
        },
    )
}

async function changeSleepTimerFade(value: string): Promise<void> {
    const fadeSeconds = Number(value)
    if (!Number.isInteger(fadeSeconds) || fadeSeconds === session.sleepTimer.fadeSeconds) {
        return
    }

    const previous = session.sleepTimer.fadeSeconds
    await save(
        () => session.updateSleepTimer({ fadeSeconds }),
        () => {
            sleepTimerFade.value = String(previous)
        },
    )
}
</script>

<template>
    <div data-part="view" data-view="settings">
        <ProfileSummary
            v-if="session.profile"
            :name="session.profile.name"
            :initials="session.profile.initials"
            :color="session.profile.color"
        />
        <BaseAlert v-if="error" tone="error" :message="error" />
        <form data-part="form" @submit.prevent>
            <BaseSelect
                v-model="language"
                data-field="language"
                :label="t('settings.language')"
                :options="languageOptions"
                :hint="t('settings.languageHint')"
                :disabled="saving"
                @update:model-value="changeLanguage"
            />
            <BaseSegmentedField
                v-model="replayGain"
                data-field="replay-gain"
                item="replay-gain"
                :label="t('settings.replayGain')"
                :options="replayGainOptions"
                :hint="t('settings.replayGainHint')"
                :disabled="saving"
                @update:model-value="changeReplayGain"
            />
            <h2 data-part="section-title">{{ t('settings.playback') }}</h2>
            <BaseCheckbox
                data-field="equalizer"
                :model-value="equalizerEnabled"
                :label="t('settings.equalizerOn')"
                :hint="t('settings.equalizerHint')"
                @update:model-value="changeEqualizer"
            />
            <p data-part="form-note">
                <BaseLink :to="{ name: 'equalizer' }" data-item="open-equalizer">{{
                    t('settings.openEqualizer')
                }}</BaseLink>
            </p>
            <BaseSelect
                v-model="crossfade"
                data-field="crossfade"
                :label="t('settings.crossfade')"
                :options="crossfadeOptions"
                :hint="t('settings.crossfadeHint')"
                :disabled="saving"
                @update:model-value="changeCrossfade"
            />
            <BaseCheckbox
                v-model="crossfadeAlbums"
                data-field="crossfade-albums"
                :label="t('settings.crossfadeAlbums')"
                :disabled="saving || crossfade === crossfadeOff"
                @update:model-value="changeCrossfadeAlbums"
            />
            <BaseSelect
                v-model="sleepTimerFade"
                data-field="sleep-timer-fade"
                :label="t('settings.sleepTimerFade')"
                :options="sleepTimerFadeOptions"
                :hint="t('settings.sleepTimerFadeHint')"
                :disabled="saving"
                @update:model-value="changeSleepTimerFade"
            />
            <BaseSelect
                v-model="autoChange"
                data-field="visualization-auto-change"
                :label="t('settings.visualizationAutoChange')"
                :options="autoChangeOptions"
                :hint="t('settings.visualizationAutoChangeHint')"
                :disabled="saving"
                @update:model-value="changeAutoChange"
            />
            <BaseSegmentedField
                v-model="quality"
                data-field="visualization-quality"
                item="visualization-quality"
                :label="t('settings.visualizationQuality')"
                :options="qualityOptions"
                :hint="t('settings.visualizationQualityHint')"
                :disabled="saving"
                @update:model-value="changeQuality"
            />
        </form>
        <section data-part="section" data-item="pin" :aria-labelledby="pinTitleId">
            <h2 :id="pinTitleId" data-part="section-title">{{ t('settings.pin') }}</h2>
            <p data-part="form-note" data-field="pin-status">
                {{ session.profile?.hasPin ? t('settings.pinOn') : t('settings.pinOff') }}
            </p>
            <p data-part="form-note">
                <BaseLink :to="{ name: 'settings-pin' }" data-item="open-pin">{{
                    t('settings.openPin')
                }}</BaseLink>
            </p>
        </section>
        <section data-part="section" data-item="about" :aria-labelledby="aboutTitleId">
            <h2 :id="aboutTitleId" data-part="section-title">{{ t('settings.about') }}</h2>
            <dl data-part="stat-list">
                <div
                    v-for="item in versions"
                    :key="item.field"
                    data-part="stat-item"
                    :data-field="item.field"
                >
                    <dt data-part="label">{{ t(item.labelKey) }}</dt>
                    <dd data-part="stat-value">{{ item.value }}</dd>
                </div>
            </dl>
        </section>
    </div>
</template>
