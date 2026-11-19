<script setup lang="ts">
import { computed, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseDialog from '@/components/base/BaseDialog.vue'
import { comboKeys, isApplePlatform } from '@/shortcuts/keys'
import { shortcutGroups, type ShortcutDefinition } from '@/shortcuts/registry'

/**
 * The overview of the keyboard shortcuts (key ?): every defined shortcut with its keys, by group.
 * A combination is a kbd of its keys (shortcut-key, with data-key) and the separators between
 * them; the modifier is Ctrl, or Command on Apple devices.
 */
const props = withDefaults(
    defineProps<{
        /** The defined shortcuts in their order. */
        shortcuts: readonly ShortcutDefinition[]
        /** Whether the device is an Apple one; by default detected. */
        apple?: boolean
    }>(),
    { apple: undefined },
)

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const idPrefix = useId()

const groups = computed(() => {
    const apple = props.apple ?? isApplePlatform()
    return shortcutGroups
        .map((group) => ({
            id: group,
            items: props.shortcuts
                .filter((shortcut) => shortcut.group === group)
                .map((shortcut) => ({
                    id: shortcut.id,
                    label: shortcut.label,
                    combos: shortcut.keys.map((combo) => ({
                        id: combo,
                        keys: comboKeys(combo, apple),
                    })),
                })),
        }))
        .filter((group) => group.items.length > 0)
})
</script>

<template>
    <BaseDialog v-model:open="open" data-item="shortcuts" :title="t('shortcuts.title')">
        <div data-part="shortcut-overview">
            <section
                v-for="group in groups"
                :key="group.id"
                data-part="shortcut-group"
                :data-item="group.id"
                :aria-labelledby="`${idPrefix}-${group.id}`"
            >
                <h3 :id="`${idPrefix}-${group.id}`" data-part="shortcut-group-title">
                    {{ t(`shortcuts.groups.${group.id}`) }}
                </h3>
                <dl data-part="shortcut-list">
                    <div
                        v-for="item in group.items"
                        :key="item.id"
                        data-part="shortcut-item"
                        :data-item="item.id"
                    >
                        <dt data-part="shortcut-description">{{ t(item.label) }}</dt>
                        <dd data-part="shortcut-keys">
                            <kbd
                                v-for="combo in item.combos"
                                :key="combo.id"
                                data-part="shortcut-combo"
                            >
                                <template v-for="(key, index) in combo.keys" :key="key.key">
                                    <span
                                        v-if="index > 0"
                                        data-part="shortcut-key-separator"
                                        aria-hidden="true"
                                        >{{ t('shortcuts.keys.separator') }}</span
                                    >
                                    <kbd data-part="shortcut-key" :data-key="key.key">{{
                                        key.labelKey ? t(key.labelKey) : key.text
                                    }}</kbd>
                                </template>
                            </kbd>
                        </dd>
                    </div>
                </dl>
            </section>
        </div>
        <p data-part="shortcut-hint">{{ t('shortcuts.hint') }}</p>
    </BaseDialog>
</template>
