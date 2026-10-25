// Differential parity sweep: runs ~15,000 CLI scenarios (command x flag combination x fixture) against the real Node
// dotenvx and the built .NET port, and compares exit code, stdout, stderr and the files left behind.
//   node scripts/parity-sweep.js [name-filter]      (build src/Dotenvx first; the full sweep takes 30 to 40 minutes;
//   SWEEP_EVERY=25 runs a sample of every 25th scenario in a few minutes)
// Prints a summary and writes the differences to $TMP/dotenvx-parity-sweep.txt. Differences listed in ACCEPTED are known,
// deliberate and explained there; anything else is a parity bug (or a new scenario that needs a decision).
const { execFile, execFileSync } = require('child_process')
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
  doctorProj: { 'app.py': 'load_dotenv()\n', 'index.js': "require('dotenv').config()\ndotenv.config()\n", 'sub/main.go': 'godotenv.Load()\n', 'node_modules/x.js': 'dotenv.config()\n', 'README.md': 'load_dotenv()\n' },
  opRef: { '.env': 'A=op://vault/item/field\nB=bw://x/password\n' },
  gitrepo: { '.env': 'A=1\n', '.gitignore': '', '.dockerignore': '' }
}

const scenarios = []
const add = (name, fx, args, env = {}) => scenarios.push({ name, fx, args, env })
const addSteps = (name, fx, steps, env = {}) => scenarios.push({ name, fx, steps, env })

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

// ---- commands added with the full-parity work (doctor, update, help, ext dispatch, armor settings, lock/native errors)
add('doctor', 'doctorProj', ['doctor'])
add('doctor dir', 'doctorProj', ['doctor', 'sub'])
add('doctor missing dir', 'doctorProj', ['doctor', 'nowhere'])
add('doctor -q', 'doctorProj', ['doctor', '-q'])
add('update', 'none', ['update'])
add('help unknown stdout exit', 'none', ['help', 'bogus'])
add('ext unknown', 'none', ['ext', 'bogus', '--x'])
add('ext vault', 'none', ['ext', 'vault'])
add('armor unknown', 'none', ['armor', 'bogus'])
add('armor status', 'none', ['armor', 'status'])
for (const sub of ['username', 'token', 'hostname', 'path', 'on', 'off']) add(`armor settings ${sub}`, 'none', ['armor', 'settings', sub])
add('lock up no env', 'none', ['lock', 'up'])
add('lock down no env', 'none', ['lock', 'down'])
add('lock up no key', 'publicOnly', ['lock', 'up'])
add('native up no env', 'none', ['native', 'up'], { CI: 'true' })
add('native pull no key', 'publicOnly', ['native', 'pull'], { CI: 'true' })
add('curl no token', 'none', ['curl', 'https://armor.dotenvx.com/api/account'])
add('curl refuses origin', 'none', ['curl', 'https://evil.example/x', '--token', 't'])
add('get --no-armor --no-native --no-1password --no-bitwarden', 'enc', ['get', '--no-armor', '--no-native', '--no-1password', '--no-bitwarden'])
add('run --token ignores for plain', 'plain', ['run', '--no-armor', '--', 'node', '-e', 'console.log(process.env.HELLO)'])
scenarios.push({ name: 'op ref get', fx: 'opRef', args: ['get'], env: {} })

// ---- option syntax shapes (commander parsing rules): = forms, attached short values, bundling, position, missing values
const shapes = [
  ['get', 'HELLO', '--env-file=.env'], ['get', 'HELLO', '-f=.env'], ['get', 'HELLO', '-f.env'], ['get', 'HELLO', '-f', '.env'],
  ['get', 'HELLO', '--env-f', '.env'], ['get', 'HELLO', '--env-file'], ['get', 'HELLO', '-f'], ['get', '-f', '.env', 'HELLO'],
  ['-q', 'get', 'HELLO'], ['--quiet', 'get', 'HELLO'], ['-lerror', 'get', 'HELLO'], ['--log-level=debug', 'get', 'HELLO'],
  ['--log-level', 'get', 'HELLO'], ['-l'], ['-qv', 'get', 'HELLO'], ['-qd', 'get', 'HELLO'], ['get', '-qv', 'HELLO'],
  ['get', 'HELLO', '--', '-q'], ['get', '--', 'HELLO'], ['get', ''], ['get', 'HELLO', 'FOO'], ['get', 'HELLO', '-V'],
  ['get', 'HELLO', '--version'], ['-V', 'get', 'HELLO'], ['--version', 'bogus'], ['get', 'HELLO', '--pretty-print'],
  ['get', 'HELLO', '-pp'], ['get', 'HELLO', '--pp'], ['get', 'HELLO', '--pretty'], ['get', '--all', 'HELLO'],
  ['get', 'HELLO', '--overload', '--overload'], ['get', 'HELLO', '-o'], ['get', 'HELLO', '--convention=nextjs'],
  ['get', 'HELLO', '--convention', 'bogus'], ['get', 'HELLO', '-fk'], ['get', 'HELLO', '-fk', '.env.keys'],
  ['get', 'HELLO', '--env-keys-file=.env.keys'], ['get', 'HELLO', '--strict', '--ignore', 'X'], ['get', 'HELLO', '--ignore=X', '--ignore=Y'],
  ['keypair', '-f', '.env', '-f', '.env'], ['keypair', '-f', '.env', '.env.keys'], ['keypair', '--pp', '-f', '.env'],
  ['set', 'A=b'], ['set', 'A', 'b', '-f=.env'], ['set', '--', 'A', 'b'], ['set', 'A', '--', '-b'], ['set', 'A', '-b'],
  ['encrypt', '-k', 'A', 'B'], ['encrypt', '-k', 'A', '-k', 'B'], ['encrypt', '-k', 'A', '--', 'B'], ['encrypt', '--key=A'],
  ['encrypt', '-ek', 'A', 'B'], ['decrypt', '-k', 'HELLO', 'FOO'], ['decrypt', '--exclude-key=HELLO'],
  ['del', '-f', '.env', 'HELLO'], ['del', 'HELLO', '-f', '.env', '.env'], ['ls', '-f', '.env', '.env.local'], ['ls', '-ef', '**/.env'],
  ['ls', '--exclude-env-file', '*.local', '--', 'sub'], ['validate', '-f', '.env', '.env.local'],
  ['run', '-f', '.env', '-f', '.env.local', '--', ...show], ['run', '-f', '.env', '.env.local', '--', ...show],
  ['run', '-e', 'A=1', '-e', 'B=2', '--', ...show], ['run', '-e', 'A=1', 'B=2', '--', ...show], ['run', '--env=A=1', '--', ...show],
  ['run', '-e', 'A=1', '--', ...show, '-q'], ['run', '--', ...show, '--help'], ['run', ...show], ['run', '-f', '.env', ...show],
  ['keypair', ''], ['del', ''], ['set', '', 'v'], ['get', 'HELLO', '--env-fil'], ['get', 'HELLO', '--overlod'], ['get', 'HELLO', '--quite'],
  ['get', 'HELLO', '--log'], ['get', 'HELLO', '--env-keys'], ['get', 'HELLO', '--no-armo'], ['run', '--overlod', '--', ...show], ['-x'], ['--bogus'],
  ['encrypt', '--key'], ['ls', '--bogus-flag'], ['get', 'HELLO', '--env-file:.env'], ['get', 'HELLO', '-f:.env'], ['run', '-e:A=1', '--', ...show],
  ['get', 'HELLO', '-f', '--overload'], ['get', 'HELLO', '--env-file', '--overload'], ['get', 'HELLO', '-f', '-q'], ['get', '-f', '--', 'HELLO'],
  ['-l', 'get', 'HELLO'], ['-l', '--quiet', 'get', 'HELLO'], ['get', 'HELLO', '-k', '--x'], ['run', '-e', '--x', '--', ...show], ['run', '-e', '-f', '--', ...show],
  ['keypair', '-f=.env'], ['encrypt', '-k=HELLO'], ['get', 'HELLO', '-ek', 'FOO', '--', 'x'],
  ['run', '--overload', '--', ...show], ['run', '--strict', '--', ...show], ['run', '--log-level', 'debug', '--', ...show]
]
for (const a of shapes) for (const fx of ['plain', 'multi']) add(`shape ${fx} ${a.join(' ')}`, fx, a)

// ---- round 4: file syntax, environment variables, key/value spellings and file/directory arguments.
// Appended to scripts/parity-sweep.js (uses its fixtures/add/show helpers).
const CR = String.fromCharCode(13)
const syntaxFixtures = {
  synComments: 'A=1 # c\nB="x # y"\n#full\n\n  C = 3\nD= spaced\nexport E=5\nF:colon\nG=\'a # b\'\nH=`t # u`\n',
  synMultiline: 'A="line1\nline2"\nB=\'s1\ns2\'\nC=`b1\nb2`\nD=after\n',
  synEscapes: 'A="a\\nb"\nB=\'a\\nb\'\nC="a\\"b"\nD=\'it\\\'s\'\nE=\\x41\nF="tab\\there"\nG="back\\\\slash"\nH=`bt\\nx`\n',
  synInterp: 'A=1\nB=${A}x\nC=$A\nD=${NOPE:-def}\nE=${A:+alt}\nF=$(echo hi)\nG=\\${A}\nH=\'${A}\'\nI=${I}\nJ=${K}\nK=${J}\n',
  synUnicode: 'HÉLLO=x\nK=日本語\nE=emoji😀\n_UNDER=1\na.b-c=2\n9START=3\nlower=l\n',
  synEmpty: '',
  synBlank: '\n\n   \n',
  synOnlyComments: '# just a comment\n# another\n',
  synWeird: '=novalue\nNOEQ\nA==b\nB=c=d\nC="unclosed\nD=ok\n',
  synWeird2: 'D=\'unclosed\nE=fine\nF=   \nG=trailing   \n',
  synLong: 'A=' + 'x'.repeat(5000) + '\n' + Array.from({ length: 300 }, (_, i) => `K${i}=v${i}`).join('\n') + '\n',
  synCrOnly: 'A=1' + CR + 'B=2' + CR + 'C=3' + CR,
  synMixed: 'A=1\nB=2' + CR + '\nC=3' + CR + 'D=4\n',
  synDupQuotes: 'A=1\nA="two"\nA=\'three\'\nB=x\nA=`four`\n',
  synExportOnly: 'export A=1\nexport B="two words"\nexport\nexport C\n',
  synBigIndent: '\t\tA=1\n \tB=2\n'
}
for (const [name, content] of Object.entries(syntaxFixtures)) fixtures[name] = { '.env': content }
const keysShown = "console.log(JSON.stringify(Object.fromEntries(Object.entries(process.env).filter(([k]) => /^(A|B|C|D|E|F|G|H|I|J|K|K\\d+|HÉLLO|_UNDER|a\\.b-c|9START|lower)$/.test(k)))))"
for (const name of Object.keys(syntaxFixtures)) {
  const fx = name
  add(`r4 get ${fx}`, fx, ['get'])
  add(`r4 get -pp ${fx}`, fx, ['get', '--pp'])
  for (const fmt of ['shell', 'eval', 'eval-export', 'colon']) add(`r4 get ${fmt} ${fx}`, fx, ['get', '--format', fmt])
  add(`r4 get A ${fx}`, fx, ['get', 'A'])
  add(`r4 get strict ${fx}`, fx, ['get', '--strict'])
  add(`r4 run ${fx}`, fx, ['run', '--', 'node', '-e', keysShown])
  add(`r4 run overload ${fx}`, fx, ['run', '--overload', '--', 'node', '-e', keysShown], { A: 'machine', B: 'machine' })
  add(`r4 encrypt ${fx}`, fx, ['encrypt'])
  add(`r4 decrypt ${fx}`, fx, ['decrypt'])
  add(`r4 decrypt stdout ${fx}`, fx, ['decrypt', '--stdout'])
  add(`r4 set ${fx}`, fx, ['set', 'A', 'new', '--plain'])
  add(`r4 set enc ${fx}`, fx, ['set', 'ZZ', 'new'])
  add(`r4 del ${fx}`, fx, ['del', 'A'])
  add(`r4 keypair ${fx}`, fx, ['keypair'])
  add(`r4 validate ${fx}`, fx, ['validate'])
  add(`r4 genexample ${fx}`, fx, ['genexample'])
  add(`r4 ls ${fx}`, fx, ['ls', '--json'])
}

// environment variables read by the CLI and the library
const envVars = [
  { DOTENV_CONFIG_QUIET: 'true' }, { DOTENV_CONFIG_QUIET: 'false' }, { DOTENV_CONFIG_IGNORE: 'MISSING_ENV_FILE' }, { DOTENV_IGNORE: 'MISSING_ENV_FILE,WRONG_PRIVATE_KEY' },
  { DOTENV_CONFIG_PATH: '.env.local' }, { DOTENV_CONFIG_PATH: '.env,.env.local' }, { DOTENV_FILE: '.env.local' }, { DOTENV_PATH: '.env.local' }, { DOTENV_F: '.env.local' },
  { DOTENV_PATH: '.env.local', DOTENV_FILE: '.env.production' }, { DOTENV_CONFIG_CONVENTION: 'nextjs' }, { DOTENV_CONVENTION: 'flow' }, { DOTENV_CONVENTION: 'bogus' },
  { DOTENV_PRIVATE_KEY: PRIV }, { DOTENV_PRIVATE_KEY: WRONG }, { DOTENV_PRIVATE_KEY_PRODUCTION: PRIV }, { DOTENV_PRIVATE_KEY_LOCAL: PRIV },
  { DOTENV_PUBLIC_KEY: PUB }, { DOTENV_KEY: 'dotenv://:key_1234@dotenvx.com/vault/.env.vault?environment=production' },
  { DOTENV_CONFIG_OVERRIDE: 'true' }, { DOTENV_CONFIG_DEBUG: 'true' }, { DOTENV_CONFIG_STRICT: 'true' }, { NO_COLOR: '1' }, { FORCE_COLOR: '1' }, { CI: 'true' },
  { DOTENV_PATH: '' }, { DOTENV_CONFIG_PATH: '' }, { DOTENV_PRIVATE_KEY: '' }
]
for (const e of envVars) {
  const tag = Object.entries(e).map(([k, v]) => `${k}=${v.slice(0, 12)}`).join(',')
  for (const fx of ['multi', 'enc', 'prodEnc']) {
    add(`r4 env ${tag} get ${fx}`, fx, ['get'], e)
    add(`r4 env ${tag} run ${fx}`, fx, ['run', '--', ...show], e)
    add(`r4 env ${tag} decrypt ${fx}`, fx, ['decrypt', '--stdout'], e)
  }
  add(`r4 env ${tag} keypair`, 'enc', ['keypair'], e)
  add(`r4 env ${tag} validate`, 'example', ['validate'], e)
  add(`r4 env ${tag} ls`, 'multi', ['ls', '--json'], e)
}

