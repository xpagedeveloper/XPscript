# Android UIForm-komponenter – cross-platform portnings-TODO

Detta dokument är inventeringen av de UIForm-komponenter och egenskaper som
för närvarande transporteras till Android. Gå igenom varje rad när samma
funktion ska implementeras på Windows, Linux, macOS, server-renderad web och
Browser-WASM.

## Gemensam portningschecklista

För varje komponent:

- [ ] Bekräfta att API:t är plattformsneutralt och dokumenterat.
- [ ] Implementera native Avalonia-stöd på Windows, Linux och macOS.
- [ ] Implementera server-web-stöd eller ett deterministiskt capability/error-svar.
- [ ] Implementera Browser-WASM-stöd eller ett deterministiskt capability/error-svar.
- [ ] Bevara värde-/state-binding, validering och action/event-semantik.
- [ ] Definiera keyboard, touch, focus, accessibility och responsive layout.
- [ ] Lägg till compiler-, runtime-, host- och executable sample-regression.
- [ ] Lägg till FullTest och dokumentation/API-metadata.

## Fält och input-kontroller

### TextField

- [ ] `AddTextField(name, label)`
- [ ] Textvärde, ändring, validering och binding.
- [ ] Placeholder, focus, keyboard/input mode och accessibility.

### TextArea

- [ ] `AddTextArea(name, label)`
- [ ] Multiline, wrapping, scrolling, ändring och binding.
- [ ] Keyboard, focus, sizing och accessibility.

### NumberField

- [ ] `AddNumberField(name, label)`
- [ ] Numerisk parsing, precision, min/max och invalid input.
- [ ] Locale, keyboard/input mode och binding.

### RangeField / Slider

- [ ] `AddRangeField(name, label)`.
- [x] `AddSlider(name, label)` med `Value` och validerad `Step`.
- [ ] Minimum, maximum, step, programmatisk uppdatering och change event.
- [ ] Keyboard/touch, tick/snap, focus och accessibility.
- [ ] Cross-platform runtime-regression; Android sample/regression finns.

### CheckBox / Switch

- [ ] `AddCheckBox(name, label)`.
- [x] `AddSwitch(name, label)` med bool-värde och Android-rendering.
- [ ] Shared binding, action-state, change event och disabled state.
- [ ] Keyboard/touch, focus och accessibility.
- [ ] Cross-platform runtime-regression; Android sample/regression finns.

### Select / ListBox / MultiListBox / RadioGroup

- [ ] `AddSelect`, `AddListBox`, `AddMultiListBox`, `AddRadioGroup`.
- [ ] `AddOption(name, value)` och options från data/binding.
- [ ] Single-/multi-selection, selected value, change event och empty state.
- [ ] Keyboard navigation, touch, focus, virtualization och accessibility.

### Date-, time- och färgfält

- [x] `AddDateField`.
- [x] `AddTimeField`.
- [x] `AddDateTimeField`.
- [x] `AddMonthField`.
- [x] `AddColorField`.
- [ ] Portera native picker-/editor-semantik till övriga plattformar.
- [ ] Definiera locale, timezone, format, keyboard och accessibility.

### Specialfält

- [ ] `AddEmailField` med email-validering och rätt input mode.
- [ ] `AddUrlField` med URL-validering och rätt input mode.
- [ ] `AddPasswordField` med masking, reveal-policy och accessibility.
- [ ] `AddHiddenField` utan visuellt editor-state men med binding/transport.

## Layout-, visual- och container-komponenter

### Icon

- [x] `AddIcon(name, icon[, label])` och Android built-in glyph fallback.
- [ ] Portabelt icon-set/fallback på desktop, web och WASM.
- [ ] Theme, high contrast, sizing, semantics och accessibility.

### Card / Panel

- [x] `AddCard(name[, label])`.
- [x] `AddPanel(name[, label])`.
- [ ] Nested child composition på alla plattformar.
- [ ] Padding, border, background, theme, visibility/enabled och responsive layout.

### ScrollView

- [x] `AddScrollView(name[, label])` med bounded Android-rendering.
- [ ] Nested child composition och regler mot obounded/nested scrolling.
- [ ] Desktop/web scrolling, keyboard, touch, focus och accessibility.

