/**
 * Generates src/environments/environment.production.ts from the BACKEND_URL env var.
 * Runs automatically before `npm run build` (npm "prebuild" hook).
 *
 * Usage:
 *   BACKEND_URL=https://xxxx.execute-api.eu-west-1.amazonaws.com/develop npm run build
 *
 * The generated file is git-ignored on purpose: the API URL is environment
 * configuration, not source code.
 */
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';

const OUT_FILE = resolve('src/environments/environment.production.ts');
const backendUrl = process.env.BACKEND_URL?.trim();

if (!backendUrl) {
  console.error(
    [
      '',
      'BACKEND_URL is not set — refusing to build a production bundle.',
      '',
      'Without it the bundle would ship the literal BACKEND_URL_PLACEHOLDER',
      'and every API call would fail at runtime.',
      '',
      'Examples:',
      '  BACKEND_URL=https://xxxx.execute-api.eu-west-1.amazonaws.com/develop npm run build',
      '  npm start                     # local dev, no BACKEND_URL needed',
      '',
    ].join('\n')
  );
  process.exit(1);
}

let parsedUrl;
try {
  parsedUrl = new URL(backendUrl);
} catch {
  console.error('BACKEND_URL must be a valid HTTPS URL.');
  process.exit(1);
}
if (parsedUrl.protocol !== 'https:' || parsedUrl.username || parsedUrl.password || parsedUrl.search || parsedUrl.hash) {
  console.error('BACKEND_URL must use HTTPS and contain no credentials, query string or fragment.');
  process.exit(1);
}

const contents = `// GENERATED FILE — do not edit, do not commit.
// Produced by scripts/set-env.mjs from the BACKEND_URL environment variable.
export const environment = {
  backendUrl: ${JSON.stringify(parsedUrl.href.replace(/\/$/, ''))},
};
`;

mkdirSync(dirname(OUT_FILE), { recursive: true });
writeFileSync(OUT_FILE, contents, 'utf8');
console.log(`environment.production.ts generated with backendUrl = ${backendUrl}`);
