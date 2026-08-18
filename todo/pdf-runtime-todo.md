# PDF runtime TODO

(c) xpagedeveloper.com 2026

Implement after `todo/ai-client-todo.md` is complete and merged.

## Goals

- [ ] Add a cross-platform PDF API to XPScript for creating static PDF documents.
- [ ] Add support for creating interactive PDF forms.
- [ ] Add support for loading existing PDFs and extracting text, metadata and structure.
- [ ] Add support for reading values and metadata from PDF form fields.
- [ ] Add support for filling and updating PDF forms.
- [ ] Keep the public XPScript API stable across Windows, Linux and macOS.
- [ ] Prefer a mature maintained .NET PDF library over implementing the PDF specification manually.

## Core classes

- [ ] Add a top-level `PDFDocument` class.
- [ ] Add a `PDFPage` class or equivalent page abstraction.
- [ ] Add a `PDFForm` / `PDFFormField` abstraction for interactive form fields.
- [ ] Add a `PDFTextExtractor` or equivalent extraction API if extraction does not fit cleanly on `PDFDocument`.
- [ ] Support creating a new empty PDF document.
- [ ] Support loading a PDF from a file path.
- [ ] Support loading a PDF from a Byte array/stream-compatible XPScript value where practical.
- [ ] Support saving to a file path.
- [ ] Support exporting the finished PDF as a Byte array for web responses, APIs and in-memory processing.

## Static PDF creation

- [ ] Add pages with standard sizes such as A4, A3, Letter and Legal.
- [ ] Support custom page width/height.
- [ ] Support portrait and landscape orientation.
- [ ] Add text with position, font, size and basic formatting.
- [ ] Support multiline text and text wrapping.
- [ ] Support paragraphs and basic document flow where feasible.
- [ ] Support lines, rectangles and basic vector drawing.
- [ ] Support images from file paths and Byte arrays.
- [ ] Support tables with rows, columns, borders and cell text.
- [ ] Support headers and footers.
- [ ] Support page numbers.
- [ ] Support document metadata such as title, author, subject, keywords and creator.
- [ ] Support links/URLs where supported by the selected PDF library.
- [ ] Support Unicode text.
- [ ] Define font embedding and fallback behavior clearly.

## PDF form creation

- [ ] Support AcroForm-compatible interactive PDF forms.
- [ ] Add text fields.
- [ ] Add multiline text fields.
- [ ] Add password fields where supported by PDF viewers.
- [ ] Add checkboxes.
- [ ] Add radio-button groups.
- [ ] Add combo boxes/dropdowns.
- [ ] Add list boxes.
- [ ] Add buttons where practical.
- [ ] Support field name, default value, current value, tooltip and read-only state.
- [ ] Support required fields where the PDF format/library supports it.
- [ ] Support field coordinates and dimensions.
- [ ] Support field appearance/font settings where practical.
- [ ] Prevent duplicate field names unless explicitly allowed.
- [ ] Expose a stable list of all form fields in a document.

## JSON binding for PDF forms

- [ ] Reuse the same document-style JSON binding principles as UIForm where practical.
- [ ] Allow a PDF form to load values from a `JsonObject`/`JsonDocument` by matching PDF field name to JSON property name.
- [ ] If a PDF form field does not exist in JSON, expose it as empty without modifying the JSON document.
- [ ] Create a missing JSON property only when a value is actually supplied or extracted for that field.
- [ ] Allow existing PDF form values to be exported to JSON.
- [ ] Allow JSON values to be applied to an existing PDF form.
- [ ] Preserve Boolean, numeric, date and string types where the form field type can be mapped safely.
- [ ] Define array/multiselect mapping for list fields.

## Fill and update existing PDF forms

- [ ] Load an existing PDF containing form fields.
- [ ] Read field names, types and current values.
- [ ] Set individual form field values.
- [ ] Fill multiple fields from a JSON document.
- [ ] Save the modified document as a new PDF.
- [ ] Preserve unrelated pages, content and fields.
- [ ] Support flattening selected or all form fields into static page content where supported.
- [ ] Keep a non-flattened output option so the PDF remains editable.
- [ ] Define behavior for unsupported field types such as signatures or vendor-specific widgets.

## PDF extraction

- [ ] Extract plain text from all pages.
- [ ] Extract text per page.
- [ ] Preserve page numbers with extracted text.
- [ ] Expose page count.
- [ ] Extract document metadata.
- [ ] Extract links where practical.
- [ ] Extract embedded images where supported and safe.
- [ ] Expose text positions/bounding boxes where the selected library supports reliable coordinates.
- [ ] Add a structured extraction result that can be converted to JSON.
- [ ] Do not claim reading order guarantees when the source PDF does not contain reliable logical structure.

