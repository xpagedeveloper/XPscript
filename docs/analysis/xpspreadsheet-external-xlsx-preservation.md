# XPSpreadsheet external XLSX preservation/editing analysis

## Goal

Allow XPSpreadsheet to open an external/unmarked `.xlsx`, modify supported workbook/worksheet/cell data, and save the workbook without rebuilding unrelated OOXML parts. Charts, images, pivot tables, tables, named ranges, slicers, custom XML, external links, metadata, macros in macro-enabled variants, and other unsupported parts should remain untouched unless XPScript explicitly modifies a part that references them.

This should complement, not replace, `SaveAsSimple()`. `SaveAsSimple()` remains the explicit "rebuild into the supported XPSpreadsheet model" operation.

## Current problem

The current runtime reads selected workbook data into an in-memory model and, on save, builds a new minimal XLSX package. That is safe for XPScript-created workbooks but unsafe for arbitrary external workbooks because unsupported package parts and worksheet XML elements are discarded. The current `CanUpdate` guard correctly prevents this.

The enhancement should change external editing from **rebuild mode** to **preservation/patch mode**.

## Recommended architecture: package-preserving patch mode

When opening an external workbook, retain the original OPC ZIP package (or a seekable copy of it) as the authoritative source package. Build the existing XPSpreadsheet object model for supported operations, but also retain enough package metadata to map workbook objects back to their original parts.

On `Save()` / `SaveAs()`:

1. Create a new temporary ZIP package.
2. Copy every original ZIP entry to the destination unchanged unless it is in the dirty-part set.
3. Regenerate or surgically patch only dirty XML parts.
4. Add any genuinely new parts and required relationships/content-type declarations.
5. Atomically replace the target file only after the new package has been fully written and validated.

This follows OOXML/Open Packaging Conventions: a workbook is a ZIP package of independent parts connected by relationships. Preserving untouched parts is substantially safer than reconstructing the entire workbook.

## Preserve at part granularity first

The first implementation should preserve at **part granularity**, not byte offsets inside XML files.

Examples:

- Changing `Sheet1!B7` dirties `xl/worksheets/sheet1.xml`.
- Changing only a cell value should not rewrite `xl/workbook.xml`, styles, drawings, images, pivot tables, themes, or unrelated sheets.
- Renaming a worksheet dirties `xl/workbook.xml` but not the worksheet part itself.
- Adding a worksheet dirties `xl/workbook.xml`, `xl/_rels/workbook.xml.rels`, `[Content_Types].xml`, and creates a new worksheet part.
- Adding a new style may dirty `xl/styles.xml` and the affected worksheet part.

Part-level preservation already gives a very large safety improvement. A later phase can patch only specific XML subtrees inside a dirty part when preserving unknown worksheet markup becomes important.

## Critical requirement: preserve unknown XML inside dirty worksheets

Replacing an entire external `sheetN.xml` with XPSpreadsheet's minimal worksheet serializer would still destroy worksheet-local features such as:

- conditional formatting
- data validation
- hyperlinks
- merged cells
- tables and table references
- drawings/images/charts
- comments/notes references
- page setup / print settings
- sheet views and panes
- row/column metadata not represented by XPSpreadsheet
- extLst extensions
- sparkline groups
- legacy drawings/forms

Therefore external-edit mode should not use the current `BuildWorksheet()` implementation directly.

Recommended strategy:

- Load the original worksheet XML document.
- Locate/create `<sheetData>`.
- Patch only the rows/cells explicitly changed by XPSpreadsheet.
- Preserve every other worksheet child element and attribute verbatim at the XML-tree level.
- Preserve unknown attributes/elements on rows and cells wherever possible.

For a changed cell, replace only the supported value/formula/style attributes/elements rather than reconstructing the entire row.

## Dirty tracking

Add explicit dirty tracking instead of inferring changes at save time.

Suggested internal state:

```text
WorkbookDirtyFlags
  WorkbookMetadata
  WorkbookRelationships
  ContentTypes
  Styles
  SharedStrings
  CalculationProperties

WorksheetDirtyState
  IsNew
  IsRemoved
  IsRenamed
  Cells: HashSet<(row,column)>
  Dimensions
  AutoFilter
  OtherSupportedWorksheetFeatures
```

Every mutating XPSpreadsheet API should mark only the minimum required dirty state.

