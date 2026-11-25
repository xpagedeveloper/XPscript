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

  const messageButton = page.getByRole('button', { name: 'Message dialog' });
  const choiceButton = page.getByRole('button', { name: 'Choice dialog' });
  const inputButton = page.getByRole('button', { name: 'Input dialog' });
  await expect(messageButton).toBeVisible();
  await expect(choiceButton).toBeVisible();
  await expect(inputButton).toBeVisible();

  await page.locator('#dialog-style-target').selectOption('message');
  await page.locator('#dialog-title-value').fill('Styled message');
  await page.locator('#dialog-override-background').check();
  await page.locator('#dialog-background').fill('#123456');
  await page.locator('#dialog-font-family').fill('Georgia');
  await page.locator('#dialog-override-font-color').check();
  await page.locator('#dialog-font-color').fill('#ffeecc');
  await page.locator('#dialog-bold').check();
  await page.getByRole('button', { name: 'Apply dialog settings' }).click();
  await expect(page.locator('#dialog-style-status')).toHaveText('Updated message dialog');

  const messageDialog = page.getByRole('dialog', { name: 'Styled message' });
  await messageButton.click();
  await expect(messageDialog).toBeVisible();
  await expect(messageDialog).toHaveCSS('background-color', 'rgb(18, 52, 86)');
  await expect(messageDialog).toHaveCSS('color', 'rgb(255, 238, 204)');
  await expect(messageDialog).toHaveCSS('font-family', /Georgia/);
  await expect(messageDialog).toHaveCSS('font-weight', '700');
  await messageDialog.getByRole('button', { name: 'OK' }).click();
  await expect(messageDialog).not.toBeVisible();
  await expect(page.locator('#dialog-result')).toHaveText('Message: closed');

  await page.locator('#dialog-style-target').selectOption('choice');
  await page.locator('#dialog-title-value').fill('Deployment decision');
  await page.getByRole('button', { name: 'Apply dialog settings' }).click();
  await page.locator('#choice-label-1').fill('Approve');
  await page.locator('#choice-value-1').fill('accepted');
  await page.locator('#choice-label-2').fill('Request changes');
  await page.locator('#choice-value-2').fill('changes_requested');
  await page.getByRole('button', { name: 'Apply choice buttons' }).click();

  const choiceDialog = page.getByRole('dialog', { name: 'Deployment decision' });
  await choiceButton.click();
  await expect(choiceDialog).toBeVisible();
  await expect(choiceDialog.getByRole('button')).toHaveCount(3);
  await choiceDialog.getByRole('button', { name: 'Approve' }).click();
  await expect(choiceDialog).not.toBeVisible();
  await expect(page.locator('#dialog-result')).toHaveText('Choice: accepted');
  await choiceButton.click();
  await expect(choiceDialog).toBeVisible();
  await choiceDialog.getByRole('button', { name: 'Request changes' }).click();
  await expect(page.locator('#dialog-result')).toHaveText('Choice: changes_requested');

  await inputButton.click();
  const inputDialog = page.getByRole('dialog', { name: 'Enter a short value' });
  await expect(inputDialog).toBeVisible();
  await page.locator('#input-dialog-value').fill('typed from Playwright');
  await page.getByRole('button', { name: 'Use value' }).click();
  await expect(inputDialog).not.toBeVisible();
  await expect(page.locator('#dialog-result')).toHaveText('Input: typed from Playwright');

  await page.locator('#toast-duration').fill('1');
  for (const position of ['top-left', 'top-right', 'bottom-left', 'bottom-right']) {
    await page.locator('#toast-position').selectOption(position);
    await page.getByRole('button', { name: 'Show toast' }).click();
    await page.getByRole('button', { name: 'Show toast' }).click();
    const toasts = page.locator(`#toast-${position} [role="status"]`);
    await expect(toasts).toHaveCount(2);
    await expect(toasts.first()).toContainText(position);
  }
  for (const position of ['top-left', 'top-right', 'bottom-left', 'bottom-right']) {
    await expect(page.locator(`#toast-${position} [role="status"]`)).toHaveCount(0, { timeout: 3000 });
  }

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
