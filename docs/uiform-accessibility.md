# UIForm accessibility

UIForm accessibility is enabled by default for desktop and browser rendering.

Field properties: `AccessibleName`, `AccessibleDescription`, `AccessibleHelpText`, `AccessibleLive`, `AccessibilityHidden`, `Focusable`, `IsTabStop`, `TabIndex`, `HasFocus`, `AccessKey`, `HotKey`.

Field function: `Focus()`.

Form API: `InitialFocus`, `FocusedField`, `Focus(name)`, `FocusFirst()`, `FocusFirstInvalid()`, `FocusNext()`, `FocusPrevious()`, `ValidationErrors`, `HasValidationErrors`, `SetValidationError`, `ClearValidationError`, `GetValidationErrors`, `ValidationSummary`, `FocusFirstError`, `AnnounceValidationErrors`, and `Announce(message[, priority])`.

Desktop maps metadata to Avalonia Automation properties, native focus/tab navigation and `DataValidationErrors`. Browser rendering emits native labels, `aria-invalid`, `aria-describedby`, `aria-live`, tabindex and autofocus metadata.

Built-in labels remain the accessible-name default. Use `AccessibleName` only when the visible label is insufficient.
