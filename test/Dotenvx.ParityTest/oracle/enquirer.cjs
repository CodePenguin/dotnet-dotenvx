// Live oracle for the prompt engine (Interop.EnquirerJs): runs one of dotenvx's real prompts (src/lib/helpers/prompts.js) against
// keys piped to stdin and prints everything the prompt wrote plus how it ended. usage: node enquirer.cjs <spec.json>
'use strict'
const fs = require('node:fs')
const path = require('node:path')

const spec = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'))
const prompts = require(path.join(process.cwd(), 'src', 'lib', 'helpers', 'prompts.js'))

const out = []
const output = { write (text) { out.push(String(text)); return true }, isTTY: false }
if (spec.columns) output.columns = spec.columns
if (spec.rows) output.rows = spec.rows

const choices = (spec.choices || []).map(choice => ({ name: choice.name, value: choice.value, ...(choice.disabled ? { disabled: true } : {}) }))
const context = { input: process.stdin, output }

function submitLabel () {
  if (spec.labelMode !== 'protect') return spec.submitLabel
  const current = { configured: spec.configured === true, filter: spec.currentFilter === true, ignore: spec.currentIgnore === true }
  return selected => {
    if (current.configured && ['filter', 'ignore'].every(key => selected.includes(key) === current[key])) return 'Done'
    if (!selected.length) return 'Remove protections'
    return current.configured ? 'Apply changes' : 'Install protections'
  }
}

const calls = {
  select: () => prompts.select({ message: spec.message, choices, ...(spec.prefix ? { prefix: spec.prefix } : {}), ...(spec.initial ? { initial: spec.initial } : {}) }, context),
  custody: () => prompts.select({ message: spec.message, choices, navigation: true, ...(spec.backValue ? { backValue: spec.backValue } : {}), ...(spec.prefix ? { prefix: spec.prefix } : {}), ...(spec.initial ? { initial: spec.initial } : {}) }, context),
  multiselect: () => prompts.multiselect({ message: spec.message, choices, initial: spec.initialNames || [] }, context),
  checklist: () => prompts.multiselect({ message: spec.message, choices, initial: spec.initialNames || [], submitLabel: submitLabel() }, context),
  password: () => prompts.password({ message: spec.message, prefix: spec.prefix, separator: spec.separator }, context),
  input: () => prompts.input({ message: spec.message }, context),
  confirm: () => prompts.confirm({ message: spec.message, initial: spec.initialBool === true }, context)
}

function finish (result) {
  console.log(JSON.stringify({ out: out.join(''), ...result }))
}

let settled = false
const promise = calls[spec.kind]()
// registered once the first render ran, so the prompt's own exit hook (which shows the cursor again) has already run
setImmediate(() => process.on('exit', () => { if (!settled) finish({ ended: true }) }))
promise.then(
  result => { settled = true; finish({ result }) },
  error => { settled = true; finish({ error: error.code || error.message }) })
