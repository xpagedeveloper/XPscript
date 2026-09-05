# UIForm accessibility

UIForm includes its accessibility, focus and validation runtime as part of the standard UIForm surface on desktop, server-rendered web and browser WebAssembly. This keeps one consistent metadata contract for ordinary fields, media controls, validation and focus behavior across all UIForm hosts.

Field properties: `AccessibleName`, `AccessibleDescription`, `AccessibleHelpText`, `AccessibleLive`, `AccessibilityHidden`, `Focusable`, `IsTabStop`, `TabIndex`, `HasFocus`, `AccessKey`, `HotKey`.

Field function: `Focus()`.

Form API: `InitialFocus`, `FocusedField`, `Focus(name)`, `FocusFirst()`, `FocusFirstInvalid()`, `FocusNext()`, `FocusPrevious()`, `ValidationErrors`, `HasValidationErrors`, `SetValidationError`, `ClearValidationError`, `GetValidationErrors`, `ValidationSummary`, `FocusFirstError`, `AnnounceValidationErrors`, and `Announce(message[, priority])`.

Desktop maps metadata to Avalonia Automation properties, native focus/tab navigation and `DataValidationErrors`.

Server-rendered web maps the same model to HTML accessibility primitives:

- visible field labels use native `label for` associations
- `AccessibleName` becomes `aria-label`
- `AccessibleDescription` and `AccessibleHelpText` are emitted as associated help text through `aria-describedby`
- validation errors use `aria-invalid`, per-field alert text and an optional linked validation summary
- `ValidationSummary` renders a summary with links to each invalid field
- `FocusFirstError` makes the first invalid focusable field the initial focus target when no explicit `InitialFocus` is set
- `AnnounceValidationErrors` controls whether the summary is exposed as an assertive live alert
- `AccessibleLive` and `Announce()` map to ARIA live regions
- `AccessibilityHidden`, `Focusable`, `IsTabStop`, `TabIndex`, `InitialFocus` and `AccessKey` are reflected in HTML focus/accessibility attributes
- built-in OK/Cancel controls are grouped as form actions
- `Image` uses its alt text and is non-focusable by default
- `WebView` uses an accessible iframe title and participates in the configured focus order

Browser WebAssembly uses the same metadata contract in the client-side renderer.

Built-in labels remain the accessible-name default. Use `AccessibleName` only when the visible label is insufficient. For `Image`, provide meaningful alt text when the image conveys information; use an empty alt text for decorative images.
