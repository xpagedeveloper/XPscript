# Notes C API cross-platform testing

The XPscript Notes object model is implemented through the native HCL Notes/Domino C API. Runtime library discovery is platform-specific:

- Windows: `nnotes.dll`
- Linux: `libnotes.so` or `libnotes64.so`
- macOS: `libnotes.dylib` or `libnotes64.dylib`

The runtime uses .NET `NativeLibrary.Load` and resolves C API exports dynamically. Native handles and pointers are represented with `nint`, so pointer-sized values remain correct on 64-bit Windows, Linux and macOS.

## Thread lifecycle

`NotesInitExtended` initializes the process and the thread that creates `NotesSession`. Any other thread that performs Notes C API work is initialized with `NotesInitThread` before the native operation and terminated with `NotesTermThread` on that same thread when the operation scope ends. Nested Notes calls on the same worker thread share one thread-initialization scope.

This operation-scoped model is intentional for .NET thread-pool hosts such as Kestrel and FastCGI. It avoids leaving Domino per-thread state attached to worker threads whose lifetime is controlled by .NET.

## Automated tests

GitHub Actions runs `tests/NativeInteropSmoke` on Ubuntu and macOS. The test intentionally does not require HCL Notes/Domino. It verifies the same platform primitives used by the Notes bridge by repeatedly:

- allocating, reading, writing and freeing unmanaged memory with `Marshal.AllocHGlobal` / `Marshal.FreeHGlobal`;
- loading a standard system native library with `NativeLibrary.Load`;
- resolving a native function export;
- converting the function pointer to a delegate and invoking it;
- unloading the native library.

On Linux the smoke test uses `libc.so.6`. On macOS it uses `/usr/lib/libSystem.B.dylib`.

The workflow also builds the compiler and compiles [`samples/notes-c-api-surface.xps`](../samples/notes-c-api-surface.xps), which validates that the generated Notes runtime including the thread-lifecycle code compiles on both Ubuntu and macOS.

## Manual Notes/Domino tests

Actual Notes C API integration remains a manual test because CI does not contain a licensed/configured HCL Notes or Domino runtime, Notes ID, `notes.ini`, or NSF test environment.

Before declaring a Notes/Domino release verified on an operating system, manually test at least:

1. `NotesSession` initialization and `Recycle()`.
2. Local and remote `NotesDatabase` open/close.
3. `NotesView` open, navigation, refresh and recycle.
4. `NotesDocument` open, item read/write, save and recycle.
5. `NotesItem` and `NotesRichTextItem`, including attachments.
6. Search, FT search and agent execution where available.
7. Repeated create/recycle cycles while observing process memory and native handle counts.
8. Process exit with both explicitly recycled and intentionally unrecycled Notes objects.
9. On a Domino server, initialize with the server runtime directory and the server `notes.ini`, confirm that `ServerKeyFileName`/the configured server ID is used, and test both passwordless server-ID operation and an explicit ID password where required.
10. Exercise the same `NotesSession` from multiple .NET worker threads and verify repeated database/document/view operations, errors and recycle paths without crashes, handle corruption or steadily increasing memory usage.

The automated native-interoperability test proves that the .NET native loader and unmanaged-memory mechanisms work on Linux and macOS. It does not prove that every HCL C API entry point, structure layout or runtime behavior is compatible with a particular Notes/Domino release; that remains part of the manual integration test.
