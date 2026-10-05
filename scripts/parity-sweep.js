// Differential parity sweep: runs ~590 CLI scenarios (command x flag combination x fixture) against the real Node
// dotenvx and the built .NET port, and compares exit code, stdout, stderr and the files left behind.
//   node scripts/parity-sweep.js [name-filter]      (build src/Dotenvx first; takes about 4 minutes)
// Prints a summary and writes the differences to $TMP/dotenvx-parity-sweep.txt. Differences listed in ACCEPTED are known,
// deliberate and explained there; anything else is a parity bug (or a new scenario that needs a decision).
const { execFile } = require('child_process')
const fs = require('fs')
const os = require('os')
const path = require('path')

const ROOT = path.resolve(__dirname, '..').split(path.sep).join('/')
const NODE_CLI = [`${ROOT}/dotenvx/src/cli/dotenvx.js`]
const DOTNET_CLI = [`${ROOT}/src/Dotenvx/bin/Debug/net10.0/dotenvx.dll`]
const { encrypt } = require(`${ROOT}/dotenvx/node_modules/@dotenvx/primitives`)

const PRIV = '1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70'
const PUB = '02b106c30579baf896ae1fddf077cbcb4fef5e7d457932974878dcb51f42b45498'
const WRONG = 'ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1'
const e = v => encrypt(PUB, v)

const HEADER = `#/-------------------[DOTENV_PUBLIC_KEY]--------------------/
#/            public-key encryption for .env files          /
#/       [how it works](https://dotenvx.com/encryption)     /
#/----------------------------------------------------------/
`
const encEnv = `${HEADER}DOTENV_PUBLIC_KEY="${PUB}"\n\n# .env\nHELLO="${e('World')}"\nFOO="${e('bar')}"\nPLAIN_PLAIN=visible\nEMPTY=\n`
const keysFile = `DOTENV_PRIVATE_KEY=${PRIV}\n`

const fixtures = {
  none: {},
  plain: { '.env': 'HELLO=World\nFOO=bar\n' },
  dups: { '.env': 'A=1\nA=2\nB=3\n' },
  quotes: { '.env': 'S=\'single $HOME\'\nD="double\\nline"\nB=`tick`\nE=\nSP=  spaced  \nX=1 # c\nexport EX=2\nML="a\nb"\nREF=${S}-${D}\nDEF=${NOPE:-fallback}\n' },
  enc: { '.env': encEnv, '.env.keys': keysFile },
  encNoKeys: { '.env': encEnv },
  encWrongKey: { '.env': encEnv, '.env.keys': `DOTENV_PRIVATE_KEY=${WRONG}\n` },
  multi: { '.env': 'A=base\nB=base\n', '.env.local': 'A=local\nC=local\n', '.env.production': 'A=prod\nD=prod\n' },
  bom: { '.env': '\uFEFFHELLO=World\nFOO=bar\n' },
  crlf: { '.env': 'HELLO=World\r\nFOO=bar\r\n' },
  nested: { 'sub/.env': 'HELLO=nested\n', 'sub/.env.keys': '', '.env': 'ROOT=1\n' },
  example: { '.env': 'HELLO=World\nSECRET=x\n', '.env.example': 'HELLO=\nSECRET=\nOPTIONAL= # optional\n' },
  exampleMissing: { '.env': 'HELLO=World\n', '.env.example': 'HELLO=\nSECRET=\n' },
  publicOnly: { '.env': `DOTENV_PUBLIC_KEY="${PUB}"\nA=1\n` },
  prodEnc: { '.env.production': encEnv.replace('DOTENV_PUBLIC_KEY=', 'DOTENV_PUBLIC_KEY_PRODUCTION='), '.env.keys': `DOTENV_PRIVATE_KEY_PRODUCTION=${PRIV}\n` },
  dot: { '.env': '.SECRET=hidden\nFOO=bar\nHELLO=World\n' },
  gitrepo: { '.env': 'A=1\n', '.gitignore': '', '.dockerignore': '' }
}

const scenarios = []
const add = (name, fx, args, env = {}) => scenarios.push({ name, fx, args, env })

