# Image creation and editing runtime TODO

(c) xpagedeveloper.com 2026

Implement after `todo/hcl-domino-data-todo.md` is complete and merged.

Backend decision: XPImage uses Magick.NET / ImageMagick. The earlier ImageSharp/SkiaSharp evaluation implementation has been discarded; implementation starts clean from this TODO.

## Required implementation strategy

- [x] Use Magick.NET as the image-processing backend. Do not build the image engine from scratch.
- [x] Use an Apache-2.0 licensed Magick.NET package/configuration suitable for Windows, Linux and macOS.
- [x] Pin and centrally manage the selected Magick.NET package/version.
- [x] Verify .NET 10 compatibility, Windows/Linux/macOS support, maintenance activity, security advisories/CVEs and transitive/native dependencies.
- [x] Use ImageMagick/Magick.NET codecs, resampling, transforms, effects, compositing, drawing, EXIF/profile and encoding APIs rather than duplicating those algorithms.
- [x] Wrap Magick.NET behind the stable XPScript-owned XPImage API; no Magick.NET types may leak into the public XPScript API.
- [x] Reuse existing XPScript path-security, HTTP, web response, upload, diagnostics and resource-lifetime infrastructure.
- [x] Keep Magick.NET out of applications that do not use XPImage.
- [x] Browser-WASM may use XPImage only inside module-level `[ServerSide]` Functions/Subs; Magick.NET and native assets must remain in the server companion and never enter the WASM client.
- [x] Reject XPImage use in browser-side procedures, class methods and module-level browser state with the normal execution-context diagnostic.
- [x] Ensure Magick.NET native/runtime assets are staged correctly for compiled and run applications on each supported platform.

## Goals

- [x] Add a cross-platform XPScript image API named `XPImage` for creating new images and modifying existing images.
- [x] Support Windows, Linux and macOS with the same public XPScript API.
- [x] Keep image processing deterministic and suitable for CLI, desktop and web-hosted XPScript.
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
- [x] Support fit/fill/contain/crop resize modes.
- [x] Crop by x, y, width and height.
- [x] Rotate by common angles and arbitrary angles where supported.
- [x] Horizontal and vertical flip.
- [x] Add padding/canvas extension.
- [x] Composite one image over another at specified coordinates.
- [x] Opacity/alpha control for compositing.
- [x] Convert between supported formats.
- [x] JPEG quality setting.
- [x] PNG/WebP encoding options where practical.

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

- [x] Brightness.
- [x] Contrast.
- [x] Saturation.
- [x] Grayscale.
- [x] Invert.
- [x] Blur.
- [x] Sharpen.
- [x] Opacity.
- [x] Background replacement for transparent areas.
- [ ] Investigate additional filters only if supported cleanly by the chosen library.

## Metadata

- [x] Read basic metadata such as format, pixel dimensions and DPI.
- [x] Read EXIF metadata where available.
- [x] Preserve EXIF, ICC, XMP and other supported metadata/profiles by default when the destination format supports them.
- [x] Never strip metadata implicitly during normal XPImage editing.
- [x] Allow explicit metadata/profile removal when requested by the script.
- [x] Handle image orientation metadata correctly on load or provide explicit auto-orient behavior.
- [x] Never trust EXIF or other metadata values as safe application input.

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
- [x] Support reading uploaded images from the existing web upload model.
- [x] Support a fully in-memory HTTP image-processing flow: receive an uploaded image via POST, create `XPImage` from the uploaded bytes, edit/resize it, and return the encoded result directly in the HTTP response without creating a temporary image file on disk.
- [x] Add FullTest coverage for the in-memory POST flow: upload a real image, resize it with `XPImage`, return it as binary HTTP output, and verify response MIME type, image format and resulting dimensions without relying on a temporary image file.
- [ ] Apply the same image size/pixel/resource limits to standalone and web execution.

## Security and resource limits

- [x] Apply existing safe path rules for image file reads and writes.
- [x] Prevent output path traversal and unrelated-file overwrite.
- [x] Define maximum encoded image file size.
- [x] Define maximum pixel count and width/height.
- [x] Reject decompression-bomb style inputs or excessive decoded dimensions.
- [x] Bound temporary buffers and intermediate image sizes.
- [x] Treat uploaded images and metadata as untrusted input.
- [x] Do not execute embedded scripts, external references or unsupported active content.
- [x] Dispose native/unmanaged image resources deterministically.
- [x] Add concurrency tests proving image instances do not share mutable state.

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
- [x] Expose practical JPEG quality and PNG/WebP encoding options without exposing Magick.NET types.

### Geometry and composition

- [x] `Resize(width, height)`.
- [x] Resize by one dimension while preserving aspect ratio.
- [x] Fit, fill, contain and crop resize modes.
- [x] `Crop(x, y, width, height)`.
- [x] `Rotate(degrees)`.
- [x] `FlipHorizontal()` and `FlipVertical()`.
- [x] Padding/canvas extension.
- [x] Composite/draw another XPImage at coordinates.
- [x] Per-image/composite opacity.
- [x] Background replacement/flattening for transparent pixels.

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

- [x] `Brightness(value)`.
- [x] `Contrast(value)`.
- [x] `Saturation(value)`.
- [x] `Grayscale()`.
- [x] `Invert()`.
- [x] `Blur(radius)`.
- [x] `Sharpen(amount)`.
- [x] Opacity adjustment.
- [ ] Add further ImageMagick effects only where they fit a stable, portable XPImage API.

### Metadata and orientation

- [x] Read EXIF metadata through an XPScript-owned representation.
- [x] Read/write supported image metadata/profile values where practical.
- [x] Preserve supported metadata/profiles by default.
- [x] Explicit metadata/profile stripping.
- [x] `AutoOrient()`.
- [x] Keep metadata untrusted and enforce size/resource limits.

## FullTest verification

- [x] Add a dedicated XPImage FullTest workflow that runs on Windows, Linux and macOS.
- [ ] FullTest must compile and execute XPImage creation, file/bytes/base64 loading, all supported formats, editing/effects/metadata, resource limits, web output and Browser-WASM `[ServerSide]` behavior.
- [x] FullTest must verify an application without XPImage does not acquire Magick.NET and an XPImage application does include the required generated license notices.
- [ ] FullTest must fail on compiler/runtime warnings relevant to XPImage.

## Implementation completion criteria

- [x] XPImage uses only the selected Magick.NET backend.
- [x] No ImageSharp or SkiaSharp dependency or image-runtime implementation remains.
- [x] No Magick.NET implementation types leak into XPScript source syntax or public XPImage API.
- [ ] CLI, desktop, Kestrel, CGI and FastCGI can use the same XPImage object model.
- [x] Applications that do not use XPImage do not acquire Magick.NET dependencies.
- [ ] Documentation and reusable examples are added under `docs/` and `examples/`.
