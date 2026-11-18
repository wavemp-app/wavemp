// Tests of the check of the app catalog templates: node --test .github/scripts/check-deploy.test.mjs
//
// The fixtures are small template folders created at runtime in a temporary directory and removed
// afterwards.

import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { after, beforeEach, describe, test } from 'node:test'
import {
    casaosAssetSources,
    findDeployProblems,
    findImages,
    findXmlProblem,
    parseArguments,
    parseImage,
    parseReleaseVersion,
    repositoryRoot,
} from './check-deploy.mjs'

const digest = `sha256:${'a'.repeat(64)}`
const otherDigest = `sha256:${'b'.repeat(64)}`
const temporaryDirectory = mkdtempSync(path.join(os.tmpdir(), 'wavemp-check-deploy-'))
let fixture

function write(relative, content) {
    const file = path.join(fixture, relative)
    mkdirSync(path.dirname(file), { recursive: true })
    writeFileSync(file, content)
}

function truenasValues(wavempTag, postgresTag = `18.7-trixie@${digest}`) {
    return [
        'images:',
        '  image:',
        '    repository: wavemp/wavemp',
        `    tag: "${wavempTag}"`,
        '  postgres_18_image:',
        '    repository: postgres',
        `    tag: "${postgresTag}"`,
        '',
    ].join('\n')
}

function casaosCompose(wavempTag, version = wavempTag, postgresTag = '18.7') {
    return [
        'name: wavemp',
        'services:',
        '  wavemp:',
        `    image: wavemp/wavemp:${wavempTag}`,
        '  wavemp-db:',
        `    image: postgres:${postgresTag}`,
        'x-casaos:',
        '  id: io.github.wavemp-app.wavemp',
        `  version: "${version}"`,
        '',
    ].join('\n')
}

function unraidTemplate(repository) {
    return `<?xml version="1.0"?>\n<Container version="2">\n  <Repository>${repository}</Repository>\n</Container>\n`
}

/** A consistent set of templates for version 1.0.7 with all copies of the CasaOS folder. */
function writeValidFixture() {
    write('docker-compose.yml', 'services:\n  wavemp-db:\n    image: postgres:18\n')
    write('deploy/truenas/wavemp/app.yaml', 'app_version: 1.0.7\nname: wavemp\n')
    write('deploy/truenas/wavemp/ix_values.yaml', truenasValues(`1.0.7@${digest}`))
    write('deploy/unraid/wavemp.xml', unraidTemplate('wavemp/wavemp:1.0.7'))
    write('deploy/unraid/wavemp-db.xml', unraidTemplate('postgres:18.7'))
    write('deploy/casaos/WaveMP/docker-compose.yml', casaosCompose('1.0.7'))
    for (const [name, source] of Object.entries(casaosAssetSources)) {
        write(source, `content of ${source}`)
        write(`deploy/casaos/WaveMP/${name}`, `content of ${source}`)
    }
}

beforeEach(() => {
    fixture = mkdtempSync(path.join(temporaryDirectory, 'repo-'))
    writeValidFixture()
})

after(() => {
    rmSync(temporaryDirectory, { recursive: true, force: true })
})

describe('parseImage', () => {
    test('splits repository, tag and digest', () => {
        assert.deepEqual(parseImage(`postgres:18.7@${digest}`), {
            repository: 'postgres',
            tag: '18.7',
            digest,
        })
        assert.deepEqual(parseImage('ghcr.io/wavemp-app/wavemp:1.0.5'), {
            repository: 'ghcr.io/wavemp-app/wavemp',
            tag: '1.0.5',
            digest: null,
        })
    })

    test('does not mistake the port of a registry for a tag', () => {
        assert.deepEqual(parseImage('registry.local:5000/wavemp/wavemp'), {
            repository: 'registry.local:5000/wavemp/wavemp',
            tag: null,
            digest: null,
        })
    })
})

describe('parseReleaseVersion', () => {
    test('accepts a version and the tag of a release', () => {
        assert.equal(parseReleaseVersion('1.0.66'), '1.0.66')
        assert.equal(parseReleaseVersion('v1.0.66'), '1.0.66')
    })

    test('rejects anything else', () => {
        for (const value of ['', '1.0', 'latest', '1.0.66-dev', 'version1.0.66', undefined]) {
            assert.throws(() => parseReleaseVersion(value), /is not a version/)
        }
    })
})

