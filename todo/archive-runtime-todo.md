# Archive runtime TODO

(c) xpagedeveloper.com 2026

Implement a cross-platform archive runtime for XPScript using a stable XPScript-owned API and a maintained managed .NET archive library underneath.

## Required implementation strategy

- [x] Add a format-independent public `Archive` object.
- [x] Add an `ArchiveEntry` object for files and folders inside an archive.
- [x] Keep all third-party types, enums and implementation details hidden behind XPScript-owned runtime classes.
- [x] Use `SharpCompress` as the preferred implementation library, subject to final compatibility, security and license verification at implementation time.
- [ ] Verify the selected SharpCompress release is MIT licensed and supports the .NET target frameworks used by XPScript.
- [x] Prefer a pure managed implementation with no platform-specific native archive binaries.
- [x] Design the runtime around `Stream` internally so file, Byte-array, HTTP, database, MIME, mobile and WASM scenarios can share the same implementation.
- [x] Reuse existing XPScript path handling and filesystem security instead of allowing the archive library to access arbitrary paths directly.
- [x] Add the dependency only when generated code actually uses the archive runtime.
- [x] Add the selected package and license information to `THIRD-PARTY-NOTICES.md`.
- [x] Ensure `scripts/validate-license-notices.ps1` passes after the new dependency is added.

## Goals

- [x] Create archives.
- [x] Open existing archives.
- [x] List files and folders.
- [x] Add files.
- [x] Add folders recursively.
- [x] Add in-memory text and Byte-array data.
- [x] Remove files and folders where the archive format supports modification.
- [x] Rename entries where the archive format supports modification.
- [x] Extract individual files.
- [x] Extract folders.
- [x] Extract complete archives.
- [x] Read archive entries directly into text or Byte arrays without extracting to disk.
- [x] Support file-based and in-memory archive workflows.
- [x] Keep behavior consistent across Windows, Linux and macOS.
- [ ] Validate Android, iOS and browser/WASM scenarios.

## Core object model

- [x] Add public XPScript object `Archive`.
- [x] Add public XPScript object `ArchiveEntry`.
- [x] Implement internal runtime class `XPScriptArchive`.
- [x] Implement internal runtime class `XPScriptArchiveEntry`.
- [x] Add a runtime source component such as `ArchiveRuntimeSource.cs` following existing runtime-source injection patterns.
- [x] Confirm the exact constructor and member syntax against current XPScript compiler rules before freezing the public API.

## Archive constructors

- [x] Support opening or targeting an archive by path.

```text
Dim archive As New Archive("backup.zip")
```

- [x] Support constructing from a Byte array or equivalent in-memory XPScript value.

```text
Dim archive As New Archive(bytes)
```

- [x] Support creating an empty archive and specifying the format explicitly.

```text
Dim archive As New Archive()
archive.Create("zip")
```

- [ ] Define whether `New Archive("file.zip")` automatically opens an existing archive or only stores the path until `Open()` is called.
- [ ] Keep constructor behavior deterministic and consistent across platforms.

## Archive properties

- [x] `Path As String`.
- [x] `Format As String`.
- [x] `Password As String` where supported.
- [x] `CompressionLevel As Integer` or a stable XPScript-owned equivalent.
- [x] `Exists As Boolean`.
- [x] `IsEncrypted As Boolean`.
- [x] `IsReadOnly As Boolean`.
- [x] `FileCount As Long`.
- [x] `FolderCount As Long`.
- [x] `CompressedSize As Long`.
- [x] `UncompressedSize As Long`.
- [x] `Entries` as an XPScript-compatible collection/array suitable for `ForAll`.
- [ ] Consider exposing resource-limit properties only if there is a clear need to override secure defaults.

## ArchiveEntry properties

