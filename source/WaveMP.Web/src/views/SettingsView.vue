<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import BaseSegmentedField from '@/components/base/BaseSegmentedField.vue'
import type { SegmentOption } from '@/components/base/BaseSegmentedControl.vue'
import BaseSelect, { type SelectOption } from '@/components/base/BaseSelect.vue'
import AvatarDialog, { type AvatarDialogProfile } from '@/components/profiles/AvatarDialog.vue'
import ProfileSummary from '@/components/profiles/ProfileSummary.vue'
import DeviceSettings from '@/components/profiles/DeviceSettings.vue'
import ScrobblingServices from '@/components/profiles/ScrobblingServices.vue'
import SubsonicAccess from '@/components/profiles/SubsonicAccess.vue'
import ThemePicker from '@/components/themes/ThemePicker.vue'
import {
    ReplayGainModeObject,
    VisualizationIntensityObject,
    VisualizationQualityObject,
    type ReplayGainMode,
    type VisualizationIntensity,
    type VisualizationQuality,
} from '@/api/generated/models'
import { useFormat } from '@/composables/useFormat'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { appLocale, isSupportedLocale, languageName, supportedLocales } from '@/i18n/locales'
import { maxCrossfadeSeconds, type CrossfadeSettings } from '@/player/crossfade'
import { useCastStore } from '@/stores/cast'
import { useEqualizerStore } from '@/stores/equalizer'
import { useHealthStore } from '@/stores/health'
import { usePlayerStore } from '@/stores/player'
import { useResumeStore } from '@/stores/resume'
import { useSessionStore } from '@/stores/session'
import { isThemeAppearance } from '@/themes/choice'
import {
    availableThemes,
    darkModeMediaQuery,
    hasVariants,
    resolveThemeId,
    themeById,
} from '@/themes/themes'
import { appVersion } from '@/version'
import {
    autoChangeChoices,
    defaultMilkdropSpeed,
    isValidMilkdropSpeed,
    milkdropSpeedChoices,
    type VisualizationSettings,
} from '@/visualization/settings'

/** Settings of the current profile; changes are saved at once. */
const { t, locale } = useI18n({ useScope: 'global' })
const session = useSessionStore()
const equalizer = useEqualizerStore()
const health = useHealthStore()
const cast = useCastStore()
/** Whether this browser plays on in the background without effects (iPhone and iPad). */
const player = usePlayerStore()
const backgroundAudio = player.backgroundAudio
const resume = useResumeStore()
const format = useFormat()

const aboutTitleId = useId()
const appearanceTitleId = useId()
const themeLabelId = useId()
const themeHintId = useId()
const avatarTitleId = useId()
const pinTitleId = useId()
const resumeTitleId = useId()
const devicesTitleId = useId()
const scrobblingTitleId = useId()
const subsonicTitleId = useId()
const developerTitleId = useId()

const avatarOpen = ref(false)

const clearResumeOpen = ref(false)
const clearingResume = ref(false)
const resumeMessage = ref<{ tone: 'success' | 'error'; text: string } | null>(null)

/**
 * Clears the playback state of the profile: the player here stops and empties, and the next
 * start begins without a queue on every device.
 */
async function clearResume(): Promise<void> {
    if (clearingResume.value) {
        return
    }

    clearingResume.value = true
    resumeMessage.value = null
    try {
        await resume.clear()
        resumeMessage.value = { tone: 'success', text: t('settings.resume.cleared') }
    } catch {
        resumeMessage.value = { tone: 'error', text: t('settings.resume.clearError') }
    } finally {
        clearingResume.value = false
        clearResumeOpen.value = false
    }
}

/** The own profile as the avatar dialog shows it. */
const avatarProfile = computed<AvatarDialogProfile | null>(() => {
    const profile = session.profile
    return profile
        ? {
              id: profile.id,
              name: profile.name,
              initials: profile.initials,
              color: profile.color,
              avatarVersion: profile.avatarVersion,
              avatarPresetId: profile.avatarPresetId,
          }
        : null
})

/** Versions of this web app and of the service (from the health check of the app shell). */
const versions = computed(() => [
    { field: 'app-version', labelKey: 'settings.appVersion', value: appVersion },
    {
        field: 'server-version',
        labelKey: 'settings.serverVersion',
        value: health.version ?? t('settings.versionUnknown'),
    },
])