describe('parseArguments', () => {
    test('reads the release and the digest', () => {
        assert.deepEqual(parseArguments([]), { release: null, digest: null })
        assert.deepEqual(parseArguments(['--release', 'v1.0.7', '--digest', digest]), {
            release: '1.0.7',
            digest,
        })
    })

    test('rejects unknown arguments, missing values and an invalid digest', () => {
        assert.throws(() => parseArguments(['--version', '1.0.7']), /Unknown argument --version/)
        assert.throws(() => parseArguments(['--release']), /Unknown argument --release/)
        assert.throws(() => parseArguments(['--digest', 'sha256:123']), /is not of the form/)
    })
})

describe('findImages', () => {
    test('finds the images of compose files, TrueNAS values and Unraid templates', () => {
        const images = [
            ...findImages('compose.yml', casaosCompose('1.0.7')),
            ...findImages('ix_values.yaml', truenasValues('1.0.7')),
            ...findImages('wavemp.xml', unraidTemplate('wavemp/wavemp:1.0.7')),
        ].map((image) => `${image.file}:${image.line} ${image.repository}:${image.tag}`)

        assert.deepEqual(images, [
            'compose.yml:4 wavemp/wavemp:1.0.7',
            'compose.yml:6 postgres:18.7',
            'ix_values.yaml:3 wavemp/wavemp:1.0.7',
            'ix_values.yaml:6 postgres:18.7-trixie',
            'wavemp.xml:3 wavemp/wavemp:1.0.7',
        ])
    })

    test('does not read the next line for a key without value', () => {
        assert.deepEqual(findImages('ix_values.yaml', 'images:\n  image:\n    repository: x\n'), [])
    })
})

describe('findXmlProblem', () => {
    test('accepts a well-formed template', () => {
        assert.equal(
            findXmlProblem(
                '<?xml version="1.0"?>\n<!-- c -->\n<Container version="2"><A>x &amp; y&#xD;</A><B/><C a=\'1\' b="2"/></Container>\n',
            ),
            null,
        )
    })

    test('reports tags that do not match', () => {
        assert.match(findXmlProblem('<Container><A></B></Container>'), /does not match/)
        assert.match(findXmlProblem('<Container><A></Container>'), /does not match/)
        assert.match(findXmlProblem('<Container>'), /is not closed/)
    })

    test('reports a raw ampersand and unquoted attributes', () => {
        assert.match(findXmlProblem('<Container>a & b</Container>'), /invalid entity/)
        assert.match(findXmlProblem('<Container version=2></Container>'), /invalid attributes/)
    })

    test('reports more than one root element and text outside of it', () => {
        assert.match(findXmlProblem('<A/><B/>'), /more than one root/)
        assert.match(findXmlProblem('text<A/>'), /outside of the root/)
        assert.match(findXmlProblem(''), /no root element/)
    })
})