- [x] `Name As String`.
- [x] `FullName As String`.
- [x] `Extension As String`.
- [x] `Size As Long`.
- [x] `CompressedSize As Long`.
- [x] `CompressionRatio`.
- [x] `Created` where the archive format exposes it reliably.
- [x] `Modified` where the archive format exposes it reliably.
- [x] `CRC` where available.
- [x] `IsDirectory As Boolean`.
- [x] `IsEncrypted As Boolean`.
- [ ] Define consistent fallback values when a format does not expose a metadata field.

## Archive lifecycle

- [x] `Create()`.
- [x] `Create(format)`.
- [x] `Open()`.
- [x] `Save()`.
- [x] `Close()`.
- [ ] Define overwrite behavior for `Create()` when the target already exists.
- [ ] Define behavior when the archive is missing, malformed, unsupported or encrypted.
- [x] Dispose streams and other resources deterministically.

## Listing and lookup

- [x] `Contains(entryName)`.
- [x] `GetEntry(entryName)`.
- [x] `Files()`.
- [x] `Folders()`.
- [x] `Find(pattern)`.
- [ ] Define wildcard semantics for `Find()` and keep them consistent with existing XPScript conventions where possible.
- [x] Normalize archive entry separators to `/` internally regardless of host operating system.

Example to validate:

```text
Dim archive As New Archive("backup.zip")

ForAll entry In archive.Entries
    If Not entry.IsDirectory Then
        Print entry.FullName & " " & CStr(entry.Size)
    End If
End ForAll
```

## Adding content

- [x] `AddFile(sourcePath)`.
- [x] `AddFile(sourcePath, archivePath)`.
- [x] `AddFolder(sourcePath)`.
- [x] `AddFolder(sourcePath, archivePath)`.
- [x] `AddFolder(sourcePath, archivePath, recursive)`.
- [x] `AddText(archivePath, text)`.
- [x] `AddBytes(archivePath, bytes)`.
- [ ] Prevent source paths from bypassing existing XPScript filesystem restrictions.
- [x] Normalize destination entry names before writing them into the archive.
- [ ] Define duplicate-entry behavior explicitly.
- [ ] Preserve timestamps only where reliable and useful.

Example to validate:

```text
Dim archive As New Archive("backup.zip")
archive.Create()
archive.AddFile("c:\data\config.json", "config/config.json")
archive.AddFolder("c:\data\images", "images", True)
archive.Save()
```

## Modifying archives

- [x] `Remove(entryName)`.
- [x] `Rename(entryName, newName)`.
- [ ] Investigate which archive formats support direct modification cleanly through SharpCompress.
- [ ] Where direct in-place modification is not safe or available, rebuild the archive into a temporary stream/file and atomically replace the original.
- [ ] Ensure failed modifications do not corrupt the original archive.
- [x] Reject modification attempts for read-only archive formats with a clear XPScript runtime error.

## Extraction

- [x] `Extract(entryName, targetPath)`.
- [x] `ExtractAll(targetDirectory)`.
- [x] `ExtractFolder(folderName, targetDirectory)`.
- [x] Support extracting directly to Byte arrays where appropriate without touching disk.
- [ ] Ensure extraction is transactional where practical when a failure occurs partway through processing.

## Reading entries without extraction

- [x] `ReadText(entryName)`.
- [x] `ReadBytes(entryName)`.
- [x] Define text encoding behavior and sensible defaults.
- [ ] Allow large entries to use streaming internally so the whole archive does not need to be buffered in memory.

## In-memory archive support

- [x] Add `ToBytes()`.
- [x] Support loading from Byte arrays.
- [ ] Consider a future stream abstraction if XPScript adds a first-class stream object.
- [x] Make in-memory operation the primary integration path for HTTP responses, REST APIs, Notes MIME attachments, database BLOBs and browser/WASM downloads.

Example to validate:

```text
Dim archive As New Archive()
archive.Create("zip")
archive.AddText("manifest.json", json)

Dim data As Variant
data = archive.ToBytes()
```

