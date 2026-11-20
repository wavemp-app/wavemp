// Tests of the Docker Hub description: node --test .github/scripts/dockerhub-description.test.mjs

import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { describe, test } from 'node:test'
import {
    absoluteUrl,
    byteLength,
    convertLinks,
    createDockerHubDescription,
    findShortDescription,
    fullDescriptionLimit,
    githubSlug,
    repositoryRoot,
    shortDescriptionLimit,
    splitSections,
} from './dockerhub-description.mjs'

const raw = 'https://raw.githubusercontent.com/wavemp-app/wavemp/main'
const github = 'https://github.com/wavemp-app/wavemp'

function readme(...sections) {
    return ['# WaveMP', '', '**A media player.**', '', ...sections, ''].join('\n')
}

describe('absoluteUrl', () => {
    test('leaves absolute addresses and e-mail links alone', () => {
        assert.equal(absoluteUrl('https://example.com/a.png'), null)
        assert.equal(absoluteUrl('mailto:someone@example.com'), null)
        assert.equal(absoluteUrl('//example.com/a'), null)
    })

    test('points pictures to the raw file and other files to their page', () => {
        assert.equal(
            absoluteUrl('assets/screenshots/home.png'),
            `${raw}/assets/screenshots/home.png`,
        )
        assert.equal(
            absoluteUrl('./assets/logo/wavemp-logo-default.svg'),
            `${raw}/assets/logo/wavemp-logo-default.svg`,
        )
        assert.equal(absoluteUrl('assets/A.JPG'), `${raw}/assets/A.JPG`)
        assert.equal(absoluteUrl('LICENSE'), `${github}/blob/main/LICENSE`)
        assert.equal(
            absoluteUrl('deploy/README.md#casaos'),
            `${github}/blob/main/deploy/README.md#casaos`,
        )
        assert.equal(absoluteUrl('/THEMING.md'), `${github}/blob/main/THEMING.md`)
    })

    test('points folders to their tree', () => {
        assert.equal(absoluteUrl('deploy/'), `${github}/tree/main/deploy/`)
    })

    test('points anchors to the README on GitHub', () => {
        assert.equal(absoluteUrl('#quick-start'), `${github}#quick-start`)
    })

    test('reports every file and rejects links outside of the repository', () => {
        const files = []
        absoluteUrl('docs/a%20b.md?plain=1', (file) => files.push(file))
        absoluteUrl('#anchor', (file) => files.push(file))
        assert.deepEqual(files, ['docs/a b.md'])
        assert.throws(() => absoluteUrl('../other/README.md'), /outside of the repository/)
    })
})

describe('convertLinks', () => {
    test('converts links, images, linked images, reference definitions and HTML', () => {
        const markdown = [
            '[License](LICENSE) and [site](https://example.com)',
            '[![Logo](assets/logo.png)](#about)',
            '![Home](assets/home.png "Home page")',
            '[notices]: THIRD-PARTY-NOTICES.md',
            '<img src="assets/logo.svg" alt="Logo"> <a href=\'deploy/\'>deploy</a>',
            '[angle](<docs/a b.md>)',
        ].join('\n')
        assert.equal(
            convertLinks(markdown),
            [
                `[License](${github}/blob/main/LICENSE) and [site](https://example.com)`,
                `[![Logo](${raw}/assets/logo.png)](${github}#about)`,
                `![Home](${raw}/assets/home.png "Home page")`,
                `[notices]: ${github}/blob/main/THIRD-PARTY-NOTICES.md`,
                `<img src="${raw}/assets/logo.svg" alt="Logo"> <a href='${github}/tree/main/deploy/'>deploy</a>`,
                `[angle](<${github}/blob/main/docs/a b.md>)`,
            ].join('\n'),
        )
    })

    test('leaves code blocks and inline code alone', () => {
        const markdown = [
            'See `[x](LICENSE)` and ``a ` [y](LICENSE)`` but [z](LICENSE).',
            '```markdown',
            '[link](LICENSE)',
            '<img src="a.png">',
            '```',
            '~~~',
            '```',
            '[still code](LICENSE)',
            '~~~',
        ].join('\n')
        const converted = convertLinks(markdown).split('\n')
        assert.equal(
            converted[0],
            `See \`[x](LICENSE)\` and \`\`a \` [y](LICENSE)\`\` but [z](${github}/blob/main/LICENSE).`,
        )
        assert.deepEqual(converted.slice(1), markdown.split('\n').slice(1))
    })
})

describe('githubSlug', () => {
    test('creates the anchors of GitHub', () => {
        const used = new Map()
        assert.equal(githubSlug('Backup & Restore', used), 'backup--restore')
        assert.equal(
            githubSlug('Hardware transcoding (optional)', used),
            'hardware-transcoding-optional',
        )
        assert.equal(githubSlug('`deploy/` folder', used), 'deploy-folder')
        assert.equal(githubSlug('Tests', used), 'tests')
        assert.equal(githubSlug('Tests', used), 'tests-1')
    })
})

