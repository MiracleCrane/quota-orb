// Run after installing Playwright locally; no account or Codex session is used.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true,
    ...(fs.existsSync(chromium.executablePath()) ? {} : { channel: 'chrome' }) });
  const page = await browser.newPage({ viewport: { width: 1080, height: 820 }, colorScheme: 'dark' });
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(pathToFileURL(path.join(__dirname, 'index.html')).href);
  const frame = page.frameLocator('iframe');
  const detail = frame.locator('#qp-detail');
  const ensureOpen = async () => {
    if (!(await detail.isVisible())) await frame.locator('#qp-expand-demo').click();
    await detail.waitFor({ state: 'visible' });
    await page.waitForTimeout(250);
  };
  await frame.locator('#qp-orb').waitFor();
  assert.equal(await frame.locator('.qp-meter.qp-five-hour').isVisible(), false);
  await ensureOpen();
  assert.equal(await frame.locator('#qp-coupons').isVisible(), false);
  await frame.locator('#qp-scenario').selectOption('both');
  assert.match(await frame.locator('#qp-orb-content').innerText(), /62%/);
  assert.equal(await frame.locator('.qp-meter.qp-five-hour').isVisible(), true);
  assert.equal(await frame.locator('.qp-hour-ring').getAttribute('hidden'), null);
  await frame.locator('#qp-scenario').selectOption('low');
  assert.match(await frame.locator('#qp-orb-content').innerText(), /12%/);
  assert.equal(await frame.locator('#qp-week-value').innerText(), '27%');
  await frame.locator('#qp-scenario').selectOption('stale');
  assert.equal(await frame.locator('#qp-stale').isVisible(), true);
  assert.equal(await frame.locator('#qp-week-value').innerText(), '78%');
  await frame.locator('#qp-settings-button').click();
  await frame.locator('#qp-coupon-toggle').check();
  assert.equal(await frame.locator('#qp-coupons').isVisible(), true);
  await frame.locator('#qp-motion-toggle').uncheck();
  assert.equal(await frame.locator('#quota-orb-preview').getAttribute('data-motion'), 'off');

  for (const width of [1024, 736, 360, 320]) {
    await page.setViewportSize({ width, height: 820 });
    for (const scenario of ['weekly', 'both', 'low', 'stale']) {
      await frame.locator('#qp-scenario').selectOption(scenario);
      await ensureOpen();
      const fits = await detail.evaluate(el => {
        const stage = document.querySelector('.qp-workspace').getBoundingClientRect();
        const card = el.getBoundingClientRect();
        return card.left >= stage.left && card.right <= stage.right && card.top >= stage.top && card.bottom <= stage.bottom;
      });
      assert.equal(fits, true, `Card must fit at ${width}px (${scenario})`);
      const overflow = await frame.locator('#quota-orb-preview').evaluate(el => el.scrollWidth > el.clientWidth + 1);
      assert.equal(overflow, false, `No horizontal overflow at ${width}px`);
    }
  }
  await page.setViewportSize({ width: 1080, height: 820 });
  await frame.locator('#qp-scenario').selectOption('both');
  await frame.locator('#qp-coupon-toggle').uncheck();
  await frame.locator('#qp-settings-button').click();
  await ensureOpen();
  if (process.env.QUOTA_ORB_SCREENSHOT) {
    await page.screenshot({ path: process.env.QUOTA_ORB_SCREENSHOT });
  }
  assert.deepEqual(errors, []);
  await browser.close();
  console.log('PASS: 4 quota states, coupon/motion controls, and card boundaries at 320–1024px.');
})().catch(error => { console.error(error); process.exit(1); });