## Format support

Initial target matrix:

- [x] ZIP read/write.
- [x] 7z read/write where the selected SharpCompress version supports writing reliably.
- [x] TAR read/write.
- [x] GZip read/write.
- [ ] BZip2 read/write.
- [ ] LZip read/write.
- [ ] Zstandard read/write.
- [x] RAR read/extract only.
- [x] XZ read-only unless reliable writing support is available at implementation time.
- [x] ARC, ARJ, ACE, LZW and other SharpCompress-supported legacy formats as read-only where practical.
- [x] Verify the exact read/write capability matrix against the selected package version before implementation.
- [x] Expose `IsReadOnly = True` for formats such as RAR that cannot be modified.

## Password and encryption support

- [x] Investigate password-protected ZIP support in the selected SharpCompress version.
- [ ] Investigate encrypted RAR and 7z read support.
- [ ] Define how `Password` is supplied and cleared.
- [ ] Never log passwords.
- [x] Return a distinct runtime error for missing password versus invalid password where the underlying library makes that distinction reliably.
- [ ] Do not expose encryption algorithms that are insecure or not portable without explicit design review.

## Filesystem integration

- [x] Route physical source and destination paths through `XPScriptFileSystemRuntime.ResolvePath()` or the equivalent existing filesystem boundary.
- [x] Preserve current XPScript relative-path behavior.
- [ ] Reuse existing file overwrite semantics where appropriate.
- [x] Reuse existing safe path and reparse-point handling where possible.
- [ ] Do not allow archive functionality to become a bypass around filesystem sandboxing or portability rules.

## Zip Slip and path traversal protection

- [x] Treat every archive entry name as untrusted input.
- [x] Reject absolute archive paths.
- [x] Reject rooted Windows paths.
- [x] Reject UNC paths.
- [x] Normalize `.` and `..` path components.
- [x] Resolve the final extraction path before writing.
- [x] Verify the final extraction path remains under the requested extraction root.
- [x] Reject entries that escape the extraction root after normalization.
- [x] Apply XPScript-owned path traversal checks even if SharpCompress also contains Zip Slip protection.

Security regression entries should include at least:

```text
../evil.txt
../../evil.txt
/absolute/file.txt
C:\evil.txt
folder/../../../evil.txt
```

## Symlink and reparse-point protection

- [x] Reject archive symlink entries by default.
- [x] Reject hard links or other link-like entries by default where applicable.
- [x] Ensure existing directories in the extraction path cannot redirect writes outside the extraction root through symbolic links or reparse points.
- [x] Reuse XPScript filesystem reparse-point checks where possible.
- [ ] Add explicit tests for link-based extraction escapes.

## Decompression bomb protection

- [x] Define a secure maximum archive entry count.
- [x] Define a secure maximum individual uncompressed entry size.
- [x] Define a secure maximum total extracted byte count.
- [x] Define a secure maximum compression ratio.
- [x] Abort safely before resource exhaustion when a limit is exceeded.
- [ ] Apply the same limits to `ReadText()`, `ReadBytes()`, extraction and in-memory workflows.
- [x] Ensure limits are enforced independently of archive metadata that may be malicious or incorrect.

Potential future public properties if override support is required:

```text
MaxExtractSize
MaxEntries
MaxCompressionRatio
```

## Compiler integration

- [x] Update `CompilerBuildEnvironment.cs` to detect archive runtime use.
- [x] Add SharpCompress only when the generated program needs the archive runtime.
- [x] Follow the same conditional package-reference pattern already used for SQLite, SQL Server, MySQL, PostgreSQL/Supabase and UI dependencies.
- [x] Use a stable generated-source marker such as `XPScriptArchive` for dependency detection.
- [x] Keep applications that do not use `Archive` completely unaffected in package size and dependencies.
- [x] Pin the SharpCompress version used by generated projects.

Conceptual detection:

