import type { Page } from '@playwright/test'

// Checks that the labels of the status tiles (`stat-item`) show their whole text.

export interface CutLabel {
    readonly text: string
    readonly reason: string
}

/**
 * The labels of the visible status tiles that do not show their whole text: wider or higher than
 * their box (cut off, for example with an ellipsis) or reaching out of their tile.
 */
export async function cutStatLabels(page: Page): Promise<CutLabel[]> {
    return page.locator("[data-part='stat-item'] > [data-part='label']").evaluateAll((labels) =>
        labels.flatMap((label) => {
            const box = label.getBoundingClientRect()
            if (box.width === 0) {
                return []
            }
            const itemBox = label.parentElement!.getBoundingClientRect()
            const text = label.textContent ?? ''
            if (label.scrollWidth > label.clientWidth + 1) {
                return [{ text, reason: 'wider than its box' }]
            }
            if (label.scrollHeight > label.clientHeight + 1) {
                return [{ text, reason: 'higher than its box' }]
            }
            if (box.left < itemBox.left - 1 || box.right > itemBox.right + 1) {
                return [{ text, reason: 'outside of its tile' }]
            }
            return []
        }),
    )
}

/** The cut off labels as lines for the message of a failed check. */
export function formatCutLabels(labels: readonly CutLabel[]): string {
    return labels.map((label) => `  "${label.text}": ${label.reason}`).join('\n')
}