This also makes preservation behavior testable.

## Original package model

Suggested internal representation:

```text
XPScriptSpreadsheetPackage
  OriginalBytes / original file source
  EntryCatalog
  WorkbookPartName
  WorkbookRelationshipsPartName
  ContentTypesPartName
  StylesPartName?
  SharedStringsPartName?
  WorksheetMap

WorksheetMap entry
  SheetName
  SheetId
  RelationshipId
  WorksheetPartName
  WorksheetRelationshipsPartName?
```

Do not assume worksheet filenames are `sheet1.xml`, `sheet2.xml`, etc. Always resolve worksheet parts through `workbook.xml` + `workbook.xml.rels`.

Relationship targets must be normalized as OPC URIs, including `../` resolution.

## Cell patching rules

For each dirty cell:

1. Find/create the correct `<row r="...">` in `<sheetData>`.
2. Find/create `<c r="A1">` in correct column order.
3. Preserve unsupported attributes on the cell unless they conflict with an explicitly changed supported property.
4. Update only the requested supported fields:
   - value/type representation
   - formula
   - style index when style was explicitly changed
5. If `Clear()` was called, define carefully whether the cell node should be removed completely or only supported data cleared while preserving unsupported cell metadata.

The safer external-workbook rule is:

- `cell.Value = ...` changes only value representation.
- `cell.Formula = ...` changes formula/value cache behavior.
- style setters change only the style index/reference.
- `cell.Clear()` is destructive and should be documented as clearing the entire cell payload that XPSpreadsheet owns; consider a separate `ClearValue()` / `ClearFormula()` API later for less destructive editing.

## Shared strings

External workbooks may use `xl/sharedStrings.xml` while XPScript currently often writes inline strings.

Safest first implementation:

- When editing an existing string cell, allow writing it as `inlineStr` in the worksheet without rewriting the existing shared strings table.
- Do not delete or compact old shared string entries.
- Preserve `sharedStrings.xml` unchanged unless a future API explicitly manages it.

This avoids global index renumbering and reduces blast radius.

## Styles

Styles are the most difficult global dependency because cells reference `cellXfs` by integer index.

Recommended rule:

- If only values/formulas change, preserve `xl/styles.xml` byte-for-byte and preserve existing cell `s` attributes.
- When formatting an existing external cell, append new fonts/fills/borders/numFmts/cellXfs rather than reindexing or rebuilding existing style collections.
- Never compact/reorder existing style tables in preservation mode.
- Deduplicate against styles XPSpreadsheet itself appends during the current session where practical.

This lets existing unsupported style details remain valid.

## Calculation chain and cached formulas

Changing formulas can make `xl/calcChain.xml` stale. Options:

1. surgically update calculation chain entries, or
2. safer initial implementation: remove only `xl/calcChain.xml` and its relationship/content-type entry when formulas are changed, and set workbook calculation properties to force recalculation on open.

Removing a stale calculation chain is generally safer than preserving incorrect dependencies. Formula value caches should also be considered stale after formula edits.

## Workbook operations

### Rename worksheet

Patch the matching `<sheet name="...">` in `xl/workbook.xml`. Preserve sheet ID, relationship ID, worksheet part, relationships, drawings, and all other parts.

Formula/name references containing sheet names are a separate problem. A robust rename eventually needs to update formulas, defined names, charts, validations, pivot sources, etc. Therefore initial preservation mode should either:

- support rename with a documented limitation, or
- keep external-workbook worksheet rename disabled until reference rewriting exists.

Recommendation: keep rename disabled for external workbooks in phase 1.

### Add worksheet

This is feasible without touching existing worksheets:

- choose a unique worksheet part name
- allocate unique `sheetId`
- allocate unique workbook relationship ID
- add worksheet part
- append workbook relationship
- add content-type override if required
- add `<sheet>` entry to workbook

This is a good phase-2 capability after cell patching works.

### Remove worksheet

Removal can break defined names, formulas, charts, pivots, external references, and relationships. Keep disabled for external workbooks initially.

## API proposal

Keep existing API simple. External preservation should mostly be automatic:

```xpscript
Dim book As New XPSpreadsheet("customer-template.xlsx")
Dim sheet As XPWorksheet
Set sheet = book.Worksheet("Data")

sheet.Cell("B7").Value = 12345
book.SaveAs("customer-output.xlsx")
```

