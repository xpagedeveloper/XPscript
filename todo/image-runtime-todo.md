# Image creation and editing runtime TODO

(c) xpagedeveloper.com 2026

Implement after `todo/hcl-domino-data-todo.md` is complete and merged.

## Implementation policy

- [ ] Prefer mature, maintained, production-proven .NET libraries and existing XPScript runtime helpers over custom implementations.
- [ ] Do not implement image codecs, EXIF parsers, resamplers, font engines or compression algorithms manually when a vetted library already provides them.
- [ ] Reuse existing XPScript path-security, HTTP, JSON, web response, upload, diagnostics and resource-lifetime infrastructure.
- [ ] Wrap third-party functionality behind a stable XPScript API so the underlying implementation can be replaced without breaking scripts.
- [ ] Evaluate licensing, maintenance status, CVE history, platform support and transitive dependencies before selecting a library.
- [ ] Prefer one cross-platform implementation where practical instead of separate Windows/Linux/macOS code paths.

## Goals

- [ ] Add a cross-platform XPScript image API for creating new images and modifying existing images.
- [ ] Support Windows, Linux and macOS with the same public XPScript API.
- [ ] Keep image processing deterministic and suitable for CLI, desktop and web-hosted XPScript.
- [ ] Support file-based and in-memory Byte-array workflows.

## Core object model

- [ ] Add a top-level `ImageDocument` or `XPSImage` class.
- [ ] Support creating a blank raster image with width, height and optional background.
- [ ] Support loading from a file path.
- [ ] Support loading from Byte array or equivalent in-memory XPScript value.
- [ ] Expose Width, Height and image format.
- [ ] Support cloning/copying without shared mutable backing state.
- [ ] Support Save(path) and ToBytes(format).
- [ ] Support explicit/disposable resource lifetime where required by the selected library.

## Supported raster formats

- [ ] PNG.
- [ ] JPEG/JPG.
- [ ] WebP where the selected maintained library supports it reliably.
- [ ] GIF static-image read/write where practical.
- [ ] BMP read/write where practical.
- [ ] Investigate TIFF support as an optional capability.
- [ ] Detect input format from content rather than trusting file extension alone.
- [ ] Reject unsupported or malformed formats with clear runtime errors.

## Basic editing

- [ ] Resize with configurable width/height.
- [ ] Preserve aspect ratio when only one dimension is supplied.
- [ ] Support fit/fill/contain/crop resize modes.
- [ ] Crop by x, y, width and height.
- [ ] Rotate by common angles and arbitrary angles where supported.
- [ ] Horizontal and vertical flip.
- [ ] Add padding/canvas extension.
- [ ] Composite one image over another at specified coordinates.
- [ ] Opacity/alpha control for compositing.
- [ ] Convert between supported formats.
- [ ] JPEG quality setting.
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
- [ ] Grayscale.
- [ ] Invert.
- [ ] Blur.
- [ ] Sharpen.
- [ ] Opacity.
- [ ] Background replacement for transparent areas.
- [ ] Investigate additional filters only if supported cleanly by the chosen library.

## Metadata

- [ ] Read basic metadata such as format, pixel dimensions and DPI.
- [ ] Read EXIF metadata where available.
- [ ] Allow stripping metadata for privacy.
- [ ] Preserve metadata only when explicitly configured and safe.
- [ ] Handle image orientation metadata correctly on load or provide explicit auto-orient behavior.
- [ ] Never trust EXIF or other metadata values as safe application input.

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

- [ ] Apply existing safe path rules for image file reads and writes.
- [ ] Prevent output path traversal and unrelated-file overwrite.
- [ ] Define maximum encoded image file size.
- [ ] Define maximum pixel count and width/height.
- [ ] Reject decompression-bomb style inputs or excessive decoded dimensions.
- [ ] Bound temporary buffers and intermediate image sizes.
- [ ] Treat uploaded images and metadata as untrusted input.
- [ ] Do not execute embedded scripts, external references or unsupported active content.
- [ ] Dispose native/unmanaged image resources deterministically.
- [ ] Add concurrency tests proving image instances do not share mutable state.

## API examples to validate

- [ ] `Dim img As New ImageDocument(800, 600)`.
- [ ] `Dim img As ImageDocument = ImageDocument.Load("input.png")` or equivalent valid XPScript form.
- [ ] `Call img.Resize(400, 300)`.
- [ ] `Call img.Crop(10, 10, 200, 100)`.
- [ ] `Call img.DrawText("Hello", 20, 20)`.
- [ ] `Call img.Save("output.webp")`.
- [ ] `data = img.ToBytes("png")`.
- [ ] Validate the exact syntax against current compiler object/member rules before freezing the public API.

## Tests and quality gates

- [ ] Create/read/write round-trip tests for each supported format.
- [ ] Resize/crop/rotate/flip regression tests.
- [ ] Text and Unicode rendering tests.
- [ ] Alpha/compositing tests.
- [ ] Metadata stripping tests.
- [ ] Malformed image negative tests.
- [ ] Oversized/decompression-bomb limit tests.
- [ ] Cross-platform tests on Windows, Ubuntu and macOS.
- [ ] Kestrel, CGI and FastCGI image response tests.
- [ ] Add documentation and reusable examples under `docs/` and `examples/`.
