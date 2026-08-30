# NotesViewNavigator cache policy

XPscript uses `NotesViewNavigator.CacheSize` as a native metadata prefetch batch size.

- Default: 64 entries.
- Range: 0 through 512. Values outside the range are clamped.
- `0` requests one native row at a time and therefore disables lookahead batching.
- Prefetch contains view-row metadata only. `ColumnValues` remains lazy.
- Prefetch is used only while the parent `NotesView.AutoUpdate` is `False`.
- With `AutoUpdate=True`, navigation refreshes the parent view and does not reuse prefetched lookahead across calls.
- `NotesView.Refresh()` increments the parent view navigation generation. A navigator detects that generation change, discards its stale lookahead and reloads from the current view position before continuing.
- Streaming navigation retains at most 2048 already traversed metadata rows behind the current cursor. This bounds history growth for long sequential loops while leaving `CacheSize` free to tune native read throughput.
- Operations that require a complete/global view, such as `Count`, `GetLast`, hierarchy resolution and position lookup, may still materialize the remaining view metadata for that operation.
