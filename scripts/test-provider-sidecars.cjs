'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const zlib = require('node:zlib');
const root = path.resolve(__dirname, '../out/sidecar-fixtures');
fs.mkdirSync(root, { recursive: true });
const { readUsage } = require('../src/Taskcode.Probe/Sidecars/deepseek-usage.cjs');
const scripts = path.resolve(__dirname, '../src/Taskcode.Probe/Sidecars');
function fixture(name) { return fs.mkdtempSync(path.join(root, name)); }
function record(time, usage) { return JSON.stringify({ time, data: { usage, prompt: 'fixture never exported' } }) + '\n'; }
test('DeepSeek counts concatenated zstd frames from today, excluding older records', () => {
  const directory = fixture('deepseek-');
  const now = new Date();
  const file = path.join(directory, 'session.v4.jsonl.zstd');
  fs.writeFileSync(file, Buffer.concat([
    zlib.zstdCompressSync(record(now.getTime(), { inputTokens: 100, outputTokens: 10, cacheReadTokens: 30, cacheWriteTokens: 5 })),
    zlib.zstdCompressSync(record(now.getTime(), { inputTokens: 20, outputTokens: 4 })),
    zlib.zstdCompressSync(record(new Date(now.getFullYear(), now.getMonth(), now.getDate() - 2).getTime(), { inputTokens: 900 }))
  ]));
  assert.deepEqual(readUsage(directory, now), { tokens: 169, sessions: 1 });
});
test('DeepSeek distinguishes a missing install, empty day and incomplete transcript', () => {
  const directory = fixture('deepseek-empty-');
  assert.equal(readUsage(path.join(directory, 'missing')).error, 'not_found');
  assert.deepEqual(readUsage(directory), { tokens: 0, sessions: 0 });
  fs.writeFileSync(path.join(directory, 'session.v4.jsonl.zstd'), Buffer.from('incomplete'));
  assert.equal(readUsage(directory).error, 'incomplete');
});
test('DeepSeek rejects invalid token counts without returning partial totals', () => {
  const directory = fixture('deepseek-invalid-');
  fs.writeFileSync(path.join(directory, 'session.v4.jsonl.zstd'), zlib.zstdCompressSync(record(Date.now(), { inputTokens: -1 })));
  assert.equal(readUsage(directory).error, 'incomplete');
});
function qoderFixture(authenticated, fails) {
  const directory = fixture('qoder-');
  const packageDir = path.join(directory, 'node_modules/@qoder-ai/qoder-agent-sdk');
  fs.mkdirSync(packageDir, { recursive: true });
  fs.writeFileSync(path.join(packageDir, 'package.json'), JSON.stringify({ name: '@qoder-ai/qoder-agent-sdk', type: 'module', main: 'index.js' }));
  fs.writeFileSync(path.join(packageDir, 'index.js'), `
    import fs from 'node:fs';
    const log = value => fs.appendFileSync(${JSON.stringify(path.join(directory, 'calls.txt'))}, value + '\\n');
    export function qodercliAuth() { log('local-auth'); return 'fixture'; }
    export async function startDaemon(options) {
      if (options.auth !== 'fixture') throw new Error('wrong auth');
      log('start');
      return {
        auth: async () => ${authenticated},
        getUsageInfo: async () => { log('usage'); ${fails ? "throw new Error('raw credential must not leak');" : "return { userQuota: { total: 100, used: 5 }, totalUsagePercentage: 5, session: { used: 999 }, secret: 'must not leak' };"} },
        stop: async () => log('stop')
      };
    }
    export function query() { throw new Error('Inference must never start'); }
  `);
  const script = path.join(directory, 'qoder-usage.cjs');
  fs.copyFileSync(path.join(scripts, 'qoder-usage.cjs'), script);
  const output = execFileSync(process.execPath, [script], { timeout: 10000, encoding: 'utf8', windowsHide: true });
  return { result: JSON.parse(output), output, calls: fs.readFileSync(path.join(directory, 'calls.txt'), 'utf8') };
}
test('Qoder queries only account usage, omits session/extra fields and stops its daemon', () => {
  const { result, output, calls } = qoderFixture(true, false);
  assert.equal(result.usage.userQuota.used, 5);
  assert.equal(result.usage.session, undefined);
  assert.equal(output.includes('secret'), false);
  assert.equal(calls, 'local-auth\nstart\nusage\nstop\n');
});
test('Qoder stops on missing login and query failure without exposing raw errors', () => {
  const signedOut = qoderFixture(false, false);
  assert.equal(signedOut.result.error, 'needs_auth');
  assert.equal(signedOut.calls, 'local-auth\nstart\nstop\n');
  const failed = qoderFixture(true, true);
  assert.equal(failed.result.error, 'query_failed');
  assert.equal(failed.output.includes('credential'), false);
  assert.equal(failed.calls.endsWith('stop\n'), true);
});
