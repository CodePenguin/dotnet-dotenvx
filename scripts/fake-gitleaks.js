// Stand-in for the external `gitleaks` program, used by the parity sweep so `ext scan` can be compared without installing it.
// FAKEGL_VERSION_EXIT, FAKEGL_EXIT, FAKEGL_OUT and FAKEGL_ERR script its behavior; every call is appended to FAKEGL_LOG.
const fs = require('fs')
const env = process.env
const args = process.argv.slice(2)
if (env.FAKEGL_LOG) fs.appendFileSync(env.FAKEGL_LOG, args.join(' ') + '\n')
if (args[0] === 'version') {
  process.stdout.write('8.0.0\n')
  process.exit(Number(env.FAKEGL_VERSION_EXIT || 0))
}
if (env.FAKEGL_OUT) process.stdout.write(env.FAKEGL_OUT)
if (env.FAKEGL_ERR) process.stderr.write(env.FAKEGL_ERR)
process.exit(Number(env.FAKEGL_EXIT || 0))
