# Browser tests

Run from the repository root with an installed Playwright Test CLI and Chrome,
Edge, or a Playwright-managed Chromium browser:

```powershell
playwright test --list
playwright test
```

The root `playwright.config.cjs` starts the local Kestrel UIForm fixture. Tests
verify submitted form values and custom result buttons, Bootstrap-styled inline
message/choice/input dialogs, configurable dialog titles, choice labels and
return values, stacked toast notifications in all four corners with automatic
expiry, remote lookup typeahead, and server-side rejection of forged lookup
values. Open `/manual.xps` on the fixture server to try the controls yourself.
Set `PLAYWRIGHT_CHROMIUM_EXECUTABLE` to use a browser executable at a nonstandard
path. `XPSCRIPT_PLAYWRIGHT_PORT` changes the fixture port when the default port
`4187` is already in use.
