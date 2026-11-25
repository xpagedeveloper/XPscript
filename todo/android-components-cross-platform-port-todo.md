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

## UIForm dialog-resultat och fristående dialoger

- [x] UIForm custom-knappar returnerar knappnamnet som `ShowDialog()`-resultat
  på desktop, Android, server-renderad web och Browser-WASM. Android och desktop
  tillämpar inmatade fältvärden innan resultatet återgår. Browser-WASM publicerar
  resultatet i `xpscript:form-result` efter att knappens event-handler körts.
- [ ] Lägg till host-interaktionstest för Android och desktop som verifierar
  resultat, handler och submitted values tillsammans.
- [ ] Lägg till Browser-WASM regression som klickar en knapp med ett eget
  `result` och verifierar form-result-eventet efter handlern.
- [x] Skärp server-web Kestrel-regressionen så den verifierar att POST-svaret
  innehåller exakt `save`-resultat tillsammans med fältvärdena.
- [ ] `ShowDialog(message, ...)`, `MsgBox` och filväljare använder fortfarande
  desktop-dialoghosten; desktop choice dialogs are now owner-modal and block
  until the user chooses. Android UIForm navigation now pushes the visible
  screen and restores that exact screen when the dialog closes, including in
  generated Android hosts.
- [ ] Route standalone `ShowDialog(message, ...)` and `MsgBox` to platform hosts:
  server-web and Browser-WASM should render the same inline dialog controls,
  while Android should present a dialog screen and restore the prior screen.
- [x] Add shared nonblocking `ShowToast` API and platform hosts. WebUI,
  Browser-WASM and Android overlay toasts on the active form; desktop creates
  a separate borderless window for each toast. Every host supports four-corner
  placement, auto-expiry, and an accessible close button.
- [x] Apply the active UIForm background color to the toast surface on every
  platform. Added runtime/UIForm background propagation to desktop, Android,
  server-rendered web and Browser-WASM renderers.
- [ ] Extend Browser-WASM dialog parity coverage beyond UIForm. Current UIForm
  modal rendering is inline in the browser; standalone dialog API parity is
  still open.

### Dialog platform behavior update 2026-11-25

- [x] Desktop standalone choice dialogs now use Avalonia's owner-modal window
  API and wait for the modal result.
- [x] Android form screens now use a push/pop stack instead of rebuilding the
  home view on close, so returning from a dialog restores the prior screen.
- [x] Android Platform FullTest compiler probe checks both reusable and
  generated Android hosts for push/pop restoration.
- [ ] Validate Android navigation on an emulator; local Android API/workload
  UI interaction remains unverified locally.
- [ ] Implement the standalone dialog and nonblocking toast host APIs across
  desktop, server-web, Browser-WASM and Android. (The toast API is complete;
  standalone message/choice/input dialog parity remains.)
- [x] Add a close X and active-form background inheritance to every toast host;
  Playwright now verifies immediate dismissal and matching form/toast colors.
- [x] Run Browser-WASM toast interaction in Chromium and verify the close X
  and form background color on the rendered application.

### CI-uppföljning 2026-11-25

- [x] Åtgärda AST-kompilering för UIListView utan UIForm. Den gemensamma
  UIExtension-runtime har nu valfri reflektionsbaserad event-dispatch, så
  UIListView-only Kestrel-appar inte kräver UIForm:s dispatcher.
- [x] Gör desktop AST-livscykelregressionen oberoende av antalet knappar
  genom att välja Avalonia-hostens dedikerade `Close`-knapp.
- [ ] Android AST-körningarna i Platform FullTest och Android UIForm AST
  Emulator valde .NET SDK 11 för det genererade Android-projektet trots att
  jobbet och kompilatorn använder .NET 10. Genererade byggen sätter nu
  `MSBuildSDKsPath` och `MSBUILD_EXE_PATH` till den valda SDK-versionen. Verifiera
  i nästa Android CI-körning om NETSDK1147 är borta; inga paket installerades
  lokalt.

### Lokal verifiering 2026-11-25

- [x] Windows headless AST UIForm-livscykelregression passerar.
- [x] Kestrel UIForm-resultat och UIListView-regressioner passerar.
- [x] Lägg till en upptäckbar Playwright Test-regression för server-renderad
  UIForm. Testet klickar på en egen dialogknapp i Chrome och verifierar både
  `save`-resultatet och de skickade fältvärdena.
- [x] Utöka Playwright-regressionen med inline message-, choice- och
  input-dialoger (text, combo box och listbox multi-select), med egna labels,
  actionknappar, titlar och choice-returvärden. Toasts stöder egen text, normal-
  och warning-typ (röd), stapling i fyra hörn och automatisk timeout.
  Webbdialogerna använder
  Bootstrap body-/textfärger, kortyta, padding och knappgap som server-renderad
  UIForm, med responsiv minsta storlek. Testet verifierar också remote lookup typeahead och att
  manipulerat lookup-ID avvisas. Detta är web-fixturens UI; XPScripts fristående
  `MsgBox`/`ShowDialog`-backend för webben återstår.
- [x] Förvärm manuell Kestrel-fixturs form och lookup-endpoint före READY och
  använd stabilt site-root så persistent compiler-cache återanvänds vid restart.
- [ ] Lokal Android-kompilering hittar Android-workloaden och emulatorn, men
  genererat projekt kräver Android API 36.1. Den lokala SDK:n har API 37.0 och
  API 30; Android-workload eller plattformspaket installerades inte.
- [x] Browser-WASM smokeprojekten har återställda NuGet assets (`--no-restore`
  används för efterföljande körningar; inga paket installerades).