```text
usesArchive = generatedSource.Contains("XPScriptArchive")
```

## License and dependency maintenance

- [x] Add SharpCompress to `THIRD-PARTY-NOTICES.md` under the MIT section.
- [ ] Include the required copyright/license notice text.
- [x] Run `scripts/validate-license-notices.ps1`.
- [ ] Check transitive dependencies before merging.
- [ ] Check active security advisories and CVEs for the selected package version.
- [ ] Add archive dependency review to normal dependency-update maintenance.

## Windows, Linux and macOS

- [x] Run the same public `Archive` API on Windows.
- [x] Run the same public `Archive` API on Linux.
- [x] Run the same public `Archive` API on macOS.
- [x] Test path separator normalization.
- [x] Test Unicode filenames.
- [ ] Test case-sensitive and case-insensitive filesystem behavior.
- [ ] Test large files and archives where practical.

## Android and iOS

- [ ] Verify SharpCompress works with the .NET Android target used by XPScript.
- [ ] Verify SharpCompress works with the .NET iOS target used by XPScript.
- [ ] Test trimming.
- [ ] Test AOT compilation.
- [ ] Test application sandbox paths.
- [ ] Test Byte-array workflows.
- [ ] Test memory pressure with larger archives.
- [ ] Confirm no native archive runtime libraries need to be packaged per CPU architecture.

## Browser/WASM

- [ ] Verify the selected SharpCompress package can be linked for the XPScript browser/WASM target.
- [x] Support archive operations over Byte arrays/in-memory streams.
- [ ] Support listing entries client-side.
- [ ] Support reading entries client-side where memory limits permit.
- [ ] Support creating ZIP output in memory for browser downloads where practical.
- [ ] Do not expose arbitrary local filesystem extraction in browser/WASM.
- [ ] Use existing XPScript server-side `[]` execution for filesystem archive operations that require server access.

Example model:

```text
Dim archive As New Archive(bytes)
Print archive.FileCount
result = archive.ReadBytes("document.pdf")
```

Server-side filesystem operations can use the normal server execution model.

## Runtime errors

- [x] Archive not found.
- [ ] Unsupported archive format.
- [ ] Invalid or corrupt archive.
- [x] Encrypted archive requires password.
- [x] Invalid password.
- [x] Archive entry not found.
- [x] Archive format is read-only.
- [ ] Unsupported compression method.
- [x] Extraction path outside target directory.
- [x] Symlink or reparse-point extraction rejected.
- [x] Maximum archive entry count exceeded.
- [x] Maximum entry size exceeded.
- [x] Maximum total extracted size exceeded.
- [x] Maximum compression ratio exceeded.
- [ ] Save or replacement operation failed without corrupting the original archive.
- [x] Map errors into existing XPScript runtime error conventions.

## ZIP tests

- [ ] Create an empty ZIP archive.
- [ ] Add one file.
- [ ] Add a file under a different archive path.
- [ ] Add a folder recursively.
- [ ] Add text directly.
- [ ] Add Byte-array data directly.
- [ ] List entries.
- [ ] Validate file and folder counts.
- [ ] Read text from an entry.
- [ ] Read bytes from an entry.
- [ ] Remove an entry.
- [ ] Rename an entry.
- [ ] Save and reopen.
- [ ] Extract one entry.
- [ ] Extract one folder.
- [ ] Extract the complete archive.
- [ ] Create an archive entirely in memory.
- [ ] Export the result with `ToBytes()`.
- [ ] Reopen those bytes and validate the contents.

## RAR tests

- [ ] Open a RAR archive.
- [ ] List files and folders.
- [ ] Read an entry.
- [ ] Extract an entry.
- [ ] Extract the complete archive.
- [ ] Test encrypted RAR where supported.
- [ ] Verify `IsReadOnly = True`.
- [ ] Verify `AddFile`, `Remove`, `Rename` and `Save` reject unsupported modification cleanly.

