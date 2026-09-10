# XPscript debugger include test

This sample verifies debugger navigation across `Include` source files.

## Files

- `root.xps` is the main program.
- `lib/helper.xps` contains `HelperValue()`.

## Run locally

Open `samples/debugger-include/root.xps` in VS Code with the XPscript extension installed and start **Debug Current File**.

Set a line breakpoint on:

```xpscript
value = HelperValue()
```

Then test the debugger controls:

- **Step Over**: execution should remain in `root.xps` and stop on the following `Print` line. `lib/helper.xps` should not be opened just because the function executes.
- **Step Into**: VS Code should open `lib/helper.xps` and stop on the first executable line inside `HelperValue()`.
- **Step Out**: execution should return to `root.xps`, and VS Code should switch back to the caller source file.

You can also set a normal or conditional breakpoint directly inside `lib/helper.xps`. When that breakpoint is hit, VS Code should open the included source file automatically.

## CLI smoke test

From the repository root:

```powershell
xpscript run .\samples\debugger-include\root.xps
```

Expected program output:

```text
VALUE=42
```
