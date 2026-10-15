#!/usr/bin/env node
// Coverage-guided parity: which branches of the upstream (Node) source does the parity suite actually execute, and which
// branches of this port (.NET) does it execute? A branch the suite never reaches is a branch whose parity is unverified.
//
//   node scripts/parity-coverage.js node   [dotnet test args...]   run the suite under V8 coverage of dotenvx/src + @dotenvx/*
//   node scripts/parity-coverage.js dotnet [dotnet test args...]   run the suite under dotnet-coverage of the Dotenvx assemblies
//   node scripts/parity-coverage.js report                         print the gaps found by the last runs
//
// Needs c8 (`C8=<path to c8> node ...`, else `npx --yes c8`) and, for .NET, the local tool `dotnet-coverage`
// (`dotnet tool restore`, see .config/dotnet-tools.json). Output goes to coverage/ (git-ignored).
'use strict'
const { spawnSync } = require('child_process')
const fs = require('fs')
const path = require('path')

const root = path.resolve(__dirname, '..')
const out = path.join(root, 'coverage')
const [mode, ...rest] = process.argv.slice(2)

function run (command, args, options = {}) {
  const result = spawnSync(command, args, { stdio: 'inherit', shell: process.platform === 'win32', cwd: root, ...options })
  return result.status
}

function c8Command () {
  return process.env.C8 ? [process.env.C8, []] : ['npx', ['--yes', 'c8']]
}

function testArgs () {
  return ['test', 'test/Dotenvx.ParityTest', '--no-build', ...rest]
}

function node () {
  const data = path.join(out, 'node-v8')
  if (!process.env.COVERAGE_KEEP) fs.rmSync(data, { recursive: true, force: true }) // COVERAGE_KEEP=1 accumulates over runs
  fs.mkdirSync(data, { recursive: true })
  const status = run('dotnet', testArgs(), { env: { ...process.env, DOTENVX_PARITY_NODE_COVERAGE: data } })
  const [command, prefix] = c8Command()
  // the upstream source plus the packages it delegates to
  run(command, [...prefix, 'report', '--temp-directory', data, '--reports-dir', path.join(out, 'node'),
    '--reporter=lcovonly', '--reporter=text-summary', '--all', '--src', 'dotenvx/src',
    '--include', 'dotenvx/src/**', '--include', 'dotenvx/node_modules/@dotenvx/**',
    '--exclude', '**/*.d.ts'])
  return status
}

function dotnet () {
  const target = path.join(out, 'dotnet.cobertura.xml')
  fs.mkdirSync(out, { recursive: true })
  const status = run('dotnet', ['dotnet-coverage', 'collect', '--output', target, '--output-format', 'cobertura',
    '--include-files', '**/Dotenvx*.dll', '--', 'dotnet', ...testArgs()])
  return status
}

// lcov: SF:<file>, DA:<line>,<hits>, BRDA:<line>,<block>,<branch>,<hits|->
function parseLcov (file) {
  const files = []
  let current
  for (const line of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
    if (line.startsWith('SF:')) files.push(current = { file: line.slice(3), lines: [], branches: [] })
    else if (line.startsWith('DA:')) {
      const [n, hits] = line.slice(3).split(',')
      if (Number(hits) === 0) current.lines.push(Number(n))
    } else if (line.startsWith('BRDA:')) {
      const [n, , , hits] = line.slice(5).split(',')
      if (hits === '0' || hits === '-') current.branches.push(Number(n))
    }
  }
  return files
}

function ranges (numbers) {
  const sorted = [...new Set(numbers)].sort((a, b) => a - b)
  const parts = []
  for (let i = 0; i < sorted.length; i++) {
    let j = i
    while (sorted[j + 1] === sorted[j] + 1) j++
    parts.push(j > i ? `${sorted[i]}-${sorted[j]}` : `${sorted[i]}`)
    i = j
  }
  return parts.join(',')
}

// cobertura: <class filename="..."> with <line number hits>; test and generated sources are ignored
function dotnetReport () {
  const file = path.join(out, 'dotnet.cobertura.xml')
  if (!fs.existsSync(file)) return []
  const files = new Map()
  for (const match of fs.readFileSync(file, 'utf8').matchAll(/<class [^>]*filename="([^"]+)"[^>]*>([\s\S]*?)<\/class>/g)) {
    const name = path.relative(root, match[1])
    if (/^test[\\/]|[\\/]obj[\\/]/.test(name)) continue
    const entry = files.get(name) || { file: name, lines: new Set(), covered: new Set() }
    for (const line of match[2].matchAll(/<line number="(\d+)" hits="(\d+)"/g)) {
      (Number(line[2]) > 0 ? entry.covered : entry.lines).add(Number(line[1]))
    }
    files.set(name, entry)
  }
  return [...files.values()].map(e => ({ file: e.file, lines: [...e.lines].filter(n => !e.covered.has(n)) }))
    .filter(e => e.lines.length).sort((a, b) => b.lines.length - a.lines.length)
}

function report () {
  const dn = dotnetReport()
  if (dn.length) {
    const text = [`port lines never executed by the parity suite: ${dn.reduce((a, e) => a + e.lines.length, 0)}`,
      ...dn.map(e => `${e.file}\n  uncovered lines: ${ranges(e.lines)}`)]
    fs.writeFileSync(path.join(out, 'dotnet-gaps.txt'), text.join('\n') + '\n')
    console.log(text[0] + `\ndetails: ${path.join('coverage', 'dotnet-gaps.txt')}`)
  }
  const lcov = path.join(out, 'node', 'lcov.info')
  if (!fs.existsSync(lcov)) {
    console.error(`no ${lcov}: run "node scripts/parity-coverage.js node" first`)
    return 1
  }
  const lines = []
  let totalLines = 0
  let totalBranches = 0
  for (const entry of parseLcov(lcov).sort((a, b) => b.lines.length - a.lines.length)) {
    totalLines += entry.lines.length
    totalBranches += entry.branches.length
    if (entry.lines.length || entry.branches.length) {
      lines.push(`${entry.file}\n  uncovered lines: ${ranges(entry.lines) || '-'}\n  uncovered branches at lines: ${ranges(entry.branches) || '-'}`)
    }
  }
  lines.unshift(`upstream lines never executed by the parity suite: ${totalLines}; branches: ${totalBranches}`)
  fs.writeFileSync(path.join(out, 'node-gaps.txt'), lines.join('\n') + '\n')
  console.log(lines.slice(0, 1).join('\n'))
  console.log(`details: ${path.join('coverage', 'node-gaps.txt')}`)
  return 0
}

fs.mkdirSync(out, { recursive: true })
const modes = { node, dotnet, report }
if (!modes[mode]) {
  console.error('usage: node scripts/parity-coverage.js node|dotnet|report [dotnet test args...]')
  process.exit(2)
}
process.exit(modes[mode]() || 0)
