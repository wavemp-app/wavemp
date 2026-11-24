import { nextTick, onDeactivated, ref, toValue, watch, type MaybeRefOrGetter, type Ref } from 'vue'
import { usePinchIn } from './usePinchIn'
import { useShortcut } from './useShortcut'
import {
    afterTransition,
    firstVisiblePosition,
    groupOverviewBlocked,
    prefersReducedMotion,
    scrollToGroup,
} from '@/library/groupJump'
import { useFullPlayerStore } from '@/stores/fullPlayer'
import { useMiniPlayerStore } from '@/stores/miniPlayer'

export interface GroupJumpOptions {
    /** The grouped list: its groups carry data-position, its state gets "zoomed-out". */
    readonly list: Ref<HTMLElement | null | undefined>
    /** Whether the list has at least two groups to jump between. */
    readonly available: MaybeRefOrGetter<boolean>
    /** Loads the groups of the whole list for the overview, if they come from the API. */
    readonly loadGroups?: () => Promise<unknown>
    /**
     * Shows a list that is loaded page by page from the position on, when the group is not loaded;
     * resolves true when it is. Lists that are loaded completely have none.
     */
    readonly jumpTo?: (position: number) => Promise<boolean>
}

/** Where the overview opens from: the heading of a group (its position and button), or none. */
export interface GroupJumpOrigin {
    readonly position?: number
    readonly opener?: HTMLElement | null
}

/**
 * The quick jump of a grouped list: opens the overview of its groups by a click on a heading, by
 * Ctrl+Minus or Escape (only while nothing else takes the key) and by pinching the list on touch
 * screens, with the group the person looks at as the current one. Choosing a group closes the
 * overview, lets the list zoom back in and scrolls it to the group; a group that is not loaded is
 * loaded first (jumpTo), without the pages before it. The focus then lies on the heading of the
 * group; closing the overview without a choice returns it to where it was.
 */
export function useGroupJump(options: GroupJumpOptions) {
    const open = ref(false)
    /** Position of the current group: the clicked heading, or the first group in view. */
    const current = ref<number | undefined>(undefined)
    const fullPlayer = useFullPlayerStore()
    const miniPlayer = useMiniPlayerStore()
    let opener: HTMLElement | null = null
    let chosen = false

    function groupElements(): HTMLElement[] {
        const list = options.list.value
        return list ? Array.from(list.querySelectorAll<HTMLElement>('[data-position]')) : []
    }

    function shown(): boolean {
        const list = options.list.value
        return !!list && list.isConnected && list.getClientRects().length > 0
    }

    function openOverview(origin: GroupJumpOrigin = {}): boolean {
        if (open.value || !toValue(options.available)) {
            return false
        }

        current.value = origin.position ?? firstVisiblePosition(groupElements())
        const active = options.list.value?.ownerDocument.activeElement
        opener = origin.opener ?? (active instanceof HTMLElement ? active : null)
        chosen = false
        open.value = true
        void options.loadGroups?.()
        return true
    }

    function find(position: number): HTMLElement | undefined {
        return groupElements().find((element) => element.dataset.position === String(position))
    }

    /** Closes the overview and jumps to the group that starts at the position. */
    async function choose(position: number): Promise<void> {
        chosen = true
        open.value = false
        await nextTick()

        let element = find(position)
        let jumped = false
        if (!element && options.jumpTo) {
            await options.jumpTo(position)
            jumped = true
            await nextTick()
            element = find(position) ?? groupElements()[0]
        }

        if (!element) {
            return
        }

        // The list zooms back in first, so the group is scrolled to where it ends up.
        await afterTransition(options.list.value)
        scrollToGroup(element, !jumped && !prefersReducedMotion())
        const heading =
            element.querySelector<HTMLElement>("[data-part='btn-group-jump']") ?? element
        heading.focus({ preventScroll: true })
    }

    function close(): void {
        open.value = false
    }

    // Closed without a choice (button, Escape): the focus returns to where it was.
    watch(open, (isOpen, wasOpen) => {
        if (!isOpen && wasOpen && !chosen && opener?.isConnected) {
            opener.focus({ preventScroll: true })
        }
    })

    // A page kept in the background closes its overview.
    onDeactivated(close)

    useShortcut('group-overview', (event) => {
        if (
            open.value ||
            !toValue(options.available) ||
            !shown() ||
            groupOverviewBlocked(event, {
                fullPlayerExpanded: fullPlayer.expanded,
                compactInline: miniPlayer.mode === 'inline',
            })
        ) {
            return false
        }

        return openOverview()
    })

    usePinchIn(options.list, () => openOverview(), options.available)

    return { open, current, openOverview, choose, close }
}
