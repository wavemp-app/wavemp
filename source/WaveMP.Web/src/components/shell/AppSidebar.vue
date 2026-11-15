<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseTree from '@/components/base/BaseTree.vue'
import type { TreeNode } from '@/components/base/BaseTreeItem.vue'
import { activeTreeId, type RouteInfo } from '@/navigation/breadcrumb'
import {
    buildLibraryTree,
    initiallyExpanded,
    type NavigationTreeNode,
} from '@/navigation/libraryTree'
import { usePlaylistsStore } from '@/stores/playlists'
import { useSessionStore } from '@/stores/session'

const props = withDefaults(
    defineProps<{
        /** Drawer state on mobile; on the desktop the sidebar is always shown. */
        open?: boolean
        /** Desktop only: the sidebar shows only the icons of the tree. */
        collapsed?: boolean
        /** Location that decides the active node; by default the current route. */
        location?: RouteInfo
    }>(),
    { open: false, collapsed: false, location: undefined },
)

const emit = defineEmits<{
    close: []
    /** The button asks to collapse or expand the sidebar (desktop). */
    toggleCollapsed: []
}>()

const { t } = useI18n()
const route = useRoute()
const playlists = usePlaylistsStore()
const session = useSessionStore()

const expanded = ref<string[]>([...initiallyExpanded])

function toTreeNode(node: NavigationTreeNode): TreeNode {
    return {
        id: node.id,
        label: node.label ?? t(node.labelKey),
        icon: node.icon,
        to: node.to,
        disabled: node.disabled,
        children: node.children?.map(toTreeNode),
    }
}

const nodes = computed(() => buildLibraryTree(playlists.playlists).map(toTreeNode))
const active = computed(() => activeTreeId(props.location ?? route))
const state = computed(() =>
    [props.open ? 'open' : 'closed', props.collapsed ? 'collapsed' : 'expanded'].join(' '),
)

// The playlists of the profile below "Playlists", loaded again for every session.
watch(
    () => session.token,
    (token) => {
        if (token) {
            void playlists.ensureLoaded()
        }
    },
    // After the store forgot the playlists of the previous session.
    { immediate: true, flush: 'post' },
)
</script>

<template>
    <aside
        id="wavemp-sidebar"
        data-part="sidebar"
        :data-state="state"
        :aria-label="t('sidebar.label')"
    >
        <BaseButton
            part="btn-sidebar-toggle"
            variant="icon"
            :icon="collapsed ? 'sidebar-expand' : 'sidebar-collapse'"
            :label="collapsed ? t('sidebar.expand') : t('sidebar.collapse')"
            :aria-expanded="collapsed ? 'false' : 'true'"
            aria-controls="wavemp-sidebar"
            @click="emit('toggleCollapsed')"
        />
        <BaseTree
            v-model:expanded="expanded"
            :nodes="nodes"
            :label="t('sidebar.label')"
            :active="active"
            :tooltips="collapsed"
        />
    </aside>
    <div
        data-part="sidebar-backdrop"
        :data-state="open ? 'open' : 'closed'"
        aria-hidden="true"
        @click="emit('close')"
    ></div>
</template>
