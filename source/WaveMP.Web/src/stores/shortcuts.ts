import { defineStore } from 'pinia'
import { ref } from 'vue'

/** Whether the overview of the keyboard shortcuts is open (key ?, or the profile menu). */
export const useShortcutsStore = defineStore('shortcuts', () => {
    const overviewOpen = ref(false)

    function openOverview(): void {
        overviewOpen.value = true
    }

    function toggleOverview(): void {
        overviewOpen.value = !overviewOpen.value
    }

    return { overviewOpen, openOverview, toggleOverview }
})
