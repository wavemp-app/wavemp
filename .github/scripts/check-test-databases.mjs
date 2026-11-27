// Check of the names of the test databases, used locally and in CI.
//
// Usage (from any directory):
//   node .github/scripts/check-test-databases.mjs
//
// dotnet test runs the test projects in parallel processes, and every fixture of the integration tests
// drops its database before and after its tests (DROP DATABASE ... WITH (FORCE)). Two fixtures with the
// same database name drop each other's database when their runs overlap. So every database name of the
// tests is declared exactly once, as a whole string literal, and starts with the key of its test project:
//   wavemp_tests_<project>_<name>
// The key is the name of the test project without "WaveMP.", "Core." and ".Tests", in lower case, with
// "_" for ".": WaveMP.Data.Tests -> data, WaveMP.Service.Tests -> service, WaveMP.Core.Scanning.Tests ->
// scanning. The check reads the C# files below tests/ and treats every string literal that starts with
// wavemp_tests as the name of a database. It reports
// - a name outside of a test project, without the key of its project, with other characters than a-z,
//   0-9 and _, or longer than PostgreSQL allows (63 bytes),
// - a name that appears more than once (declare it once, as a constant, and use the constant),
// - a name that is put together from parts: a literal next to "+", an interpolated string, or a
//   constant or property named *DatabaseName followed by "+", which the check could not see in full.
// Exits with 0 if every name is fine, otherwise with 1.

import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

export const scriptsDirectory = path.dirname(fileURLToPath(import.meta.url))
export const repositoryRoot = path.resolve(scriptsDirectory, '..', '..')

/** The start of every database name of the tests. */
export const databasePrefix = 'wavemp_tests'

/** The longest name PostgreSQL allows (NAMEDATALEN - 1 bytes). */
export const maxNameLength = 63

const testProject = /^tests\/WaveMP\.(.+)\.Tests\//
const validName = /^[a-z0-9_]+$/

/** A string literal that starts with the prefix; group 1 is a leading $ or @, group 2 the name. */
const nameLiteral = new RegExp(`([$@]*)"(${databasePrefix}[^"\\r\\n]*)"`, 'g')

/** A constant or property named *DatabaseName that something is added to. */
const composedName = /\b\w*DatabaseName\s*\+/g

/** Whether the file at the path (relative, forward slashes) is C# code of the tests. */
export function isCheckedPath(relativePath) {
    if (!relativePath.startsWith('tests/') || !relativePath.endsWith('.cs')) {
        return false
    }
    const parts = relativePath.split('/')
    return !parts.includes('bin') && !parts.includes('obj') && !parts.includes('node_modules')
}

/** The key of the test project of a file (e.g. "scanning"), or null for a file outside of a test project. */
export function projectKey(relativePath) {
    const match = testProject.exec(relativePath)
    if (!match) {
        return null
    }
    return match[1].replace(/^Core\./, '').toLowerCase().replaceAll('.', '_')
}

/** The start every database name of the test project of the key must have. */
export function namePrefix(key) {
    return `${databasePrefix}_${key}_`
}

/** The text without the lines that are only comments, with the same line numbers. */
function withoutCommentLines(text) {
    return text
        .split('\n')
        .map((line) => (/^\s*(?:\/\/|\*|\/\*)/.test(line) ? '' : line))
        .join('\n')
}

function lineAt(text, index) {
    return text.slice(0, index).split('\n').length
}

/**
 * The database names in the C# text and the places where a name is put together from parts.
 * Names: { name, line, prefix (a leading $ or @), composed }; composed: { line, text }.
 */
export function findDatabaseNames(text) {
    const code = withoutCommentLines(text)
    const names = []
    for (const match of code.matchAll(nameLiteral)) {
        const end = match.index + match[0].length
        const before = code.slice(0, match.index).trimEnd()
        const after = code.slice(end).trimStart()
        names.push({
            name: match[2],
            line: lineAt(code, match.index),
            prefix: match[1],
            composed: before.endsWith('+') || after.startsWith('+'),
        })
    }
    const composed = []
    for (const match of code.matchAll(composedName)) {
        composed.push({ line: lineAt(code, match.index), text: match[0].replace(/\s*\+$/, '') })
    }
    return { names, composed }
}