describe('findDeployProblems', () => {
    test('accepts consistent templates', () => {
        assert.deepEqual(findDeployProblems(fixture), [])
    })

    test('accepts the templates of this repository', () => {
        assert.deepEqual(findDeployProblems(repositoryRoot), [])
    })

    test('reports floating tags', () => {
        write('deploy/unraid/wavemp.xml', unraidTemplate('wavemp/wavemp:latest'))
        write('deploy/unraid/wavemp-db.xml', unraidTemplate('postgres:18'))

        const problems = findDeployProblems(fixture)

        assert.ok(
            problems.some((problem) =>
                /wavemp\/wavemp:latest is not a fixed version/.test(problem),
            ),
        )
        assert.ok(problems.some((problem) => /postgres:18 is not a fixed version/.test(problem)))
    })

    test('reports an image without tag and an invalid digest', () => {
        write('deploy/unraid/wavemp.xml', unraidTemplate('wavemp/wavemp'))
        write('deploy/unraid/wavemp-db.xml', unraidTemplate('postgres:18.7@sha256:123'))

        const problems = findDeployProblems(fixture)

        assert.ok(problems.some((problem) => /wavemp\/wavemp has no tag/.test(problem)))
        assert.ok(problems.some((problem) => /invalid digest "sha256:123"/.test(problem)))
    })

    test('reports different versions of WaveMP and PostgreSQL', () => {
        write('deploy/unraid/wavemp.xml', unraidTemplate('wavemp/wavemp:1.0.8'))
        write('deploy/unraid/wavemp-db.xml', unraidTemplate('postgres:18.6'))

        const problems = findDeployProblems(fixture)

        assert.ok(
            problems.some((problem) =>
                /different versions of WaveMP: 1\.0\.7, 1\.0\.8/.test(problem),
            ),
        )
        assert.ok(problems.some((problem) => /different versions of PostgreSQL/.test(problem)))
    })

    test('reports another major version of PostgreSQL than docker-compose.yml', () => {
        write('docker-compose.yml', 'services:\n  wavemp-db:\n    image: postgres:19\n')

        assert.deepEqual(findDeployProblems(fixture), [
            'deploy/ uses PostgreSQL 18.7, but docker-compose.yml uses PostgreSQL 19',
        ])
    })

    test('reports version fields of the catalogs that do not name the version', () => {
        write('deploy/truenas/wavemp/app.yaml', 'app_version: 1.0.6\nname: wavemp\n')
        write('deploy/casaos/WaveMP/docker-compose.yml', casaosCompose('1.0.7', '1.0.6'))

        assert.deepEqual(findDeployProblems(fixture), [
            'deploy/casaos/WaveMP/docker-compose.yml: x-casaos version 1.0.6 is not 1.0.7',
            'deploy/truenas/wavemp/app.yaml: app_version 1.0.6 is not 1.0.7',
        ])
    })

    test('accepts templates that name the release and pin its digest', () => {
        assert.deepEqual(findDeployProblems(fixture, { release: '1.0.7', digest }), [])
    })

    test('reports templates that do not name the release', () => {
        assert.deepEqual(findDeployProblems(fixture, { release: '1.0.8' }), [
            'deploy/ uses WaveMP 1.0.7, but the release is 1.0.8',
        ])
    })

    test('reports every version that is not the release', () => {
        write('deploy/unraid/wavemp.xml', unraidTemplate('wavemp/wavemp:1.0.8'))

        const problems = findDeployProblems(fixture, { release: '1.0.8' })

        assert.ok(problems.includes('deploy/ uses WaveMP 1.0.7, but the release is 1.0.8'))
        assert.ok(!problems.some((problem) => /WaveMP 1\.0\.8, but/.test(problem)))
    })

    test('reports a pinned digest that is not the one of the published image', () => {
        assert.deepEqual(findDeployProblems(fixture, { release: '1.0.7', digest: otherDigest }), [
            `deploy/truenas/wavemp/ix_values.yaml:3: wavemp/wavemp:1.0.7 pins ${digest}, but the published image is ${otherDigest}`,
        ])
    })

    test('reports templates that pin no digest of WaveMP', () => {
        write('deploy/truenas/wavemp/ix_values.yaml', truenasValues('1.0.7'))

        assert.deepEqual(findDeployProblems(fixture, { digest }), [
            `deploy/ pins no digest of WaveMP; pin ${digest} (TrueNAS)`,
        ])
        assert.deepEqual(findDeployProblems(fixture), [])
    })

    test('reports a template that is not well-formed XML', () => {
        write('deploy/unraid/wavemp-db.xml', '<Container><Repository>postgres:18.7</Repository>')

        assert.deepEqual(findDeployProblems(fixture), [
            'deploy/unraid/wavemp-db.xml: not well-formed XML: element <Container> is not closed',
        ])
    })

    test('reports addresses of files of the repository that do not exist', () => {
        write(
            'deploy/README.md',
            'Icon: https://raw.githubusercontent.com/wavemp-app/wavemp/main/assets/logo/wavemp-logo.svg\n' +
                'Missing: https://raw.githubusercontent.com/wavemp-app/wavemp/main/assets/missing.png\n',
        )

        assert.deepEqual(findDeployProblems(fixture), [
            'deploy/README.md: https://raw.githubusercontent.com/wavemp-app/wavemp/main/assets/missing.png points to a file that does not exist',
        ])
    })

    test('reports missing and outdated copies in the CasaOS folder', () => {
        rmSync(path.join(fixture, 'deploy/casaos/WaveMP/icon.png'))
        write('deploy/casaos/WaveMP/screenshot-1.png', 'an older screenshot')

        assert.deepEqual(findDeployProblems(fixture), [
            'deploy/casaos/WaveMP/icon.png is missing (copy of assets/logo/wavemp-logo-512.png)',
            'deploy/casaos/WaveMP/screenshot-1.png differs from assets/screenshots/catalogs/home.png; copy it again',
        ])
    })

    test('reports an empty deploy folder', () => {
        const empty = mkdtempSync(path.join(temporaryDirectory, 'empty-'))

        assert.deepEqual(findDeployProblems(empty), ['deploy/ contains no templates'])
    })
})
