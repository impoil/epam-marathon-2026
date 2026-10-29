// Serve the existing production bundle locally and verify ?demo cannot authorize it.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { chromium } = require('playwright');

async function main() {
  const root = path.resolve(__dirname, '../../frontend/restaurant-frontend-app/dist/restaurant-frontend-app/browser');
  assert.ok(fs.existsSync(path.join(root, 'index.html')), 'Build production first');
  const mime = { '.js': 'text/javascript', '.css': 'text/css', '.html': 'text/html', '.svg': 'image/svg+xml', '.woff2': 'font/woff2' };
  const server = http.createServer((request, response) => {
    let file = path.resolve(root, '.' + new URL(request.url, 'http://localhost').pathname);
    if (!file.startsWith(root + path.sep) && file !== root) {
      response.writeHead(403).end();
      return;
    }
    if (!fs.existsSync(file) || !fs.statSync(file).isFile()) file = path.join(root, 'index.html');
    response.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
    fs.createReadStream(file).pipe(response);
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({
      executablePath: process.env.HW6_CHROMIUM || '/usr/bin/chromium', headless: true,
      args: ['--no-sandbox', '--disable-dev-shm-usage'],
    });
    const page = await browser.newPage();
    const authorizationHeaders = [];
    // Public landing-page reads receive local fixtures. No real API is contacted.
    await page.route('https://example.com/**', async (route) => {
      authorizationHeaders.push(route.request().headers()['authorization']);
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    });
    await page.goto(`http://127.0.0.1:${server.address().port}/reservations/demo-reservation/group-order?demo`);
    await page.waitForURL('**/login');
    await page.locator('app-header').waitFor();
    assert.equal(await page.locator('app-group-order-page').count(), 0);
    assert.deepEqual(await page.evaluate(() => [localStorage.getItem('auth'), localStorage.getItem('user')]), [null, null]);
    assert.ok(authorizationHeaders.every((header) => !header), 'No demo authorization may reach the API');
    console.log('Production browser check passed: ?demo leaves storage empty and the protected Owner route redirects to /login.');
  } finally {
    if (browser) await browser.close();
    await new Promise((resolve) => server.close(resolve));
  }
}
main().catch((error) => { console.error(error); process.exitCode = 1; });
