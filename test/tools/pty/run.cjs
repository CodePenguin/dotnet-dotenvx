// Runs a command in a pseudo terminal, plays a script of expectations and keystrokes against it and prints what the screen showed.
// usage: node run.cjs <spec.json>
//   spec: { command: [exe, ...args], cwd, env, cols, rows, steps: [{ expect?: text, send?: keys, settleMs?: n }], exitTimeoutMs }
//   output (one JSON line): { snapshots: [screen after each step], final: screen at exit, exitCode, timedOut }
// A screen is a list of lines; each line is the text with the cell attributes (colour, bold, dim, underline, inverse) written as
// {fg;bg;flags} markers wherever they change, so two runs compare equal only if they look identical.
'use strict'
const fs = require('node:fs')
const pty = require('node-pty')
const { Terminal } = require('@xterm/headless')

const spec = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'))
const cols = spec.cols || 80
const rows = spec.rows || 24
const terminal = new Terminal({ cols, rows, allowProposedApi: true, scrollback: 1000 })

// an empty value in the spec removes the variable (node-pty can't pass an unset one, and "CI" or "NO_COLOR" being set at all changes behaviour)
const env = { ...process.env, ...spec.env }
for (const key of Object.keys(spec.env || {})) if (spec.env[key] === '') delete env[key]

const child = pty.spawn(spec.command[0], spec.command.slice(1), {
  name: 'xterm-256color',
  cols,
  rows,
  cwd: spec.cwd,
  env
})

let pending = Promise.resolve()
let lastData = Date.now()
child.onData(data => {
  lastData = Date.now()
  pending = pending.then(() => new Promise(resolve => terminal.write(data, resolve)))
})

let exited = false
let exitCode
child.onExit(event => { exited = true; exitCode = event.exitCode })

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms))

function attributes (cell) {
  const color = (mode, value) => mode === 0 ? '-' : `${mode}:${value}`
  const flags = (cell.isBold() ? 'b' : '') + (cell.isDim() ? 'd' : '') + (cell.isUnderline() ? 'u' : '') + (cell.isInverse() ? 'i' : '') + (cell.isItalic() ? 't' : '')
  return `${color(cell.getFgColorMode(), cell.getFgColor())};${color(cell.getBgColorMode(), cell.getBgColor())};${flags}`
}

function screen () {
  const buffer = terminal.buffer.active
  const lines = []
  for (let y = 0; y < buffer.length; y++) {
    const line = buffer.getLine(y)
    if (!line) continue
    let text = ''
    let current = ''
    let trailing = 0
    for (let x = 0; x < cols; x++) {
      const cell = line.getCell(x)
      if (!cell) break
      const chars = cell.getChars() || ' '
      const attr = attributes(cell)
      if (attr !== current) { text += `{${attr}}`; current = attr }
      text += chars
      trailing = chars === ' ' && attr === '-;-;' ? trailing + 1 : 0
    }
    lines.push(trailing ? text.slice(0, text.length - trailing) : text)
  }
  for (let i = 0; i < lines.length; i++) lines[i] = lines[i].replace(/{[^}]*}$/, '')
  while (lines.length && lines[lines.length - 1] === '') lines.pop()
  return lines
}

function plain () {
  return screen().map(line => line.replace(/\{[^}]*\}/g, '')).join('\n')
}

async function settle (ms) {
  // wait until the terminal has been quiet for ms and every chunk is parsed
  while (Date.now() - lastData < ms) await sleep(20)
  await pending
}

async function waitFor (text, timeout) {
  const deadline = Date.now() + timeout
  while (Date.now() < deadline) {
    await pending
    if (plain().includes(text)) return true
    if (exited) return false
    await sleep(25)
  }
  return false
}

;(async () => {
  const snapshots = []
  let timedOut = false
  for (const step of spec.steps || []) {
    if (step.expect != null && !(await waitFor(step.expect, step.timeoutMs || 20000))) { timedOut = true; break }
    await settle(step.settleMs || 150)
    snapshots.push(screen())
    if (step.send != null && !exited) child.write(step.send)
  }
  const deadline = Date.now() + (spec.exitTimeoutMs || 15000)
  while (!exited && Date.now() < deadline) await sleep(25)
  if (!exited) { timedOut = true; child.kill() }
  await sleep(100)
  await settle(150)
  console.log(JSON.stringify({ snapshots, final: screen(), exitCode, timedOut }))
  process.exit(0)
})()