// Every language with a translation file, each in its own language, so it can be found without
// understanding the current one; sorted by these names.
const languageOptions: SelectOption[] = supportedLocales
    .map((value) => ({ value, label: languageName(value) }))
    .sort((a, b) => a.label.localeCompare(b.label, undefined, { sensitivity: 'base' }))

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

// Every setting shows a choice at once and the store saves it in the background, one change after
// the other (see createSettingSaver), so the fields stay usable while a change is saved and quick
// keys or clicks are not lost. If saving fails, the store shows the saved value again.

// The language of the profile as one of the languages of the web app (English for others).
const language = computed(() => (session.language ? appLocale(session.language) : locale.value))
const error = ref<string | null>(null)

/** Saves a change of the profile and shows an error if it fails. */
async function save(change: () => Promise<void>): Promise<void> {
    error.value = null
    try {
        await change()
    } catch {
        error.value = t('settings.saveError')
    }
}

async function changeReplayGain(value: string): Promise<void> {
    if (!replayGainModes.has(value) || value === session.replayGain) {
        return
    }

    await save(() => session.updateReplayGain(value as ReplayGainMode))
}

async function changeLanguage(value: string): Promise<void> {
    if (!isSupportedLocale(value) || value === session.language) {
        return
    }

    await save(() => session.updateLanguage(value))
}

// The theme shows at once (useThemeSync) and is saved in the background like the other settings.
const systemDark = useMediaQuery(darkModeMediaQuery)
const themeId = computed(() => resolveThemeId(session.theme.id))
const chosenTheme = computed(() => themeById(themeId.value))
// The shipped themes and the themes admins uploaded (loaded once a profile is selected).
const themeChoices = computed(() => availableThemes())
const appearanceOptions = computed<SegmentOption[]>(() =>
    (['system', 'light', 'dark'] as const).map((value) => ({
        value,
        label: t(`settings.themeAppearances.${value}`),
    })),
)
/** The appearance only applies to themes with a light and a dark variant. */
const appearanceHint = computed(() =>
    hasVariants(chosenTheme.value)
        ? t('settings.themeAppearanceHint')
        : t('settings.themeAppearanceSingle', { theme: chosenTheme.value.name }),
)

async function changeTheme(id: string): Promise<void> {
    if (id !== session.theme.id) {
        await save(() => session.updateTheme({ id }))
    }
}

