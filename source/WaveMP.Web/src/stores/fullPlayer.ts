import { defineStore } from 'pinia'
import { ref } from 'vue'

/**
 * Whether the full-screen player (mobile) is open. The shell opens and closes it; the stage of Now
 * Playing below it rests meanwhile, so the visualization is not drawn twice.
 */
export const useFullPlayerStore = defineStore('fullPlayer', () => {
    const expanded = ref(false)

    function expand(): void {
        expanded.value = true
    }

    function collapse(): void {
        expanded.value = false
    }

    return { expanded, expand, collapse }
})
