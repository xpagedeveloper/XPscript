# Image creation and editing runtime TODO

(c) xpagedeveloper.com 2026

Implement after `todo/hcl-domino-data-todo.md` is complete and merged.

Backend decision: XPImage uses Magick.NET / ImageMagick. The earlier ImageSharp/SkiaSharp evaluation implementation has been discarded; implementation starts clean from this TODO.

## Required implementation strategy

- [x] Use Magick.NET as the image-processing backend. Do not build the image engine from scratch.
- [ ] Use an Apache-2.0 licensed Magick.NET package/configuration suitable for Windows, Linux and macOS.
- [ ] Pin and centrally manage the selected Magick.NET package/version.
- [ ] Verify .NET 10 compatibility, Windows/Linux/macOS support, maintenance activity, security advisories/CVEs and transitive/native dependencies.
- [ ] Use ImageMagick/Magick.NET codecs, resampling, transforms, effects, compositing, drawing, EXIF/profile and encoding APIs rather than duplicating those algorithms.
- [x] Wrap Magick.NET behind the stable XPScript-owned XPImage API; no Magick.NET types may leak into the public XPScript API.
- [ ] Reuse existing XPScript path-security, HTTP, web response, upload, diagnostics and resource-lifetime infrastructure.
- [x] Keep Magick.NET out of applications that do not use XPImage.
- [ ] Browser-WASM may use XPImage only inside module-level `[ServerSide]` Functions/Subs; Magick.NET and native assets must remain in the server companion and never enter the WASM client.
- [ ] Reject XPImage use in browser-side procedures, class methods and module-level browser state with the normal execution-context diagnostic.
- [ ] Ensure Magick.NET native/runtime assets are staged correctly for compiled and run applications on each supported platform.

## Goals

- [x] Add a cross-platform XPScript image API named `XPImage` for creating new images and modifying existing images.
- [x] Support Windows, Linux and macOS with the same public XPScript API.
- [ ] Keep image processing deterministic and suitable for CLI, desktop and web-hosted XPScript.
- [x] Support file-based and in-memory Byte-array workflows.

## Core object model

- [x] Add the top-level `XPImage` class.
- [x] Support creating a blank raster image with width, height and optional background.
- [x] Support loading from a file path.
- [x] Support loading from Byte array or equivalent in-memory XPScript value.
- [x] Expose Width, Height and image format.
- [x] Support cloning/copying without shared mutable backing state.
- [x] Support Save(path) and ToBytes(format).
- [x] Support explicit/disposable resource lifetime where required by the selected library.

## Supported raster formats

- [x] PNG.
- [x] JPEG/JPG.
- [x] WebP where the selected maintained library supports it reliably.
- [x] GIF static-image read/write where practical.
- [x] BMP read/write where practical.
- [x] Support TIFF read/write through Magick.NET.
- [x] Detect input format from content rather than trusting file extension alone.
- [x] Reject unsupported or malformed formats with clear runtime errors.

## Basic editing

- [x] Resize with configurable width/height.
- [x] Preserve aspect ratio when only one dimension is supplied.
- [ ] Support fit/fill/contain/crop resize modes.
- [x] Crop by x, y, width and height.
- [x] Rotate by common angles and arbitrary angles where supported.
- [x] Horizontal and vertical flip.
- [ ] Add padding/canvas extension.
- [ ] Composite one image over another at specified coordinates.
- [ ] Opacity/alpha control for compositing.
- [x] Convert between supported formats.
- [x] JPEG quality setting.
- [ ] PNG/WebP encoding options where practical.

## Drawing and annotation

- [ ] Draw text with font family, size and basic style.
- [ ] Support Unicode text.
- [ ] Define font discovery/fallback behavior cross-platform.
- [ ] Draw lines.
- [ ] Draw rectangles and rounded rectangles where supported.
- [ ] Draw ellipses/circles.
- [ ] Fill shapes.
- [ ] Draw images/icons onto another image.
- [ ] Add borders.
- [ ] Add simple watermark text or watermark image.
- [ ] Support text alignment and basic wrapping.

## Color and effects

- [ ] Brightness.
- [ ] Contrast.
- [ ] Saturation.
- [x] Grayscale.
- [x] Invert.
- [x] Blur.
- [x] Sharpen.
- [ ] Opacity.
- [ ] Background replacement for transparent areas.
- [ ] Investigate additional filters only if supported cleanly by the chosen library.

## Metadata

- [x] Read basic metadata such as format, pixel dimensions and DPI.
- [ ] Read EXIF metadata where available.
- [ ] Preserve EXIF, ICC, XMP and other supported metadata/profiles by default when the destination format supports them.
- [x] Never strip metadata implicitly during normal XPImage editing.
- [x] Allow explicit metadata/profile removal when requested by the script.
- [x] Handle image orientation metadata correctly on load or provide explicit auto-orient behavior.
- [ ] Never trust EXIF or other metadata values as safe application input.

## Avalonia desktop integration

- [ ] Allow an `XPImage` instance to be used directly as the source of an Avalonia-backed UI image.
- [ ] Extend UIForm image source handling so it accepts both the existing String source and `XPImage`.
- [ ] Bridge XPImage to Avalonia entirely in memory without temporary files.
- [ ] Give Avalonia an independent/read-safe representation so later XPImage mutations cannot corrupt the displayed image.
- [ ] Dispose Avalonia bitmap/stream resources deterministically when replaced or detached.
- [ ] Keep Avalonia types out of the public XPImage API and keep Avalonia dependencies out of CLI/web applications.

