// Local artifact validation only. Does not create or publish a Pages project.
import { readdirSync, readFileSync, statSync, existsSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';

const root = resolve('dist/restaurant-frontend-app/browser');
function walk(directory) {
  return readdirSync(directory).flatMap((name) => {
    const file = join(directory, name);
    return statSync(file).isDirectory() ? walk(file) : [file];
  });
}
if (!existsSync(join(root, 'index.html')) || existsSync(join(root, '404.html'))) {
  throw new Error('Pages SPA requires index.html and no root 404.html. Build the production browser directory.');
}
const files = walk(root);
if (files.length > 20_000) throw new Error('Pages Free limit exceeded: 20,000 files.');
for (const file of files) {
  if (statSync(file).size > 25 * 1024 * 1024) throw new Error(`Pages 25 MiB asset limit exceeded: ${relative(root, file)}`);
  if (/(^|[/\\])(\.env(?:\..*)?|credentials|_worker\.js|functions)([/\\]|$)|\.(?:tfstate|tfplan|zip|map)$/.test(relative(root, file))) {
    throw new Error(`Unexpected private/server artifact: ${relative(root, file)}`);
  }
}
const headers = readFileSync(join(root, '_headers'), 'utf8');
for (const expected of ['X-Content-Type-Options: nosniff', 'X-Frame-Options: DENY', 'Referrer-Policy: no-referrer']) {
  if (!headers.includes(expected)) throw new Error(`Missing static security header: ${expected}`);
}
const largest = Math.max(...files.map((file) => statSync(file).size));
console.log(`Pages artifact check passed: ${files.length} files, largest ${largest} bytes; SPA fallback and security headers ready.`);
