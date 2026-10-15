// Live oracle for the library API (src/lib/main.js: config, parse, get, set, ls): runs a list of calls in the current directory and
// prints, as one JSON line, what each returned (or threw) plus every message the library logged and the final process env.
// usage: node sdk.cjs <spec.json> <path to the dotenvx submodule>      (cwd = the scratch directory holding the envfiles)
//   spec: { env: {NAME: value}, calls: [{ fn, args: [...], options: {...} }] }
'use strict'
const fs = require('node:fs')
const path = require('node:path')

const spec = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'))
const main = require(path.join(process.argv[3], 'src', 'lib', 'main.js'))
const { logger } = main

// record instead of print: the level filter is the logger's business, what the library asks it to say is what must match
let messages = []
for (const level of ['error', 'infoerror', 'warn', 'success', 'info', 'help', 'verbose', 'debug']) {
  logger[level] = (message) => { messages.push([level, typeof message === 'object' ? JSON.stringify(message) : String(message)]) }
}

for (const [name, value] of Object.entries(spec.env || {})) process.env[name] = value

// what the process itself writes to stdout/stderr is captured here, below the Envfile redaction that config() installs on top
const written = { stdout: [], stderr: [] }
for (const name of ['stdout', 'stderr']) {
  process[name].write = (chunk, encoding, callback) => {
    written[name].push(Buffer.isBuffer(chunk) ? chunk.toString('utf8') : String(chunk))
    if (typeof encoding === 'function') encoding()
    else if (typeof callback === 'function') callback()
    return true
  }
}
const events = main.withEvents()

function describeError (error) {
  if (!error) return null
  return { code: error.code === undefined ? null : error.code, message: error.messageWithHelp || error.message }
}

let options
async function call (entry) {
  options = entry.options ? JSON.parse(JSON.stringify(entry.options)) : undefined
  const args = entry.args || []
  switch (entry.fn) {
    case 'config': {
      const result = main.config(options)
      return { parsed: result.parsed, error: describeError(result.error) }
    }
    case 'parse': return { parsed: main.parse(args[0], options) }
    case 'get': {
      const value = await main.get(args[0], options)
      return { value: value === undefined ? { undefined: true } : value }
    }
    case 'set': {
      const result = await main.set(args[0], args[1], options)
      return { changed: result.changedFilepaths, unchanged: result.unchangedFilepaths }
    }
    case 'ls': return { files: await main.ls(args[0], args[1], args[2]) }
    case 'events.config': {
      const result = events.config(options)
      await events.flushEvents()
      return { parsed: result.parsed, error: describeError(result.error) }
    }
    case 'events.get': {
      const value = await events.get(args[0], options)
      await events.flushEvents()
      return { value: value === undefined ? { undefined: true } : value }
    }
    case 'events.set': {
      const result = await events.set(args[0], args[1], options)
      await events.flushEvents()
      return { changed: result.changedFilepaths, unchanged: result.unchangedFilepaths }
    }
    case 'write': {
      // args: [stream, text...] - each text is one write call; the captured output is what got through the redaction
      written.stdout = []
      written.stderr = []
      for (const text of args.slice(1)) process[args[0]].write(text)
      return { output: written[args[0]].join('') }
    }
    default: throw new Error(`unknown fn ${entry.fn}`)
  }
}

;(async () => {
  const calls = []
  for (const entry of spec.calls) {
    messages = []
    const row = { fn: entry.fn }
    try {
      row.result = await call(entry)
    } catch (error) {
      row.threw = describeError(error)
    }
    if (options && options.processEnv) row.processEnv = options.processEnv
    row.messages = messages
    calls.push(row)
  }
  const env = {}
  for (const name of spec.watch || []) env[name] = process.env[name] === undefined ? null : process.env[name]
  fs.writeSync(1, JSON.stringify({ calls, env }) + '\n')
})()
