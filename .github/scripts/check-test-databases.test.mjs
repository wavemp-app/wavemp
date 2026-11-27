// Tests of the check of the test database names: node --test .github/scripts/check-test-databases.test.mjs

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { describe, test } from 'node:test'
import {
    findDatabaseNameProblems,
    findDatabaseNames,
    formatProblem,
    isCheckedPath,
    listFiles,
    namePrefix,
    projectKey,
    repositoryRoot,
} from './check-test-databases.mjs'

const dataFile = 'tests/WaveMP.Data.Tests/Repositories/PlaylistRepositoryTests.cs'
const serviceFile = 'tests/WaveMP.Service.Tests/IntegrationTests/PlaylistsApiTests.cs'

/** The problems of files given as { path: text }. */
function problemsOf(files) {
    return findDatabaseNameProblems(Object.keys(files), (file) => files[file] ?? null)
}

describe('isCheckedPath', () => {
    test('checks the C# files of the tests', () => {
        for (const file of [dataFile, serviceFile, 'tests/WaveMP.Testing/Databases/TestDatabase.cs']) {
            assert.equal(isCheckedPath(file), true, file)
        }
    })

    test('leaves out build output, other kinds of files and the code outside of tests', () => {
        for (const file of [
            'tests/WaveMP.Data.Tests/obj/Debug/net10.0/WaveMP.Data.Tests.AssemblyInfo.cs',
            'tests/WaveMP.Data.Tests/bin/Debug/net10.0/Generated.cs',
            'tests/WaveMP.E2E/tests/library.spec.ts',
            'tests/WaveMP.Data.Tests/WaveMP.Data.Tests.csproj',
            'source/WaveMP.Data/WaveMpDbContext.cs',
        ]) {
            assert.equal(isCheckedPath(file), false, file)
        }
    })
})

describe('projectKey', () => {
    test('is the name of the test project without WaveMP, Core and Tests, in lower case', () => {
        assert.equal(projectKey(dataFile), 'data')
        assert.equal(projectKey(serviceFile), 'service')
        assert.equal(projectKey('tests/WaveMP.Core.Scanning.Tests/Scans/LibraryScannerTests.cs'), 'scanning')
        assert.equal(projectKey('tests/WaveMP.Core.Online.Metadata.Tests/A.cs'), 'online_metadata')
    })

    test('is null outside of a test project', () => {
        assert.equal(projectKey('tests/WaveMP.Testing/Databases/TestDatabase.cs'), null)
        assert.equal(projectKey('tests/Helper.cs'), null)
    })

    test('gives the prefix of the names of the project', () => {
        assert.equal(namePrefix('data'), 'wavemp_tests_data_')
    })
})

describe('findDatabaseNames', () => {
    test('finds every literal that starts with wavemp_tests, with its line', () => {
        const text = [
            'public class A',
            '{',
            '    private const string DatabaseName = "wavemp_tests_data_a";',
            '    private const string Other = "music";',
            '    _factory = new WaveMpWebApplicationFactory(',
            '        "wavemp_tests_service_b",',
            '        freshDatabase: true);',
            '}',
        ].join('\n')

        const { names, composed } = findDatabaseNames(text)

        assert.deepEqual(
            names.map(({ name, line, composed: isComposed }) => ({ name, line, isComposed })),
            [
                { name: 'wavemp_tests_data_a', line: 3, isComposed: false },
                { name: 'wavemp_tests_service_b', line: 6, isComposed: false },
            ],
        )
        assert.deepEqual(composed, [])
    })

    test('finds names that are put together with +, also across lines', () => {
        const text = [
            'const string A = "wavemp_tests" + "_a";',
            'string b = DatabaseName + "_restart";',
            'string c = WaveMpWebApplicationFactory.TestDatabaseName',
            '    + "_c";',
        ].join('\n')

        const { names, composed } = findDatabaseNames(text)

        assert.deepEqual(
            names.map(({ name, composed: isComposed }) => ({ name, isComposed })),
            [{ name: 'wavemp_tests', isComposed: true }],
        )
        assert.deepEqual(composed, [
            { line: 2, text: 'DatabaseName' },
            { line: 3, text: 'TestDatabaseName' },
        ])
    })

    test('ignores lines that are only comments', () => {
        const text = ['/// Uses "wavemp_tests_data_doc".', '// "wavemp_tests_data_note" + x', 'int a;'].join('\n')

        assert.deepEqual(findDatabaseNames(text), { names: [], composed: [] })
    })

    test('keeps the $ of an interpolated string', () => {
        const { names } = findDatabaseNames('string a = $"wavemp_tests_data_{suffix}";')

        assert.equal(names[0].prefix, '$')
    })
})

