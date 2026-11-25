// Tests of the text of the GitHub release: node --test .github/scripts/release-notes.test.mjs

import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { describe, test } from 'node:test'
import {
    createReleaseNotes,
    findBranchLinks,
    findReleaseTag,
    isFixedRef,
    parseArguments,
    parseRepositoryUrl,
    pinReleaseNotes,
    releaseNotesMilestones,
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
            `[Older notes](${github}/blob/v1.0.80/docs/releases/release-m4.md#casting), ` +
                `[this section](${github}/blob/v1.0.80/docs/releases/release-m5.md#themes), [web](https://example.com)`,
            '`../../THEMING.md` stays code.',
        ])
    })

    test('moves addresses of this repository at a branch to the tag and keeps fixed ones', () => {
        const commit = 'a'.repeat(40)
        const markdown = [
            `![Home](${raw}/main/assets/screenshots/home.png)`,
            `<img src="${raw}/main/assets/screenshots/phone.png" width="200">`,
            `[Guide](${github}/blob/main/THEMING.md#uploaded-themes) [deploy](${github}/tree/main/deploy/)`,
            `![Old](${raw}/v1.0.72/assets/old.png) [commit](${github}/blob/${commit}/README.md)`,
            `[Releases](${github}/releases) [other](https://raw.githubusercontent.com/other/repo/main/x.png)`,
        ].join('\n')
        const files = []

        const text = createReleaseNotes(markdown, {
            milestone: '6',
            tag: 'v1.0.90',
            exists: (filePath) => files.push(filePath) > 0,
        })

        assert.deepEqual(text.split('\n'), [
            `![Home](${raw}/v1.0.90/assets/screenshots/home.png)`,
            `<img src="${raw}/v1.0.90/assets/screenshots/phone.png" width="200">`,
            `[Guide](${github}/blob/v1.0.90/THEMING.md#uploaded-themes) [deploy](${github}/tree/v1.0.90/deploy/)`,
            `![Old](${raw}/v1.0.72/assets/old.png) [commit](${github}/blob/${commit}/README.md)`,
            `[Releases](${github}/releases) [other](https://raw.githubusercontent.com/other/repo/main/x.png)`,
        ])
        assert.deepEqual(files, [
            'assets/screenshots/home.png',
            'assets/screenshots/phone.png',
            'THEMING.md',
            'deploy',
        ])
    })

    test('reports addresses at a branch to files that do not exist', () => {
        assert.throws(
            () =>
                createReleaseNotes(`![a](${raw}/main/assets/gone.png)`, {
                    milestone: '6',
                    tag: 'v1.0.90',
                    exists: () => false,
                }),
            /release-m6\.md links to files that do not exist: assets\/gone\.png/,
        )
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
        const milestones = releaseNotesMilestones()
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
            pin: false,
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
        assert.throws(() => parseArguments(['--pin', '--tag', 'v1.0.80']), /Usage/)
        assert.throws(() => parseArguments(['--pin', '--output', 'notes.md']), /Usage/)
    })

    test('reads --pin with and without a milestone', () => {
        assert.deepEqual(parseArguments(['--pin']), {
            milestone: null,
            tag: null,
            output: null,
            pin: true,
        })
        assert.equal(parseArguments(['--pin', '--milestone', '5']).milestone, '5')
        assert.equal(parseArguments(['--milestone', '5', '--pin']).pin, true)
    })
})

describe('pinReleaseNotes', () => {
    test('pins relative links and addresses at a branch to the tag, but keeps anchors', () => {
        const markdown = [
            '![Home](../../assets/screenshots/home.png)',
            `<img src="${raw}/main/assets/screenshots/phone.png" alt="">`,
            '[THEMING.md](../../THEMING.md), [this section](#themes), [M4](release-m4.md#casting)',
            `[Guide](${github}/blob/main/THEMING.md#uploaded-themes)`,
            '`../../THEMING.md` stays code.',
        ].join('\n')

        const pinned = pinReleaseNotes(markdown, { milestone: '5', tag: 'v1.0.72' })

        assert.deepEqual(pinned.split('\n'), [
            `![Home](${raw}/v1.0.72/assets/screenshots/home.png)`,
            `<img src="${raw}/v1.0.72/assets/screenshots/phone.png" alt="">`,
            `[THEMING.md](${github}/blob/v1.0.72/THEMING.md), [this section](#themes), ` +
                `[M4](${github}/blob/v1.0.72/docs/releases/release-m4.md#casting)`,
            `[Guide](${github}/blob/v1.0.72/THEMING.md#uploaded-themes)`,
            '`../../THEMING.md` stays code.',
        ])
        assert.equal(pinReleaseNotes(pinned, { milestone: '5', tag: 'v1.0.72' }), pinned)
        assert.deepEqual(findBranchLinks(pinned), [])
    })

    test('reports files that do not exist at the tag', () => {
        assert.throws(
            () =>
                pinReleaseNotes('![a](../../assets/new.png)', {
                    milestone: '5',
                    tag: 'v1.0.72',
                    exists: () => false,
                }),
            /release-m5\.md links to files that do not exist at v1\.0\.72: assets\/new\.png/,
        )
    })
})

describe('addresses of the repository', () => {
    test('are split into prefix, ref, path and suffix', () => {
        assert.deepEqual(parseRepositoryUrl(`${raw}/main/assets/a.png?x=1`), {
            prefix: `${raw}/`,
            ref: 'main',
            filePath: 'assets/a.png',
            suffix: '?x=1',
        })
        assert.equal(parseRepositoryUrl(`${github}/blob/v1.0.72/THEMING.md#top`)?.ref, 'v1.0.72')
        assert.equal(parseRepositoryUrl(`${github}/releases/tag/v1.0.72`), null)
        assert.equal(parseRepositoryUrl('https://example.com/main/a.png'), null)
    })

    test('are fixed at a release tag or a commit only', () => {
        assert.equal(isFixedRef('v1.0.72'), true)
        assert.equal(isFixedRef('0123456789abcdef0123456789abcdef01234567'), true)
        assert.equal(isFixedRef('main'), false)
        assert.equal(isFixedRef('1.0.72'), false)
    })

    test('point to a tag or a commit in all release notes of this repository', () => {
        for (const milestone of releaseNotesMilestones()) {
            const notes = releaseNotesPath(milestone)
            const markdown = readFileSync(path.join(repositoryRoot, notes), 'utf8')
            assert.deepEqual(findBranchLinks(markdown), [], notes)
        }
    })
})

describe('findReleaseTag', () => {
    test('is the first tag in the order of the versions that contains the release notes', () => {
        const tags = ['v1.0.66', 'v1.0.72', 'v1.0.80']
        const exists = (tag, filePath) =>
            filePath === 'docs/releases/release-m5.md' && tag !== 'v1.0.66'
        assert.equal(findReleaseTag('5', tags, exists), 'v1.0.72')
        assert.equal(findReleaseTag('6', tags, exists), null)
    })
})