// ---- get
for (const fx of ['plain', 'dups', 'quotes', 'enc', 'encNoKeys', 'encWrongKey', 'bom', 'crlf', 'dot', 'none']) {
  add(`get all ${fx}`, fx, ['get'])
  add(`get KEY ${fx}`, fx, ['get', 'HELLO'])
  add(`get missingkey ${fx}`, fx, ['get', 'NOPE'])
  add(`get --all ${fx}`, fx, ['get', '--all', '-ik', 'HELLO', '-ik', 'FOO'])
  for (const fmt of ['json', 'shell', 'colon', 'eval', 'eval-export']) add(`get --format ${fmt} ${fx}`, fx, ['get', '--format', fmt])
  add(`get --pp ${fx}`, fx, ['get', '--pp'])
  add(`get --mask ${fx}`, fx, ['get', '--mask'])
  add(`get --mask 2 ${fx}`, fx, ['get', '--mask', '2'])
  add(`get --strict ${fx}`, fx, ['get', '--strict'])
  add(`get -ik glob ${fx}`, fx, ['get', '-ik', 'H*'])
  add(`get -ek glob ${fx}`, fx, ['get', '-ek', 'H*'])
  add(`get -ik -ek ${fx}`, fx, ['get', '-ik', '*', '-ek', 'HELLO'])
  add(`get -ik multiple space ${fx}`, fx, ['get', '--include-key', 'HELLO', 'FOO'])
}
add('get dot -ik *', 'dot', ['get', '-ik', '*'])
add('get dot -ik .*', 'dot', ['get', '-ik', '.*'])
add('get -f missing', 'plain', ['get', '-f', '.env.nope'])
add('get -f missing strict', 'plain', ['get', '-f', '.env.nope', '--strict'])
add('get -f missing ignore', 'plain', ['get', '-f', '.env.nope', '--ignore', 'MISSING_ENV_FILE'])
add('get -f two', 'multi', ['get', '-f', '.env', '-f', '.env.local'])
add('get -f comma', 'multi', ['get', '-f', '.env,.env.local'])
add('get -f two overload', 'multi', ['get', '-f', '.env', '-f', '.env.local', '--overload'])
add('get -f dir', 'nested', ['get', '-f', 'sub'])
add('get convention nextjs', 'multi', ['get', '--convention', 'nextjs'])
add('get convention flow', 'multi', ['get', '--convention', 'flow'])
add('get convention bad', 'multi', ['get', '--convention', 'bogus'])
add('get -e', 'plain', ['get', '-e', 'X=1'])
add('get -e only', 'none', ['get', '-e', 'X=1', 'X'])
add('get env override existing', 'plain', ['get', 'HELLO'], { HELLO: 'machine' })
add('get env override overload', 'plain', ['get', 'HELLO', '--overload'], { HELLO: 'machine' })
add('get -fk', 'encNoKeys', ['get', '-fk', 'k.keys'], {})
add('get env private key', 'encNoKeys', ['get', 'HELLO'], { DOTENV_PRIVATE_KEY: PRIV })
add('get prod enc', 'prodEnc', ['get', 'HELLO', '-f', '.env.production'])
add('get prod enc convention', 'prodEnc', ['get', 'HELLO', '--convention', 'nextjs'])