Suggested capability properties:

```text
book.PreservationMode          ' True for external package-preserving mode
book.CanUpdate                 ' True when requested operations are safely supported
book.HasUnsupportedFeatures    ' optional diagnostic hint
book.LastSavePreservedPackage  ' optional diagnostic/testing property
```

A stronger API could expose:

```xpscript
book.OpenPreserve("template.xlsx")
```

but automatic preserve mode on external `.xlsx` is cleaner if safety guarantees are strong enough.

`SaveAsSimple()` should remain available as an explicit destructive conversion.

## Phase plan

### Phase 1 — safest/highest value

Support external workbook preservation for:

- modify existing cell values
- modify existing formulas
- add values/formulas to previously empty cells
- save/save-as while preserving all unrelated package parts
- preserve all unknown worksheet XML outside explicitly changed cells
- preserve existing styles and relationships
- invalidate stale calculation chain when formulas change

Do not initially support external workbook:

- worksheet rename/remove
- style mutation
- row/column deletion
- structural range insertion/deletion

### Phase 2

Add:

- append-only external style editing
- row height / column width patching
- AutoFilter patching
- add new worksheet
- XPScript workbook marker as a non-destructive custom property if desired

### Phase 3

Add more structural editing only with dependency-aware updates:

- rename/remove worksheets
- insert/delete rows/columns
- tables
- named ranges
- merged cells
- hyperlinks/data validation/conditional formatting

## Testing strategy

Create fixture workbooks produced by Excel/LibreOffice containing combinations of unsupported features. Tests should compare package parts before/after.

Required assertions:

- untouched ZIP entries have identical uncompressed bytes
- chart/image/pivot/table/theme/custom XML parts remain present and unchanged
- unrelated worksheet XML is unchanged
- worksheet relationship files are unchanged unless required
- only expected dirty XML nodes differ in modified worksheets
- workbook opens without repair warnings in Excel/LibreOffice-compatible validation scenarios
- repeated save operations are stable

Useful fixtures:

1. workbook with chart + image + custom formatting; modify one plain cell
2. workbook with pivot table; modify source-independent cell
3. workbook with table + data validation + conditional formatting; modify one value
4. workbook with shared strings; change/add string value
5. workbook with formulas + calcChain; change formula
6. workbook with unusual worksheet part names/relationship IDs
7. workbook containing macros should be rejected as `.xlsm` under current `.xlsx`-only policy unless macro-enabled formats are deliberately added later

## Security limits

Keep the existing ZIP bomb defenses, XML DTD prohibition, part-count limits, expanded-size limits, and per-part limits.

Preservation mode must additionally guard against:

- duplicate ZIP entry names
- path traversal / invalid OPC part names
- external relationship targets when traversing package internals
- relationship cycles
- oversized relationship/XML parts
- malformed relationship target normalization

Do not dereference external URLs; preserve external relationships as opaque package metadata.

## Dependency choice

A custom implementation remains viable because XPSpreadsheet already uses `System.IO.Compression` + `System.Xml.Linq` and the preservation algorithm primarily needs OPC part copying plus targeted XML patching.

The Open XML SDK is also technically suitable and is designed for package/part manipulation, but adopting it would add a significant dependency and change the runtime architecture. The strongest reason to use it would be schema-aware manipulation of increasingly complex features, not basic preservation itself.

Recommendation: implement phase 1 with the existing BCL-only architecture. Re-evaluate Open XML SDK only if phase 3 expands into broad schema-aware editing.

## Recommendation

Implement **surgical package preservation** as a separate writer path:

```text
new XPScript-created workbook -> current/rebuilt writer
external workbook + safe supported edits -> preservation writer
explicit SaveAsSimple -> rebuilt writer
```

The central invariant should be:

> If XPSpreadsheet did not explicitly modify a package part or XML node, it must preserve it.

That gives XPScript the template-filling workflow that is most valuable in business applications while sharply limiting the risk of destroying Excel features XPSpreadsheet does not understand.

## Branch note

This analysis branch starts from current `main` (`5945c69a12cda001f7749c168807ced4175dfd88`). The XPRange/formatting work in PR #561 is not yet on `main`. A future implementation branch should rebase/branch from the post-#561 main state if that PR is merged, so external style/layout preservation can integrate with the richer style model rather than duplicating it.