// spellings of keys and values given to set / get / del / keypair
const keySpellings = ['a.b', 'a-b', 'lower', '9x', 'A B', 'A=B', 'ÄÖ', '$X', '-x', '--x', 'export', 'A_PLAIN', 'DOTENV_PUBLIC_KEY', 'K'.repeat(300)]
const valueSpellings = ['', ' ', '"', "'", '`', '#', '\\', '=', 'a=b', '$HOME', '${X}', ' lead', 'trail ', 'multi\nline', 'tab\tx', 'ünï😀', 'encrypted:abc', 'x'.repeat(3000), '-dash', '--flaglike']
for (const k of keySpellings) {
  for (const fx of ['plain', 'enc']) {
    add(`r4 set key ${JSON.stringify(k).slice(0, 24)} ${fx}`, fx, ['set', k, 'v'])
    add(`r4 set key plain ${JSON.stringify(k).slice(0, 24)} ${fx}`, fx, ['set', k, 'v', '--plain'])
  }
  add(`r4 get key ${JSON.stringify(k).slice(0, 24)}`, 'synUnicode', ['get', k])
  add(`r4 del key ${JSON.stringify(k).slice(0, 24)}`, 'synUnicode', ['del', k])
  add(`r4 keypair key ${JSON.stringify(k).slice(0, 24)}`, 'enc', ['keypair', k])
}
for (const v of valueSpellings) {
  const tag = JSON.stringify(v).slice(0, 24)
  add(`r4 set value ${tag} plain`, 'plain', ['set', 'NEWKEY', v, '--plain'])
  add(`r4 set value ${tag} enc`, 'plain', ['set', 'NEWKEY', v])
  add(`r4 set value ${tag} over enc`, 'enc', ['set', 'HELLO', v])
}

// files and directories given to -f / positional arguments
fixtures.spaces = { 'my dir/.env': 'A=spaces\n', 'ünï/.env': 'A=unicode\n', 'emptydir/.keep': '', '.env': 'A=root\n', 'a b.env': 'A=named\n' }
fixtures.dirAsEnv = { 'sub/x': '', '.env/inner': 'A=1\n', '.env.local': 'A=local\n' }
const fileArgs = ['my dir/.env', 'my dir', 'ünï', 'ünï/.env', 'emptydir', 'a b.env', '', ' ', ',', '.env,', ',.env', ' .env ', '.', '..', './', './.env', 'sub', 'sub/', '.env.nope', 'emptydir/.env']
for (const f of fileArgs) {
  const tag = JSON.stringify(f)
  for (const fx of ['spaces', 'dirAsEnv']) {
    add(`r4 file ${tag} get ${fx}`, fx, ['get', '-f', f])
    add(`r4 file ${tag} run ${fx}`, fx, ['run', '-f', f, '--', ...show])
    add(`r4 file ${tag} decrypt ${fx}`, fx, ['decrypt', '-f', f])
    add(`r4 file ${tag} encrypt ${fx}`, fx, ['encrypt', '-f', f])
    add(`r4 file ${tag} set ${fx}`, fx, ['set', 'A', 'x', '--plain', '-f', f])
    add(`r4 file ${tag} del ${fx}`, fx, ['del', 'A', '-f', f])
    add(`r4 file ${tag} keypair ${fx}`, fx, ['keypair', '-f', f])
    add(`r4 file ${tag} validate ${fx}`, fx, ['validate', '-f', f])
  }
  add(`r4 file ${tag} ls`, 'spaces', ['ls', f])
  add(`r4 file ${tag} genexample`, 'spaces', ['genexample', f])
  add(`r4 file ${tag} gitignore`, 'spaces', ['gitignore', '--pattern', f])
}

// ---- round 5: multi-step lifecycles, cross-implementation compatibility, key naming, child process semantics, helper commands.
const S = (args, extra = {}) => ({ args, ...extra })
const lifeFixtures = ['plain', 'quotes', 'dups', 'multi', 'synComments', 'synMultiline', 'synEscapes', 'synInterp', 'synUnicode', 'synDupQuotes', 'crlf', 'bom', 'synMixed']
for (const fx of lifeFixtures) {
  addSteps(`r5 life encrypt get ${fx}`, fx, [S(['encrypt']), S(['get'])])
  addSteps(`r5 life roundtrip ${fx}`, fx, [S(['encrypt']), S(['decrypt']), S(['get'])])
  addSteps(`r5 life twice ${fx}`, fx, [S(['encrypt']), S(['encrypt']), S(['decrypt']), S(['decrypt'])])
  addSteps(`r5 life set after ${fx}`, fx, [S(['encrypt']), S(['set', 'A', 'changed']), S(['get']), S(['decrypt', '--stdout'])])
  addSteps(`r5 life set plain after ${fx}`, fx, [S(['encrypt']), S(['set', 'A', 'changed', '--plain']), S(['get', '--format', 'shell'])])
  addSteps(`r5 life del after ${fx}`, fx, [S(['encrypt']), S(['del', 'A']), S(['get'])])
  addSteps(`r5 life keys only ${fx}`, fx, [S(['encrypt', '-k', 'A']), S(['get']), S(['decrypt', '-k', 'A']), S(['get'])])
  addSteps(`r5 life exclude ${fx}`, fx, [S(['encrypt', '-ek', 'A', 'B']), S(['get']), S(['decrypt', '-ek', 'A']), S(['get'])])
  addSteps(`r5 life stdout ${fx}`, fx, [S(['encrypt', '--stdout']), S(['encrypt']), S(['decrypt', '--stdout']), S(['get', '--mask'])])
  addSteps(`r5 life keys lost ${fx}`, fx, [S(['encrypt']), { rm: ['.env.keys'] }, S(['get']), S(['decrypt']), S(['run', '--', ...show])])
  addSteps(`r5 life keys wrong ${fx}`, fx, [S(['encrypt']), { write: { '.env.keys': 'DOTENV_PRIVATE_KEY=' + WRONG + '\n' } }, S(['get']), S(['decrypt']), S(['set', 'A', 'x'])])
  addSteps(`r5 life custom keys ${fx}`, fx, [S(['encrypt', '-fk', 'k/my.keys']), S(['get', '-fk', 'k/my.keys']), S(['get']), S(['decrypt', '-fk', 'k/my.keys'])])
  addSteps(`r5 life env key ${fx}`, fx, [S(['encrypt']), { rm: ['.env.keys'] }, S(['get'], { })], { DOTENV_PRIVATE_KEY: PRIV })
  addSteps(`r5 life keypair ${fx}`, fx, [S(['encrypt']), S(['keypair']), S(['keypair', 'DOTENV_PRIVATE_KEY', '--format', 'colon']), S(['keypair', '--pp'])])
  addSteps(`r5 life validate ${fx}`, fx, [S(['genexample']), S(['encrypt']), S(['validate']), S(['validate', '--strict'])])
}

// cross-compatibility: one implementation writes, the other reads (the compared side is the second step)
for (const fx of lifeFixtures) {
  for (const writer of ['node', 'dotnet']) {
    addSteps(`r5 cross ${writer} encrypt then self get ${fx}`, fx, [S(['encrypt'], { cli: writer }), S(['get']), S(['decrypt', '--stdout']), S(['run', '--', ...show])])
    addSteps(`r5 cross ${writer} encrypt then self decrypt ${fx}`, fx, [S(['encrypt'], { cli: writer }), S(['decrypt']), S(['get'])])
    addSteps(`r5 cross ${writer} set then self get ${fx}`, fx, [S(['set', 'A', 'hello world'], { cli: writer }), S(['get', 'A']), S(['get'])])
    addSteps(`r5 cross ${writer} set then self set ${fx}`, fx, [S(['set', 'A', 'one'], { cli: writer }), S(['set', 'B', 'two']), S(['set', 'A', 'three']), S(['get'])])
    addSteps(`r5 cross ${writer} encrypt then self del ${fx}`, fx, [S(['encrypt'], { cli: writer }), S(['del', 'A']), S(['get']), S(['encrypt'])])
    addSteps(`r5 cross ${writer} encrypt then self keypair ${fx}`, fx, [S(['encrypt'], { cli: writer }), S(['keypair']), S(['keypair', '--pp'])])
  }
}

// key names for private keys: derived from the env file name
const envNames = ['.env', '.env.production', '.env.local', '.env.staging.local', '.env.my-app', 'config/.env', '.env.v1.0', '.env.CI', '.env.production.local', '.env.Prod', 'x.env', '.env.', 'sub/.env.test', '.env.a_b', '.env.ünï']
for (const name of envNames) {
  fixtures['name ' + name] = { [name]: 'HELLO=World\nFOO=bar\n' }
  const fx = 'name ' + name
  addSteps(`r5 name ${name} encrypt get`, fx, [S(['encrypt', '-f', name]), S(['get', '-f', name]), S(['keypair', '-f', name]), S(['decrypt', '-f', name])])
  addSteps(`r5 name ${name} set get`, fx, [S(['set', 'A', 'x', '-f', name]), S(['get', '-f', name])])
  addSteps(`r5 name ${name} run`, fx, [S(['encrypt', '-f', name]), S(['run', '-f', name, '--', ...show]), S(['ls'])])
  for (const writer of ['node', 'dotnet']) addSteps(`r5 name ${name} cross ${writer}`, fx, [S(['encrypt', '-f', name], { cli: writer }), S(['get', '-f', name]), S(['decrypt', '-f', name, '--stdout'])])
}
addSteps('r5 name several files', 'multi', [S(['encrypt', '-f', '.env', '-f', '.env.local', '-f', '.env.production']), S(['get', '-f', '.env', '-f', '.env.local']), S(['keypair', '-f', '.env', '-f', '.env.local']), S(['decrypt', '-f', '.env.local'])])
addSteps('r5 name convention nextjs', 'multi', [S(['encrypt', '--convention', 'nextjs']), S(['get', '--convention', 'nextjs']), S(['ls'])])
addSteps('r5 name convention flow', 'multi', [S(['encrypt', '--convention', 'flow']), S(['get', '--convention', 'flow'])])

// child process semantics of `run`
const childScripts = {
  exit0: 'process.exit(0)', exit1: 'process.exit(1)', exit3: 'process.exit(3)', exit255: 'process.exit(255)', exit256: 'process.exit(256)', exitNeg: 'process.exit(-1)',
  throws: 'throw new Error("boom")', stdoutNoNewline: 'process.stdout.write("no newline")', stderrNoNewline: 'process.stderr.write("err no newline")',
  both: 'console.log("out1");console.error("err1");console.log("out2");console.error("err2")', bigOut: 'console.log("x".repeat(200000))', manyLines: 'for(let i=0;i<2000;i++)console.log("line "+i)',
  readStdin: 'let d="";process.stdin.on("data",c=>d+=c).on("end",()=>console.log("stdin:"+JSON.stringify(d)))', args: 'console.log(JSON.stringify(process.argv.slice(1)))',
  env: 'console.log(JSON.stringify({H:process.env.HELLO,PWD:require("path").basename(process.cwd())==="x"}))', redactMe: 'console.log("the secret is World, World again, wor ld")', unicodeOut: 'console.log("héllo 日本語 😀")',
  selfKill: 'process.kill(process.pid,"SIGTERM");setTimeout(()=>{},1000)', crOnly: 'process.stdout.write("a\\rb\\r")', binaryOut: 'process.stdout.write(Buffer.from([0,1,2,255,254]))'
}
for (const [name, code] of Object.entries(childScripts)) {
  addSteps(`r5 child ${name}`, 'plain', [S(['run', '--', 'node', '-e', code], { stdin: name === 'readStdin' ? 'piped in\nline2\n' : undefined })])
  addSteps(`r5 child ${name} quiet`, 'plain', [S(['run', '-q', '--', 'node', '-e', code])])
  addSteps(`r5 child ${name} redact`, 'plain', [S(['run', '--redact', '--', 'node', '-e', code])])
  addSteps(`r5 child ${name} strict`, 'plain', [S(['run', '--strict', '--', 'node', '-e', code])])
}
for (const argv of [['a b', 'c'], ['', 'x'], ['"q"', "'s'"], ['$HOME', '${HELLO}', '$(echo hi)'], ['-n', '--flag', '-x=1'], ['ünï', '日本語'], ['a\nb'], ['--', 'x'], ['%PATH%', '!x!'], ['a&b', 'c|d', 'e>f', 'g<h', '(i)', '^j'], ['\\', '\\\\', 'a\\b'], ['tab\there'], ['*', '?', '[a]']]) {
  addSteps(`r5 child argv ${JSON.stringify(argv)}`, 'plain', [S(['run', '--', 'node', '-e', 'console.log(JSON.stringify(process.argv.slice(1)))', ...argv])])
}
for (const cmd of [['node'], ['node', '--version-not-a-flag'], ['nonexistent-command-xyz'], ['./nope'], ['cmd', '/c', 'echo', 'hi'], ['sh', '-c', 'echo hi'], ['node', '-p', '1+1'], ['echo', 'hi'], ['']]) {
  addSteps(`r5 child cmd ${JSON.stringify(cmd)}`, 'plain', [S(['run', '--', ...cmd])])
}
addSteps('r5 child no separator', 'plain', [S(['run', 'node', '-p', '1+2'])])
addSteps('r5 child separator twice', 'plain', [S(['run', '--', 'node', '-p', 'process.argv.slice(1).join(",")', '--', 'x'])])
addSteps('r5 child env inheritance', 'plain', [S(['run', '--', 'node', '-p', 'process.env.HELLO + "|" + process.env.PATH.length'])], { HELLO: 'machine' })
addSteps('r5 child overload', 'plain', [S(['run', '--overload', '--', 'node', '-p', 'process.env.HELLO'])], { HELLO: 'machine' })
addSteps('r5 child mask', 'enc', [S(['run', '--mask', '--', 'node', '-p', 'process.env.HELLO']), S(['run', '--mask', '3', '--', 'node', '-p', 'process.env.HELLO'])])
addSteps('r5 child validate', 'example', [S(['run', '--validate', '--', 'node', '-p', '1']), S(['run', '--validate', '--strict', '--', 'node', '-p', '1'])])