## Other format tests

- [ ] Add round-trip tests for every format supported for writing.
- [ ] Add read/extract tests for every read-only format exposed publicly.
- [ ] Include nested directories and Unicode filenames.
- [ ] Include empty files and empty folders where the format supports them.
- [ ] Include malformed archive negative tests.

## Security tests

- [x] Zip Slip path traversal archive.
- [x] Absolute path archive entry.
- [x] Windows drive-root path archive entry.
- [x] UNC path archive entry.
- [ ] Mixed separator traversal.
- [x] Symlink escape attempt.
- [ ] Reparse-point escape attempt on Windows.
- [x] Excessive entry count.
- [x] Excessive uncompressed size.
- [x] Extreme compression ratio.
- [ ] Incorrect archive size metadata.
- [ ] Corrupt compressed stream.
- [ ] Password-protected archive with missing password.
- [ ] Password-protected archive with incorrect password.
- [ ] Verify failed extraction does not leave unsafe partial files outside controlled locations.

## Cross-platform CI

- [x] Run archive runtime tests on Windows x64.
- [x] Run archive runtime tests on Linux x64.
- [x] Run archive runtime tests on macOS.
- [ ] Add Android build validation.
- [ ] Add iOS build validation.
- [ ] Add browser/WASM build validation.
- [ ] Add trimming/AOT validation where those publish modes are supported.

## Documentation

- [x] Document `Archive` constructors.
- [ ] Document all `Archive` properties.
- [ ] Document all `Archive` methods.
- [x] Document `ArchiveEntry`.
- [x] Document the format read/write matrix.
- [x] Document RAR read-only behavior.
- [x] Document password support and limitations.
- [ ] Document secure extraction behavior.
- [ ] Document resource limits.
- [x] Document Byte-array/in-memory usage.
- [ ] Document browser/WASM limitations.
- [ ] Document server-side archive handling.
- [ ] Document Android/iOS usage and storage considerations.

## Examples

- [ ] Add `archive-create.xps`.
- [ ] Add `archive-list.xps`.
- [ ] Add `archive-extract.xps`.
- [ ] Add `archive-memory.xps`.
- [ ] Add `archive-rar.xps`.
- [ ] Add `archive-password.xps` if password support is exposed in v1.

## Suggested implementation milestones

### Milestone 1: ZIP foundation

- [ ] Finalize `Archive` and `ArchiveEntry` public API.
- [ ] Add conditional SharpCompress dependency injection.
- [ ] Implement ZIP create/open/list/add/remove/rename/save/extract.
- [ ] Implement `ReadText`, `ReadBytes` and `ToBytes`.
- [ ] Implement path traversal, symlink/reparse-point and decompression-bomb protection.
- [ ] Add ZIP unit, integration and security tests.
- [ ] Add initial documentation and examples.

### Milestone 2: Additional archive formats

- [ ] Add RAR read/extract support.
- [ ] Add 7z support according to verified write capability.
- [ ] Add TAR, GZip, BZip2, LZip and Zstandard support.
- [ ] Add additional read-only formats where useful.
- [ ] Add password/encryption support where reliable.
- [ ] Add format-specific tests.

### Milestone 3: Mobile and WASM validation

- [ ] Complete Byte-array and stream-oriented workflows.
- [ ] Validate Android.
- [ ] Validate iOS and AOT.
- [ ] Validate browser/WASM.
- [ ] Add browser download integration where appropriate.
- [ ] Complete cross-platform documentation.

## Architectural rule

The public dependency direction must remain:

```text
XPScript code
    ↓
Archive / ArchiveEntry
    ↓
XPScript archive runtime
    ↓
SharpCompress
```

- [x] XPScript scripts must never depend directly on SharpCompress namespaces, classes, enums or package-specific behavior.
- [x] Preserve the ability to replace or upgrade the underlying archive engine without breaking the public XPScript API.
