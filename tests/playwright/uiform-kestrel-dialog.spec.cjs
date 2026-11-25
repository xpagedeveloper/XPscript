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

test('manual UIForm shows working browser dialogs and validates remote lookups', async ({ page }) => {
  await page.goto('/manual.xps');
  await expect(page.getByRole('heading', { name: 'UIForm manual test bench' })).toBeVisible();

  const messageButton = page.getByRole('button', { name: 'Message (alert)' });
  const choiceButton = page.getByRole('button', { name: 'Choice (confirm)' });
  const inputButton = page.getByRole('button', { name: 'Input (prompt)' });
  await expect(messageButton).toBeVisible();
  await expect(choiceButton).toBeVisible();
  await expect(inputButton).toBeVisible();

  const messageDialogPromise = page.waitForEvent('dialog');
  const messageClick = messageButton.click();
  const messageDialog = await messageDialogPromise;
  expect(messageDialog.type()).toBe('alert');
  await messageDialog.accept();
  await messageClick;

  const choiceDialogPromise = page.waitForEvent('dialog');
  const choiceClick = choiceButton.click();
  const choiceDialog = await choiceDialogPromise;
  expect(choiceDialog.type()).toBe('confirm');
  await choiceDialog.accept();
  await choiceClick;
  await expect(page.locator('#dialog-result')).toHaveText('Choice: Yes');

  const inputDialogPromise = page.waitForEvent('dialog');
  const inputClick = inputButton.click();
  const inputDialog = await inputDialogPromise;
  expect(inputDialog.type()).toBe('prompt');
  await inputDialog.accept('typed from Playwright');
  await inputClick;
  await expect(page.locator('#dialog-result')).toHaveText('Input: typed from Playwright');

  const search = page.getByPlaceholder('Search...');
  await search.fill('al');
  await expect.poll(async () => page.locator('#xps_customerId option').allTextContents()).toContain('Alex Morgan');
  await page.locator('#xps_customerId').selectOption({ label: 'Alex Morgan' });

  const lookupSelect = page.locator('#xps_customerId');
  await lookupSelect.evaluate((select) => {
    const option = document.createElement('option');
    option.value = 'untrusted-id';
    option.textContent = 'Untrusted value';
    select.append(option);
  });
  await lookupSelect.selectOption('untrusted-id');
  const rejectedPost = page.waitForResponse((response) =>
    response.request().method() === 'POST' && response.url().endsWith('/manual.xps'),
  );
  await page.getByRole('button', { name: 'Save form' }).click();
  expect((await rejectedPost).status()).toBeGreaterThanOrEqual(400);
});