## AI image integration boundary

- [ ] Keep local image processing separate from AI image generation/editing.
- [ ] Add an optional future `AIImageTool` that can attach to `AIClient` rather than hard-coding AI generation into the image runtime.
- [ ] Allow AIImageTool to create images from prompts through a configurable OpenAI-compatible or provider-specific endpoint adapter.
- [ ] Allow AIImageTool to edit supplied source images where the configured provider supports image editing.
- [ ] Support configurable endpoint, headers, model and provider-specific request properties through the same provider-neutral principles as `AIClient`.
- [ ] Return generated/edited images as normal XPScript image/Byte-array values so subsequent local image operations can be applied.
- [ ] Do not require AI dependencies for normal local image processing.

## Web runtime integration

- [ ] Allow images to be returned directly via Kestrel, CGI and FastCGI without creating public temporary files.
- [ ] Set correct MIME type for PNG, JPEG, WebP, GIF and other supported formats.
- [ ] Support inline and attachment responses where useful.
- [ ] Support reading uploaded images from the existing web upload model.
- [ ] Apply the same image size/pixel/resource limits to standalone and web execution.

## Security and resource limits

- [x] Apply existing safe path rules for image file reads and writes.
- [ ] Prevent output path traversal and unrelated-file overwrite.
- [x] Define maximum encoded image file size.
- [x] Define maximum pixel count and width/height.
- [ ] Reject decompression-bomb style inputs or excessive decoded dimensions.
- [ ] Bound temporary buffers and intermediate image sizes.
- [ ] Treat uploaded images and metadata as untrusted input.
- [ ] Do not execute embedded scripts, external references or unsupported active content.
- [x] Dispose native/unmanaged image resources deterministically.
- [ ] Add concurrency tests proving image instances do not share mutable state.

## Public XPImage API to implement

### Construction, loading and properties

- [x] `XPImage(width, height)`.
- [x] `XPImage(width, height, background)`.
- [x] `XPImage.Load(path)`.
- [x] `XPImage.FromBytes(data)`.
- [x] `XPImage.FromBase64(data)` accepting raw base64 and `data:image/...;base64,...` input.
- [x] `Width`.
- [x] `Height`.
- [x] `Format`.
- [x] `DpiX`.
- [x] `DpiY`.
- [x] `Clone()`.
- [x] `Dispose()`.

### Output and encoding

- [x] `Save(path)`.
- [x] `Save(path, quality)` where quality applies to the selected encoder.
- [x] `ToBytes(format)`.
- [ ] Expose practical JPEG quality and PNG/WebP encoding options without exposing Magick.NET types.

### Geometry and composition

- [x] `Resize(width, height)`.
- [x] Resize by one dimension while preserving aspect ratio.
- [ ] Fit, fill, contain and crop resize modes.
- [x] `Crop(x, y, width, height)`.
- [x] `Rotate(degrees)`.
- [x] `FlipHorizontal()` and `FlipVertical()`.
- [ ] Padding/canvas extension.
- [ ] Composite/draw another XPImage at coordinates.
- [ ] Per-image/composite opacity.
- [ ] Background replacement/flattening for transparent pixels.

### Drawing and annotation

- [ ] Draw text with font family, size, style and color.
- [ ] Unicode text and documented cross-platform font discovery/fallback.
- [ ] Text alignment and basic wrapping.
- [ ] Draw lines.
- [ ] Draw rectangles and rounded rectangles.
- [ ] Draw ellipses/circles.
- [ ] Filled and outlined shapes.
- [ ] Borders.
- [ ] Text and image watermarks.

### Effects

- [ ] `Brightness(value)`.
- [ ] `Contrast(value)`.
- [ ] `Saturation(value)`.
- [x] `Grayscale()`.
- [x] `Invert()`.
- [x] `Blur(radius)`.
- [x] `Sharpen(amount)`.
- [ ] Opacity adjustment.
- [ ] Add further ImageMagick effects only where they fit a stable, portable XPImage API.

### Metadata and orientation

- [ ] Read EXIF metadata through an XPScript-owned representation.
- [ ] Read/write supported image metadata/profile values where practical.
- [ ] Preserve supported metadata/profiles by default.
- [x] Explicit metadata/profile stripping.
- [x] `AutoOrient()`.
- [ ] Keep metadata untrusted and enforce size/resource limits.

## FullTest verification

- [x] Add a dedicated XPImage FullTest workflow that runs on Windows, Linux and macOS.
- [ ] FullTest must compile and execute XPImage creation, file/bytes/base64 loading, all supported formats, editing/effects/metadata, resource limits, web output and Browser-WASM `[ServerSide]` behavior.
- [ ] FullTest must verify an application without XPImage does not acquire Magick.NET and an XPImage application does include the required generated license notices.
- [ ] FullTest must fail on compiler/runtime warnings relevant to XPImage.

## Implementation completion criteria

- [x] XPImage uses only the selected Magick.NET backend.
- [x] No ImageSharp or SkiaSharp dependency or image-runtime implementation remains.
- [x] No Magick.NET implementation types leak into XPScript source syntax or public XPImage API.
- [ ] CLI, desktop, Kestrel, CGI and FastCGI can use the same XPImage object model.
- [ ] Applications that do not use XPImage do not acquire Magick.NET dependencies.
- [ ] Documentation and reusable examples are added under `docs/` and `examples/`.