// helper commands: gitignore / genexample / validate / ls with existing content variants
const ignoreContents = { empty: '', nonl: 'node_modules', nl: 'node_modules\n', crlf: 'node_modules\r\n.env\r\n', patterns: '.env*\n', negated: '!.env\n.env*\n', comments: '# c\n.env.keys\n', spaces: '  .env  \n', dockerLike: '.env\n.git\n' }
for (const [name, content] of Object.entries(ignoreContents)) {
  fixtures['ignore ' + name] = { '.env': 'A=1\n', '.gitignore': content, '.dockerignore': content, 'package.json': '{}', '.vercelignore': content, '.npmignore': content }
  addSteps(`r5 gitignore ${name}`, 'ignore ' + name, [S(['gitignore']), S(['gitignore']), S(['gitignore', '--pattern', '.env*', '--pattern', '*.keys'])])
  addSteps(`r5 ext gitignore ${name}`, 'ignore ' + name, [S(['ext', 'gitignore', '--pattern', 'x']), S(['ext', 'gitignore'])])
}
addSteps('r5 gitignore none', 'plain', [S(['gitignore']), S(['gitignore', '--pattern', '.env*', '*.keys'])])
const exampleContents = { empty: '', comments: '# header\nA=\n# mid\nB=\n', optional: 'A=\nB= # optional\n', extra: 'A=\nZ_EXTRA=\n', quoted: 'A=""\nB=\'x\'\n', dup: 'A=\nA=\n', spaces: ' A = \n', unicode: 'HÉLLO=\n' }
for (const [name, content] of Object.entries(exampleContents)) {
  fixtures['example ' + name] = { '.env': 'A=1\nB=2\nC=3\nHELLO=x\n', '.env.example': content }
  addSteps(`r5 genexample ${name}`, 'example ' + name, [S(['genexample']), S(['genexample']), S(['validate']), S(['validate', '--strict'])])
  addSteps(`r5 validate ${name}`, 'example ' + name, [S(['validate']), S(['validate', '--strict']), S(['validate', '-f', '.env', '--ignore', 'MISSING_REQUIRED_KEYS'])])
}
addSteps('r5 genexample several', 'multi', [S(['genexample', '-f', '.env', '-f', '.env.local']), S(['genexample', '-f', '.env.production'])])
addSteps('r5 genexample dir', 'nested', [S(['genexample', 'sub']), S(['genexample', '.']), S(['genexample', 'nowhere'])])
for (const depthDir of ['.', 'sub', 'sub/deep', 'nowhere']) {
  fixtures.deep = { '.env': 'A=1', 'sub/.env': 'A=2', 'sub/deep/.env.local': 'A=3', 'sub/deep/node_modules/.env': 'x', 'sub/.git/.env': 'x', 'a.photoslibrary/.env': 'x', '.hidden/.env': 'h', 'sub/.env.example': 'x', 'sub/.env.keys': 'k', 'sub/.env.vault': 'v' }
  addSteps(`r5 ls ${depthDir}`, 'deep', [S(['ls', depthDir]), S(['ls', depthDir, '-f', '**/.env.local']), S(['ls', depthDir, '-ef', 'sub/**'])])
}

// ---- round 6: real git repositories (precommit / prebuild / hook install), lock flows with piped passphrases, armor settings.
const G = (...args) => ({ git: args })
const repoFixtures = {
  plainEnv: { '.env': 'A=1\nB=2\n' },
  encEnv: { '.env': encEnv, '.env.keys': keysFile },
  mixed: { '.env': encEnv, '.env.keys': keysFile, '.env.local': 'LOCAL=plain\n', '.env.example': 'A=\n', 'sub/.env': 'N=plain\n', 'sub/.env.example': 'N=\n' },
  plainKey: { '.env': 'A=1\nB_PLAIN=2\nDOTENV_PUBLIC_KEY="' + PUB + '"\n' },
  dupes: { '.env': 'A=encrypted:abc\nA=plain\n' },
  production: { '.env.production': encEnv, '.env.test': 'X=1\n', 'test/.env': 'T=1\n', 'tests/.env': 'T=1\n', 'spec/.env.local': 'S=1\n' }
}
const ignoreVariants = { none: null, env: '.env\n', wildcard: '.env*\n', keysOnly: '.env.keys\n', negated: '.env*\n!.env.example\n', nested: 'sub/.env\n', dockerEnv: '.env\n.env.local\n' }
for (const [fxName, files] of Object.entries(repoFixtures)) {
  for (const [ignName, ign] of Object.entries(ignoreVariants)) {
    const name = `repo ${fxName} ${ignName}`
    fixtures[name] = { ...files, ...(ign === null ? {} : { '.gitignore': ign, '.dockerignore': ign }) }
    const staged = [G('init', '-q'), G('add', '-A', '-f')]
    const committed = [G('init', '-q'), G('add', '-A', '-f'), G('commit', '-q', '-m', 'm')]
    addSteps(`r6 ${name} precommit staged`, name, [...staged, S(['precommit']), S(['precommit', '.']), S(['ext', 'precommit']), S(['precommit', 'sub'])])
    addSteps(`r6 ${name} precommit unstaged`, name, [G('init', '-q'), S(['precommit']), S(['ext', 'precommit', 'sub'])])
    addSteps(`r6 ${name} precommit committed`, name, [...committed, S(['precommit']), S(['precommit', 'sub'])])
    addSteps(`r6 ${name} precommit nogit`, name, [S(['precommit']), S(['precommit', 'sub'])])
    addSteps(`r6 ${name} prebuild`, name, [S(['prebuild']), S(['prebuild', 'sub']), S(['ext', 'prebuild']), ...staged, S(['prebuild'])])
  }
}
for (const fxName of ['plainEnv', 'encEnv', 'mixed']) {
  const name = `repo ${fxName} none`
  addSteps(`r6 ${name} hook install`, name, [G('init', '-q'), { write: { '.git/hooks/.keep': '' } }, S(['precommit', '--install']), S(['precommit', '--install']), S(['ext', 'precommit', '--install']), S(['precommit', '-i'])])
  addSteps(`r6 ${name} hook install existing`, name, [G('init', '-q'), { write: { '.git/hooks/pre-commit': '#!/bin/sh\necho existing\n' } }, S(['precommit', '--install']), S(['precommit', '--install'])])
  addSteps(`r6 ${name} hook install nogit`, name, [S(['precommit', '--install'])])
  addSteps(`r6 ${name} hook install dir`, name, [G('init', '-q'), { write: { '.git/hooks/.keep': '' } }, S(['precommit', 'sub', '--install'])])
}
for (const [label, content] of Object.entries({ crlfHook: '#!/bin/sh\r\necho x\r\n', noNl: '#!/bin/sh\necho x', hasPrecommit: '#!/bin/sh\ndotenvx precommit\n', hasExt: '#!/bin/sh\ndotenvx ext precommit\n' })) {
  addSteps(`r6 hook existing ${label}`, 'repo plainEnv none', [G('init', '-q'), { write: { '.git/hooks/pre-commit': content } }, S(['precommit', '--install'])])
}

// lock: passphrase piped to stdin (salts and IVs are random, payloads are normalized)
const passphrases = { simple: 'pw\n', unicode: 'pässword 🔑\n', spaces: '  with spaces  \n', long: 'p'.repeat(500) + '\n', crlf: 'pw\r\n', noNewline: 'pw', empty: '\n', two: 'pw\nextra\n', tab: 'a\tb\n', quote: '"q\'s"\n' }
for (const [name, pass] of Object.entries(passphrases)) {
  addSteps(`r6 lock ${name}`, 'enc', [S(['lock', 'up'], { stdin: pass }), S(['lock', 'down'], { stdin: pass }), S(['get'])])
  addSteps(`r6 lock ${name} wrong then right`, 'enc', [S(['lock', 'up'], { stdin: pass }), S(['lock', 'down'], { stdin: 'wrong\n' }), S(['lock', 'down'], { stdin: pass })])
  for (const writer of ['node', 'dotnet']) addSteps(`r6 lock ${name} cross ${writer}`, 'enc', [S(['lock', 'up'], { cli: writer, stdin: pass }), S(['lock', 'down'], { stdin: pass }), S(['get'])])
}
addSteps('r6 lock twice', 'enc', [S(['lock', 'up'], { stdin: 'pw\n' }), S(['lock', 'up'], { stdin: 'pw\n' }), S(['lock', 'down'], { stdin: 'pw\n' }), S(['lock', 'down'], { stdin: 'pw\n' })])
addSteps('r6 lock then use', 'enc', [S(['lock', 'up'], { stdin: 'pw\n' }), S(['get']), S(['get'], { stdin: 'pw\n' }), S(['run', '--', ...show], { stdin: 'pw\n' }), S(['decrypt', '--stdout'], { stdin: 'pw\n' }), S(['keypair'], { stdin: 'pw\n' }), S(['set', 'A', 'x'], { stdin: 'pw\n' })])
addSteps('r6 lock several keys', 'prodEnc', [S(['lock', 'up'], { stdin: 'pw\n' }), S(['lock', 'down'], { stdin: 'pw\n' })])
addSteps('r6 lock multi env files', 'multi', [S(['encrypt', '-f', '.env', '-f', '.env.production']), S(['lock', 'up'], { stdin: 'pw\n' }), S(['lock', 'down'], { stdin: 'pw\n' }), S(['get', '-f', '.env.production'])])
addSteps('r6 lock custom keys file', 'enc', [S(['lock', 'up', '-fk', '.env.keys'], { stdin: 'pw\n' }), S(['lock', 'down', '-fk', '.env.keys'], { stdin: 'pw\n' })])
addSteps('r6 lock env file option', 'multi', [S(['encrypt', '-f', '.env.production']), S(['lock', 'up', '-f', '.env.production'], { stdin: 'pw\n' }), S(['lock', 'down', '-f', '.env.production'], { stdin: 'pw\n' })])
addSteps('r6 lock no keys file', 'plain', [S(['lock', 'up'], { stdin: 'pw\n' }), S(['lock', 'down'], { stdin: 'pw\n' })])
addSteps('r6 lock env var key', 'encNoKeys', [S(['lock', 'up'], { stdin: 'pw\n' })], { DOTENV_PRIVATE_KEY: PRIV })

// armor settings persist across invocations (hermetic config directory)
const settingsSteps = [['username'], ['token'], ['token', '--unmask'], ['device'], ['device', '--unmask'], ['hostname'], ['path'], ['on'], ['off'], ['status']]
for (const sub of settingsSteps) {
  addSteps(`r6 armor settings ${sub.join(' ')} twice`, 'none', [S(['armor', ...(sub[0] === 'status' ? [] : ['settings']), ...sub]), S(['armor', ...(sub[0] === 'status' ? [] : ['settings']), ...sub])])
}
addSteps('r6 armor on off status', 'none', [S(['armor', 'settings', 'on']), S(['armor', 'status']), S(['armor', 'settings', 'off']), S(['armor', 'status']), S(['armor', 'settings', 'on']), S(['armor', 'settings', 'hostname']), S(['armor', 'settings', 'path'])])
addSteps('r6 armor settings env hostname', 'none', [S(['armor', 'settings', 'hostname']), S(['armor', 'settings', 'path'])], { DOTENVX_CONFIG: '<DIR>/cfg/dir', DOTENV_CONFIG_HOSTNAME: 'x.example' })
addSteps('r6 armor settings config dir variants', 'none', [S(['armor', 'settings', 'path']), S(['armor', 'settings', 'on']), S(['armor', 'settings', 'path'])], { DOTENVX_CONFIG: '<DIR>/new/nested/dir' })
addSteps('r6 armor no armor env', 'none', [S(['armor', 'status']), S(['armor', 'settings', 'on']), S(['armor', 'status'])], { DOTENVX_NO_ARMOR: 'true' })

// ---- round 7: root-level flags (log levels, quiet, verbose, version) placed around subcommands, across fixtures.
const rootFlags = [['-q'], ['--quiet'], ['-v'], ['--verbose'], ['-l', 'warn'], ['--log-level', 'error'], ['-l', 'verbose'], ['-l', 'bogus'], ['-l'], ['-V'], ['--version'], ['-q', '-v'], ['-v', '-q'], ['-q', '-q']]
const rootCmds = [['get'], ['get', 'HELLO'], ['get', 'NOPE'], ['set', 'NEW', 'v'], ['del', 'HELLO'], ['keypair'], ['ls'], ['encrypt'], ['decrypt'], ['validate'], ['genexample'], ['gitignore'], ['run', '--', ...show], ['ext', 'ls']]
for (const fx of ['plain', 'enc', 'none']) {
  for (const flags of rootFlags) {
    for (const cmd of rootCmds) {
      const label = `r7 ${fx} ${flags.join(' ')} | ${cmd.slice(0, 2).join(' ')}`
      add(`${label} before`, fx, [...flags, ...cmd], { CI: 'true' })
      if (cmd[0] !== 'run') add(`${label} after`, fx, [...cmd, ...flags], { CI: 'true' })
    }
  }
}
for (const flags of rootFlags) {
  add(`r7 flags only ${flags.join(' ')}`, 'plain', flags, { CI: 'true' })
  add(`r7 flags help ${flags.join(' ')}`, 'plain', [...flags, 'help'], { CI: 'true' })
  add(`r7 flags help get ${flags.join(' ')}`, 'plain', [...flags, 'help', 'get'], { CI: 'true' })
  add(`r7 flags unknown ${flags.join(' ')}`, 'plain', [...flags, 'nope'], { CI: 'true' })
}
// log level environment variables
for (const [k, v] of [['DOTENVX_LOG_LEVEL', 'error'], ['DOTENVX_LOG_LEVEL', 'verbose'], ['DOTENVX_LOG_LEVEL', 'bogus'], ['DOTENVX_QUIET', 'true'], ['DOTENVX_VERBOSE', 'true'], ['DOTENVX_DEBUG', 'true'], ['NO_COLOR', '1'], ['FORCE_COLOR', '1']]) {
  for (const cmd of [['get'], ['set', 'NEW', 'v'], ['get', 'NOPE'], ['encrypt'], ['keypair'], ['run', '--', ...show]]) {
    add(`r7 env ${k}=${v} ${cmd.slice(0, 2).join(' ')}`, 'plain', cmd, { CI: 'true', [k]: v })
  }
}

