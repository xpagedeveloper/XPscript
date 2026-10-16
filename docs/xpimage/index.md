# XPImage processing

AST scalar Optional ByRef constructors give omitted arguments fresh lifetime-safe storage and preserve supplied aliases through defaults and Parent.New. Image-owning default routines clean their locals on success/error; base/derived body failures clear completed image fields without user Delete or replacing the old target. See [Optional ByRef constructor regression](../../tests/ast-compile-probe/image-constructor-optional-byref.xps), including errors in both child and base defaults. This does not add image/reference constructor parameter signatures or GC cleanup.

Experimental AST constructors can mix required scalar ByRef with trailing Optional ByVal. Image-owning default routines retain normal scope cleanup; reference mutations survive default/base/derived failures. Failed construction preserves the old target, clears completed image fields and never invokes user Delete. See [mixed-constructor lifetime regression](../../tests/ast-compile-probe/image-constructor-mixed.xps). successful objects still require explicit field cleanup or Delete.

Experimental AST image setters use snapshot-owned storage: writing a typed XPImage class field clones the incoming image. Replacing or clearing that field recycles its old image; failed cloning leaves the old value intact. ByVal setter parameters clone at entry. ByRef setter parameters borrow the supplied reference: replacing the parameter cannot replace or recycle the caller's variable, but explicit image mutations or Recycle still affect it. Storing either parameter in a typed field creates an independent copy. Nothing clears storage. Release image fields with `Set field = Nothing` or explicit Delete of their owner; GC/scope-exit cleanup is not implemented. Variant fields do not establish image ownership.

XPImage is XPScript's cross-platform raster image object. It uses Magick.NET internally while keeping the public API independent of ImageMagick types. The regression example for the API is [xpimage-runtime.xps](../../samples/xpimage-runtime.xps).

## AST compilation

Required scalar ByRef constructors preserve caller writes even if a base body, derived body or later base argument raises an error. The existing image procedure scopes clean locals, and the non-virtual failed-construction chain clears completed ancestor fields without running user Delete. A failed assignment still preserves the old object and its field images. See [ByRef constructor failure regression](../../tests/ast-compile-probe/image-constructor-byref.xps). This adds no XPImage/reference constructor parameter support or GC ownership; the references in this subset address scalar storage only.

Optional scalar ByVal constructor defaults can call image-owning module routines. Those routines retain normal typed-local cleanup on return/error. Defaults run before constructor bodies; if a default throws, that body is never entered and an existing assignment target survives. This includes defaults of an implicitly called all-Optional base constructor. A later derived-body failure still clears completed ancestor image fields without user Delete. See [Optional constructor ownership regression](../../tests/ast-compile-probe/image-constructor-optional.xps). No image/reference constructor parameters, GC cleanup or extra ownership engine is introduced.

Derived constructors can forward scalar ByVal arguments with a first-statement `Call Parent.New(...)`. If a derived body throws after base construction, the non-virtual failed-construction chain clears that class's fields and all completed ancestor fields, using the same StoreField helper; it never runs user Delete. Each body's local image scope also closes. A failure during base argument evaluation prevents that base body from running. Successful construction retains all stored field snapshots, and a failed assignment preserves the caller's old instance. Variant observation aliases are not owners. See [three-level constructor failure regression](../../tests/ast-compile-probe/image-constructor-derived.xps); this does not introduce finalizers or scope-exit object cleanup.

Root-class `Sub New` bodies use ordinary image procedure scopes: locals are cleaned on exit and successfully stored typed image fields survive construction. If construction throws, its typed image fields are cleared/recycled before propagation, without running user Delete. Existing assignment targets remain unchanged. Observation references retained in Variant are not owners, and may observe recycled images; this adds no GC/finalizer or scope-exit cleanup for class fields. See [constructor cleanup regression](../../tests/ast-compile-probe/image-constructor.xps). Explicit Delete or field clearing is still required after successful construction.

Derived destructors run from leaf to root: each body sees its own image fields alive, then those fields are cleared and the base Delete chain runs in finally. Local images use the usual procedure scope. A derived Exit Sub or error cannot skip ancestor cleanup. When several levels throw, the later ancestor error replaces an earlier one; the named caller reference is still cleared. Re-entry and repeated deletion cannot rerun a class's body. Original source images and borrowed Variant references remain outside field ownership. See [multi-level throwing destructor regression](../../tests/ast-compile-probe/image-destructor-derived.xps). This does not add GC finalizers or automatic object-scope cleanup.

A class can define a parameterless `Sub Delete()` whose body runs before its own image fields are cleared. Each declared destructor participates in the generated base chain, which runs even after Exit Sub or errors. Body-local images use the ordinary image scope. Both those locals and the root's fields are recycled if its destructor throws, and the named caller reference is cleared while the error propagates. Body execution is guarded against re-entry and repeated calls through surviving aliases. See [throwing image destructor regression](../../tests/ast-compile-probe/image-destructor.xps). Do not depend on GC or scope exit for this explicit Delete behavior.

`Delete owner` automatically clears and recycles its typed XPImage fields, including private fields and fields declared at each inheritance level. Other aliases still reference that object, but its image fields are now Nothing. Repeated deletion is safe. Borrowed Variant fields and the original images copied into typed fields are not consumed. Use `Set field = Nothing` to release a field earlier, or Delete the owner when finished. Simply assigning the owner Nothing or leaving its scope does not run this cleanup; GC finalizers remain outside this AST increment. See [the explicit Delete regression](../../tests/ast-compile-probe/image-field-delete.xps).