### Tab, Grid, Button och bildinnehåll

- [x] Tab- och named-grid-layout används av Android UIForm.
- [ ] Portera tab navigation, grid sizing och responsive behavior.
- [ ] Button icon/image content på alla plattformar.
- [ ] Native image rendering, fallback och source-policy.

## Data-driven kontroll

### ListView

- [x] `AddListView(name[, label])` och Android option-list rendering.
- [x] `AddOption`-baserad selection/change transport.
- [ ] XPJsonArray-/XPJsonObject-backed rows.
- [ ] Item template, item-click, selection och empty state.
- [ ] Virtualization, memory limits och cross-platform adapters.

## Media och rich content

### Image / XPImage

- [ ] UIForm Image på alla plattformar.
- [ ] `XPImage` som runtime source.
- [ ] `assets/...`, `data:image`, HTTP/HTTPS och lokala filer enligt platform policy.
- [ ] Loaded lifecycle, decode errors och read-only packaged assets.

### Audio

- [x] `AddAudio(name[, label])` och Android Media3-host.
- [x] `Source`, `Play`, `Pause`, `Stop`, `Position`, `Duration`, `Volume`, `AutoPlay`,
  `Loop`, `Muted`, `PlaybackRate` och playback events i shared API.
- [ ] Desktop playback backend.
- [ ] Server-web/WASM HTML audio backend.
- [ ] Background/resume, disposal, source policy och error semantics.

### Video

- [x] `AddVideo(name[, label])` och Android Media3/ExoPlayer-host.
- [x] `Source`-validering för assets och URI-scheman.
- [x] Playback properties/actions/events och Android compiler/CI regression.
- [ ] Desktop playback backend.
- [ ] Server-web/WASM HTML video backend.
- [ ] Source policy, fullscreen, resize/aspect ratio, lifecycle och errors.

### Carousel

- [x] `AddCarousel(name, sources...)` med Android ViewPager2-host.
- [x] Index, loop, auto-advance, indicators och source normalization.
- [ ] Desktop/web/WASM rendering.
- [ ] Swipe, mouse, keyboard, programmatic navigation och current-item event.

### WebView

- [ ] `AddWebView(name[, label])` på desktop, Android, server-web och WASM.
- [ ] URL/navigation policy, sandbox, permissions, lifecycle och unsupported behavior.

## Status- och aktivitetskontroller

### ProgressBar / ActivityIndicator

- [x] `AddProgressBar` med `Value` och `IsIndeterminate`.
- [x] `AddActivityIndicator` med `IsRunning`.
- [ ] Native rendering på övriga plattformar.
- [ ] Accessibility, animation policy, reduced motion och runtime regression.

## Device- och framtida rich-content-kontroller

### CameraPreview

- [x] `AddCameraPreview(name[, label])` och Android unavailable-state fallback.
- [ ] Android camera service, permissions, lifecycle, capture och privacy.
- [ ] Desktop capability fallback.
- [ ] Server-web/WASM permission/user-gesture behavior.

### Map

- [ ] `AddMap` med center, zoom, markers och marker-click event.
- [ ] Location/GPS abstraction utan Android-typer i shared API.
- [ ] Provider, license, network, privacy och unsupported-platform policy.

### DocumentViewer / PDF

- [ ] Portable source/API och security policy.
- [ ] Android, desktop, server-web och WASM rendering eller capability error.
- [ ] Navigation, zoom, lifecycle, download/cache och accessibility.

## Android-specifika leveranskontroller

- [x] Android compiler-generated host stödjer komponenterna ovan där de är markerade.
- [x] Android UIForm host stödjer Avalonia native controls där de är markerade.
- [x] Android CI bygger relevanta APK-regressioner.
- [ ] Kör hela regressionen på API 30 x86_64 emulator i GitHub Actions.
- [ ] Lägg till fysisk Android-telefon som valbar smoke-testmiljö.
- [ ] Dokumentera Media3, ViewPager2 och övriga tredjepartslicenser.
- [ ] Markera en komponent som klar först när API, implementation, policy,
  accessibility och regression finns på varje stödd plattform.