// ---- run (child prints selected env)
const show = ['node', '-e', 'console.log(JSON.stringify({H:process.env.HELLO,F:process.env.FOO,A:process.env.A,B:process.env.B,X:process.env.X}))']
for (const fx of ['plain', 'dups', 'quotes', 'enc', 'encNoKeys', 'multi', 'bom', 'crlf', 'none']) {
  add(`run ${fx}`, fx, ['run', '--', ...show])
  add(`run --quiet ${fx}`, fx, ['run', '--quiet', '--', ...show])
  add(`run --verbose ${fx}`, fx, ['run', '--verbose', '--', ...show])
  add(`run --strict ${fx}`, fx, ['run', '--strict', '--', ...show])
  add(`run --redact ${fx}`, fx, ['run', '--redact', '--', ...show])
  add(`run --mask ${fx}`, fx, ['run', '--mask', '--', ...show])
  add(`run --validate ${fx}`, fx, ['run', '--validate', '--', ...show])
}
add('run -e', 'none', ['run', '-e', 'X=1', '-e', 'HELLO=env', '--', ...show])
add('run -e variadic', 'none', ['run', '-e', 'X=1', 'HELLO=env', '--', ...show])
add('run -f two', 'multi', ['run', '-f', '.env', '-f', '.env.local', '--', ...show])
add('run -f two overload', 'multi', ['run', '-f', '.env', '-f', '.env.local', '--overload', '--', ...show])
add('run -f missing', 'plain', ['run', '-f', '.env.nope', '--', ...show])
add('run -f missing ignore', 'plain', ['run', '-f', '.env.nope', '--ignore', 'MISSING_ENV_FILE', '--', ...show])
add('run -f missing strict', 'plain', ['run', '-f', '.env.nope', '--strict', '--', ...show])
add('run convention nextjs', 'multi', ['run', '--convention', 'nextjs', '--', ...show])
add('run exit code', 'plain', ['run', '--', 'node', '-e', 'process.exit(7)'])
add('run no command', 'plain', ['run'])
add('run no separator', 'plain', ['run', ...show])
add('run -fk dir', 'nested', ['run', '-fk', 'sub', '--', ...show])
add('run env existing', 'plain', ['run', '--', ...show], { HELLO: 'machine' })
add('run env existing overload', 'plain', ['run', '--overload', '--', ...show], { HELLO: 'machine' })
add('run expansion', 'quotes', ['run', '--', 'node', '-e', 'console.log(process.env.REF, process.env.DEF)'])
add('run private key env', 'encNoKeys', ['run', '--', ...show], { DOTENV_PRIVATE_KEY: PRIV })
add('run wrong key', 'encWrongKey', ['run', '--', ...show])

// ---- set
for (const fx of ['none', 'plain', 'enc', 'dups', 'quotes', 'publicOnly']) {
  add(`set plain ${fx}`, fx, ['set', 'NEW', 'value', '--plain'], {})
  add(`set encrypt ${fx}`, fx, ['set', 'NEW', 'value'])
  add(`set existing ${fx}`, fx, ['set', 'HELLO', 'changed'])
  add(`set same ${fx}`, fx, ['set', 'HELLO', 'World'])
  add(`set -c ${fx}`, fx, ['set', 'NEW', 'value', '-c'])
  add(`set -c --plain ${fx}`, fx, ['set', 'NEW', 'value', '-c', '--plain'])
  add(`set special ${fx}`, fx, ['set', 'NEW', 'a b "c" \'d\' $E \\n', '--plain'])
  add(`set multiline ${fx}`, fx, ['set', 'NEW', 'line1\nline2', '--plain'])
}
add('set _PLAIN key', 'plain', ['set', 'X_PLAIN', 'v'])
add('set no-create', 'none', ['set', 'A', 'b', '--no-create'])
add('set create', 'none', ['set', 'A', 'b'])
add('set -f two', 'multi', ['set', 'A', 'x', '--plain', '-f', '.env', '-f', '.env.local'])
add('set -f comma', 'multi', ['set', 'A', 'x', '--plain', '-f', '.env,.env.local'])
add('set missing value', 'plain', ['set', 'A'])
add('set -fk custom', 'plain', ['set', 'A', 'x', '-fk', 'my.keys'])
add('set -fk custom two', 'plain', ['set', 'A', 'x', '-fk', 'one.keys', '-fk', 'two.keys'])
add('set nested -f', 'nested', ['set', 'A', 'x', '-f', 'sub/.env'])
add('set shebang', 'none', ['set', 'A', 'x'])
add('set production', 'prodEnc', ['set', 'NEW', 'v', '-f', '.env.production'])