describe('findDatabaseNameProblems', () => {
    test('accepts names of their own with the key of their project', () => {
        assert.deepEqual(
            problemsOf({
                [dataFile]: 'const string DatabaseName = "wavemp_tests_data_playlists";',
                [serviceFile]: 'new WaveMpWebApplicationFactory("wavemp_tests_service_playlists");',
                'tests/WaveMP.Data.Tests/Readme.md': '"wavemp_tests_data_playlists"',
            }),
            [],
        )
    })

    test('reports a name that two fixtures share, at every place', () => {
        const problems = problemsOf({
            [dataFile]: 'const string DatabaseName = "wavemp_tests_data_playlists";',
            'tests/WaveMP.Data.Tests/Repositories/SmartPlaylistRepositoryTests.cs':
                'x\nconst string DatabaseName = "wavemp_tests_data_playlists";',
        })

        assert.equal(problems.length, 2)
        assert.match(problems[0].message, /SmartPlaylistRepositoryTests\.cs:2/)
        assert.match(problems[1].message, /PlaylistRepositoryTests\.cs:1/)
    })

    test('reports the same name used twice in one file', () => {
        const problems = problemsOf({
            [serviceFile]: 'new("wavemp_tests_service_settings");\nnew("wavemp_tests_service_settings");',
        })

        assert.deepEqual(
            problems.map(({ line }) => line),
            [1, 2],
        )
    })

    test('reports a name with the key of another project, which would collide across processes', () => {
        const problems = problemsOf({ [serviceFile]: 'new("wavemp_tests_data_playlists");' })

        assert.equal(problems.length, 1)
        assert.match(problems[0].message, /must start with "wavemp_tests_service_"/)
    })

    test('reports the old names without a key and names outside of a test project', () => {
        const problems = problemsOf({
            [dataFile]: 'const string DatabaseName = "wavemp_tests_playlists";',
            'tests/WaveMP.Testing/Databases/Defaults.cs': 'const string Name = "wavemp_tests_shared";',
        })

        assert.equal(problems.length, 2)
        assert.match(problems[0].message, /must start with "wavemp_tests_data_"/)
        assert.match(problems[1].message, /outside of a test project/)
    })

    test('reports names put together from parts', () => {
        const problems = problemsOf({
            [serviceFile]: [
                'const string DatabaseName = "wavemp_tests_service_restart";',
                'new(DatabaseName + "_jobs");',
                'new($"wavemp_tests_service_{name}");',
                'new("wavemp_tests_service_a" + suffix);',
            ].join('\n'),
        })

        assert.deepEqual(
            problems.map(({ line, message }) => ({ line, message: message.split(';')[0] })),
            [
                { line: 2, message: 'Database name put together from DatabaseName and +' },
                { line: 3, message: 'Database name "wavemp_tests_service_{name}" is an interpolated string' },
                { line: 4, message: 'Database name "wavemp_tests_service_a" is put together with +' },
            ],
        )
    })

    test('reports characters PostgreSQL would need quotes for and names that are too long', () => {
        const long = `wavemp_tests_data_${'x'.repeat(46)}`
        const problems = problemsOf({ [dataFile]: `a = "wavemp_tests_data_Mixed";\nb = "${long}";` })

        assert.equal(problems.length, 2)
        assert.match(problems[0].message, /may only contain a-z, 0-9 and _/)
        assert.match(problems[1].message, /longer than 63 bytes/)
    })

    test('skips files that are gone', () => {
        assert.deepEqual(findDatabaseNameProblems([dataFile], () => null), [])
    })
})

describe('formatProblem', () => {
    const problem = { file: dataFile, line: 3, message: 'Message.' }

    test('writes path, line and message', () => {
        assert.equal(formatProblem(problem), `${dataFile}:3: Message.`)
    })

    test('writes an annotation in GitHub Actions', () => {
        assert.equal(formatProblem(problem, true), `::error file=${dataFile},line=3::Message.`)
    })
})

describe('the repository', () => {
    test('gives every test fixture a database of its own with the key of its project', () => {
        const files = listFiles(repositoryRoot)
        const readFile = (file) => {
            try {
                return readFileSync(path.join(repositoryRoot, file), 'utf8')
            } catch {
                return null
            }
        }

        assert.deepEqual(findDatabaseNameProblems(files, readFile), [])
    })
})