## PDF form extraction

- [ ] Detect whether a PDF contains an interactive form.
- [ ] Enumerate all form fields.
- [ ] Extract field name.
- [ ] Extract field type.
- [ ] Extract current value.
- [ ] Extract default value where present.
- [ ] Extract read-only/required flags where present.
- [ ] Extract available choices for combo/list fields.
- [ ] Extract selected values for list fields.
- [ ] Return all form data as a `JsonObject`/`JsonDocument` convenience API.
- [ ] Preserve fields with empty values in the PDF form result when explicitly requested.

## Scanned PDFs and OCR boundary

- [ ] Detect PDFs that contain no useful extractable text.
- [ ] Keep OCR as a separate optional capability rather than silently running OCR during normal extraction.
- [ ] Define a future pluggable OCR provider/tool interface if OCR support is added.
- [ ] Do not make OCR a dependency of the base PDF runtime.

## Security and resource limits

- [ ] Validate input file paths with the existing XPScript path-security rules.
- [ ] Prevent output path traversal and unrelated-file overwrite.
- [ ] Apply configurable maximum PDF file size.
- [ ] Apply configurable maximum page count.
- [ ] Apply extraction output limits to avoid unbounded memory usage.
- [ ] Defend against malformed/corrupt PDFs and decompression/resource bombs as far as supported by the selected library.
- [ ] Do not automatically execute embedded JavaScript, launch actions, attachments or external references.
- [ ] Treat embedded files as untrusted data.
- [ ] Do not leak PDF passwords or sensitive field values in diagnostics.
- [ ] Dispose file handles, streams and PDF document resources deterministically.
- [ ] Add concurrency tests proving separate PDF operations do not share mutable document/form state.

## Encryption and protected PDFs

- [ ] Detect encrypted/password-protected PDFs.
- [ ] Allow opening with a supplied password where the selected library supports it.
- [ ] Return clear errors for protected PDFs when no valid password is supplied.
- [ ] Never include supplied passwords in diagnostics or logs.
- [ ] Evaluate support for creating password-protected PDFs as an optional feature.
- [ ] Evaluate owner/user permission controls only if the selected library provides reliable standards-based support.

## API shape examples to validate

- [ ] `Dim pdf As New PDFDocument`
- [ ] `Call pdf.AddPage("A4")`
- [ ] `Call pdf.AddText("Hello", 50, 50)`
- [ ] `Call pdf.Save("output.pdf")`
- [ ] `Dim pdf As PDFDocument = PDFDocument.Load("input.pdf")` or equivalent valid XPScript assignment form.
- [ ] `Print pdf.PageCount`
- [ ] `Print pdf.ExtractText()`
- [ ] `Dim data As JsonObject = pdf.GetFormData()` or equivalent object assignment form.
- [ ] `Call pdf.SetFormData(data)`
- [ ] `Call pdf.FlattenForm()`
- [ ] Validate exact XPScript syntax against the compiler before finalizing the public API.

## Web/runtime integration

- [ ] Allow generated PDFs to be returned directly through the XPScript web runtime without requiring a temporary public file.
- [ ] Support correct `application/pdf` response content type.
- [ ] Support inline and attachment content-disposition behavior.
- [ ] Work consistently through Kestrel, CGI and FastCGI.
- [ ] Support extracting uploaded PDFs through the existing web request/upload model when available.
- [ ] Apply the same PDF size/resource limits in standalone and web execution.

## Tests and quality gates

- [ ] Add static PDF creation regression tests on Windows, Ubuntu and macOS.
- [ ] Verify generated PDFs can be reopened and parsed by the same runtime.
- [ ] Verify Unicode text round-trip.
- [ ] Add PDF form creation regression tests.
- [ ] Add form fill/read round-trip tests.
- [ ] Add JSON-to-form and form-to-JSON round-trip tests.
- [ ] Add missing-JSON-field tests proving missing keys stay absent until a value is supplied.
- [ ] Add static text extraction fixtures.
- [ ] Add multi-page extraction fixtures.
- [ ] Add malformed PDF negative tests.
- [ ] Add encrypted PDF negative/positive tests if password support is implemented.
- [ ] Add resource-limit tests.
- [ ] Add Kestrel, CGI and FastCGI PDF response tests.
- [ ] Add documentation and reusable examples under `docs/` and `examples/`.