// ---- round 8: genexample / gitignore / validate / ls depth: directory arguments, multiple env files, pre-existing outputs, patterns.
const r8Fixtures = {
  empty: {},
  one: { '.env': 'A=1\nB=2\n' },
  two: { '.env': 'A=1\nB=2\n', '.env.local': 'B=3\nC=4\n' },
  withExample: { '.env': 'A=1\nB=2\n', '.env.example': 'A=\nZ=\n' },
  exampleOnly: { '.env.example': 'A=\n' },
  reserved: { '.env': 'A=1\n', '.env.keys': 'K=1\n', '.env.example': 'A=\n', '.env.previous': 'A=0\n', '.env.vault': 'V=1\n', '.env.project': 'P=1\n', '.env.me': 'M=1\n', '.env.x': 'X=1\n' },
  tree: { '.env': 'A=1\n', 'sub/.env': 'S=1\n', 'sub/.env.local': 'L=1\n', 'sub/deep/.env': 'D=1\n', 'other/.env.production': 'P=1\n' },
  commented: { '.env': '# top\nA=1 # inline\n\n# gap\nB="x y"\nexport C=3\n' },
  encrypted: { '.env': encEnv, '.env.keys': keysFile },
  dirIsFile: { 'sub': 'not a directory' },
  gi: { '.gitignore': 'node_modules\n', '.env': 'A=1\n' },
  giNoNl: { '.gitignore': 'node_modules', '.env': 'A=1\n' },
  giCrlf: { '.gitignore': 'node_modules\r\n.env*\r\n', '.env': 'A=1\n' },
  giAll: { '.gitignore': '.env*\n', '.dockerignore': '.env*\n', '.npmignore': '.env*\n', '.vercelignore': '.env*\n', '.env': 'A=1\n' },
  giMixed: { '.gitignore': '.env\n', '.dockerignore': '', '.npmignore': '*.log\n', '.env': 'A=1\n' },
  giPkg: { 'package.json': '{"name":"x"}\n', '.env': 'A=1\n' },
  giVercel: { 'vercel.json': '{}\n', '.vercel/project.json': '{}\n', '.env': 'A=1\n' }
}
for (const [n, files] of Object.entries(r8Fixtures)) fixtures['r8 ' + n] = files
const r8 = Object.keys(r8Fixtures)
const dirArgs = [[], ['.'], ['sub'], ['sub/'], ['./sub'], ['missing'], ['..'], ['sub/deep'], ['-'], ['']]
for (const fx of r8) {
  for (const d of dirArgs) {
    for (const cmd of ['genexample', 'ls', 'prebuild', 'precommit']) {
      if (d[0] === '..') continue // the parent is the live temp directory, which differs between the two runs
      const lbl = `r8 ${cmd} ${fx} [${d.join(' ')}]`
      add(lbl, 'r8 ' + fx, [cmd, ...d], { CI: 'true' })
    }
    addSteps(`r8 genexample twice ${fx} [${d.join(' ')}]`, 'r8 ' + fx, [S(['genexample', ...d]), S(['genexample', ...d]), S(['validate'])], { CI: 'true' })
  }
  for (const f of [['.env'], ['.env.local'], ['.env', '.env.local'], ['.env.nope'], ['.env', '.env.nope'], ['sub/.env'], ['.env', '.env']]) {
    add(`r8 genexample -f ${f.join(',')} ${fx}`, 'r8 ' + fx, ['genexample', '-f', ...f], { CI: 'true' })
    add(`r8 genexample -f each ${f.join(',')} ${fx}`, 'r8 ' + fx, ['genexample', ...f.flatMap(x => ['-f', x])], { CI: 'true' })
    add(`r8 validate -f ${f.join(',')} ${fx}`, 'r8 ' + fx, ['validate', ...f.flatMap(x => ['-f', x])], { CI: 'true' })
    add(`r8 ls -f ${f.join(',')} ${fx}`, 'r8 ' + fx, ['ls', '-f', ...f], { CI: 'true' })
  }
  for (const p of [['.env*'], ['.env'], ['.env', '.env.keys'], ['*.log'], ['dir/'], ['!x'], ['#c'], [' sp '], ['a b'], ['']]) {
    add(`r8 gitignore --pattern ${JSON.stringify(p)} ${fx}`, 'r8 ' + fx, ['gitignore', '--pattern', ...p], { CI: 'true' })
    addSteps(`r8 gitignore twice ${JSON.stringify(p)} ${fx}`, 'r8 ' + fx, [S(['gitignore', '--pattern', ...p]), S(['gitignore', '--pattern', ...p]), S(['gitignore'])], { CI: 'true' })
  }
}
for (const fx of ['r8 withExample', 'r8 exampleOnly', 'r8 reserved', 'r8 two']) {
  for (const ex of [['validate'], ['validate', '--strict'], ['validate', '-f', '.env.local'], ['validate', '--ignore', 'MISSING_ENV_FILE'], ['ls', '--json'], ['ls', '-ef', '.env.local'], ['ls', '--exclude-env-file', '.env*']]) {
    add(`r8 ${ex.join(' ')} ${fx}`, fx, ex, { CI: 'true' })
  }
}

// ---- round 9: external programs through fakes on PATH (gitleaks): `ext scan` against scripted exit codes and output.
const glOutputs = { none: '', clean: 'no leaks found\n', crlf: 'no leaks found\r\n', trailing: 'ok\n\n\n', unicode: '✓ ünï 日本語\n', multi: 'a\nb\nc\n', spaces: '  indented  \n', long: 'x'.repeat(5000) + '\n', noNl: 'nonl' }
const glErrs = { none: '', warn: 'warning: something\n', both: 'e1\ne2\n' }
for (const [oName, out] of Object.entries(glOutputs)) {
  for (const [eName, err] of Object.entries(glErrs)) {
    for (const exit of [0, 1, 2, 126]) {
      addSteps(`r9 scan out=${oName} err=${eName} exit=${exit}`, 'plain', [S(['ext', 'scan'])], { CI: 'true', FAKEGL_OUT: out, FAKEGL_ERR: err, FAKEGL_EXIT: String(exit) })
    }
  }
}
for (const v of [1, 2, 127]) addSteps(`r9 scan version exit=${v}`, 'plain', [S(['ext', 'scan'])], { CI: 'true', FAKEGL_VERSION_EXIT: String(v), FAKEGL_OUT: 'leak\n', FAKEGL_EXIT: '1' })
for (const flag of [['-q'], ['-v'], ['-l', 'debug'], ['-l', 'error']]) {
  addSteps(`r9 scan root ${flag.join(' ')} clean`, 'plain', [S([...flag, 'ext', 'scan'])], { CI: 'true', FAKEGL_OUT: 'no leaks\n' })
  addSteps(`r9 scan root ${flag.join(' ')} leak`, 'plain', [S([...flag, 'ext', 'scan'])], { CI: 'true', FAKEGL_OUT: 'leak found\n', FAKEGL_EXIT: '1' })
}
for (const extra of [['--bogus'], ['x'], ['--help'], ['-h']]) addSteps(`r9 scan extra ${extra.join(' ')}`, 'plain', [S(['ext', 'scan', ...extra])], { CI: 'true', FAKEGL_OUT: 'ok\n' })
addSteps('r9 scan in repo', 'repo plainEnv none', [{ git: ['init', '-q'] }, S(['ext', 'scan'])], { CI: 'true', FAKEGL_OUT: 'ok\n' })
addSteps('r9 scan empty path fake', 'plain', [S(['ext', 'scan'])], { CI: 'true', FAKEGL_OUT: 'ok\n', FAKEGL_EXIT: '3' })

// ---- round 10: external programs through the fake op / bw / powershell / secret-tool (test/fakes/secretcli.cs): secret references and native key stores.
const fcSeed = extra => JSON.stringify({
  ['op/' + fakeSanitize('op://vault/item/field')]: 'op-secret',
  ['op/' + fakeSanitize('op://vault/item/multi')]: 'line1\nline2\n',
  ['op/' + fakeSanitize('op://vault/item/space')]: '  padded  ',
  ['op/' + fakeSanitize('op://vault/item/empty')]: '',
  ['bw/' + fakeSanitize('github') + '_password']: 'bw-secret',
  ['bw/' + fakeSanitize('folder') + '_' + 'item']: 'x',
  ['bw/' + fakeSanitize('folder/item') + '_username']: 'bw-user',
  session: 'unlocked-session',
  master: 'master-pw',
  ...extra
})
const refFixtures = {
  opOne: { '.env': 'A=op://vault/item/field\nB=plain\n' },
  opAll: { '.env': 'A=op://vault/item/field\nM=op://vault/item/multi\nS=op://vault/item/space\nE=op://vault/item/empty\nMISS=op://vault/item/none\n' },
  bwOne: { '.env': 'A=bw://github/password\nB=plain\n' },
  bwAll: { '.env': 'A=bw://github/password\nU=bw://folder/item/username\nMISS=bw://nothing/password\n' },
  bothRefs: { '.env': 'A=op://vault/item/field\nB=bw://github/password\nC=${A}-ref\n' },
  quotedRef: { '.env': 'A="op://vault/item/field"\nB=\'bw://github/password\'\nC=x op://vault/item/field\n' },
  refsMulti: { '.env': 'A=op://vault/item/field\n', '.env.local': 'A=bw://github/password\nB=op://vault/item/multi\n' }
}
for (const [n, files] of Object.entries(refFixtures)) fixtures['r10 ' + n] = files
const envsBw = { session: { BW_SESSION: 'session-token' }, locked: { BW_SESSION: '' }, withMaster: { BW_SESSION: '', BW_PASSWORD: 'master-pw' } }
for (const fx of Object.keys(refFixtures)) {
  for (const [eName, e] of Object.entries(envsBw)) {
    for (const cmd of [['get'], ['get', 'A'], ['get', '--format', 'json'], ['get', '--all'], ['run', '--', ...show], ['validate'], ['get', '--no-1password'], ['get', '--no-bitwarden'], ['get', '--no-1password', '--no-bitwarden'], ['run', '--no-1password', '--', ...show], ['decrypt', '--stdout'], ['ls']]) {
      addSteps(`r10 ref ${fx} ${eName} ${cmd.join(' ')}`, 'r10 ' + fx, [S(cmd)], { FAKECLI_SEED: fcSeed({}), ...e })
    }
  }
  addSteps(`r10 ref ${fx} no programs`, 'r10 ' + fx, [S(['get'])], { FAKECLI_SEED: JSON.stringify({}), BW_SESSION: 'x' })
}
addSteps('r10 ref encrypt then get', 'r10 opOne', [S(['encrypt']), S(['get']), S(['get', 'A'])], { FAKECLI_SEED: fcSeed({}) })
addSteps('r10 ref set then get', 'r10 opOne', [S(['set', 'C', 'op://vault/item/field']), S(['get']), S(['set', 'D', 'bw://github/password']), S(['get'])], { FAKECLI_SEED: fcSeed({}), BW_SESSION: 'token' })

// native stores: the key lives in the OS store (powershell on Windows, secret-tool on Linux), keyed by the public key
const nativeKey = 'dotenvx:' + PUB
const nativeFile = 'native/' + fakeSanitize(nativeKey)
const nativeFixtures = { inStoreOnly: { '.env': encEnv }, inFileOnly: { '.env': encEnv, '.env.keys': keysFile }, both: { '.env': encEnv, '.env.keys': keysFile }, neither: { '.env': encEnv }, wrongInFile: { '.env': encEnv, '.env.keys': `DOTENV_PRIVATE_KEY=${WRONG}\n` } }
for (const [n, files] of Object.entries(nativeFixtures)) fixtures['r10 native ' + n] = files
const storeHas = { inStoreOnly: true, inFileOnly: false, both: true, neither: false, wrongInFile: true }
for (const [n, has] of Object.entries(storeHas)) {
  const seed = () => fcSeed(has ? { [nativeFile]: PRIV } : {})
  const base = { FAKECLI_SEED: seed(), CI: '' }
  for (const cmd of [['native', 'up'], ['native', 'down'], ['native', 'push'], ['native', 'pull'], ['native', 'up', '-fk', 'other.keys'], ['native', 'down', '-fk', 'other.keys'], ['native', 'push', '-f', '.env'], ['native', 'pull', '-f', '.env'], ['native'], ['get'], ['get', 'HELLO'], ['get', '--no-native'], ['decrypt', '--stdout'], ['decrypt', '--stdout', '--no-native'], ['run', '--', ...show], ['keypair'], ['keypair', '--no-native'], ['ls']]) {
    addSteps(`r10 native ${n} ${cmd.join(' ')}`, 'r10 native ' + n, [S(cmd)], base)
  }
  addSteps(`r10 native ${n} up down up`, 'r10 native ' + n, [S(['native', 'up']), S(['native', 'down']), S(['native', 'up']), S(['get'])], base)
  addSteps(`r10 native ${n} down up down`, 'r10 native ' + n, [S(['native', 'down']), S(['native', 'up']), S(['native', 'down']), S(['get'])], base)
  addSteps(`r10 native ${n} push pull`, 'r10 native ' + n, [S(['native', 'push']), S(['native', 'pull']), S(['native', 'push']), S(['get'])], base)
}
addSteps('r10 native set stores key', 'none', [S(['set', 'A', '1']), S(['get']), S(['native', 'down']), S(['get'])], { FAKECLI_SEED: JSON.stringify({}), CI: '' })
addSteps('r10 native encrypt stores key', 'plain', [S(['encrypt']), S(['get']), S(['native', 'down']), S(['get'])], { FAKECLI_SEED: JSON.stringify({}), CI: '' })
addSteps('r10 native CI disables', 'r10 native inStoreOnly', [S(['get']), S(['native', 'up'])], { FAKECLI_SEED: fcSeed({ [nativeFile]: PRIV }), CI: 'true' })

// ---- round 12: seeded random operation sequences (the same sequences on every run), each step run by the CLI under test.
const r12Pool = [
  ['get'], ['get', 'A'], ['get', 'B'], ['get', 'NEW'], ['get', '--all'], ['get', '--format', 'json'], ['get', '--format', 'shell'], ['get', '--mask'],
  ['set', 'A', 'one'], ['set', 'B', 'two words'], ['set', 'NEW', '$HOME'], ['set', 'EMPTY', ''], ['set', 'Q', '"quoted"'], ['set', 'A', 'again', '--plain'],
  ['set', 'P', 'plain value', '--plain'], ['set', 'ML', 'line1\nline2'], ['set', 'U', 'ünï 日本語'],
  ['del', 'A'], ['del', 'NEW'], ['del', 'MISSING'],
  ['encrypt'], ['encrypt', '-k', 'A'], ['encrypt', '-ek', 'B'], ['encrypt', '--stdout'], ['encrypt', '-f', '.env.local'],
  ['decrypt'], ['decrypt', '-k', 'A'], ['decrypt', '-ek', 'B'], ['decrypt', '--stdout'], ['decrypt', '-f', '.env.local'],
  ['keypair'], ['keypair', 'A'], ['keypair', '-f', '.env.local'],
  ['ls'], ['validate'], ['genexample'], ['validate'],
  ['run', '--', ...show], ['run', '--overload', '--', ...show], ['run', '-f', '.env.local', '--', ...show],
  ['set', 'A', 'local', '-f', '.env.local'], ['set', 'C', 'c', '-f', '.env.local', '--plain'], ['del', 'C', '-f', '.env.local']
]
const r12Starts = ['none', 'plain', 'enc', 'multi', 'dups', 'quotes', 'example', 'publicOnly', 'encNoKeys']
let r12Seed = 20261008
const r12Rand = () => { r12Seed = (r12Seed * 1103515245 + 12345) & 0x7fffffff; return r12Seed / 0x7fffffff }
for (let n = 0; n < 450; n++) {
  const fx = r12Starts[Math.floor(r12Rand() * r12Starts.length)]
  const length = 3 + Math.floor(r12Rand() * 5)
  const steps = Array.from({ length }, () => S(r12Pool[Math.floor(r12Rand() * r12Pool.length)]))
  addSteps(`r12 seq ${String(n).padStart(3, '0')} ${fx}`, fx, steps, { CI: 'true' })
}

