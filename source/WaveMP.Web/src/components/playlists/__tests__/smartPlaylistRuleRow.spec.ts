import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { SmartPlaylistFieldObject, SmartPlaylistOperatorObject } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { newRule, type RuleDraft } from '@/playlists/smartRules'
import SmartPlaylistRuleRow from '../SmartPlaylistRuleRow.vue'

function mountRow(change: Partial<RuleDraft>) {
    const rule: RuleDraft = {
        ...newRule(SmartPlaylistFieldObject.Artist),
        operator: SmartPlaylistOperatorObject.Is,
        text: 'K.Flay',
        ...change,
    }
    return mount(SmartPlaylistRuleRow, {
        props: { rule, index: 0 },
        global: { plugins: [createWaveMpI18n('en')] },
    })
}

describe('SmartPlaylistRuleRow', () => {
    it('marks a rule the server could not link and explains it below the text', () => {
        const wrapper = mountRow({ referenceId: null })

        expect(wrapper.attributes('data-reference')).toBe('unlinked')
        expect(wrapper.find('[data-part="field-hint"]').text()).toBe(
            'Not found in the library. The rule compares the name as text and does not follow a ' +
                'renamed entry.',
        )
    })

    it('shows a linked rule without a hint', () => {
        const wrapper = mountRow({ referenceId: '01990000-0000-7000-8000-000000000001' })

        expect(wrapper.attributes('data-reference')).toBe('linked')
        expect(wrapper.find('[data-part="field-hint"]').exists()).toBe(false)
    })

    it('forgets the link when the text changes', async () => {
        const wrapper = mountRow({ referenceId: '01990000-0000-7000-8000-000000000001' })

        await wrapper.find('[data-field="rule-text"] input').setValue('K Flay')

        const [[changed]] = wrapper.emitted('update:rule') as [[RuleDraft]]
        expect(changed).toMatchObject({ text: 'K Flay', referenceId: undefined })
    })
})
