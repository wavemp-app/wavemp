import { globalIgnores } from 'eslint/config'
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'
import pluginVue from 'eslint-plugin-vue'
import pluginOxlint from 'eslint-plugin-oxlint'
import skipFormatting from 'eslint-config-prettier/flat'
import stylistic from '@stylistic/eslint-plugin'

// To allow more languages other than `ts` in `.vue` files, uncomment the following lines:
// import { configureVueProject } from '@vue/eslint-config-typescript'
// configureVueProject({ scriptLangs: ['ts', 'tsx'] })
// More info at https://github.com/vuejs/eslint-config-typescript/#advanced-setup

export default defineConfigWithVueTs(
    {
        name: 'app/files-to-lint',
        files: ['**/*.{vue,ts,mts,tsx}'],
    },

    globalIgnores(['**/dist/**', '**/dist-ssr/**', '**/coverage/**', 'src/api/generated/**']),

    ...pluginVue.configs['flat/essential'],
    vueTsConfigs.recommended,

    ...pluginOxlint.buildFromOxlintConfigFile('.oxlintrc.json'),

    skipFormatting,

    // At most 120 characters per line (CLAUDE.md, .editorconfig). Prettier wraps the code at 100; this
    // rule also reports the strings, template literals and comments that Prettier leaves as they are. A
    // comment line that holds nothing but an address may be longer. Comes after skipFormatting, which
    // switches the rule off.
    {
        name: 'app/max-len',
        plugins: { '@stylistic': stylistic },
        rules: {
            '@stylistic/max-len': [
                'error',
                {
                    code: 120,
                    tabWidth: 4,
                    ignorePattern: String.raw`^\s*(?:\/\/|\*|\/\*)\s*<?https?:\/\/\S+$`,
                },
            ],
        },
    },
)