// ---- encrypt / decrypt
for (const fx of ['none', 'plain', 'enc', 'dups', 'quotes', 'publicOnly', 'bom', 'crlf', 'dot']) {
  add(`encrypt ${fx}`, fx, ['encrypt'])
  add(`encrypt --stdout ${fx}`, fx, ['encrypt', '--stdout'])
  add(`encrypt -k ${fx}`, fx, ['encrypt', '-k', 'HELLO'])
  add(`encrypt -ek ${fx}`, fx, ['encrypt', '-ek', 'HELLO'])
  add(`encrypt -k glob ${fx}`, fx, ['encrypt', '-k', 'H*', '-ek', 'HOLA'])
  add(`encrypt no-create ${fx}`, fx, ['encrypt', '--no-create'])
}
add('encrypt -f missing', 'plain', ['encrypt', '-f', '.env.nope'])
add('encrypt -f missing no-create', 'plain', ['encrypt', '-f', '.env.nope', '--no-create'])
add('encrypt -f two', 'multi', ['encrypt', '-f', '.env', '-f', '.env.local'])
add('encrypt -fk dir', 'plain', ['encrypt', '-fk', 'keys.env'])
add('encrypt nested', 'nested', ['encrypt', '-f', 'sub/.env'])
add('encrypt production', 'multi', ['encrypt', '-f', '.env.production'])
for (const fx of ['enc', 'encNoKeys', 'encWrongKey', 'plain', 'quotes', 'dups', 'prodEnc', 'crlf']) {
  add(`decrypt ${fx}`, fx, ['decrypt'])
  add(`decrypt --stdout ${fx}`, fx, ['decrypt', '--stdout'])
  add(`decrypt --stdout --mask ${fx}`, fx, ['decrypt', '--stdout', '--mask'])
  add(`decrypt -k ${fx}`, fx, ['decrypt', '-k', 'HELLO'])
  add(`decrypt -ek ${fx}`, fx, ['decrypt', '-ek', 'HELLO'])
}
add('decrypt -fk', 'encNoKeys', ['decrypt', '-fk', 'k.keys'])
add('decrypt -f missing', 'plain', ['decrypt', '-f', '.env.nope'])
add('decrypt prod', 'prodEnc', ['decrypt', '-f', '.env.production'])
add('decrypt env key', 'encNoKeys', ['decrypt'], { DOTENV_PRIVATE_KEY: PRIV })
add('decrypt nested', 'nested', ['decrypt', '-f', 'sub/.env'])

