const { expect, test } = require('playwright/test');

test('UIForm custom dialog result includes submitted values', async ({ page }) => {
  await page.goto('/form.xps');
  await expect(page.getByRole('heading', { name: 'Contact form' })).toBeVisible();

  await page.getByLabel('Existing').fill('Changed in browser');
  await page.getByLabel('Missing').fill('Created in browser');

  const responsePromise = page.waitForResponse((response) =>
    response.request().method() === 'POST' && response.url().endsWith('/form.xps'),
  );
  await page.getByRole('button', { name: 'Save' }).click();
  const response = await responsePromise;

  expect(response.status()).toBe(200);
  expect(await response.json()).toMatchObject({
    existing: 'Changed in browser',
    missing: 'Created in browser',
    dialogResult: 'save',
  });
});
