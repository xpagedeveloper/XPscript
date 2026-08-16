# Memory management and object/resource lifetime

(c) xpagedeveloper.com 2026

XPScript uses .NET managed-memory semantics. Managed objects are reclaimed by the .NET garbage collector after the last strong reference becomes unreachable. XPScript does not call `GC.Collect()` as normal language behavior and does not promise immediate deallocation when a reference is cleared.

## Managed object references

- `Nothing` is the empty state of an object reference. It is distinct from Variant `Null` and Variant `Empty`.
- `Set object = Nothing` clears only that reference cell. If another alias still refers to the object, the object remains reachable.
- `Delete object` invokes the class `Sub Delete` contract and invalidates the shared object-reference cell. Aliases to that same cell therefore observe the deleted/Nothing state.
- `Sub Delete` is deterministic language cleanup. It is not a GC finalizer. XPScript does not synthesize CLR finalizers for user classes.
- Clearing the last reference makes a managed object eligible for GC. Collection timing remains controlled by .NET.
- Locals use normal CLR local-variable lifetime. ByRef wrappers exist only for the generated call path and are not stored in a runtime-global cache.
- Module globals and Static values are intentionally strong references for their declared lifetime. Reassignment or clearing releases the previous reference unless another alias still owns it.

## Arrays, Lists and Type values

- Dynamic `Erase` replaces array storage with empty storage, releasing references held by removed elements.
- Fixed-array `Erase` overwrites every slot with its type default, releasing object references from those slots.
- `ReDim` without Preserve replaces the old backing array. Removed elements are no longer retained by the array runtime.
- `ReDim Preserve` copies only retained coordinates into new storage. The temporary old backing array is method-local and is not cached after the operation.
- List `Erase` removes the dictionary entry. List `Clear` removes all entries. Removed values are no longer strongly referenced by the list.
- Type/UDT copy helpers create replacement values and arrays. The compiler does not maintain hidden global aliases to Type copies. Explicit object-reference fields keep normal object-reference semantics.

## JSON, HTTP and caches

- JSON nodes and navigators are managed objects with no process-global object cache. Child element wrappers may keep their owning JSON node reachable while the wrapper itself is reachable, which is required for mutation semantics.
- Native HTTP requests dispose request, response, response stream, cancellation source, handlers and clients according to their ownership model.
- `XPScriptHttpClient` owns its `HttpClient` and handler and implements idempotent `IDisposable` cleanup.
- HTTP response objects keep copied response bytes only for the lifetime of the response object. They do not retain the network response or socket.

## Files, locks, processes and native resources

- `Close` removes the file number from the runtime table and disposes reader, writer and file stream. Closing a stream also releases OS byte-range locks associated with that handle.
- `Unlock` explicitly releases requested file locks while the file remains open.
- `Reset` operations reset language state but do not replace `Close` for an open file handle unless the specific API documents that behavior.
- Directory enumeration releases its enumerator when enumeration completes or a new enumeration starts.
- `Shell` does not own the child process lifetime after successful launch. The temporary local `Process` object is disposed immediately after launch, releasing the parent-side process handle without terminating the child.
- Native DllImport libraries resolved for generated P/Invoke calls are process-lifetime dependencies. They are intentionally left loaded because generated native entry points may use them for the remainder of the process.
- The runtime does not expose general unmanaged allocation APIs that require user-managed `FreeHGlobal` or equivalent cleanup.
- COM-specific lifetime management is not currently part of the standalone runtime surface. If COM objects are added later, their ownership and release contract must be explicit rather than relying on forced GC.

## IDisposable and IAsyncDisposable

Runtime-owned `IDisposable` objects are disposed by the runtime that creates them. Disposal paths are idempotent where an object can be explicitly disposed more than once. Short-lived request, response, stream, reader, writer, lock-handle and process wrapper objects use deterministic cleanup.

XPScript does not automatically call `Dispose` or `DisposeAsync` on arbitrary user-managed CLR objects when a variable goes out of scope. Ownership of externally supplied managed objects remains with the API that creates or transfers them unless a specific XPScript wrapper documents ownership transfer.

## Testing contract

Lifetime regressions may use `WeakReference` and explicit GC cycles in test-only code to prove GC eligibility. This does not change language behavior. Production/runtime source must not use `GC.Collect()` for normal memory management.

Cross-platform lifetime tests cover object aliasing and Delete behavior, array/list release contracts, file open/close and lock release, process-wrapper disposal policy, HTTP disposal patterns, absence of production `GC.Collect()`, and bounded stress execution.