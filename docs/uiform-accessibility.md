# UIForm accessibility

UIForm keeps baseline accessibility enabled for desktop and browser rendering. Native labels and control semantics remain available without enabling the extended accessibility API.

The generated accessibility/focus/validation runtime is feature-gated inside the UIForm pipeline. It is emitted only when the script references one of the extended accessibility members. This optimization is local to UIForm and does not change any general compiler optimization behavior.

Field properties: `AccessibleName`, `AccessibleDescription`, `AccessibleHelpText`, `AccessibleLive`, `AccessibilityHidden`, `Focusable`, `IsTabStop`, `TabIndex`, `HasFocus`, `AccessKey`, `HotKey`.

Field function: `Focus()`.

Form API: `InitialFocus`, `FocusedField`, `Focus(name)`, `FocusFirst()`, `FocusFirstInvalid()`, `FocusNext()`, `FocusPrevious()`, `ValidationErrors`, `HasValidationErrors`, `SetValidationError`, `ClearValidationError`, `GetValidationErrors`, `ValidationSummary`, `FocusFirstError`, `AnnounceValidationErrors`, and `Announce(message[, priority])`.

Desktop maps metadata to Avalonia Automation properties, native focus/tab navigation and `DataValidationErrors`. Browser rendering emits native labels, `aria-invalid`, `aria-describedby`, `aria-live`, tabindex and autofocus metadata when the extended API is used.

Built-in labels remain the accessible-name default. Use `AccessibleName` only when the visible label is insufficient.