/**
 * The problems of the files: { file, line, message }, with paths relative to the root.
 * readFile returns the text of a path relative to the root, or null for a file that is gone.
 */
export function findDatabaseNameProblems(files, readFile) {
    const problems = []
    const places = new Map()
    for (const file of files) {
        if (!isCheckedPath(file)) {
            continue
        }
        const text = readFile(file)
        if (text === null) {
            continue
        }
        const key = projectKey(file)
        const { names, composed } = findDatabaseNames(text)
        for (const { name, line, prefix, composed: isComposed } of names) {
            const report = (message) => problems.push({ file, line, message })
            places.set(name, [...(places.get(name) ?? []), { file, line }])
            if (key === null) {
                report(`Database name "${name}" outside of a test project (tests/WaveMP.<Project>.Tests).`)
            } else if (!name.startsWith(namePrefix(key))) {
                report(`Database name "${name}" must start with "${namePrefix(key)}", the key of its test project.`)
            }
            if (prefix.includes('$')) {
                report(`Database name "${name}" is an interpolated string; write the whole name as one literal.`)
            } else if (!validName.test(name)) {
                report(`Database name "${name}" may only contain a-z, 0-9 and _.`)
            }
            if (Buffer.byteLength(name, 'utf8') > maxNameLength) {
                report(`Database name "${name}" is longer than ${maxNameLength} bytes, which PostgreSQL allows.`)
            }
            if (isComposed) {
                report(`Database name "${name}" is put together with +; write the whole name as one literal.`)
            }
        }
        for (const { line, text: expression } of composed) {
            problems.push({
                file,
                line,
                message:
                    `Database name put together from ${expression} and +; `
                    + 'declare every name as one literal of its own.',
            })
        }
    }
    for (const [name, found] of places) {
        if (found.length < 2) {
            continue
        }
        for (const place of found) {
            const others = found
                .filter((other) => other !== place)
                .map((other) => `${other.file}:${other.line}`)
                .join(', ')
            problems.push({
                ...place,
                message:
                    `Database name "${name}" also appears at ${others}; every fixture needs a database of its own, `
                    + 'declare the name once.',
            })
        }
    }
    return problems.sort((a, b) => a.file.localeCompare(b.file) || a.line - b.line)
}

/** The text of a problem: an annotation in GitHub Actions, otherwise path:line: message. */
export function formatProblem(problem, githubActions = false) {
    return githubActions
        ? `::error file=${problem.file},line=${problem.line}::${problem.message}`
        : `${problem.file}:${problem.line}: ${problem.message}`
}

/** The files below the root that git tracks or would track (paths relative to it, with forward slashes). */
export function listFiles(root) {
    const output = execFileSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'], {
        cwd: root,
        encoding: 'utf8',
        maxBuffer: 1 << 28,
    })
    return [...new Set(output.split('\0').filter((file) => file.length > 0))]
}

function readTextFile(root, file) {
    try {
        return readFileSync(path.join(root, file), 'utf8')
    } catch (error) {
        if (error.code === 'ENOENT') {
            return null
        }
        throw error
    }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    const files = listFiles(repositoryRoot)
    const problems = findDatabaseNameProblems(files, (file) => readTextFile(repositoryRoot, file))
    if (problems.length > 0) {
        const githubActions = process.env.GITHUB_ACTIONS === 'true'
        for (const problem of problems) {
            console.error(formatProblem(problem, githubActions))
        }
        console.error(`${problems.length} problem(s) with the names of the test databases.`)
        process.exit(1)
    }
    console.log('Every test database has a name of its own with the key of its test project.')
}