// ---- round 13: dynamic extension dispatch (`dotenvx <name>` runs `dotenvx-<name>` from PATH) through test/fakes/extension.cs.
const r13Names = 'foo,bar-baz,foo.bar,armor,ext-foo,ext-vault,Upper'
const r13Env = extra => ({ CI: 'true', FAKEEXT_NAMES: r13Names, ...extra })
const r13Args = [[], ['a'], ['a', 'b'], ['--x'], ['-x'], ['--x=1'], ['-f', '.env'], ['--'], ['--', 'x'], ['x', '--', 'y'], [''], ['a b', 'c'], ['"q"', "'s'"], ['$HOME', '%PATH%'], ['ünï', '日本語'], ['-h'], ['--help'], ['-V'], ['--version'], ['help'], ['-q'], ['-l', 'debug'], ['get'], ['--file', '.env'], ['-e']]
for (const name of ['foo', 'bar-baz', 'foo.bar', 'Upper', 'armor', 'missing', 'FOO', 'foo_bar']) {
  for (const a of r13Args) {
    addSteps(`r13 dispatch ${name} [${a.join(' ')}]`, 'plain', [S([name, ...a])], r13Env({}))
  }
}
for (const exit of [0, 1, 2, 3, 127, 255]) {
  addSteps(`r13 exit ${exit}`, 'plain', [S(['foo', 'x'])], r13Env({ FAKEEXT_EXIT: String(exit) }))
  addSteps(`r13 exit ${exit} then get`, 'plain', [S(['foo']), S(['get'])], r13Env({ FAKEEXT_EXIT: String(exit) }))
}
addSteps('r13 stdout and stderr', 'plain', [S(['foo'])], r13Env({ FAKEEXT_OUT: 'out text\n', FAKEEXT_ERR: 'err text\n', FAKEEXT_EXIT: '4' }))
addSteps('r13 stdout no newline', 'plain', [S(['foo'])], r13Env({ FAKEEXT_OUT: 'no newline' }))
addSteps('r13 unicode output', 'plain', [S(['foo'])], r13Env({ FAKEEXT_OUT: 'ünï 日本語 🔑\n' }))
for (const stdin of ['', 'line\n', 'two\nlines\n', 'ünï\n', 'no newline', 'x'.repeat(100000)]) {
  addSteps(`r13 stdin ${stdin.length}`, 'plain', [S(['foo'], { stdin })], r13Env({ FAKEEXT_STDIN: '1' }))
}
addSteps('r13 env passthrough', 'plain', [S(['foo'])], r13Env({ FAKEEXT_ENVVARS: 'HELLO,DOTENV_PRIVATE_KEY,NODE_ENV,NOPE', HELLO: 'machine', NODE_ENV: 'test', DOTENV_PRIVATE_KEY: 'k' }))
addSteps('r13 env not loaded from .env', 'plain', [S(['foo'])], r13Env({ FAKEEXT_ENVVARS: 'HELLO,FOO' }))
addSteps('r13 env after run', 'plain', [S(['run', '--', 'node', '-e', '0']), S(['foo'])], r13Env({ FAKEEXT_ENVVARS: 'HELLO,FOO' }))
for (const flags of [['-q'], ['-v'], ['-l', 'debug'], ['-l', 'error'], ['--quiet'], ['-q', '-v']]) {
  addSteps(`r13 root ${flags.join(' ')} then dispatch`, 'plain', [S([...flags, 'foo', 'x'])], r13Env({}))
  addSteps(`r13 root ${flags.join(' ')} missing`, 'plain', [S([...flags, 'missing', 'x'])], r13Env({}))
}
addSteps('r13 no programs on PATH', 'plain', [S(['foo']), S(['armor']), S(['ext', 'foo']), S(['ext', 'vault'])], { CI: 'true' })
for (const sub of [[], ['x'], ['--x'], ['-h'], ['--help'], ['a', 'b']]) {
  addSteps(`r13 ext foo [${sub.join(' ')}]`, 'plain', [S(['ext', 'foo', ...sub])], r13Env({}))
  addSteps(`r13 ext vault [${sub.join(' ')}]`, 'plain', [S(['ext', 'vault', ...sub])], r13Env({}))
  addSteps(`r13 ext missing [${sub.join(' ')}]`, 'plain', [S(['ext', 'nothing', ...sub])], r13Env({}))
  addSteps(`r13 ext builtin [${sub.join(' ')}]`, 'plain', [S(['ext', 'ls', ...sub])], r13Env({}))
}
addSteps('r13 ext vault exit codes', 'plain', [S(['ext', 'vault', 'x'])], r13Env({ FAKEEXT_EXIT: '5' }))
addSteps('r13 ext foo exit codes', 'plain', [S(['ext', 'foo', 'x'])], r13Env({ FAKEEXT_EXIT: '5' }))
addSteps('r13 builtin names win', 'plain', [S(['get']), S(['ls']), S(['help'])], r13Env({}))