- [x] `playwright test --list` hittar nu UIForm-testfilen. Lokal körning av
  Playwright-regressionerna passerar för custom result, browser dialogs,
  remote lookup typeahead och server-side avvisning av ogiltigt lookup-ID.

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
- [x] Android AST-host använder `NumericUpDown` för Number/Decimal/Currency med min/max/step, initialvärde och change-event.
- [ ] Numerisk parsing, precision, min/max och invalid input.
- [ ] Locale, keyboard/input mode och binding.

### RangeField / Slider

- [ ] `AddRangeField(name, label)`.
- [x] `AddSlider(name, label)` med `Value` och validerad `Step`.
- [ ] Minimum, maximum, step, programmatisk uppdatering och change event.
- [ ] Keyboard/touch, tick/snap, focus och accessibility.
- [x] Cross-platform runtime-regression; Android sample/regression finns och
  desktop/Browser-WASM renderar Slider/RangeField.

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

### ColorField picker-strategi

- [x] Använd plattformens native color picker när den finns och har stabil API:
  server-web/Browser-WASM använder HTML `input type="color"`.
- [ ] Använd Androids native picker när en kompatibel Android/Avalonia-host
  finns tillgänglig och kan returnera `#RRGGBB` med change-event.
- [ ] Använd desktop-native picker när den valda Avalonia-versionen erbjuder
  en kompatibel kontroll.
- [x] Behåll shared `XPImage.CreateColorMap(width, height)` som fallback för
  hosts utan native picker.
- [x] Öppna fallback-kartan i en större host-overlay och mappa vald ruta till
  `#RRGGBB`; Android använder shared `XPImage.ColorMapColorAt` och desktop
  använder samma färgkartssemantik.

### Specialfält

- [ ] `AddEmailField` med email-validering och rätt input mode.
- [ ] `AddUrlField` med URL-validering och rätt input mode.
- [ ] `AddPasswordField` med masking, reveal-policy och accessibility.
- [ ] `AddHiddenField` utan visuellt editor-state men med binding/transport.
- [x] Android AST-host renderar WeekField som ISO-år/vecka-editor och bevarar HiddenField vid submit.

## Layout-, visual- och container-komponenter

### Icon

- [x] `AddIcon(name, icon[, label])` och Android built-in glyph fallback.
- [x] Portabelt glyph/fallback på desktop, server-web och Browser-WASM.
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

- [>] Desktop UIForm Image stöder lokala filer, `assets/...`, `data:image` och
  HTTP/HTTPS; Android/web har befintliga adapters och övriga plattformar återstår.
- [x] Gemensam kompilatorregel för alla plattformar: literal lokala
  Image/Audio/Video-källor stageas vid kompilering till programmets `assets/`;
  samma källa återanvänder asset-filen, medan en större eller nyare källfil
  ersätter den.
- [ ] `XPImage` som runtime source.
- [ ] `assets/...`, `data:image`, HTTP/HTTPS och lokala filer enligt platform policy.
- [ ] Loaded lifecycle, decode errors och read-only packaged assets.

### Audio

- [x] `AddAudio(name[, label])` och Android Media3-host.
- [x] `Source`, `Play`, `Pause`, `Stop`, `Position`, `Duration`, `Volume`, `AutoPlay`,
  `Loop`, `Muted`, `PlaybackRate` och playback events i shared API.
- [>] Desktop host renders Audio/Video through the existing Avalonia WebView
  backend on Windows/Linux/macOS; native media commands and lifecycle remain.
- [ ] Evaluate LibVLCSharp (LGPL-2.1-or-later; Windows/Linux/macOS support) as
  the simplest complete player backend.
- [ ] Evaluate GStreamer (LGPL; native multiplatform pipeline) when packaging
  and deployment control is more important than a small integration.
- [ ] Evaluate FFMediaToolkit (MIT wrapper), remembering that its separately
  supplied FFmpeg binaries/codecs retain their own LGPL/GPL licensing.
- [ ] Select and document one backend, native library packaging, codec policy,
  notices and license obligations before implementing playback.
- [ ] Optimize for the smallest deployment: prefer Windows Media Foundation,
  macOS AVFoundation and the installed Linux media service before bundling a
  full player runtime.
- [ ] Keep the shared `DesktopMediaAdapter` platform-neutral while selecting
  native backends per OS: Media Foundation on Windows, AVFoundation on macOS
  and system GStreamer on Linux.
- [ ] Measure published size in MB for each backend and record the result,
  including native dependencies, codecs, notices and optional feature packs.
- [ ] Use LibVLC/FFmpeg/GStreamer bundled runtimes only when native APIs cannot
  satisfy the required formats or playback features.
- [x] Server-web/WASM grundrendering med HTML `<audio>`.
- [ ] Background/resume, disposal, source policy och error semantics.

### Video

- [x] `AddVideo(name[, label])` och Android Media3/ExoPlayer-host.
- [x] `Source`-validering för assets och URI-scheman.
- [x] Playback properties/actions/events och Android compiler/CI regression.
- [>] Desktop playback rendering via the existing Avalonia WebView backend;
  native media commands and lifecycle remain.
- [ ] Keep the selected backend behind the desktop media adapter; do not expose
  LibVLC, GStreamer or FFmpeg types through shared XPScript UIForm APIs.
- [x] Server-web/WASM grundrendering med HTML `<video>`.
- [ ] Source policy, fullscreen, resize/aspect ratio, lifecycle och errors.

### Carousel

- [x] `AddCarousel(name, sources...)` med Android ViewPager2-host.
- [x] Index, loop, auto-advance, indicators och source normalization.
- [>] Desktop rendering via the existing WebView bridge and server-web HTML rendering;
  Browser-WASM rendering and runtime current-item event remain.
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
- [x] Desktop file-picker fallback for image capture.
- [x] Server-web/WASM HTML camera/upload input with `accept="image/*"` and
  `capture="environment"`.

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