// ---- del / keypair / ls / validate / genexample / gitignore / precommit / prebuild
for (const fx of ['plain', 'dups', 'quotes', 'enc', 'multi', 'bom', 'crlf', 'none']) {
  add(`del ${fx}`, fx, ['del', 'HELLO'])
  add(`del missing key ${fx}`, fx, ['del', 'NOPE'])
}
add('del -f two', 'multi', ['del', 'A', '-f', '.env', '-f', '.env.local'])
add('del A dups', 'dups', ['del', 'A'])
add('del multiline', 'quotes', ['del', 'ML'])
for (const fx of ['plain', 'enc', 'encNoKeys', 'prodEnc', 'publicOnly', 'none', 'nested']) {
  add(`keypair ${fx}`, fx, ['keypair'])
  add(`keypair KEY ${fx}`, fx, ['keypair', 'DOTENV_PRIVATE_KEY'])
  add(`keypair --pp ${fx}`, fx, ['keypair', '--pp'])
  add(`keypair colon ${fx}`, fx, ['keypair', '--format', 'colon', 'DOTENV_PUBLIC_KEY'])
}
add('keypair -f two', 'multi', ['keypair', '-f', '.env', '-f', '.env.production'])
add('keypair -f prod', 'prodEnc', ['keypair', '-f', '.env.production'])
add('keypair -fk', 'encNoKeys', ['keypair', '-fk', 'nokeys'])
add('keypair env key', 'encNoKeys', ['keypair'], { DOTENV_PRIVATE_KEY: PRIV })
add('keypair -f missing', 'plain', ['keypair', '-f', '.env.nope'])
for (const fx of ['plain', 'multi', 'nested', 'none']) {
  add(`ls ${fx}`, fx, ['ls'])
  add(`ls --json ${fx}`, fx, ['ls', '--json'])
  add(`ls -q ${fx}`, fx, ['ls', '-q'])
  add(`ls -f ${fx}`, fx, ['ls', '-f', '.env.local'])
  add(`ls -ef ${fx}`, fx, ['ls', '-ef', '.env.local'])
  add(`ls dir ${fx}`, fx, ['ls', 'sub'])
}
add('ls -f multiple', 'multi', ['ls', '-f', '.env', '.env.local'])
add('ls -f glob', 'multi', ['ls', '-f', '.env.*'])
for (const fx of ['example', 'exampleMissing', 'plain', 'none']) {
  add(`validate ${fx}`, fx, ['validate'])
  add(`validate --ignore ${fx}`, fx, ['validate', '--ignore', 'VALIDATION_FAILED'])
  add(`validate -f ${fx}`, fx, ['validate', '-f', '.env'])
  add(`validate -e ${fx}`, fx, ['validate', '-e', 'SECRET=1'])
}
for (const fx of ['plain', 'multi', 'quotes', 'enc', 'none', 'example']) {
  add(`genexample ${fx}`, fx, ['genexample'])
  add(`genexample -f ${fx}`, fx, ['genexample', '-f', '.env'])
}
add('genexample dir', 'nested', ['genexample', 'sub'])
add('genexample missing -f', 'plain', ['genexample', '-f', '.env.nope'])
for (const fx of ['none', 'plain', 'gitrepo']) {
  add(`gitignore ${fx}`, fx, ['gitignore'])
  add(`gitignore --pattern ${fx}`, fx, ['gitignore', '--pattern', '.env.keys', 'secrets*'])
  add(`precommit ${fx}`, fx, ['precommit'])
  add(`prebuild ${fx}`, fx, ['prebuild'])
  add(`ext gitignore ${fx}`, fx, ['ext', 'gitignore'])
  add(`ext precommit ${fx}`, fx, ['ext', 'precommit'])
  add(`ext prebuild ${fx}`, fx, ['ext', 'prebuild'])
  add(`ext genexample ${fx}`, fx, ['ext', 'genexample'])
}
add('precommit enc', 'enc', ['precommit'])
add('prebuild enc', 'enc', ['prebuild'])
add('ext ls', 'multi', ['ext', 'ls'])
add('ext scan', 'plain', ['ext', 'scan'])

// ---- global options / misc
for (const g of [['-v'], ['-q'], ['-d'], ['-l', 'debug'], ['-l', 'error'], ['-l', 'bogus'], ['--version'], ['-V']]) add(`global ${g.join(' ')}`, 'plain', ['get', 'HELLO', ...g])
add('get unknown opt', 'plain', ['get', '--bogus', 'HELLO'])
add('get unknown short', 'plain', ['get', '-Z', 'HELLO'])
add('set unknown opt', 'none', ['set', 'A', 'b', '--bogus', '--plain'])
add('run unknown opt', 'plain', ['run', '--bogus', '--', ...show])
add('del unknown opt', 'plain', ['del', 'A', '--bogus'])
add('unknown command', 'plain', ['bogus'])
add('no args', 'plain', [])
add('get --help', 'plain', ['get', '--help'])
add('get bogus --help', 'plain', ['get', '--bogus', '--help'])
add('env DOTENV_CONFIG_QUIET', 'plain', ['run', '--', 'node', '-e', '1'], { DOTENV_CONFIG_QUIET: 'true' })
add('env DOTENV_CONFIG_IGNORE', 'none', ['run', '--', 'node', '-e', '1'], { DOTENV_CONFIG_IGNORE: 'MISSING_ENV_FILE' })

// Known, deliberate differences (a different help/debug renderer, or output that embeds its own temp directory name).
const ACCEPTED = [
  /--help$/, /bogus --help$/, /^no args$/, // System.CommandLine renders help differently from commander
  /^global -d$/, /^global -l debug$/, // debug option dumps have a different shape
  /^ls --json /, // absolute paths embed the per-run temp directory (the two sides use different ones)
  /^get convention bad$/ // both crash with an uncaught error; only the stack trace text differs
]

