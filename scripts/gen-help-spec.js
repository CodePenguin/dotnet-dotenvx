// Dumps what commander's Help class would print for every command of the pinned dotenvx CLI into
// src/Dotenvx/Help/help-spec.json: per command the usage line, description, and the argument/option/subcommand
// (term, description) rows exactly as commander computes them. src/Dotenvx/Help/CommanderHelp.cs lays these out with
// commander's own wrapping algorithm, so `dotenvx <cmd> --help` is byte-identical to Node's at any terminal width.
//   node scripts/gen-help-spec.js        (run from the repo root after `cd dotenvx && npm install --omit=dev`)
const fs = require('fs')
const path = require('path')

const root = path.resolve(__dirname, '..')
const dotenvxDir = path.join(root, 'dotenvx')

// With no arguments, program.parse runs the root's dynamic [command] action, which calls
// executeDynamic(program, undefined, ...): replace that helper (before dotenvx.js loads it) to capture the fully built
// command tree without printing help or ending the process
let program
const executeDynamicPath = require.resolve(path.join(dotenvxDir, 'src/lib/helpers/executeDynamic.js'))
require.cache[executeDynamicPath] = {
  id: executeDynamicPath,
  filename: executeDynamicPath,
  loaded: true,
  exports: (captured) => { program = captured }
}
process.argv = ['node', 'dotenvx']
require(path.join(dotenvxDir, 'src/cli/dotenvx.js'))
if (!program) throw new Error('could not capture the commander program')

const helper = program.createHelp()
const hidden = new Set()

// text added with addHelpText("after", ...): whatever outputHelp() prints beyond helpInformation()
function afterHelp (cmd) {
  let out = ""
  const saved = cmd._outputConfiguration
  cmd._outputConfiguration = { ...saved, writeOut: (text) => { out += text } }
  try { cmd.outputHelp() } finally { cmd._outputConfiguration = saved }
  const base = cmd.helpInformation()
  return out.startsWith(base) ? out.slice(base.length) : ""
}

// which of dotenvx's collectors (src/cli/dotenvx.js) an option's parser is, so the port can rebuild `this.opts()`
function parserKind (option) {
  if (!option.parseArg) return null
  const source = option.parseArg.toString()
  if (source.includes('previous === undefined')) return 'envKeys'
  if (source.includes('previous || []')) return option.parseArg('a,b', undefined).length === 2 ? 'envFile' : 'env'
  return 'unknown'
}

function dump (cmd) {
  const entry = {
    name: cmd.name(),
    // commander rejects extra operands only when allowExcessArguments(false) was set
    allowExcessArguments: cmd._allowExcessArguments !== false,
    argumentCount: cmd.registeredArguments.length,
    lastArgumentVariadic: cmd.registeredArguments.length > 0 && Boolean(cmd.registeredArguments[cmd.registeredArguments.length - 1].variadic),
    // every option exactly as registered (commander seeds opts() with the defaults in this order)
    registrations: cmd.options.map(o => ({
      attribute: o.attributeName(),
      long: o.long || null,
      short: o.short || null,
      negate: Boolean(o.negate),
      variadic: Boolean(o.variadic),
      takes: o.required ? 'required' : (o.optional ? 'optional' : 'none'),
      hasDefault: o.defaultValue !== undefined,
      default: o.defaultValue === undefined ? null : o.defaultValue,
      parser: parserKind(o)
    })),
    usage: helper.commandUsage(cmd),
    description: helper.commandDescription(cmd),
    padWidth: helper.padWidth(cmd, helper),
    arguments: helper.visibleArguments(cmd).map(a => ({ term: helper.argumentTerm(a), description: helper.argumentDescription(a) })),
    options: helper.visibleOptions(cmd).map(o => ({ term: helper.optionTerm(o), description: helper.optionDescription(o) })),
    commands: helper.visibleCommands(cmd).map(c => ({ term: helper.subcommandTerm(c), description: helper.subcommandDescription(c), name: c.name() })),
    hidden: Boolean(cmd._hidden),
    after: afterHelp(cmd),
    children: cmd.commands.map(dump)
  }
  if (cmd._hidden) hidden.add(entry.name)
  return entry
}

const spec = dump(program)
fs.mkdirSync(path.join(root, 'src/Dotenvx/Help'), { recursive: true })
fs.writeFileSync(path.join(root, 'src/Dotenvx/Help/help-spec.json'), JSON.stringify(spec, null, 1) + '\n')
console.log(`wrote spec for ${JSON.stringify(spec).match(/"usage"/g).length} commands`)