// ---- round 14: filesystem edge cases: odd file names, directories where files are expected, unusual content and sizes.
const r14Cmds = [['get'], ['get', 'A'], ['set', 'NEW', 'v'], ['del', 'A'], ['encrypt'], ['decrypt'], ['keypair'], ['ls'], ['validate'], ['genexample'], ['run', '--', ...show]]
const r14Content = {
  empty: '',
  whitespace: '  \n\t\n   \n',
  onlyComments: '# a\n# b\n',
  nul: 'A=1\u0000B=2\nC=3\n',
  binary: Buffer.from([0, 1, 2, 255, 254, 10, 65, 61, 49, 10]).toString('latin1'),
  invalidUtf8: Buffer.from([65, 61, 0xc3, 0x28, 10, 66, 61, 50, 10]).toString('latin1'),
  lone: 'A',
  eqOnly: '=\n==\n=x\n',
  hugeLine: 'A=' + 'x'.repeat(16 * 1024) + '\nB=2\n',
  manyKeys: Array.from({ length: 2000 }, (_, i) => `K${i}=v${i}`).join('\n') + '\n',
  deepValue: 'A=' + '${A}'.repeat(50) + '\nB=${A}\n',
  trailingSpaces: 'A=1   \nB=2\t\n',
  noTrailingNewline: 'A=1\nB=2',
  mixedNewlines: 'A=1\r\nB=2\rC=3\nD=4\n'
}
for (const [label, content] of Object.entries(r14Content)) {
  fixtures['r14 content ' + label] = { '.env': ['binary', 'invalidUtf8'].includes(label) ? Buffer.from(content, 'latin1') : content }
  for (const cmd of r14Cmds) add(`r14 content ${label} ${cmd.slice(0, 2).join(' ')}`, 'r14 content ' + label, cmd, { CI: 'true' })
}
const r14Names = { space: 'my env', unicode: 'ünï日本語', dots: '..env', dash: '-dash', long: 'e'.repeat(150), percent: '%PATH%', dollar: '$HOME', quote: "it's", leadingDot: '.hidden', trailingSpace: 'trail ', semicolon: 'a;b', brackets: '[a]', star: 'a*b' }
for (const [label, name] of Object.entries(r14Names)) {
  if (process.platform === 'win32' && /[*"<>|?:]|\s$/.test(name)) continue
  fixtures['r14 name ' + label] = { [name]: 'A=1\nB=2\n' }
  for (const cmd of [['get'], ['set', 'NEW', 'v'], ['encrypt'], ['decrypt'], ['keypair'], ['validate'], ['genexample']]) {
    add(`r14 name ${label} ${cmd[0]}`, 'r14 name ' + label, [...cmd, '-f', name], { CI: 'true' })
    add(`r14 name ${label} ${cmd[0]} dir`, 'r14 name ' + label, [...cmd, '-f', name, '-fk', name + '.keys'], { CI: 'true' })
  }
  fixtures['r14 dirname ' + label] = { [name + '/.env']: 'A=1\n' }
  add(`r14 dirname ${label} ls`, 'r14 dirname ' + label, ['ls', name], { CI: 'true' })
  add(`r14 dirname ${label} genexample`, 'r14 dirname ' + label, ['genexample', name], { CI: 'true' })
  add(`r14 dirname ${label} precommit`, 'r14 dirname ' + label, ['precommit', name], { CI: 'true' })
}
// a directory where a file is expected
fixtures['r14 envIsDir'] = { '.env/inner': 'x\n' }
fixtures['r14 keysIsDir'] = { '.env': 'A=1\n', '.env.keys/inner': 'x\n' }
fixtures['r14 exampleIsDir'] = { '.env': 'A=1\n', '.env.example/inner': 'x\n' }
fixtures['r14 gitignoreIsDir'] = { '.env': 'A=1\n', '.gitignore/inner': 'x\n' }
for (const fx of ['r14 envIsDir', 'r14 keysIsDir', 'r14 exampleIsDir', 'r14 gitignoreIsDir']) {
  for (const cmd of r14Cmds.concat([['gitignore'], ['prebuild'], ['precommit'], ['validate', '--strict']])) add(`${fx} ${cmd.slice(0, 2).join(' ')}`, fx, cmd, { CI: 'true' })
}
// the keys file in an unexpected state
const r14Keys = { empty: '', garbage: 'not a key file\n', wrongName: 'FOO=bar\n', duplicate: `DOTENV_PRIVATE_KEY=${PRIV}\nDOTENV_PRIVATE_KEY=${WRONG}\n`, quoted: `DOTENV_PRIVATE_KEY="${PRIV}"\n`, crlf: `DOTENV_PRIVATE_KEY=${PRIV}\r\n`, spaced: `DOTENV_PRIVATE_KEY = ${PRIV}\n`, bom: `﻿DOTENV_PRIVATE_KEY=${PRIV}\n`, shortKey: 'DOTENV_PRIVATE_KEY=abc\n', upper: `DOTENV_PRIVATE_KEY=${PRIV.toUpperCase()}\n`, comment: `# k\nDOTENV_PRIVATE_KEY=${PRIV}\n# end\n` }
for (const [label, keys] of Object.entries(r14Keys)) {
  fixtures['r14 keys ' + label] = { '.env': encEnv, '.env.keys': keys }
  for (const cmd of [['get'], ['decrypt', '--stdout'], ['keypair'], ['run', '--', ...show], ['set', 'NEW', 'v'], ['encrypt'], ['validate']]) add(`r14 keys ${label} ${cmd[0]}`, 'r14 keys ' + label, cmd, { CI: 'true' })
}

// ---- round 15: path forms for file and directory arguments (relative, absolute, parent, backslash, doubled separators, links).
fixtures['r15 tree'] = { '.env': 'ROOT=1\n', 'sub/.env': 'SUB=1\nHELLO=sub\n', 'sub/.env.keys': '', 'sub/deep/.env': 'DEEP=1\n', 'other/.env.production': 'P=1\n', '.env.example': 'ROOT=\n', 'sub/.env.example': 'SUB=\n' }
fixtures['r15 enc'] = { '.env': encEnv, '.env.keys': keysFile, 'sub/.env': encEnv, 'sub/.env.keys': keysFile }
const r15Spellings = file => {
  const slash = file.split('/')
  const out = [file, './' + file, file.replace('/', '//'), file.replace('/', '/./'), 'sub/../' + file, '<DIR>/' + file, file.split('/').join(String.fromCharCode(92)), '<DIR>' + String.fromCharCode(92) + file.split('/').join(String.fromCharCode(92)), file + '/', '../' + '<DIRNAME>' + '/' + file]
  return [...new Set(out)]
}
const dirnameToken = '<DIRNAME>'
const r15Files = ['sub/.env', 'sub/deep/.env', '.env']
for (const fx of ['r15 tree', 'r15 enc']) {
  for (const file of r15Files) {
    for (const spelled of r15Spellings(file)) {
      for (const cmd of [['get'], ['get', 'HELLO'], ['decrypt', '--stdout'], ['keypair'], ['validate'], ['run', '--', ...show]]) {
        add(`r15 ${fx} ${cmd[0]} -f ${spelled}`, fx, [...cmd.slice(0, cmd[0] === 'run' ? 1 : cmd.length), '-f', spelled, ...(cmd[0] === 'run' ? cmd.slice(1) : [])], { CI: 'true' })
      }
      add(`r15 ${fx} set -f ${spelled}`, fx, ['set', 'NEWKEY', 'v', '-f', spelled], { CI: 'true' })
      add(`r15 ${fx} encrypt -f ${spelled}`, fx, ['encrypt', '-f', spelled], { CI: 'true' })
      add(`r15 ${fx} del -f ${spelled}`, fx, ['del', 'SUB', '-f', spelled], { CI: 'true' })
    }
  }
  for (const keys of ['sub/.env.keys', './sub/.env.keys', '<DIR>/sub/.env.keys', 'sub\\.env.keys', 'nope/.env.keys', '.env.keys']) {
    add(`r15 ${fx} get -fk ${keys}`, fx, ['get', '-f', 'sub/.env', '-fk', keys], { CI: 'true' })
    add(`r15 ${fx} encrypt -fk ${keys}`, fx, ['encrypt', '-f', 'sub/.env', '-fk', keys], { CI: 'true' })
  }
  for (const d of ['.', './', 'sub', 'sub/', './sub', 'sub//', 'sub/.', 'sub/deep/..', '<DIR>', '<DIR>/sub', '<DIR>/sub/', 'sub\\deep', 'sub/deep', 'other', 'nope', 'sub/.env', 'C:\\nope']) {
    for (const cmd of ['ls', 'genexample', 'prebuild', 'precommit']) add(`r15 ${fx} ${cmd} dir ${d}`, fx, [cmd, d], { CI: 'true' })
    addSteps(`r15 ${fx} genexample twice ${d}`, fx, [S(['genexample', d]), S(['genexample', d]), S(['validate'])], { CI: 'true' })
  }
}
// links
fixtures['r15 links'] = { 'real/.env': 'HELLO=real\nA=1\n', 'real/.env.keys': '' }
const r15Link = (name, steps) => addSteps('r15 link ' + name, 'r15 links', steps, { CI: 'true' })
r15Link('file link get', [{ link: ['real/.env', '.env'] }, S(['get'])])
r15Link('file link set', [{ link: ['real/.env', '.env'] }, S(['set', 'B', '2']), S(['get'])])
r15Link('file link encrypt decrypt', [{ link: ['real/.env', '.env'] }, S(['encrypt']), S(['get']), S(['decrypt']), S(['get'])])
r15Link('file link del', [{ link: ['real/.env', '.env'] }, S(['del', 'A']), S(['get'])])
r15Link('keys link', [{ write: { '.env': 'HELLO=x\n' } }, { link: ['real/.env.keys', '.env.keys'] }, S(['encrypt']), S(['get'])])
r15Link('dir link ls', [{ link: ['real', 'linked'] }, S(['ls']), S(['ls', 'linked'])])
r15Link('dir link get', [{ link: ['real', 'linked'] }, S(['get', '-f', 'linked/.env'])])
r15Link('dir link genexample', [{ link: ['real', 'linked'] }, S(['genexample', 'linked']), S(['validate', '-f', 'linked/.env'])])
r15Link('dangling link', [{ link: ['missing/.env', '.env'] }, S(['get']), S(['set', 'A', '1']), S(['encrypt']), S(['ls'])])
r15Link('link loop', [{ link: ['.env', '.env'] }, S(['get'])])
r15Link('link to dir as file', [{ link: ['real', '.env'] }, S(['get']), S(['ls'])])
r15Link('link example', [{ link: ['real/.env', '.env.example'] }, S(['genexample']), S(['validate'])])
r15Link('link gitignore', [{ link: ['real/.env', '.gitignore'] }, S(['gitignore'])])

// ---- round 16: standard streams: piped stdin into children, large and binary output, interleaving, exit codes, a reader that goes away.
const r16Child = code => ['run', '--', 'node', '-e', code]
const r16Stdins = { empty: '', short: 'hello', newline: 'a\nb\n', crlf: 'a\r\nb\r\n', unicode: 'ünï 日本語 🔑\n', nul: 'a\u0000b\n', big: 'x'.repeat(1024 * 1024), manyLines: Array.from({ length: 20000 }, (_, i) => 'line' + i).join('\n') + '\n', noNewline: 'no newline at all' }
const r16Readers = {
  all: 'let d="";process.stdin.on("data",c=>d+=c).on("end",()=>console.log("len:"+d.length+" "+JSON.stringify(d.slice(0,20))))',
  firstLine: 'process.stdin.once("data",c=>{console.log("first:"+JSON.stringify(String(c).slice(0,10)));process.exit(0)})',
  ignore: 'console.log("not reading")',
  closeEarly: 'process.stdin.destroy();console.log("closed")'
}
for (const [sName, stdin] of Object.entries(r16Stdins)) {
  for (const [rName, code] of Object.entries(r16Readers)) {
    addSteps(`r16 stdin ${sName} reader ${rName}`, 'plain', [S(r16Child(code), { stdin })], { CI: 'true' })
  }
}
// output volume and shape
const r16Writers = {
  bigStdout: 'process.stdout.write("x".repeat(5*1024*1024))',
  bigStderr: 'process.stderr.write("y".repeat(2*1024*1024))',
  manyLines: 'for(let i=0;i<50000;i++)console.log("line"+i)',
  noNewline: 'process.stdout.write("no newline")',
  binary: 'process.stdout.write(Buffer.from([0,1,2,255,254,10]))',
  unicode: 'console.log("ünï 日本語 🔑")',
  interleaved: 'for(let i=0;i<200;i++){console.log("out"+i);console.error("err"+i)}',
  longLine: 'console.log("z".repeat(1000000))',
  empty: ''
}
for (const [name, code] of Object.entries(r16Writers)) {
  addSteps(`r16 output ${name}`, 'plain', [S(r16Child(code))], { CI: 'true' })
  addSteps(`r16 output ${name} quiet`, 'plain', [S(['-q', ...r16Child(code)])], { CI: 'true' })
}
// exit codes and signals
for (const code of [0, 1, 2, 126, 127, 128, 129, 255, 256, 257, 511, 1000, -1, -2]) {
  addSteps(`r16 exit ${code}`, 'plain', [S(r16Child(`process.exit(${code})`))], { CI: 'true' })
  addSteps(`r16 exitCode ${code}`, 'plain', [S(r16Child(`process.exitCode=${code}`))], { CI: 'true' })
}
for (const sig of ['SIGTERM', 'SIGKILL', 'SIGINT', 'SIGHUP', 'SIGUSR1']) {
  addSteps(`r16 self signal ${sig}`, 'plain', [S(r16Child(`process.kill(process.pid,"${sig}");setTimeout(()=>{},2000)`))], { CI: 'true' })
}
addSteps('r16 child throws', 'plain', [S(r16Child('throw new Error("boom")'))], { CI: 'true' })
addSteps('r16 child unhandled rejection', 'plain', [S(r16Child('Promise.reject(new Error("rej"))'))], { CI: 'true' })
addSteps('r16 child syntax error', 'plain', [S(r16Child('}{'))], { CI: 'true' })
addSteps('r16 child runs long then exits', 'plain', [S(r16Child('setTimeout(()=>{console.log("late");process.exit(7)},1500)'))], { CI: 'true' })
addSteps('r16 child spawns grandchild', 'plain', [S(r16Child('const {spawnSync}=require("child_process");const r=spawnSync(process.execPath,["-e","console.log(process.env.HELLO);process.exit(9)"]);console.log("gc",r.status,String(r.stdout).trim());process.exit(r.status)'))], { CI: 'true' })
// a reader that goes away
for (const cmd of [['get'], ['get', '--format', 'json'], ['ls'], ['keypair'], ['decrypt', '--stdout'], ['encrypt', '--stdout']]) {
  addSteps(`r16 broken stdout ${cmd.join(' ')}`, 'enc', [S(cmd, { breakStdout: true })], { CI: 'true' })
}
fixtures['r16 manyKeys'] = { '.env': Array.from({ length: 600 }, (_, i) => `K${i}=value${i}`).join('\n') + '\n' }
for (const cmd of [['get'], ['get', '--format', 'shell'], ['get', '--format', 'eval'], ['ls'], ['keypair'], ['encrypt', '--stdout'], ['run', '--', 'node', '-e', 'console.log(Object.keys(process.env).filter(k=>k.startsWith("K")).length)']]) {
  addSteps(`r16 many keys ${cmd.slice(0, 3).join(' ')}`, 'r16 manyKeys', [S(cmd)], { CI: 'true' })
  addSteps(`r16 many keys broken ${cmd.slice(0, 3).join(' ')}`, 'r16 manyKeys', [S(cmd, { breakStdout: true })], { CI: 'true' })
}

// ---- round 17: conventions (nextjs, flow) and environment selection crossed with commands, files present and NODE_ENV / DOTENV_ENV.
const r17Files = {
  '.env': 'A=base\nB=base\nONLY_BASE=1\n',
  '.env.local': 'A=local\nC=local\n',
  '.env.development': 'A=development\nD=development\n',
  '.env.development.local': 'A=development.local\nE=development.local\n',
  '.env.test': 'A=test\nF=test\n',
  '.env.test.local': 'A=test.local\n',
  '.env.production': 'A=production\nG=production\n',
  '.env.production.local': 'A=production.local\n',
  '.env.staging': 'A=staging\nH=staging\n',
  '.env.staging.local': 'A=staging.local\n',
  '.env.defaults': 'A=defaults\nB=defaults\nZ=defaults\n'
}
fixtures['r17 all'] = r17Files
fixtures['r17 partial'] = { '.env': r17Files['.env'], '.env.production': r17Files['.env.production'] }
fixtures['r17 none'] = {}
fixtures['r17 encrypted'] = { '.env': encEnv, '.env.keys': keysFile, '.env.local': 'A=local\n', '.env.development': 'A=development\n' }
const r17Envs = [{}, { NODE_ENV: 'development' }, { NODE_ENV: 'test' }, { NODE_ENV: 'production' }, { NODE_ENV: 'staging' }, { NODE_ENV: '' }, { NODE_ENV: 'Production' }, { DOTENV_ENV: 'production' }, { DOTENV_ENV: 'staging', NODE_ENV: 'production' }, { DOTENV_ENV: '', NODE_ENV: 'test' }, { DOTENV_ENV: 'weird env' }]
const r17Conventions = ['nextjs', 'flow', 'NEXTJS', 'bogus', '', 'next']
const r17Cmds = [['get'], ['get', 'A'], ['get', '--all'], ['get', '--format', 'shell'], ['run', '--', ...show], ['run', '--overload', '--', ...show], ['validate'], ['get', '-e', 'A=inline'], ['get', '--strict']]
for (const fx of ['r17 all', 'r17 partial', 'r17 none', 'r17 encrypted']) {
  for (const conv of r17Conventions) {
    for (const [i, extra] of r17Envs.entries()) {
      for (const cmd of r17Cmds) {
        const withConv = cmd[0] === 'run' ? [cmd[0], ...(cmd[1] === '--overload' ? ['--overload'] : []), '--convention', conv, '--', ...show] : [...cmd, '--convention', conv]
        add(`r17 ${fx} ${conv} env${i} ${cmd.slice(0, 3).join(' ')}`, fx, withConv, { CI: 'true', ...extra })
      }
    }
  }
}
// -f / -e / convention interplay
for (const [label, extraArgs] of Object.entries({
  fileThenConv: ['-f', '.env.staging', '--convention', 'nextjs'],
  convThenFile: ['--convention', 'nextjs', '-f', '.env.staging'],
  twoFiles: ['-f', '.env.staging', '-f', '.env.local', '--convention', 'flow'],
  overload: ['--convention', 'flow', '--overload'],
  overloadFile: ['--convention', 'nextjs', '--overload', '-f', '.env.staging'],
  envs: ['--convention', 'flow', '-e', 'A=cli', '-e', 'B=cli2'],
  envFile: ['--convention', 'flow', '-e', 'C=x', '-f', '.env.staging'],
  keysFile: ['--convention', 'nextjs', '-fk', '.env.keys'],
  ignore: ['--convention', 'nextjs', '--ignore', 'MISSING_ENV_FILE'],
  strictConv: ['--convention', 'flow', '--strict']
})) {
  for (const extra of [{}, { NODE_ENV: 'production' }, { NODE_ENV: 'staging' }]) {
    for (const fx of ['r17 all', 'r17 partial']) {
      add(`r17 combo ${label} ${fx} ${JSON.stringify(extra)}`, fx, ['get', ...extraArgs], { CI: 'true', ...extra })
      add(`r17 combo ${label} run ${fx} ${JSON.stringify(extra)}`, fx, ['run', ...extraArgs, '--', ...show], { CI: 'true', ...extra })
    }
  }
}
// the library-style variable names for the same options
for (const [k, v] of [['DOTENV_CONFIG_CONVENTION', 'nextjs'], ['DOTENV_CONFIG_CONVENTION', 'flow'], ['DOTENV_CONVENTION', 'nextjs'], ['DOTENV_CONFIG_OVERRIDE', 'true'], ['DOTENV_CONFIG_PATH', '.env.staging'], ['DOTENV_CONFIG_PATH', '.env.staging,.env.local'], ['DOTENV_CONFIG_DEBUG', 'true'], ['DOTENV_CONFIG_QUIET', 'true']]) {
  for (const cmd of [['get'], ['run', '--', ...show]]) add(`r17 var ${k}=${v} ${cmd[0]}`, 'r17 all', cmd, { CI: 'true', [k]: v })
}

// ---- round 18: every environment variable the CLI reads, with truthy, falsy and odd values, crossed with the commands that consult them.
const r18Vars = [
  'DOTENVX_NO_ARMOR', 'DOTENVX_NO_1PASSWORD', 'DOTENVX_NO_BITWARDEN', 'DOTENVX_NO_NATIVE', 'DOTENVX_ARMOR_ON', 'DOTENVX_ARMOR_USER', 'DOTENVX_ARMOR_USERNAME',
  'DOTENVX_ARMOR_TOKEN', 'DOTENVX_BITWARDEN_PASSWORD', 'DOTENVX_TEAM_REQUIRED', 'DOTENVX_VERSION', 'DOTENVX_VERSION_LAST_CHECK',
  'DOTENV_QUIET', 'DOTENV_IGNORE', 'DOTENV_ENV', 'DOTENV_CONVENTION', 'DOTENV_CONFIG_QUIET', 'DOTENV_CONFIG_IGNORE', 'DOTENV_CONFIG_CONVENTION',
  'DOTENV_PRIVATE_KEYS', 'DOTENV_PUBLIC_KEY', 'NO_COLOR', 'FORCE_COLOR', 'TERM', 'COLUMNS', 'LANG', 'LC_ALL', 'TZ', 'CI', 'BW_SESSION', 'NODE_ENV'
]
const r18Values = ['true', '1', '0', 'false', '', 'MISSING_ENV_FILE', 'x', 'ünï']
const r18Cmds = [['get'], ['get', 'NOPE'], ['run', '--', ...show], ['set', 'NEW', 'v'], ['encrypt'], ['decrypt', '--stdout'], ['validate'], ['native']]
fixtures['r18 enc'] = { '.env': encEnv, '.env.keys': keysFile, '.env.example': 'HELLO=\nFOO=\nPLAIN_PLAIN=\nEMPTY=\n' }
for (const name of r18Vars) {
  for (const value of r18Values) {
    for (const cmd of r18Cmds) {
      add(`r18 ${name}=${JSON.stringify(value)} ${cmd.slice(0, 2).join(' ')}`, 'r18 enc', cmd, { ...(name === 'CI' ? {} : { CI: 'true' }), DOTENVX_ARMOR_HOSTNAME: 'http://127.0.0.1:9', [name]: value })
    }
  }
}

// ---- round 19: Unicode in key names, values and file names: composed and decomposed forms, special casing, surrogate pairs.
const r19Names = {
  ascii: 'production', sharp: 'straße', dotless: 'ısı', dotted: 'İstanbul', composed: 'café', decomposed: 'café', greek: 'σίσυφος', final: 'ΟΔΟΣ',
  ligature: 'ﬁne', dz: 'ǆ', emoji: 'rocket🚀', cjk: '本番', arabic: 'إنتاج', zwj: 'a‍b', combining: 'ạ̈', turkishI: 'ISTANBUL', upper: 'MiXeD', digits: '٣٤', mathBold: '𝐀𝐁', apostrophe: "o'neil"
}
for (const [label, name] of Object.entries(r19Names)) {
  const file = '.env.' + name
  fixtures['r19 file ' + label] = { [file]: 'A=1\nB=two words\n' }
  for (const cmd of [['encrypt'], ['set', 'NEW', 'v'], ['keypair'], ['get'], ['decrypt', '--stdout'], ['validate'], ['genexample'], ['ls']]) {
    const args = cmd[0] === 'ls' || cmd[0] === 'genexample' ? cmd : [...cmd, '-f', file]
    add(`r19 file ${label} ${cmd[0]}`, 'r19 file ' + label, args, { CI: 'true' })
  }
  addSteps(`r19 file ${label} lifecycle`, 'r19 file ' + label, [S(['encrypt', '-f', file]), S(['get', '-f', file]), S(['keypair', '-f', file]), S(['set', 'C', 'ü', '-f', file]), S(['decrypt', '-f', file]), S(['get', '-f', file])], { CI: 'true' })
  addSteps(`r19 file ${label} env private key`, 'r19 file ' + label, [S(['encrypt', '-f', file]), S(['keypair', '-f', file, '--format', 'shell'])], { CI: 'true' })
}
const r19Keys = { composed: 'café', decomposed: 'café', sharp: 'straße', turkish: 'ıİ', emoji: 'k🚀', cjk: '键', greek: 'Σ', zwj: 'a‍b', surrogate: '𝐀', bidi: 'a‮b' }
const r19Values = { composed: 'café', decomposed: 'café', emoji: '🔑🚀', surrogatePair: '𝐀', rtl: 'שלום', zeroWidth: 'a​b', bom: '﻿x', nbsp: 'a b', lineSep: 'a b', paraSep: 'a b', nel: 'a\u0085b', bell: 'a\u0007b', esc: 'a\u001b[31mb', longUnicode: 'é'.repeat(5000) }
for (const [kl, key] of Object.entries(r19Keys)) {
  for (const [vl, value] of Object.entries(r19Values)) {
    const fx = 'r19 kv ' + kl + ' ' + vl
    fixtures[fx] = { '.env': `${key}=${value.replace(/\n/g, ' ')}\nPLAIN=1\n` }
    if (kl === 'composed' || vl === 'composed' || vl === 'emoji') {
      add(`r19 kv get ${kl} ${vl}`, fx, ['get'], { CI: 'true' })
      add(`r19 kv get key ${kl} ${vl}`, fx, ['get', key], { CI: 'true' })
    }
    addSteps(`r19 kv lifecycle ${kl} ${vl}`, fx, [S(['encrypt']), S(['get']), S(['decrypt']), S(['get', '--format', 'shell']), S(['set', key, value]), S(['get', key]), S(['del', key]), S(['get'])], { CI: 'true' })
  }
}
// time zone and locale must not change anything the CLI prints
for (const extra of [{ TZ: 'UTC' }, { TZ: 'Asia/Tokyo' }, { TZ: 'America/Los_Angeles' }, { LANG: 'tr_TR.UTF-8', LC_ALL: 'tr_TR.UTF-8' }, { LANG: 'de_DE.UTF-8', LC_ALL: 'de_DE.UTF-8' }, { LANG: 'C', LC_ALL: 'C' }]) {
  for (const cmd of [['get'], ['ls'], ['encrypt'], ['keypair'], ['validate'], ['run', '--', ...show], ['help'], ['get', '--help']]) {
    add(`r19 locale ${JSON.stringify(extra)} ${cmd[0]}`, 'enc', cmd, { CI: 'true', ...extra })
  }
}

// ---- every option of every command, generated from the help spec: with a value, as --long=value, without its value, as a
// misspelled flag (suggestions), and as a lone flag. Commands that reach the network or open prompts are skipped.
const NETWORK = new Set(['login', 'logout', 'curl', 'update', 'armor', 'help', 'ext'])
const specTree = JSON.parse(fs.readFileSync(path.join(ROOT, 'src/Dotenvx/Help/help-spec.json'), 'utf8'))
const walkSpec = (cmd, trail) => {
  const here = trail.concat(cmd.name)
  const top = here[1]
  if (here.length > 1 && !NETWORK.has(top) && !(top === 'native' && here.length === 2) && !['dec', 'enc'].includes(top)) {
    const pathArgs = here.slice(1)
    const needsArgs = cmd.usage.includes('<') ? ['K'] : []
    for (const o of cmd.options) {
      const flags = o.term.split(/[, ]+/).filter(f => f.startsWith('-'))
      const long = flags.find(f => f.startsWith('--'))
      if (!long || long === '--help' || long === '--version') continue
      const takesValue = /[<[]/.test(o.term)
      const base = [...pathArgs, ...needsArgs]
      const key = base.join(' ')
      for (const fx of ['plain']) {
        if (takesValue) {
          add(`opt ${fx} ${key} ${long} v`, fx, [...base, long, 'v'], { CI: 'true' })
          add(`opt ${fx} ${key} ${long}=v`, fx, [...base, `${long}=v`], { CI: 'true' })
          add(`opt ${fx} ${key} ${long} (missing)`, fx, [...base, long], { CI: 'true' })
        } else {
          add(`opt ${fx} ${key} ${long}`, fx, [...base, long], { CI: 'true' })
        }
        add(`opt ${fx} ${key} ${long}z (typo)`, fx, [...base, long + 'z'], { CI: 'true' })
      }
    }
  }
  for (const child of cmd.children) walkSpec(child, here)
}
walkSpec(specTree, [])

// ---- round 11: every pair of options of the commands that read or write env files, generated from the help spec.
const r11Values = {
  '--env': ['production', 'local'], '-e': ['production'], '--env-file': ['.env.local'], '--env-keys-file': ['.env.keys', 'other.keys'],
  '--convention': ['nextjs', 'flow'], '--ignore': ['MISSING_ENV_FILE'], '--token': ['dxt_x'], '--mask': ['*'], '--include-key': ['HELLO', 'H*'],
  '--exclude-key': ['FOO', 'A'], '--format': ['json', 'shell', 'eval'], '--key': ['HELLO', 'A*']
}
const r11Skip = new Set(['--help', '--token', '--no-armor', '--no-native', '--no-1password', '--no-bitwarden', '-h'])
const r11Fixtures = ['plain', 'enc', 'multi']
const r11Commands = { get: ['get'], set: ['set', 'K', 'v'], encrypt: ['encrypt'], decrypt: ['decrypt'], keypair: ['keypair'], run: ['run'], del: ['del', 'HELLO'] }
for (const c of specTree.children) {
  const base = r11Commands[c.name]
  if (!base) continue
  const opts = c.options.map(o => {
    const flags = o.term.split(/[, ]+/).filter(f => f.startsWith('-'))
    const long = flags.find(f => f.startsWith('--'))
    const takesRequired = o.term.includes('<')
    const takesOptional = o.term.includes('[')
    const vals = takesRequired || takesOptional ? (r11Values[long] || ['x']).slice(0, 2) : [null]
    return { long, short: flags.find(f => !f.startsWith('--')), vals, optional: takesOptional }
  }).filter(o => o.long && !r11Skip.has(o.long))
  const spell = (o, v) => v === null ? [o.long] : [o.long, v]
  for (let i = 0; i < opts.length; i++) {
    for (let j = i + 1; j < opts.length; j++) {
      for (const fx of r11Fixtures) {
        const a = opts[i], b = opts[j]
        const va = a.vals[0], vb = b.vals[0]
        const args = c.name === 'run' ? [...base, ...spell(a, va), ...spell(b, vb), '--', ...show] : [...base, ...spell(a, va), ...spell(b, vb)]
        add(`r11 ${fx} ${c.name} ${a.long}=${va} ${b.long}=${vb}`, fx, args, { CI: 'true' })
      }
    }
  }
}

// ---- round 20: `ext` subcommands (every option, value shapes, fixtures), and longer seeded random sequences over several env files.
const r20Ext = specTree.children.find(c => c.name === 'ext')
const r20Fixtures = ['plain', 'enc', 'multi', 'example', 'none', 'r15 tree']
for (const sub of r20Ext ? r20Ext.children : []) {
  if (sub.name === 'scan') continue
  const base = ['ext', sub.name]
  const positional = sub.usage.includes('[directory]') ? [[], ['.'], ['sub'], ['missing']] : [[]]
  const opts = sub.options.map(o => {
    const flags = o.term.split(/[, ]+/).filter(f => f.startsWith('-'))
    return { long: flags.find(f => f.startsWith('--')), short: flags.find(f => !f.startsWith('--')), value: o.term.includes('<') || o.term.includes('['), variadic: o.term.includes('...') }
  }).filter(o => o.long && !['--help'].includes(o.long))
  const valuesFor = o => o.long === '--pattern' ? [['.env*'], ['.env', '.env.keys'], ['*.log'], ['!x']] : o.long === '--env-file' ? [['.env'], ['.env.local'], ['.env', '.env.local'], ['nope']] : [['x']]
  for (const fx of r20Fixtures) {
    for (const pos of positional) {
      add(`r20 ext ${sub.name} ${fx} [${pos.join(' ')}]`, fx, [...base, ...pos], { CI: 'true' })
      for (const o of opts) {
        if (!o.value) add(`r20 ext ${sub.name} ${fx} [${pos.join(' ')}] ${o.long}`, fx, [...base, ...pos, o.long], { CI: 'true' })
        else {
          for (const v of valuesFor(o)) {
            add(`r20 ext ${sub.name} ${fx} [${pos.join(' ')}] ${o.long} ${v.join(',')}`, fx, [...base, ...pos, o.long, ...v], { CI: 'true' })
            add(`r20 ext ${sub.name} ${fx} [${pos.join(' ')}] ${o.short || o.long}= ${v.join(',')}`, fx, [...base, ...pos, ...(o.short ? [o.short + '=' + v[0]] : [o.long + '=' + v[0]]), ...v.slice(1)], { CI: 'true' })
          }
          add(`r20 ext ${sub.name} ${fx} [${pos.join(' ')}] ${o.long} (missing)`, fx, [...base, ...pos, o.long], { CI: 'true' })
        }
      }
      for (let i = 0; i < opts.length; i++) for (let j = i + 1; j < opts.length; j++) {
        const spell = o => o.value ? [o.long, valuesFor(o)[0][0]] : [o.long]
        add(`r20 ext ${sub.name} ${fx} [${pos.join(' ')}] ${opts[i].long} + ${opts[j].long}`, fx, [...base, ...pos, ...spell(opts[i]), ...spell(opts[j])], { CI: 'true' })
      }
    }
  }
  // the top-level twin must agree with the ext spelling run for run
  for (const fx of r20Fixtures) addSteps(`r20 ext ${sub.name} twin ${fx}`, fx, [S(['ext', sub.name]), S([sub.name]), S(['ext', sub.name]), S([sub.name])], { CI: 'true' })
}

// longer sequences over several env files (a different seed and a wider pool than round 12)
const r20Pool = [
  ['encrypt'], ['encrypt', '-f', '.env.production'], ['encrypt', '-f', '.env', '-f', '.env.production'], ['decrypt', '-f', '.env.production'], ['decrypt'],
  ['set', 'A', 'x', '-f', '.env.production'], ['set', 'B', 'y'], ['set', 'C', 'z', '--plain'], ['set', 'D', 'w', '-f', '.env.production', '--plain'], ['del', 'A', '-f', '.env.production'], ['del', 'B'],
  ['get', '-f', '.env.production'], ['get', '-f', '.env', '-f', '.env.production'], ['get', '--all'], ['get', '-e', 'production'], ['keypair', '-f', '.env.production'], ['keypair'],
  ['run', '-f', '.env.production', '--', ...show], ['run', '-f', '.env', '-f', '.env.production', '--overload', '--', ...show],
  ['ext', 'genexample'], ['genexample'], ['validate'], ['ext', 'gitignore'], ['gitignore', '--pattern', '.env*'], ['ls'], ['prebuild'], ['precommit'], ['ext', 'prebuild'],
  ['lock', 'up'], ['lock', 'down'], ['native', 'up'], ['native', 'down']
]
let r20Seed = 20261010
const r20Rand = () => { r20Seed = (r20Seed * 1103515245 + 12345) & 0x7fffffff; return r20Seed / 0x7fffffff }
for (let n = 0; n < 250; n++) {
  const fx = ['multi', 'enc', 'plain', 'none', 'prodEnc'][Math.floor(r20Rand() * 5)]
  const length = 6 + Math.floor(r20Rand() * 7)
  const steps = Array.from({ length }, () => { const a = r20Pool[Math.floor(r20Rand() * r20Pool.length)]; return a[0] === 'lock' ? S(a, { stdin: 'pw\n' }) : S(a) })
  addSteps(`r20 seq ${String(n).padStart(3, '0')} ${fx}`, fx, steps, { CI: '', FAKECLI_SEED: JSON.stringify({}) }) // native stores are the per-run fake, so the sequences never touch the machine's real store
}

// Differences that may only be in the stderr text: the operating system's own wording (EISDIR/ENOENT in Node, the .NET message here)
const ACCEPTED_STDERR_ONLY = [/^r16 self signal SIGUSR1$/, // Node opens its inspector on SIGUSR1: both runs race for port 9229
  /^r20 seq /, // the lock passphrase prompt redraws differently (accepted difference #3)
  /^r20 ext precommit .*--(install|uninstall)/, // the hook path outside a repository: ENOENT in Node, the .NET wording inside the same dotenvx message
  /^r11 .* encrypt --profile=x --stdout/, // "Profile selection requires an Envfile" is an uncaught error in both: only the stack trace text differs
  /^opt plain spec --file(=| )v/, // a missing file: ENOENT in Node, the .NET wording here
  /^r18 DOTENV(_CONFIG)?_CONVENTION=/, // an invalid convention is an uncaught error in both: only the stack trace text differs
  /^r17 .* (bogus|next|NEXTJS) env/, // an invalid convention is an uncaught error in both: only the stack trace text differs
  /^r15 link link loop/, // ELOOP in Node, the system message here
  /^r15 .* (encrypt|set|get|decrypt|keypair|validate|run) -f(k)? .*(deep|nope)/, // a missing file: ENOENT in Node, the .NET wording here
  /^r14 (envIsDir|keysIsDir|exampleIsDir|gitignoreIsDir) /, // a directory read as a file: EISDIR in Node, access denied in .NET
  /^r6 lock /, /^r6 repo .* hook install/, /^r4 file /, /^r5 life custom keys /, /^r5 child /] // r5 child: the node binary's own messages differ only in the casing of node.exe

// Known, deliberate differences (a different help/debug renderer, or output that embeds its own temp directory name).
const ACCEPTED = [
  ...(process.platform === 'win32' ? [/^r16 many keys broken run /] : []), // commandAction.js flushes with stdout.write('') once an action returns an exitCode: libuv's zero-length write to a closed pipe fails with EPIPE on Windows (uncaught, exit 1) and succeeds on POSIX, so Node's crash is the runtime's, not dotenvx's
  ...(process.platform === 'darwin' ? [/^r16 many keys encrypt --stdout$/] : []), // Node on macOS exits before a >64KB write to a pipe drains and cuts the output at ~417 keys; .NET prints it all
  /^r20 ext ls (twin )?r15 tree/, // directory order follows the filesystem crawl in Node (trap #64)
  /^r18 DOTENV_PRIVATE_KEYS=/, // the derived file name is ".env." (trailing dot): Windows file APIs in .NET treat it as ".env", Node does not (documented difference)
  /^r15 r15 tree ls dir /, // directory order follows the filesystem crawl in Node (trap #64)
  /^r14 (name|dirname) dash /, // `-dash` as an argument is the debug flag plus an unknown option: the debug line is printed before the error here
  /^r10 ref .* get --all$/, // --all dumps the whole environment, which includes the per-run fake vault directory
  /^r9 scan root -l debug /, // debug output has a different shape
  /^r8 ls tree \[/, // directory order follows the filesystem crawl in Node (trap #64) and an ordinal sort here
  /^global -d$/, /^global -l debug$/, // debug option dumps have a different shape
  /^(r8 )?ls --json /, // absolute paths embed the per-run temp directory (the two sides use different ones)
  /^shape [a-zA-Z]+ get HELLO --convention bogus$/, // both crash with an uncaught error; only the stack trace text differs
  /^shape [a-zA-Z]+ get HELLO --env-file --overload$/, // node itself reads --env-file from anywhere in its argv ("node: --overload: not found")
  /^r4 (ls |env .* ls$|file .* ls$)/, // absolute paths embed the per-run temp directory
  /^r4 env DOTENV_(CONFIG_)?CONVENTION=bogus /, // both crash with an uncaught error whose stack trace text differs
  /^r4 (set value "-dash"|set value "-dash" )/, // debug option dumps differ (the file content is compared)
  /^r4 file "sub[/]" (encrypt|set) spaces$/, // Node accepts a trailing slash on a missing file name on Windows; .NET rejects the path
  /^r5 life stdout /, // the masked random public key keeps its first characters, which differ per run
  /^r5 name .env. /, // Windows strips a trailing dot when .NET creates a file, Node keeps it
  /^r6 armor settings device/, // the device key is random per run
  /^r6 lock tab cross /, // a tab typed into a piped passphrase is handled by enquirer's key handling; not reproduced
  /^opt plain precommit --install$/, // the operating system's own error text ("ENOENT ..." in Node, the .NET message here) is not mirrored
  /^opt plain get --convention/, // both crash/print with an uncaught error whose stack trace text differs
  /^opt plain (ls --json|get --all)$/, // absolute paths embed the per-run temp directory
  /^shape [a-zA-Z]+ run -e (--x|-f) /, // a dash-leading value of a variadic option cannot be passed through System.CommandLine
  /^shape [a-zA-Z]+ run -f .env .env.local /, // the stray value becomes the command: both fail to start it, with the OS-specific wording of each launcher
  /^shape [a-zA-Z]+ run .*-q$/, // the child is `node` resolved from PATH: only the casing of node.exe differs in its own error
  /^shape [a-zA-Z]+ (-qd|--log-level=debug|run --log-level debug)/, // debug option dumps have a different shape
  /^get convention bad$/ // both crash with an uncaught error; only the stack trace text differs
]

// ---- runner
const normalize = (s, dir) => {
  s = s.split(dir).join('<DIR>').split(dir.split(path.sep).join('/')).join('<DIR>')
  s = s.replace(/locked:([0-9a-f]+):[A-Za-z0-9_-]+/g, 'locked:$1:<payload>')
  s = s.replace(/encrypted:[A-Za-z0-9+/=]+/g, 'encrypted:ENC')
  s = s.replace(/\b[0-9a-f]{64,}\b/g, 'HEX').replace(/\b0[23][0-9a-f]{64}\b/g, 'PUB')
  s = s.replace(/\(([0-9A-F]{3}) ([0-9A-F]{3})\)/g, '(KEY)') // a freshly generated public key, shortened
  s = s.replace(/"_":"[^"]*"/g, '"_":"LAUNCHER"') // the shell sets `_` to the program that started the CLI (node or dotnet)
  s = s.replace(/sweep-[A-Za-z0-9]{6}/gi, 'sweep-DIR') // the per-run directory, also where it appears JSON-escaped inside a dumped environment
  s = s.replace(/0[23][0-9a-f]{4}\*{20,}/g, 'PUBMASK') // a masked random public key
  s = s.replace(/_0[23][0-9a-f]{64}/g, '_PUB') // a public key inside a fake vault file name
  s = s.replace(/scanned in \d+s/g, 'scanned in Ns')
  s = s.replace(/\[dotenvx@[^\]]+\]/g, '[dotenvx@V]')
  return s
}

// A scenario is one command line (args) or a list of steps run in one directory, in order. A step is
// { args, cli: 'self'|'node'|'dotnet', stdin } (cli names the CLI that runs it; 'self' is the CLI being compared, so a
// step run by the OTHER implementation proves cross-compatibility), { write: { file: content } } or { rm: [files] }.
// Fake external programs, each a single-file app in test/fakes/<name>.cs (secretcli: op, bw, powershell, secret-tool; security; extension:
// dotenvx-<name>; gitleaks). A fake is published once to a cached directory and its apphost is copied under every tool name a scenario needs
// (the apphost finds its dll by its own build-time name, so the other published files are copied unchanged).
function fakeSanitize (v) { return v.replace(/[^A-Za-z0-9]/g, "_") }
const publishedFakes = new Map()
const installedFakes = new Map()
const cleanupAtExit = dir => process.on("exit", () => { try { fs.rmSync(dir, { recursive: true, force: true }) } catch (e) { /* best effort */ } })
const publishFake = name => {
  if (publishedFakes.has(name)) return publishedFakes.get(name)
  const out = fs.mkdtempSync(path.join(fs.realpathSync(os.tmpdir()), 'sweep-fake-' + name + '-'))
  execFileSync('dotnet', ['publish', path.join(ROOT, 'test', 'fakes', name + '.cs'), '-o', out], { stdio: 'ignore' })
  cleanupAtExit(out)
  publishedFakes.set(name, out)
  return out
}
const installFake = (name, tools) => {
  const key = name + ':' + tools.join(',')
  if (installedFakes.has(key)) return installedFakes.get(key)
  const published = publishFake(name)
  const suffix = process.platform === 'win32' ? '.exe' : ''
  const bin = fs.mkdtempSync(path.join(fs.realpathSync(os.tmpdir()), 'sweep-bin-'))
  for (const f of fs.readdirSync(published)) {
    if (f === name + suffix) { for (const tool of tools) fs.copyFileSync(path.join(published, f), path.join(bin, tool + suffix)) }
    else fs.copyFileSync(path.join(published, f), path.join(bin, f))
  }
  cleanupAtExit(bin)
  installedFakes.set(key, bin)
  return bin
}

function runOne (cliArgs, sc, isNode) {
  return new Promise(resolve => {
    const dir = fs.mkdtempSync(path.join(fs.realpathSync(os.tmpdir()), 'sweep-'))
    for (const [f, c] of Object.entries(fixtures[sc.fx])) {
      fs.mkdirSync(path.dirname(path.join(dir, f)), { recursive: true })
      fs.writeFileSync(path.join(dir, f), c)
    }
    const env = { ...process.env, ...sc.env }
    if (sc.env.FAKEEXT_NAMES !== undefined) {
      const pathKey = Object.keys(env).find(k => k.toLowerCase() === 'path') || 'PATH'
      env[pathKey] = os.tmpdir() + path.delimiter + installFake('extension', sc.env.FAKEEXT_NAMES.split(',').map(n => 'dotenvx-' + n)) + path.delimiter + (env[pathKey] || '')
      delete env.FAKEEXT_NAMES
    }
    let fakeVault = null
    if (sc.env.FAKECLI_SEED !== undefined) {
      fakeVault = path.join(fs.realpathSync(os.tmpdir()), 'sweep-fc-' + path.basename(dir))
      for (const [rel, content] of Object.entries(JSON.parse(sc.env.FAKECLI_SEED))) { fs.mkdirSync(path.dirname(path.join(fakeVault, rel)), { recursive: true }); fs.writeFileSync(path.join(fakeVault, rel), content) }
      fs.mkdirSync(fakeVault, { recursive: true })
      const pathKey = Object.keys(env).find(k => k.toLowerCase() === 'path') || 'PATH'
      env[pathKey] = installFake('secretcli', ['op', 'bw', 'powershell', 'secret-tool']) + path.delimiter + (env[pathKey] || '')
      env.FAKE_SECRET_CLI_DIR = fakeVault
      delete env.FAKECLI_SEED
    }
    if (Object.keys(sc.env).some(k => k.startsWith('FAKEGL_'))) {
      const pathKey = Object.keys(env).find(k => k.toLowerCase() === 'path') || 'PATH'
      env[pathKey] = installFake('gitleaks', ['gitleaks']) + path.delimiter + (env[pathKey] || '')
      env.FAKEGL_LOG = path.join(fs.realpathSync(os.tmpdir()), 'sweep-gl-' + path.basename(dir) + '.log')
    }
    // hermetic Armor/device config: a per-run directory unless the scenario names one
    env.DOTENVX_CONFIG = (sc.env.DOTENVX_CONFIG || '<DIR>/.dxcfg').split('<DIR>').join(dir)
    env.DOTENVX_NO_ARMOR = sc.env.DOTENVX_NO_ARMOR || ''
    for (const k of Object.keys(env)) if (k.startsWith('DOTENV') && !(k in sc.env) && k !== 'DOTENVX_CONFIG' && k !== 'DOTENVX_NO_ARMOR') delete env[k]
    if (!env.DOTENVX_NO_ARMOR) delete env.DOTENVX_NO_ARMOR
    delete env.PUBLIC
    const steps = sc.steps || [{ args: sc.args }]
    let stdout = ''
    let stderr = ''
    let code = 0
    const finish = () => {
      const files = {}
      const walk = d => { for (const n of fs.readdirSync(d)) { const p = path.join(d, n); if (n === '.git') { const hook = path.join(p, 'hooks', 'pre-commit'); if (fs.existsSync(hook)) files['.git/hooks/pre-commit'] = normalize(fs.readFileSync(hook, 'utf8'), dir); continue } const rel = path.relative(dir, p).split(path.sep).join('/'); if (fs.lstatSync(p).isSymbolicLink()) { files[rel] = 'LINK ' + normalize(fs.readlinkSync(p), dir) + (fs.existsSync(p) && fs.statSync(p).isFile() ? ' => ' + normalize(fs.readFileSync(p, 'utf8'), dir) : ''); continue } if (fs.statSync(p).isDirectory()) walk(p); else files[path.relative(dir, p).split(path.sep).join('/')] = normalize(fs.readFileSync(p, 'utf8'), dir) } }
      walk(dir)
      if (fakeVault) {
        const walkVault = (d, rel) => { for (const n of fs.readdirSync(d)) { const p = path.join(d, n); if (fs.statSync(p).isDirectory()) walkVault(p, rel + n + '/'); else files[normalize('<fake cli>/' + rel + n, dir)] = normalize(fs.readFileSync(p, 'utf8'), dir) } }
        walkVault(fakeVault, '')
        fs.rmSync(fakeVault, { recursive: true, force: true })
      }
      if (env.FAKEGL_LOG && fs.existsSync(env.FAKEGL_LOG)) { files['<gitleaks calls>'] = fs.readFileSync(env.FAKEGL_LOG, 'utf8'); fs.rmSync(env.FAKEGL_LOG, { force: true }) }
      try { fs.rmSync(dir, { recursive: true, force: true, maxRetries: 3 }) } catch (e2) { /* best effort */ }
      resolve({ code, stdout: normalize(stdout, dir), stderr: normalize(stderr, dir), files })
    }
    const runStep = index => {
      if (index >= steps.length) return finish()
      const step = steps[index]
      if (step.write) { for (const [f, c] of Object.entries(step.write)) { fs.mkdirSync(path.dirname(path.join(dir, f)), { recursive: true }); fs.writeFileSync(path.join(dir, f), c) } return runStep(index + 1) }
      if (step.git) {
        const gitEnv = { ...process.env, GIT_CONFIG_GLOBAL: os.devNull, GIT_CONFIG_NOSYSTEM: '1', GIT_AUTHOR_NAME: 'n', GIT_AUTHOR_EMAIL: 'n@e', GIT_COMMITTER_NAME: 'n', GIT_COMMITTER_EMAIL: 'n@e' }
        try { execFileSync('git', step.git, { cwd: dir, env: gitEnv, stdio: 'ignore' }) } catch (e) { /* a failing git step is part of the scenario */ }
        return runStep(index + 1)
      }
      if (step.link) {
        const [target, at] = step.link
        const linkPath = path.join(dir, at)
        const resolved = path.resolve(path.dirname(linkPath), target)
        fs.mkdirSync(path.dirname(linkPath), { recursive: true })
        const isDir = fs.existsSync(resolved) && fs.statSync(resolved).isDirectory()
        try { fs.symlinkSync(isDir ? resolved : target, linkPath, isDir ? 'junction' : 'file') } catch (e) { /* a failing link is part of the scenario */ }
        return runStep(index + 1)
      }
      if (step.rm) { for (const f of step.rm) fs.rmSync(path.join(dir, f), { recursive: true, force: true }); return runStep(index + 1) }
      const nodeCli = step.cli === 'node' || (step.cli !== 'dotnet' && isNode)
      let args = step.args.map(a => a.split('<DIR>').join(dir).split('<DIRNAME>').join(path.basename(dir)))
      if (['encrypt', 'set'].includes(args[0])) args = [args[0], ...(sc.env.FAKECLI_SEED !== undefined ? [] : ['--no-native']), '--no-armor', ...args.slice(1)] // before the scenario's own arguments, which may end in an option that takes a value
      // the node binary itself consumes --env-file ("node: v: not found") wherever it appears in argv: spell it -f for node only
      if (nodeCli) args = args.flatMap(a => a === '--env-file' ? ['-f'] : a.startsWith('--env-file=') ? ['-f', a.slice(11)] : [a])
      const exe = nodeCli ? 'node' : 'dotnet'
      const child = execFile(exe, [...(nodeCli ? NODE_CLI : DOTNET_CLI), ...args], { cwd: dir, env, encoding: 'utf8', timeout: 60000, maxBuffer: 64 * 1024 * 1024 }, (err, out, errOut) => {
        if (steps.length > 1) { stdout += `--- step ${index} exit ${err ? (err.code ?? 1) : 0}
`; stderr += `--- step ${index}
` }
        stdout += out
        stderr += errOut
        code = err ? (err.code ?? 1) : 0
        runStep(index + 1)
      })
      child.stdin.on('error', () => {}) // a child that exits without reading its stdin
      child.stdout.on('error', () => {})
      if (step.breakStdout) child.stdout.destroy() // the reader goes away: the CLI's next write to stdout fails
      if (step.stdin !== undefined) child.stdin.end(step.stdin)
      else child.stdin.end()
    }
    runStep(0)
  })
}

async function main () {
  const only = process.argv[2]
  // SWEEP_EVERY=N runs every Nth scenario (a fast, repeatable sample for pull requests); the full sweep runs without it
  const every = Math.max(1, Number(process.env.SWEEP_EVERY) || 1)
  // macOS keeps keys through /usr/bin/security, an absolute path no fake on PATH can replace: scenarios that run `native` stay off the real keychain
  const usesNative = s => (s.steps || [{ args: s.args }]).some(step => step.args && step.args[0] === 'native' && s.env.FAKECLI_SEED !== undefined)
  const list = scenarios.filter(s => !only || s.name.includes(only)).filter(s => !(process.platform === 'darwin' && usesNative(s))).filter((_, index) => index % every === 0)
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
  const unexpected = diffs.filter(d => !ACCEPTED.some(a => a.test(d.sc.name)) && !(ACCEPTED_STDERR_ONLY.some(a => a.test(d.sc.name)) && d.fields.every(f => f === 'stderr')))
  diffs.length = 0
  diffs.push(...unexpected)
  diffs.sort((a, b) => a.sc.name.localeCompare(b.sc.name))
  const lines = []
  for (const x of diffs) {
    lines.push(`### ${x.sc.name} [${x.fields.join(',')}]  args=${JSON.stringify(x.sc.steps || x.sc.args)} env=${JSON.stringify(x.sc.env)}`)
    for (const f of x.fields) {
      if (f === 'files') {
        for (const name of new Set([...Object.keys(x.n.files), ...Object.keys(x.d.files)])) {
          const [fa, fb] = [JSON.stringify(x.n.files[name]), JSON.stringify(x.d.files[name])]
          if (fa === fb) continue
          let p = 0
          while (p < (fa || '').length && fa[p] === (fb || '')[p]) p++
          const from = Math.max(0, p - 60)
          lines.push(`  node file ${name}: ${fa === undefined ? '(absent)' : fa.slice(from, from + (process.env.SWEEP_FULL ? 100000 : 240))}`)
          lines.push(`  dotn file ${name}: ${fb === undefined ? '(absent)' : fb.slice(from, from + (process.env.SWEEP_FULL ? 100000 : 240))}`)
        }
      } else if (f === 'exit') lines.push(`  exit node=${x.n.code} dotnet=${x.d.code}`)
      else { const [a, b] = [JSON.stringify(x.n[f]), JSON.stringify(x.d[f])]; let p = 0; while (p < a.length && a[p] === b[p]) p++; const from = Math.max(0, Math.min(p - 40, p)); lines.push(`  ${f} node=${a.slice(from, from + 300)}`); lines.push(`  ${f} dotn=${b.slice(from, from + 300)}`) } // the window starts just before the first difference
    }
  }
  fs.writeFileSync(path.join(os.tmpdir(), 'dotenvx-parity-sweep.txt'), lines.join('\n'))
  console.log(`${list.length} scenarios, ${diffs.length} differ`)
  if (diffs.length) {
    console.log(lines.join(String.fromCharCode(10)).slice(0, 20000)) // CI has no access to the temp file
    process.exitCode = 1
  }
}
main()
