import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';

const script = resolve('scripts/set-env.mjs');
for (const url of ['', 'http://localhost:4200', 'https://user:password@example.test', 'https://example.test/?token=secret', 'https://example.test/#secret']) {
  test(`rejects unsafe production URL: ${url.replace(/password|secret/g, 'redacted')}`, () => {
    const directory = mkdtempSync(join(tmpdir(), 'greentasty-env-'));
    try {
      const result = spawnSync(process.execPath, [script], { cwd: directory, env: { ...process.env, BACKEND_URL: url } });
      assert.equal(result.status, 1);
    } finally { rmSync(directory, { recursive: true, force: true }); }
  });
}
test('generates safely quoted HTTPS endpoint and strips trailing slash', () => {
  const directory = mkdtempSync(join(tmpdir(), 'greentasty-env-'));
  try {
    const endpoint = 'https://abcdefghij.execute-api.eu-north-1.amazonaws.com/demo';
    const result = spawnSync(process.execPath, [script], { cwd: directory, env: { ...process.env, BACKEND_URL: endpoint + '/' } });
    assert.equal(result.status, 0);
    assert.ok(readFileSync(join(directory, 'src/environments/environment.production.ts'), 'utf8').includes(JSON.stringify(endpoint)));
  } finally { rmSync(directory, { recursive: true, force: true }); }
});
