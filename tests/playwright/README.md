# Browser tests

Run from the repository root with an installed Playwright Test CLI and Chrome,
Edge, or a Playwright-managed Chromium browser:

```powershell
playwright test --list
playwright test
```

The root `playwright.config.cjs` starts the local Kestrel UIForm fixture. The
test verifies that clicking a custom dialog button returns its result together
with values entered in the browser. Set `PLAYWRIGHT_CHROMIUM_EXECUTABLE` to use
a browser executable at a nonstandard path. `XPSCRIPT_PLAYWRIGHT_PORT` changes
the fixture port when the default port `4187` is already in use.
