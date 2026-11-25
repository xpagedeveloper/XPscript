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

  await page.locator('#dialog-title-target').selectOption('message');
  await page.locator('#dialog-title-value').fill('Custom message title');
  await page.getByRole('button', { name: 'Apply title' }).click();

  const messageDialog = page.getByRole('dialog', { name: 'Custom message title' });
  await messageButton.click();
  await expect(messageDialog).toBeVisible();
  await expect(messageDialog).toHaveClass(/bg-body/);
  await expect(messageDialog).toHaveCSS('background-color', 'rgb(255, 255, 255)');
  await expect(messageDialog).toHaveCSS('padding', '24px');
  await expect(messageDialog).toHaveCSS('min-height', '192px');
  await expect(messageDialog.getByRole('button', { name: 'OK' })).toHaveClass(/btn-primary/);
  await messageDialog.getByRole('button', { name: 'OK' }).click();
  await expect(messageDialog).not.toBeVisible();
  await expect(page.locator('#dialog-result')).toHaveText('Message: closed');

  await page.locator('#dialog-title-target').selectOption('choice');
  await page.locator('#dialog-title-value').fill('Deployment decision');
  await page.getByRole('button', { name: 'Apply title' }).click();
  await page.locator('#choice-label-1').fill('Approve');
  await page.locator('#choice-value-1').fill('accepted');
  await page.locator('#choice-label-2').fill('Request changes');
  await page.locator('#choice-value-2').fill('changes_requested');
  await page.getByRole('button', { name: 'Apply choice buttons' }).click();

  const choiceDialog = page.getByRole('dialog', { name: 'Deployment decision' });
  await choiceButton.click();
  await expect(choiceDialog).toBeVisible();
  await expect(choiceDialog.locator('#choice-actions')).toHaveCSS('gap', '8px');
  await expect(choiceDialog.getByRole('button')).toHaveCount(3);
  await choiceDialog.getByRole('button', { name: 'Approve' }).click();
  await expect(choiceDialog).not.toBeVisible();
  await expect(page.locator('#dialog-result')).toHaveText('Choice: accepted');
  await choiceButton.click();
  await expect(choiceDialog).toBeVisible();
  await choiceDialog.getByRole('button', { name: 'Request changes' }).click();
  await expect(page.locator('#dialog-result')).toHaveText('Choice: changes_requested');

  const inputDialog = page.getByRole('dialog', { name: 'Enter a short value' });
  await page.locator('#input-label-setting').fill('Your name');
  await page.locator('#input-button-setting').fill('Save answer');
  await page.getByRole('button', { name: 'Apply input settings' }).click();
  await inputButton.click();
  await expect(inputDialog).toBeVisible();
  await expect(inputDialog.locator('#input-dialog-label')).toHaveText('Your name');
  await expect(inputDialog.getByRole('button', { name: 'Save answer' })).toBeVisible();
  await page.locator('#input-dialog-value').fill('typed from Playwright');
  await page.getByRole('button', { name: 'Save answer' }).click();
  await expect(inputDialog).not.toBeVisible();
  await expect(page.locator('#dialog-result')).toHaveText('Your name: typed from Playwright');

  await page.locator('#input-kind-setting').selectOption('combo');
  await page.locator('#input-label-setting').fill('Favorite color');
  await page.locator('#input-button-setting').fill('Choose color');
  await page.locator('#input-options-setting').fill('Red,Green,Blue');
  await page.getByRole('button', { name: 'Apply input settings' }).click();
  await inputButton.click();
  await page.locator('#input-dialog-value').selectOption('Green');
  await page.getByRole('button', { name: 'Choose color' }).click();
  await expect(page.locator('#dialog-result')).toHaveText('Favorite color: Green');

  await page.locator('#input-kind-setting').selectOption('multi');
  await page.locator('#input-label-setting').fill('Favorite colors');
  await page.locator('#input-button-setting').fill('Save colors');
  await page.getByRole('button', { name: 'Apply input settings' }).click();
  await inputButton.click();
  await page.locator('#input-dialog-value').selectOption(['Red', 'Blue']);
  await page.getByRole('button', { name: 'Save colors' }).click();
  await expect(page.locator('#dialog-result')).toHaveText('Favorite colors: Red, Blue');

  await page.locator('#toast-duration').fill('1');
  for (const position of ['top-left', 'top-right', 'bottom-left', 'bottom-right']) {
    await page.locator('#toast-message').fill(`Stacked toast at ${position}`);
    await page.locator('#toast-type').selectOption('normal');
    await page.locator('#toast-position').selectOption(position);
    await page.getByRole('button', { name: 'Show toast' }).click();
    await page.getByRole('button', { name: 'Show toast' }).click();
    const toasts = page.locator(`#toast-${position} [role="status"]`);
    await expect(toasts).toHaveCount(2);
    await expect(toasts.first()).toContainText(position);
    await expect(toasts.first().locator('.toast-header')).toHaveText('Message');
    await expect(toasts.first().locator('.toast-body')).toContainText(`Stacked toast at ${position}`);
    await expect(toasts.first()).toHaveClass(/bg-body/);
  }
  for (const position of ['top-left', 'top-right', 'bottom-left', 'bottom-right']) {
    await expect(page.locator(`#toast-${position} [role="status"]`)).toHaveCount(0, { timeout: 3000 });
  }
  await page.locator('#toast-message').fill('Low disk space');
  await page.locator('#toast-type').selectOption('warning');
  await page.locator('#toast-position').selectOption('bottom-right');
  await page.getByRole('button', { name: 'Show toast' }).click();
  const warningToast = page.locator('#toast-bottom-right [role="alert"]');
  await expect(warningToast).toContainText('Low disk space');
  await expect(warningToast.locator('.toast-header strong')).toHaveClass(/text-danger/);
  await expect(warningToast.locator('.toast-body')).toHaveClass(/text-danger/);
  await expect(warningToast).toHaveCount(0, { timeout: 3000 });

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