Experimental AST supports XPImage Property Get factories, including scalar Optional/ByRef indexes. Use `Dim picture As XPImage = factory.Fresh` or assign the getter to an existing typed image local. The returned image survives accessor exit and is then owned by the caller's usual image scope; unused accessor locals, replaced results and failed getter results are recycled. No implicit clone is added. If a getter exposes an image owned by a field/static/other scope, return an explicit Clone when that owner must keep its original. Do not discard an owning getter result in a member chain; capture it for cleanup or explicitly Recycle it. XPImage Property Set uses snapshot-owned typed fields as described above; GC/scope-exit cleanup is not added. See [getter lifetime regression](../../tests/ast-compile-probe/image-property-get.xps) and run `pwsh -NoProfile -File tests/ast-compile-probe/run-ast-image-get.ps1` for focused execution/diagnostics/package-notice checks.

The AST compiler uses the same XPImage runtime directly, including `New XPImage(width, height [, background])`, `Dim image As New XPImage(...)`, `Load`, `FromBytes`, `FromBase64`, information properties and overloaded editing/encoding methods. Names are case-insensitive; information properties are read-only. Invalid constructors, method arguments and members produce source-mapped diagnostics through both CLI and MCP.

ByVal image parameters receive an independent clone. ByRef parameters can mutate or replace the caller's image. Locally owned images are recycled on procedure exit, including early exit and exceptions; returned image references survive, including Variant results. Static images remain available across calls. Evaluate a replacement before recycling the previous image, so `Set image = image.Clone()` is safe.

Magick.NET and its native libraries are selected only when image runtime code is used. The existing addon build path supplies package references, platform assets and the direct/transitive `Third-party-license.txt`; image-free AST programs do not acquire Magick.NET. Executable AST coverage is in `tests/ast-compile-probe/xpimage-runtime.xps`, and `xpimage-parity.xps` verifies the shared contract against normal compilation.

## Extended effects

All editing methods mutate the current XPImage unless their description says that they return a new image.

| Member | Purpose |
|---|---|
| `AutoLevel()` | Automatically stretches channel levels. |
| `AutoGamma()` | Automatically adjusts gamma. |
| `Normalize()` | Normalizes image levels. |
| `ChangeGamma(value)` | Applies gamma correction; value must be greater than 0 and at most 10. |
| `Threshold(percent)` | Applies a 0-100 percent threshold. |
| `AdaptiveThreshold(width, height, offset)` | Applies a local adaptive threshold. |
| `AutoThreshold([method])` | Automatic thresholding; supported methods are `otsu`, `kapur` and `triangle`. |
| `Sepia(percent)` | Applies sepia toning. |
| `Hue(value)` | Shifts hue using a value from -100 through 100. |
| `Colorize(color, percent)` | Blends a color into the image. |
| `Despeckle()` | Reduces speckle noise. |
| `Median(radius)` | Applies a median filter. |
| `EdgeDetect(radius)` | Detects edges. |
| `Emboss(radius, sigma)` | Applies embossing. |
| `MotionBlur(radius, sigma, angle)` | Applies directional motion blur. |
| `OilPaint(radius, sigma)` | Applies an oil-paint effect. |
| `Trim()` | Removes border areas according to ImageMagick trim behavior. |
| `GetPixel(x, y)` | Returns the pixel color as a string. |
| `SetPixel(x, y, color)` | Replaces one pixel. |
| `MakeTransparent(color)` | Makes the specified color transparent. |
| `ReplaceColor(sourceColor, targetColor)` | Replaces matching source-color pixels. |
| `Statistics()` | Returns an `XPJsonObject` containing image and channel statistics. |
| `Histogram([maxColors])` | Returns an `XPJsonArray` containing the most frequent colors. |
| `Compare(other)` | Returns normalized RMS difference from 0 to 100. |
| `Difference(other)` | Returns a new XPImage containing the visual difference image. |

`Compare()` measures pixel distortion, not the percentage of pixels that changed. For example, a result of 10 means a normalized RMS difference of 10/100; it does not mean exactly 10 percent of pixels differ.

## Information properties

`HasAlpha`, `ColorSpace`, `ColorType`, `Gamma`, `Depth`, `Quality`, `Orientation` and `PixelCount` expose current image information. `Gamma` is read-only at the XPscript level; use `ChangeGamma(value)` to apply gamma correction. `PixelCount` is `Width * Height`.

## Statistics and histogram

`Statistics()` returns `width`, `height`, `pixelCount` and channel objects named `red`, `green`, `blue` and, when present, `alpha`. Each channel contains `minimum`, `maximum`, `mean` and `standardDeviation` normalized to 0 through 1.

`Histogram()` defaults to the 20 most frequent colors. `Histogram(maxColors)` accepts 1 through 4096. Each entry contains `color`, `count` and `percentage`. Histogram data describes pixel colors; it does not identify semantic content such as sky, skin, people or objects.

## Resource and portability behavior

XPImage operations are subject to XPScript's image dimension, pixel-count, encoded-size and ImageMagick resource limits. The same XPImage API is intended for Windows, Linux and macOS. ImageMagick implementation objects and enums are deliberately not part of the XPscript surface.