describe('splitSections', () => {
    test('splits at ## and ### headings outside of code blocks', () => {
        const { intro, sections } = splitSections(
            readme(
                '## One',
                'a',
                '### Sub',
                'b',
                '```sh',
                '## not a heading',
                '```',
                '#### Deeper',
                '## Two',
                'c',
            ),
        )
        assert.equal(intro, '# WaveMP\n\n**A media player.**\n')
        assert.deepEqual(
            sections.map((section) => [section.title, section.chunks.map((chunk) => chunk.slug)]),
            [
                ['One', ['one', 'sub']],
                ['Two', ['two']],
            ],
        )
        assert.equal(
            sections[0].chunks[1].text,
            '### Sub\nb\n```sh\n## not a heading\n```\n#### Deeper',
        )
    })
})

describe('findShortDescription', () => {
    test('takes the bold tagline below the title', () => {
        assert.equal(findShortDescription(readme()), 'A media player.')
    })

    test('fails without a tagline or with a tagline that is too long', () => {
        assert.throws(() => findShortDescription('# WaveMP\n\nPlain text.\n'), /bold tagline/)
        const long = 'x'.repeat(shortDescriptionLimit + 1)
        assert.throws(() => findShortDescription(`# WaveMP\n\n**${long}**\n`), /101 bytes/)
        // The limit counts bytes, not characters.
        const umlauts = 'ä'.repeat(51)
        assert.throws(() => findShortDescription(`# WaveMP\n\n**${umlauts}**\n`), /102 bytes/)
    })
})

describe('createDockerHubDescription', () => {
    const markdown = readme(
        '## About',
        'About [it](LICENSE).',
        '',
        '## Quick Start',
        'Start.',
        '',
        '### Settings',
        'S'.repeat(400),
        '',
        '### Backup & Restore',
        'Back up.',
        '',
        '## Usage',
        'Use.',
        '',
        '## License',
        'MIT.',
    )
    const sections = ['About', 'Quick Start', 'License']

    test('takes the chosen sections and links to the others', () => {
        const result = createDockerHubDescription(markdown, { sections })
        assert.equal(result.shortDescription, 'A media player.')
        assert.deepEqual(result.omitted, ['Usage'])
        assert.equal(
            result.fullDescription,
            [
                '# WaveMP',
                '',
                '**A media player.**',
                '',
                '## About',
                `About [it](${github}/blob/main/LICENSE).`,
                '',
                '## Quick Start',
                'Start.',
                '',
                '### Settings',
                'S'.repeat(400),
                '',
                '### Backup & Restore',
                'Back up.',
                '',
                '## License',
                'MIT.',
                '',
                '---',
                '',
                `This description is a part of the [README on GitHub](${github}#readme), which also covers [Usage](${github}#usage).`,
                '',
            ].join('\n'),
        )
    })

    test('leaves out the subsections that do not fit and stays within the limit', () => {
        const limit = 1000
        const result = createDockerHubDescription(markdown, { sections, limit })
        assert.deepEqual(result.omitted, ['Settings', 'Usage'])
        assert.ok(byteLength(result.fullDescription) <= limit)
        assert.match(result.fullDescription, /### Backup & Restore/)
        assert.match(result.fullDescription, /## License/)
        assert.doesNotMatch(result.fullDescription, /### Settings/)
        assert.match(
            result.fullDescription,
            new RegExp(
                `covers \\[Settings\\]\\(${github}#settings\\) and \\[Usage\\]\\(${github}#usage\\)\\.`,
            ),
        )
    })

    test('names a section that is left out completely by its heading', () => {
        const big = readme(
            '## Big',
            'B'.repeat(500),
            '',
            '### Sub',
            'b'.repeat(500),
            '',
            '## License',
            'MIT.',
        )
        const result = createDockerHubDescription(big, { sections: ['Big', 'License'], limit: 400 })
        assert.deepEqual(result.omitted, ['Big'])
        assert.match(result.fullDescription, /## License\nMIT\.\n\n---\n/)
    })

    test('fails if the first part does not fit or a linked file is missing', () => {
        assert.throws(
            () => createDockerHubDescription(markdown, { sections, limit: 100 }),
            /does not fit/,
        )
        assert.throws(
            () =>
                createDockerHubDescription(markdown, {
                    sections,
                    exists: (file) => file !== 'LICENSE',
                }),
            /do not exist: LICENSE/,
        )
    })
})

describe('README.md of this repository', () => {
    const markdown = readFileSync(path.join(repositoryRoot, 'README.md'), 'utf8')
    const result = createDockerHubDescription(markdown, {
        exists: (file) => existsSync(path.join(repositoryRoot, file)),
    })

    test('fits into the limits of Docker Hub', () => {
        assert.ok(byteLength(result.fullDescription) <= fullDescriptionLimit)
        assert.ok(byteLength(result.shortDescription) <= shortDescriptionLimit)
    })

    test('keeps the Quick Start and has no relative links left', () => {
        assert.match(result.fullDescription, /^## Quick Start$/m)
        const relative = [
            ...result.fullDescription.matchAll(
                /\]\((?!https?:)[^)]*\)|\b(?:src|href)="(?!https?:)[^"]*"/g,
            ),
        ]
        assert.deepEqual(
            relative.map((match) => match[0]),
            [],
        )
    })
})