// ---- runner
const normalize = (s, dir) => {
  s = s.split(dir).join('<DIR>').split(dir.split(path.sep).join('/')).join('<DIR>')
  s = s.replace(/encrypted:[A-Za-z0-9+/=]+/g, 'encrypted:ENC')
  s = s.replace(/\b[0-9a-f]{64,}\b/g, 'HEX').replace(/\b0[23][0-9a-f]{64}\b/g, 'PUB')
  s = s.replace(/scanned in \d+s/g, 'scanned in Ns')
  s = s.replace(/\[dotenvx@[^\]]+\]/g, '[dotenvx@V]')
  return s
}

function runOne (cliArgs, sc, isNode) {
  return new Promise(resolve => {
    const dir = fs.mkdtempSync(path.join(fs.realpathSync(os.tmpdir()), 'sweep-'))
    for (const [f, c] of Object.entries(fixtures[sc.fx])) {
      fs.mkdirSync(path.dirname(path.join(dir, f)), { recursive: true })
      fs.writeFileSync(path.join(dir, f), c)
    }
    let args = [...sc.args]
    if (isNode && ['encrypt', 'set'].includes(args[0])) args = [args[0], ...args.slice(1), '--no-native', '--no-armor']
    const env = { ...process.env, ...sc.env }
    for (const k of Object.keys(env)) if (k.startsWith('DOTENV') && !(k in sc.env)) delete env[k]
    delete env.PUBLIC
    const exe = isNode ? 'node' : 'dotnet'
    execFile(exe, [...(isNode ? NODE_CLI : DOTNET_CLI), ...args], { cwd: dir, env, encoding: 'utf8', timeout: 60000 }, (err, stdout, stderr) => {
      const files = {}
      const walk = d => { for (const n of fs.readdirSync(d)) { const p = path.join(d, n); if (fs.statSync(p).isDirectory()) walk(p); else files[path.relative(dir, p).split(path.sep).join('/')] = normalize(fs.readFileSync(p, 'utf8'), dir) } }
      walk(dir)
      try { fs.rmSync(dir, { recursive: true, force: true, maxRetries: 3 }) } catch (e2) { /* best effort */ }
      resolve({ code: err ? (err.code ?? 1) : 0, stdout: normalize(stdout, dir), stderr: normalize(stderr, dir), files })
    })
  })
}

async function main () {
  const only = process.argv[2]
  const list = scenarios.filter(s => !only || s.name.includes(only))
  const diffs = []
  let i = 0
  const worker = async () => {
    while (i < list.length) {
      const sc = list[i++]
      const [n, d] = await Promise.all([runOne(null, sc, true), runOne(null, sc, false)])
      const fields = []
      if (n.code !== d.code) fields.push('exit')
      if (n.stdout !== d.stdout) fields.push('stdout')
      if (n.stderr !== d.stderr) fields.push('stderr')
      if (JSON.stringify(n.files) !== JSON.stringify(d.files)) fields.push('files')
      if (fields.length) diffs.push({ sc, fields, n, d })
    }
  }
  await Promise.all(Array.from({ length: 6 }, worker))
  const unexpected = diffs.filter(d => !ACCEPTED.some(a => a.test(d.sc.name)))
  diffs.length = 0
  diffs.push(...unexpected)
  diffs.sort((a, b) => a.sc.name.localeCompare(b.sc.name))
  const lines = []
  for (const x of diffs) {
    lines.push(`### ${x.sc.name} [${x.fields.join(',')}]  args=${JSON.stringify(x.sc.args)} env=${JSON.stringify(x.sc.env)}`)
    for (const f of x.fields) {
      if (f === 'files') {
        lines.push(`  node files: ${JSON.stringify(x.n.files).slice(0, 400)}`)
        lines.push(`  dotn files: ${JSON.stringify(x.d.files).slice(0, 400)}`)
      } else if (f === 'exit') lines.push(`  exit node=${x.n.code} dotnet=${x.d.code}`)
      else { lines.push(`  ${f} node=${JSON.stringify(x.n[f]).slice(0, 300)}`); lines.push(`  ${f} dotn=${JSON.stringify(x.d[f]).slice(0, 300)}`) }
    }
  }
  fs.writeFileSync('C:/Users/codep/.claude/scratch/sweep.out.txt', lines.join('\n'))
  console.log(`${list.length} scenarios, ${diffs.length} differ`)
}
main()
