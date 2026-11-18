// Tests of the text of the GitHub release: node --test .github/scripts/release-notes.test.mjs

import assert from 'node:assert/strict'
import { existsSync, readdirSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { describe, test } from 'node:test'
import {
    createReleaseNotes,
    parseArguments,
    releaseNotesPath,
    repositoryRoot,
} from './release-notes.mjs'

const github = 'https://github.com/wavemp-app/wavemp'
const raw = 'https://raw.githubusercontent.com/wavemp-app/wavemp'

describe('createReleaseNotes', () => {
    test('turns relative links and images into addresses at the tag', () => {
        const markdown = [
            '# WaveMP M5: Themes',
            '',
            '![Aurora](../../assets/screenshots/themes/aurora.png)',
            'See [THEMING.md](../../THEMING.md) and [the templates](/deploy/).',
            '<img src="../../assets/logo/wavemp-logo.svg" alt="">',
            '[Older notes](release-m4.md#casting), [this section](#themes), [web](https://example.com)',
            '`../../THEMING.md` stays code.',
        ].join('\n')

        const text = createReleaseNotes(markdown, { milestone: '5', tag: 'v1.0.80' })

        assert.deepEqual(text.split('\n'), [
            '# WaveMP M5: Themes',
            '',
            `![Aurora](${raw}/v1.0.80/assets/screenshots/themes/aurora.png)`,
            `See [THEMING.md](${github}/blob/v1.0.80/THEMING.md) and [the templates](${github}/tree/v1.0.80/deploy/).`,
            `<img src="${raw}/v1.0.80/assets/logo/wavemp-logo.svg" alt="">`,
            `[Older notes](${github}/blob/v1.0.80/docs/releases/release-m4.md#casting), [this section](${github}/blob/v1.0.80/docs/releases/release-m5.md#themes), [web](https://example.com)`,
            '`../../THEMING.md` stays code.',
        ])
    })

    test('reports links to files that do not exist', () => {
        assert.throws(
            () =>
                createReleaseNotes('![a](missing.png) [b](../../THEMING.md)', {
                    milestone: '5',
                    tag: 'v1.0.80',
                    exists: (filePath) => filePath === 'THEMING.md',
                }),
            /release-m5\.md links to files that do not exist: docs\/releases\/missing\.png/,
        )
    })

    test('reports links outside of the repository', () => {
        assert.throws(
            () => createReleaseNotes('[a](../../../x.md)', { milestone: '5', tag: 'v1.0.80' }),
            /outside of the repository/,
        )
    })

    test('accepts the release notes of this repository', () => {
        const milestones = readdirSync(path.join(repositoryRoot, 'docs', 'releases'))
            .map((name) => /^release-m(\d+)\.md$/.exec(name)?.[1])
            .filter(Boolean)
        assert.ok(milestones.length > 0)
        for (const milestone of milestones) {
            const file = path.join(repositoryRoot, releaseNotesPath(milestone))
            assert.doesNotThrow(() =>
                createReleaseNotes(readFileSync(file, 'utf8'), {
                    milestone,
                    tag: 'v1.0.1',
                    exists: (filePath) => existsSync(path.join(repositoryRoot, filePath)),
                }),
            )
        }
    })
})

describe('parseArguments', () => {
    test('reads milestone, tag and output', () => {
        assert.deepEqual(parseArguments(['--milestone', '5', '--tag', 'v1.0.80']), {
            milestone: '5',
            tag: 'v1.0.80',
            output: null,
        })
        assert.equal(
            parseArguments(['--tag', 'v1.0.80', '--milestone', '5', '--output', 'notes.md']).output,
            'notes.md',
        )
    })

    test('rejects missing and invalid arguments', () => {
        assert.throws(() => parseArguments(['--milestone', '5']), /Usage/)
        assert.throws(() => parseArguments(['--milestone', 'M5', '--tag', 'v1.0.80']), /invalid/)
        assert.throws(() => parseArguments(['--milestone', '5', '--tag', '1.0.80']), /invalid/)
        assert.throws(() => parseArguments(['--other', 'x']), /Unknown/)
    })
})
