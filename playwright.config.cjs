const fs = require('node:fs');
const Module = require('node:module');
const path = require('node:path');

// Allow the globally installed Playwright package to provide the test API when
// this checkout does not have a local node_modules directory.
const globalModuleRoots = [
  process.env.APPDATA && path.join(process.env.APPDATA, 'npm', 'node_modules'),
  process.env.NPM_CONFIG_PREFIX && path.join(process.env.NPM_CONFIG_PREFIX, 'node_modules'),
  process.env.HOME && path.join(process.env.HOME, '.npm-global', 'lib', 'node_modules'),
  '/usr/local/lib/node_modules',
  '/usr/lib/node_modules',
].filter(Boolean);
const playwrightRoot = globalModuleRoots.find((root) =>
  fs.existsSync(path.join(root, 'playwright', 'package.json')) ||
  fs.existsSync(path.join(root, '@playwright', 'test', 'package.json')),
);
if (playwrightRoot) {
  process.env.NODE_PATH = [playwrightRoot, process.env.NODE_PATH].filter(Boolean).join(path.delimiter);
  Module._initPaths();
}

const port = Number(process.env.XPSCRIPT_PLAYWRIGHT_PORT || 4187);
const browserCandidates = [
  process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE,
  process.env.ProgramFiles && path.join(process.env.ProgramFiles, 'Google', 'Chrome', 'Application', 'chrome.exe'),
  process.env['ProgramFiles(x86)'] && path.join(process.env['ProgramFiles(x86)'], 'Google', 'Chrome', 'Application', 'chrome.exe'),
  process.env.ProgramFiles && path.join(process.env.ProgramFiles, 'Microsoft', 'Edge', 'Application', 'msedge.exe'),
  process.env['ProgramFiles(x86)'] && path.join(process.env['ProgramFiles(x86)'], 'Microsoft', 'Edge', 'Application', 'msedge.exe'),
  '/usr/bin/google-chrome-stable',
  '/usr/bin/google-chrome',
  '/usr/bin/chromium',
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
].filter(Boolean);
const systemChromium = browserCandidates.find((candidate) => fs.existsSync(candidate));

module.exports = {
  testDir: './tests/playwright',
  testMatch: '**/*.spec.cjs',
  reporter: 'list',
  timeout: 30_000,
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    headless: true,
    launchOptions: systemChromium ? { executablePath: systemChromium } : {},
  },
  webServer: {
    command: 'dotnet run --project tests/WebUIFormKestrelSmoke/WebUIFormKestrelSmoke.csproj --configuration Release --no-restore -- --serve',
    url: `http://127.0.0.1:${port}/form.xps`,
    env: { XPSCRIPT_WEBUIFORM_SMOKE_PORT: String(port) },
    timeout: 120_000,
    reuseExistingServer: !process.env.CI,
  },
};
