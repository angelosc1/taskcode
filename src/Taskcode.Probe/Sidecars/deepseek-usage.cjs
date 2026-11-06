'use strict';
// Local counts only. Never emits, persists, or forwards transcript contents.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const zlib = require('node:zlib');
function readUsage(root, now = new Date()) {
  if (typeof zlib.zstdDecompressSync !== 'function') return { error: 'zstd_missing' };
  if (!fs.existsSync(root)) return { error: 'not_found' };
  const start = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  const end = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1).getTime();
  let tokens = 0, sessions = 0, files = 0, bytes = 0;
  const walk = (directory, depth) => {
    if (depth > 6) return;
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      if (entry.isSymbolicLink()) continue;
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) { walk(file, depth + 1); continue; }
      if (entry.name !== 'session.v4.jsonl.zstd') continue;
      if (++files > 2000) throw new Error('limit');
      const stat = fs.statSync(file);
      if (stat.mtimeMs < start) continue;
      if (stat.size > 64 * 1024 * 1024) throw new Error('limit');
      // The Harness appends independent frames; Node stops after each frame.
      // bytesWritten is the decoder's authoritative compressed input boundary.
      const compressed = fs.readFileSync(file);
      const frames = [];
      let offset = 0, decodedSize = 0;
      while (offset < compressed.length) {
        if (frames.length >= 200000) throw new Error('limit');
        const frame = zlib.zstdDecompressSync(compressed.subarray(offset), { info: true, maxOutputLength: 64 * 1024 * 1024 });
        if (!frame.engine.bytesWritten || frame.engine.bytesWritten > compressed.length - offset) throw new Error('invalid');
        offset += frame.engine.bytesWritten;
        decodedSize += frame.buffer.length;
        if (decodedSize > 64 * 1024 * 1024) throw new Error('limit');
        frames.push(frame.buffer);
      }
      const data = Buffer.concat(frames);
      bytes += data.length;
      if (bytes > 512 * 1024 * 1024) throw new Error('limit');
      let seen = false;
      for (const line of data.toString('utf8').split('\n')) {
        if (!line.includes('"usage"')) continue;
        const record = JSON.parse(line);
        if (record.time < start || record.time >= end || !Number.isFinite(record.time)) continue;
        const usage = record.data?.usage;
        if (!usage) continue;
        const count = ['inputTokens', 'outputTokens', 'cacheReadTokens', 'cacheWriteTokens']
          .map(key => usage[key] ?? 0);
        if (count.some(value => !Number.isSafeInteger(value) || value < 0)) throw new Error('invalid');
        tokens += count.reduce((sum, value) => sum + value, 0);
        if (!Number.isSafeInteger(tokens)) throw new Error('overflow');
        seen = true;
      }
      if (seen) sessions++;
    }
  };
  try { walk(root, 0); return { tokens, sessions }; }
  catch { return { error: 'incomplete' }; }
}
module.exports = { readUsage };
if (require.main === module) process.stdout.write(JSON.stringify(readUsage(path.join(os.homedir(), '.dsh', 'sessions'))));