async function changeAppearance(value: string): Promise<void> {
    if (isThemeAppearance(value) && value !== session.theme.appearance) {
        await save(() => session.updateTheme({ appearance: value }))
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

const crossfade = computed(() => crossfadeValue(session.crossfade))
const sleepTimerFade = computed(() => String(session.sleepTimer.fadeSeconds))

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

const autoChange = computed(() => autoChangeValue(session.visualization))

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

const intensityOptions = computed<SegmentOption[]>(() => [
    {
        value: VisualizationIntensityObject.Normal,
        item: 'normal',
        label: t('settings.visualizationIntensities.normal'),
    },
    {
        value: VisualizationIntensityObject.Reduced,
        item: 'reduced',
        label: t('settings.visualizationIntensities.reduced'),
    },
])
const intensities = new Set<string>(Object.values(VisualizationIntensityObject))

/** Speeds the menu offers; another value of the profile is added. */
const milkdropSpeedOptions = computed<SelectOption[]>(() =>
    [...new Set([...milkdropSpeedChoices, session.visualization.milkdropSpeed])]
        .sort((a, b) => a - b)
        .map((percent) => ({
            value: String(percent),
            label:
                percent === defaultMilkdropSpeed
                    ? t('settings.visualizationMilkdropSpeedDefault', {
                          percent: format.number(percent),
                      })
                    : t('settings.visualizationMilkdropSpeedPercent', {
                          percent: format.number(percent),
                      }),
        })),
)

const milkdropSpeed = computed(() => String(session.visualization.milkdropSpeed))

const equalizerEnabled = computed(() => equalizer.settings.enabled)

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
    const seconds = Number(value)
    if (value === crossfade.value || (value !== crossfadeOff && !Number.isInteger(seconds))) {
        return
    }

    await save(() =>
        session.updateCrossfade(
            value === crossfadeOff ? { enabled: false } : { enabled: true, seconds },
        ),
    )
}

async function changeCrossfadeAlbums(value: boolean): Promise<void> {
    if (value !== session.crossfade.albumsGapless) {
        await save(() => session.updateCrossfade({ albumsGapless: value }))
    }
}

async function changeAutoplay(value: boolean): Promise<void> {
    if (value !== session.autoplay.enabled) {
        await save(() => session.updateAutoplay({ enabled: value }))
    }
}

async function changeAutoplayVideoOnly(value: boolean): Promise<void> {
    if (value !== session.autoplay.includeVideoOnly) {
        await save(() => session.updateAutoplay({ includeVideoOnly: value }))
    }
}

async function changeVideoAmbient(value: boolean): Promise<void> {
    if (value !== session.video.ambientMode) {
        await save(() => session.updateVideo({ ambientMode: value }))
    }
}

async function changeSkipIntroAndOutro(value: boolean): Promise<void> {
    if (value !== session.video.skipIntroAndOutro) {
        await save(() => session.updateVideo({ skipIntroAndOutro: value }))
    }
}

/**
 * "Off" turns the automatic change off and keeps its interval (the button of the visualization turns
 * it on again); an interval turns it on with that interval.
 */
async function changeAutoChange(value: string): Promise<void> {
    const seconds = Number(value)
    if (value === autoChange.value || (value !== autoChangeOff && !Number.isInteger(seconds))) {
        return
    }

    await save(() =>
        session.updateVisualization(
            value === autoChangeOff
                ? { autoChange: false }
                : { autoChange: true, autoChangeSeconds: seconds },
        ),
    )
}

async function changeQuality(value: string): Promise<void> {
    if (qualities.has(value) && value !== session.visualization.quality) {
        await save(() => session.updateVisualization({ quality: value as VisualizationQuality }))
    }
}

async function changeIntensity(value: string): Promise<void> {
    if (intensities.has(value) && value !== session.visualization.intensity) {
        await save(() =>
            session.updateVisualization({ intensity: value as VisualizationIntensity }),
        )
    }
}

async function changeMilkdropSpeed(value: string): Promise<void> {
    const percent = Number(value)
    if (isValidMilkdropSpeed(percent) && percent !== session.visualization.milkdropSpeed) {
        await save(() => session.updateVisualization({ milkdropSpeed: percent }))
    }
}

async function changeSleepTimerFade(value: string): Promise<void> {
    const fadeSeconds = Number(value)
    if (Number.isInteger(fadeSeconds) && fadeSeconds !== session.sleepTimer.fadeSeconds) {
        await save(() => session.updateSleepTimer({ fadeSeconds }))
    }
}
</script>

<template>
    <div data-part="view" data-view="settings">
        <ProfileSummary
            v-if="session.profile"
            :name="session.profile.name"
            :initials="session.profile.initials"
            :color="session.profile.color"
            :user-id="session.profile.id"
            :avatar-version="session.profile.avatarVersion"
        />
        <BaseAlert v-if="error" tone="error" :message="error" />
        <section data-part="section" data-item="appearance" :aria-labelledby="appearanceTitleId">
            <h2 :id="appearanceTitleId" data-part="section-title">
                {{ t('settings.appearance') }}
            </h2>
            <div data-part="field" data-field="theme">
                <span :id="themeLabelId" data-part="label">{{ t('settings.theme') }}</span>
                <ThemePicker
                    :model-value="themeId"
                    :themes="themeChoices"
                    :labelledby="themeLabelId"
                    :describedby="themeHintId"
                    :appearance="session.theme.appearance"
                    :system-dark="systemDark"
                    @update:model-value="changeTheme"
                />
                <span :id="themeHintId" data-part="field-hint">{{ t('settings.themeHint') }}</span>
            </div>
            <BaseSegmentedField
                :model-value="session.theme.appearance"
                data-field="theme-appearance"
                item="theme-appearance"
                :label="t('settings.themeAppearance')"
                :options="appearanceOptions"
                :hint="appearanceHint"
                :disabled="!hasVariants(chosenTheme)"
                @update:model-value="changeAppearance"
            />
        </section>
        <form data-part="form" @submit.prevent>
            <BaseSelect
                :model-value="language"
                data-field="language"
                :label="t('settings.language')"
                :options="languageOptions"
                :hint="t('settings.languageHint')"
                @update:model-value="changeLanguage"
            />
            <BaseSegmentedField
                :model-value="session.replayGain"
                data-field="replay-gain"
                item="replay-gain"
                :label="t('settings.replayGain')"
                :options="replayGainOptions"
                :hint="t('settings.replayGainHint')"
                :disabled="cast.connected"
                @update:model-value="changeReplayGain"
            />
            <h2 data-part="section-title">{{ t('settings.playback') }}</h2>
            <p v-if="cast.connected" data-part="form-note" data-item="cast-effects">
                {{ t('cast.effectsUnavailable') }}
            </p>
            <p v-else-if="backgroundAudio" data-part="form-note" data-item="background-effects">
                {{ t('player.backgroundEffects') }}
            </p>
            <BaseCheckbox
                data-field="equalizer"
                :model-value="equalizerEnabled"
                :label="t('settings.equalizerOn')"
                :hint="t('settings.equalizerHint')"
                :disabled="cast.connected"
                @update:model-value="changeEqualizer"
            />
            <p data-part="form-note">
                <BaseLink :to="{ name: 'equalizer' }" data-item="open-equalizer">{{
                    t('settings.openEqualizer')
                }}</BaseLink>
            </p>
            <BaseSelect
                :model-value="crossfade"
                data-field="crossfade"
                :label="t('settings.crossfade')"
                :options="crossfadeOptions"
                :hint="t('settings.crossfadeHint')"
                :disabled="cast.connected"
                @update:model-value="changeCrossfade"
            />
            <BaseCheckbox
                :model-value="session.crossfade.albumsGapless"
                data-field="crossfade-albums"
                :label="t('settings.crossfadeAlbums')"
                :disabled="cast.connected || crossfade === crossfadeOff"
                @update:model-value="changeCrossfadeAlbums"
            />
            <BaseCheckbox
                :model-value="session.autoplay.enabled"
                data-field="autoplay"
                :label="t('settings.autoplay')"
                :hint="t('settings.autoplayHint')"
                @update:model-value="changeAutoplay"
            />
            <BaseCheckbox
                :model-value="session.autoplay.includeVideoOnly"
                data-field="autoplay-video-only"
                :label="t('settings.autoplayVideoOnly')"
                :disabled="!session.autoplay.enabled"
                @update:model-value="changeAutoplayVideoOnly"
            />
            <BaseCheckbox
                :model-value="session.video.ambientMode"
                data-field="video-ambient"
                :label="t('settings.videoAmbient')"
                :hint="t('settings.videoAmbientHint')"
                @update:model-value="changeVideoAmbient"
            />
            <BaseCheckbox
                :model-value="session.video.skipIntroAndOutro"
                data-field="video-skip-intro-outro"
                :label="t('settings.videoSkipIntroAndOutro')"
                :hint="t('settings.videoSkipIntroAndOutroHint')"
                @update:model-value="changeSkipIntroAndOutro"
            />
            <BaseSelect
                :model-value="sleepTimerFade"
                data-field="sleep-timer-fade"
                :label="t('settings.sleepTimerFade')"
                :options="sleepTimerFadeOptions"
                :hint="t('settings.sleepTimerFadeHint')"
                @update:model-value="changeSleepTimerFade"
            />
            <BaseSelect
                :model-value="autoChange"
                data-field="visualization-auto-change"
                :label="t('settings.visualizationAutoChange')"
                :options="autoChangeOptions"
                :hint="t('settings.visualizationAutoChangeHint')"
                @update:model-value="changeAutoChange"
            />
            <BaseSegmentedField
                :model-value="session.visualization.quality"
                data-field="visualization-quality"
                item="visualization-quality"
                :label="t('settings.visualizationQuality')"
                :options="qualityOptions"
                :hint="t('settings.visualizationQualityHint')"
                @update:model-value="changeQuality"
            />
            <BaseSegmentedField
                :model-value="session.visualization.intensity"
                data-field="visualization-intensity"
                item="visualization-intensity"
                :label="t('settings.visualizationIntensity')"
                :options="intensityOptions"
                :hint="t('settings.visualizationIntensityHint')"
                @update:model-value="changeIntensity"
            />
            <BaseSelect
                :model-value="milkdropSpeed"
                data-field="visualization-milkdrop-speed"
                :label="t('settings.visualizationMilkdropSpeed')"
                :options="milkdropSpeedOptions"
                :hint="t('settings.visualizationMilkdropSpeedHint')"
                @update:model-value="changeMilkdropSpeed"
            />
        </form>
        <section data-part="section" data-item="resume" :aria-labelledby="resumeTitleId">
            <h2 :id="resumeTitleId" data-part="section-title">
                {{ t('settings.resume.title') }}
            </h2>
            <p data-part="form-note">{{ t('settings.resume.hint') }}</p>
            <BaseAlert
                v-if="resumeMessage"
                :tone="resumeMessage.tone"
                :message="resumeMessage.text"
            />
            <div data-part="view-actions">
                <BaseButton
                    data-item="clear-playback-state"
                    icon="clear-queue"
                    :label="t('settings.resume.clear')"
                    :disabled="clearingResume"
                    @click="clearResumeOpen = true"
                />
            </div>
        </section>
        <BaseDialog
            v-model:open="clearResumeOpen"
            data-item="clear-playback-state"
            :title="t('settings.resume.clearDialog.title')"
        >
            <p>{{ t('settings.resume.clearDialog.text') }}</p>
            <template #footer>
                <BaseButton :label="t('actions.cancel')" @click="clearResumeOpen = false" />
                <BaseButton
                    variant="primary"
                    data-item="confirm-clear-playback-state"
                    :label="t('settings.resume.clearDialog.confirm')"
                    :disabled="clearingResume"
                    @click="clearResume"
                />
            </template>
        </BaseDialog>
        <section data-part="section" data-item="devices" :aria-labelledby="devicesTitleId">
            <h2 :id="devicesTitleId" data-part="section-title">
                {{ t('devices.settings.title') }}
            </h2>
            <p data-part="form-note">{{ t('devices.settings.hint') }}</p>
            <DeviceSettings />
        </section>
        <section data-part="section" data-item="avatar" :aria-labelledby="avatarTitleId">
            <h2 :id="avatarTitleId" data-part="section-title">{{ t('settings.avatar') }}</h2>
            <p data-part="form-note" data-field="avatar-status">
                {{
                    session.profile?.avatarVersion
                        ? t('settings.avatarOn')
                        : t('settings.avatarOff')
                }}
            </p>
            <div data-part="view-actions">
                <BaseButton
                    data-item="change-avatar"
                    icon="change-avatar"
                    :label="t('settings.changeAvatar')"
                    @click="avatarOpen = true"
                />
            </div>
        </section>
        <AvatarDialog
            v-model:open="avatarOpen"
            :target="{ kind: 'me' }"
            :profile="avatarProfile"
            @saved="session.refresh()"
        />
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
        <section data-part="section" data-item="scrobbling" :aria-labelledby="scrobblingTitleId">
            <h2 :id="scrobblingTitleId" data-part="section-title">{{ t('scrobbling.title') }}</h2>
            <p data-part="form-note">{{ t('scrobbling.hint') }}</p>
            <ScrobblingServices />
        </section>
        <section data-part="section" data-item="subsonic" :aria-labelledby="subsonicTitleId">
            <h2 :id="subsonicTitleId" data-part="section-title">{{ t('subsonic.title') }}</h2>
            <p data-part="form-note">{{ t('subsonic.hint') }}</p>
            <SubsonicAccess />
        </section>
        <section data-part="section" data-item="developer" :aria-labelledby="developerTitleId">
            <h2 :id="developerTitleId" data-part="section-title">
                {{ t('settings.developer.title') }}
            </h2>
            <BaseCheckbox
                :model-value="player.videoDiagnosticsEnabled"
                data-field="video-diagnostics"
                :label="t('settings.developer.videoDiagnostics')"
                :hint="t('settings.developer.videoDiagnosticsHint')"
                @update:model-value="player.setVideoDiagnostics"
            />
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
