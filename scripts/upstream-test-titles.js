// Lists upstream test titles (dotenvx/tests) that no C# test cites. Run from the repo root: node scripts/upstream-test-titles.js [file-fragment]
// A title is 'cited' when its exact text appears in test/**/*.cs (a Refer to remark) or in docs/upstream-tests-not-ported.md.
// Pass a fragment of an upstream test file path to also print its uncited titles.
// Files for out-of-scope features (see the skip pattern below) are not checked; docs/upstream-tests-not-ported.md lists them.
const fs = require('fs')
const path = require('path')
const root = 'dotenvx/tests'
function walk (d) { return fs.readdirSync(d, { withFileTypes: true }).flatMap(e => e.isDirectory() ? walk(path.join(d, e.name)) : [path.join(d, e.name)]) }
function walkCs (d) {
  return fs.readdirSync(d, { withFileTypes: true }).flatMap(e => e.isDirectory()
    ? (['bin', 'obj', 'node_modules'].includes(e.name) ? [] : walkCs(path.join(d, e.name)))
    : (e.name.endsWith('.cs') ? [path.join(d, e.name)] : []))
}
const oos = fs.existsSync('docs/upstream-tests-not-ported.md') ? fs.readFileSync('docs/upstream-tests-not-ported.md', 'utf8') : ''
const csRaw = walkCs('test').map(f => fs.readFileSync(f, 'utf8')).join('\n') + '\n' + oos
// join doc-comment continuation lines so a title wrapped over two `///` lines still matches
const cs = csRaw + '\n' + csRaw.replace(/\s*\r?\n\s*\/\/\/ ?/g, ' ')
const showMissing = process.argv[2]
const skip = /armor|login|logout|curl|spinner|session|api[\\/]|decryptors|prompts|ptyCommand|createSpinner|createElapsed|buildApiError|isNetworkError|isTeam|teamChoices|sanitizeCommand|removeDynamic|removeOptions|login-helpers|deviceValue|executeDynamic|executeExtension|keypairMetadata/i
const out = []
for (const f of walk(root).filter(f => f.endsWith('.test.js') && !skip.test(f))) {
  const src = fs.readFileSync(f, 'utf8')
  const re = /(?:t\.test|\bit|\btest)\(\s*(['"`])((?:\\.|(?!\1).)*)\1/g
  const titles = []
  let m
  while ((m = re.exec(src)) !== null) titles.push(m[2].split("\\'").join("'"))
  const missing = titles.filter(t => !cs.includes(t))
  const name = f.split(path.sep).join('/')
  if (missing.length) out.push(name + ' ' + missing.length + '/' + titles.length)
  if (missing.length && showMissing && name.includes(showMissing)) out.push(...missing.map(t => '    - ' + t))
}
console.log(out.join('\n'))
