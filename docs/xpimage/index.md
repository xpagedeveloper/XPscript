# XPImage processing

XPImage is XPScript's cross-platform raster image object. It uses Magick.NET internally while keeping the public API independent of ImageMagick types. The regression example for the API is [xpimage-runtime.xps](../../samples/xpimage-runtime.xps).

## AST compilation

Experimental AST supports XPImage Property Get factories, including scalar Optional/ByRef indexes. Use `Dim picture As XPImage = factory.Fresh` or assign the getter to an existing typed image local. The returned image survives accessor exit and is then owned by the caller's usual image scope; unused accessor locals, replaced results and failed getter results are recycled. No implicit clone is added. If a getter exposes an image owned by a field/static/other scope, return an explicit Clone when that owner must keep its original. Do not discard an owning getter result in a member chain; capture it for cleanup or explicitly Recycle it. XPImage Property Set remains unsupported with XPS3001; persistent class-field lifetime management is not added. See [getter lifetime regression](../../tests/ast-compile-probe/image-property-get.xps) and run `pwsh -NoProfile -File tests/ast-compile-probe/run-ast-image-get.ps1` for focused execution/diagnostics/package-notice checks.

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
