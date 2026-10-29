/* Optional local verification: requires Playwright and Chromium installed outside the app.
 * No deployed API or AWS resources are used. Error cases inject failed Observables
 * through Angular's development debug API; they do not simulate a deployed .NET E2E.
 */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');

const output = path.join(__dirname, 'screenshots');
const base = process.env.HW6_FRONTEND_URL || 'http://127.0.0.1:4200';
const route = '/reservations/demo-reservation/group-order?demo';

async function main() {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({
    executablePath: process.env.HW6_CHROMIUM || '/usr/bin/chromium',
    headless: true,
    args: ['--no-sandbox', '--disable-dev-shm-usage'],
  });
  const errors = [];
  const realApiRequests = [];
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, timezoneId: 'Europe/Kyiv' });
  await context.route('https://*.execute-api.*.amazonaws.com/**', async (request) => {
    realApiRequests.push(request.request().url());
    await request.abort();
  });
  const page = await context.newPage();
  page.on('pageerror', (error) => errors.push(error.message));

  async function ready() {
    await page.goto(base + route);
    await page.getByRole('heading', { name: 'Consolidated pre-order' }).waitFor();
    await page.evaluate(() => document.fonts.ready);
    await page.locator('.grand-total').scrollIntoViewIfNeeded();
    await page.evaluate(() => window.scrollTo(0, 0));
  }
  async function shot(name) {
    const modalOpen = await page.getByRole('dialog').count();
    await page.screenshot({ path: path.join(output, name + '.png'), fullPage: modalOpen === 0 });
  }
  async function noOverflow() {
    const sizes = await page.evaluate(() => ({ viewport: innerWidth, content: document.documentElement.scrollWidth }));
    assert.equal(sizes.content, sizes.viewport, 'horizontal overflow');
  }
  // Only used to inject controlled failures or count outgoing mock service calls.
  async function injectFailure(method) {
    await page.evaluate((name) => {
      const component = window.ng.getComponent(document.querySelector('app-group-order-page'));
      const service = component.service;
      const original = service[name].bind(service);
      const Observable = service.getSessions$('demo-group').constructor;
      service[name] = () => new Observable((subscriber) => subscriber.error(new Error('Browser verification failure')));
      window.hw6Restore = () => { service[name] = original; };
    }, method);
  }
  async function restore() {
    await page.evaluate(() => window.hw6Restore());
  }
  try {
    await ready();
    assert.equal(await page.locator('app-participant-card').count(), 4);
    assert.match(await page.locator('.grand-total').innerText(), /\$50\.50/);
    assert.equal(await page.locator('app-participant-card').first().getByRole('button', { name: /Remove/ }).count(), 0);
    await noOverflow();
    await shot('01-owner-review-desktop');

    await page.setViewportSize({ width: 375, height: 812 });
    await noOverflow();
    await shot('02-owner-review-mobile-375');
    await page.getByRole('button', { name: 'Remove Kateryna', exact: true }).click();
    let dialog = page.getByRole('dialog');
    await dialog.waitFor();
    assert.match(await dialog.innerText(), /Kateryna/);
    await noOverflow();
    await shot('03-remove-guest-mobile-375');
    await page.keyboard.press('Escape');
    await dialog.waitFor({ state: 'detached' });
    assert.equal(await page.getByRole('button', { name: 'Remove Kateryna', exact: true }).evaluate((button) => button === document.activeElement), true);

    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.getByRole('button', { name: 'Remove Kateryna', exact: true }).click();
    dialog = page.getByRole('dialog');
    await dialog.waitFor();
    await shot('04-remove-guest-desktop');
    await injectFailure('removeSession$');
    await dialog.getByRole('button', { name: 'Remove Guest', exact: true }).click();
    await dialog.getByRole('alert').waitFor();
    assert.equal(await page.locator('app-participant-card').count(), 4);
    await restore();
    await dialog.getByRole('button', { name: 'Remove Guest', exact: true }).click();
    await dialog.waitFor({ state: 'detached' });
    assert.equal(await page.locator('app-participant-card').count(), 3);
    assert.match(await page.locator('.grand-total').innerText(), /\$26\.50/);
    assert.match(await page.locator('.reservation-details').innerText(), /3 of 5 guests/);
    await shot('05-guest-removed');

    await injectFailure('getSessions$');
    await page.getByRole('button', { name: 'Refresh', exact: true }).click();
    await page.getByText(/Couldn't refresh/).waitFor();
    assert.equal(await page.locator('app-participant-card').count(), 3);
    assert.equal(await page.locator('app-toast').count(), 0, 'silent refresh must not show an error toast');
    await shot('06-refresh-error');
    await restore();
    await page.getByRole('button', { name: 'Refresh', exact: true }).click();
    await page.getByText(/Couldn't refresh/).waitFor({ state: 'detached' });

    await page.getByRole('button', { name: 'Submit Pre-Order', exact: true }).click();
    dialog = page.getByRole('dialog');
    await dialog.waitFor();
    await shot('07-submit-confirmation');
    await injectFailure('submit$');
    await dialog.getByRole('button', { name: 'Submit', exact: true }).click();
    await dialog.getByRole('alert').waitFor();
    await shot('08-submit-error');
    await restore();
    await page.evaluate(() => {
      const component = window.ng.getComponent(document.querySelector('app-group-order-page'));
      const original = component.service.submit$.bind(component.service);
      window.hw6SubmitCalls = 0;
      component.service.submit$ = (...args) => { window.hw6SubmitCalls++; return original(...args); };
    });
    await dialog.getByRole('button', { name: 'Submit', exact: true }).click();
    assert.equal(await dialog.getByRole('button', { name: 'Submitting…', exact: true }).isDisabled(), true);
    await dialog.waitFor({ state: 'detached' });
    assert.equal(await page.evaluate(() => window.hw6SubmitCalls), 1);
    assert.match(await page.locator('.group-status').innerText(), /Sent to Restaurant/);
    assert.equal(await page.getByRole('button', { name: 'Remove Guest', exact: true }).count(), 0);
    assert.equal(await page.getByRole('button', { name: 'Submit Pre-Order', exact: true }).count(), 0);
    await page.getByText('Pre-order sent to the restaurant', { exact: true }).waitFor({ state: 'hidden' });
    await shot('09-sent-to-restaurant');

    await page.setViewportSize({ width: 375, height: 812 });
    await noOverflow();
    await shot('10-sent-to-restaurant-mobile-375');

    await ready();
    await injectFailure('getGroupInvite$');
    await page.evaluate(() => window.ng.getComponent(document.querySelector('app-group-order-page')).load());
    await page.getByRole('button', { name: 'Try again', exact: true }).waitFor();
    await shot('11-load-error-mobile-375');
    await restore();
    await page.getByRole('button', { name: 'Try again', exact: true }).click();
    await page.getByRole('heading', { name: 'Consolidated pre-order' }).waitFor();
    assert.equal(await page.locator('app-participant-card').count(), 4);
    assert.deepEqual(await page.evaluate(() => [...document.images].filter((image) => image.complete && !image.naturalWidth).map((image) => image.src)), []);
    assert.deepEqual(errors, [], 'uncaught browser errors');
    assert.deepEqual(realApiRequests, [], 'demo must not reach the deployed API');
    console.log('Browser checks passed: desktop, 375px, dialogs, keyboard, Remove/Submit, failures/retry, no overflow, no real API requests.');
  } finally {
    await browser.close();
  }
}
main().catch((error) => { console.error(error); process.exitCode = 1; });
