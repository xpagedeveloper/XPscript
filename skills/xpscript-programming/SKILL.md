# XPscript programming skill

Use this skill when writing, reviewing, explaining, or modifying XPscript (`.xps`) programs.

## Goal

Produce idiomatic XPscript that matches the repository's current compiler/runtime behavior. Prefer repository documentation and samples over assumptions based on VB, LotusScript, PowerShell, PHP, C#, or other similar languages.

## Source of truth

When syntax or behavior is uncertain, consult these files in this order:

1. `docs/language-reference.md` — language syntax and built-ins.
2. `docs/api-reference.md` — runtime objects and higher-level APIs.
3. `docs/forall-iteration.md` — the common `ForAll` iterable contract.
4. `docs/file-io-reference.md` — filesystem and file I/O.
5. Feature references such as `docs/native-xml.md`, `docs/notes-mime-entity.md`, `docs/sqlite.md` and `docs/mssql.md`.
6. `samples/*.xps` and `demo/**/*.xps` — executable examples.
7. Compiler/runtime implementation only when documentation is ambiguous.

Do not invent a built-in or class member because it exists in a related language.

The normal CLI `compile` and `run` commands use the AST compiler. Use `ast-compile` or `ast-run` to make that selection explicit. During migration, `legacy-compile`/`compilev1` and `legacy-run`/`runv1` invoke the legacy compiler; do not document legacy behavior as the default.

## Core language model

XPscript is case-insensitive and VB/LotusScript-like, but it has its own runtime and compatibility rules.

Prefer explicit declarations in generated programs:

```xpscript
Option Declare

Sub Main()
    Dim message As String
    message = "Hello from XPscript"
    Print message
End Sub
```

Use `Sub ... End Sub` for procedures without a result and `Function ... End Function` for value-returning procedures.

Functions return values by assigning to the function name:

Use `Static name As Type` inside a procedure when a local must retain its value across calls. Experimental `ast-compile` supports scalar storage, initializer-free arrays, Lists, Variant, Object and user-class references. `Static item As Object` starts as Nothing; use `Set item = New Object()` to create it once and retain it across calls. Strings begin empty and numeric/Boolean values begin at zero/false. Scalar explicit initializers are supported as an AST extension; complete initializer semantics remain pending. See `docs/ast-csharp-emitter.md`. This concerns procedure locals; classes and class members remain instance-based.

That assignment stores the current result and execution continues. `Exit Function` returns the stored result immediately; reaching `End Function` also returns it. Do not treat assigning the function name as an early return.

```xpscript
Function Add(a As Integer, b As Integer) As Integer
    Add = a + b
End Function
```

Optional parameters are supported:

For the experimental `ast-compile` path, omitted trailing Optional parameters execute real procedure bodies with fresh storage for omitted ByRef defaults. Explicit empty argument slots and complete constant/default validation remain pending; see `docs/ast-csharp-emitter.md`. Do not assume full Optional compatibility from this experimental subset.

```xpscript
Function Greeting(name As String, Optional prefix As String = "Hello") As String
    Greeting = prefix & " " & name
End Function
```

Use block control flow unless a repository sample proves a shorter form is supported:

`GoTo label` targets a case-insensitive label in the same Sub or Function. Experimental `ast-compile` rejects GoSub as unimplemented; use `Call routine(...)` for a Sub or a Function expression instead, passing required values as parameters. GoTo coverage includes If/ElseIf/Else, While, Do and Select; transfers involving For/ForAll remain migration work. See `docs/ast-csharp-emitter.md` and the executable GoTo fixtures in `tests/ast-compile-probe`.

For a declared List, `ForAll alias In values` exposes a writable element alias: assignment to the alias updates the List, and `ListTag(alias)` returns its tag. Prefer different alias names in nested loops for legacy compatibility. Experimental AST covers typed List declarations without initializers, alias reads/writes, tags and iteration snapshots; ByRef alias copy-back and the complete List API remain pending. See `docs/forall-iteration.md` and `tests/ast-compile-probe/forall-list-alias.xps`.

Use `IsElement(values(tag))` to test for a List tag before reading its value. The experimental AST path supports this for declared Lists and returns False for a missing tag; it evaluates the tag expression once. Dynamically supplied Lists and complete argument validation remain migration work.

Experimental AST supports Sub and Function calls with matching typed ByRef List elements or ForAll aliases. It evaluates each List/tag once in argument order and copies changed values back, including if the called procedure throws. Named Function wrappers also support forwarding ordinary ByRef parameters. Declare List parameters as `ByVal values List As Long` or `ByRef values List As Long`: ByVal shares the List object, while ByRef also allows replacing the caller's List reference. Functions can return Lists through `As Variant`; initialize a typed List with that result, for example `Dim values List As Long = MakeValues()`. IsElement accepts one keyed List access and checks dynamically supplied Lists without reading a missing element.

Use `Erase values(tag)` to remove one List entry or `Erase values` to clear all entries. Experimental AST supports these forms for declared Lists, including safe removal of a missing tag and removals during ForAll snapshot iteration. It also supports fixed one-dimensional declared arrays; dynamic and multidimensional array lifecycle remains migration work.

```xpscript
If value > 0 Then
    Print "positive"
ElseIf value < 0 Then
    Print "negative"
Else
    Print "zero"
End If
```

## Classes and object lifetime

AST constructors accept `ByVal values List As Long` or `ByRef values List As Box`, including implicit ByRef and trailing Optional Lists. ByVal shares elements but replacing the parameter stays local; ByRef can replace the caller's List slot, with duplicate aliases and writes preserved on errors. Pass the exact declared element type: Lists are not covariant. Omitted Lists default to Nothing, with fresh omitted ByRef slots; a factory returning Variant can provide a fresh typed List and later defaults may read it. Lists do not own their images/objects; typed image field stores clone as usual. Ordinary class-method List signatures and array/scalar XPImage parameters remain deferred. See `class-constructor-list.xps`, `image-constructor-list.xps` and the typed List constructor contract.

AST constructors accept Variant and JSON/XML/CSV/spreadsheet/HTTP references as required or Optional ByVal/ByRef parameters. ByVal is a snapshot but native mutations are shared; ByRef must receive exact-type writable storage and changes remain visible on failure. Omitted native references start as Nothing, omitted Variant as Empty, with fresh omitted ByRef cells and sequential dependent defaults. Nothing is not an implicit Variant default; use Empty/omission or a typed reference as appropriate. Variant-held images remain borrowed; Delete/failure only clears the instance's owned typed image fields. Dispose HTTP clients explicitly. Declared scalar XPImage parameters and array signatures remain deferred. See `class-constructor-native.xps`, `class-constructor-variant.xps` and `image-constructor-variant.xps`.

Experimental AST supports multiple `Sub New` signatures, including Optional parameters and Parent.New forwarding. Use distinct parameter types/modes; parameter names/default values do not distinguish signatures. ByRef requires exact-type writable storage. Identity and more specific reference conversions win; equivalent matches prefer fewer omitted arguments. Ambiguous calls are errors, not declaration-order choices. See `class-constructor-overloads.xps` and the constructor overload contract in `docs/ast-csharp-emitter.md`. XPImage/array parameter signatures remain deferred.

Experimental AST constructors accept Optional ByRef Object/user-class parameters, including implicit ByRef. `Sub New(Optional value As Box = New Box())` gets fresh typed omitted storage; no initializer means Nothing. A later default may read/mutate earlier references. Supplied exact-type locals/parameters/fields, initializer-free typed module references, array/List elements and writable List ForAll aliases stay live through replacement and errors; ByVal is a snapshot. Array bounds and concrete class types are preserved. Do not infer writable array ForAll aliases or ordinary List-call alias changes. Borrowed/default user objects keep their own image fields until explicitly cleaned. See `class-constructor-optional-refref.xps`, `class-constructor-reference-storage.xps` and `image-constructor-optional-refref.xps`.

Experimental AST supports `Sub New(Optional ByVal value As Box = New Box())` and Optional ByVal Object references. Omit a trailing suffix with `New Record()` / `Parent.New()`; no initializer means Nothing. Defaults run once in order before bodies, can read earlier parameters/module routines and create fresh objects per call, but cannot read instance state or later parameters. Use explicit ByVal: Optional ByRef references are also supported as described above. Clearing a parameter stays local, while object mutations are shared. Default-created user objects and their image fields are not automatically owned or Deleted; retain their owners for explicit cleanup. Required scalar/reference ByRef parameters may precede these defaults. See `class-constructor-optional-reference.xps`, `image-constructor-optional-reference.xps` and the Optional reference constructor contract.

Experimental AST supports required `Sub New(ByRef value As Box)` or `As Object`, including implicit ByRef. Pass an exact-type writable local/parameter/user-class field, not a New expression, accessor, Variant or differently declared ancestor/subclass/Object slot. A correctly declared variable holding Nothing is valid. `New Record(value)` / `Parent.New(value)` can replace or clear the caller's reference; passing the same slot twice preserves live aliases, even on error. Required references can precede scalar Optional parameters. A child's ByVal reference forwarded to a ByRef base remains only a child snapshot. Replacement does not Delete old or replacement objects or their images; retain old owners for explicit cleanup. XPImage parameters, initialized compatibility module storage and array signatures remain deferred. See `class-constructor-reference-byref.xps` and `image-constructor-reference-byref.xps`; normal ByRef constructor behavior still differs, and ordinary method/module signature migration is separate.

Experimental AST constructors accept required `ByVal value As Object` or `ByVal value As Box` for a declared user class. Call `New Record(box)` or `Call Parent.New(box)`. Object mutations are shared, but replacing the parameter with Nothing does not replace the caller's reference. User-class upcasts, Nothing and existing Variant conversions are supported; unrelated classes/downcasts and statically scalar Object arguments are invalid. Required reference parameters can precede scalar Optional parameters, whose defaults can read earlier reference members. Use Set for reference assignment. The input object is borrowed, including its XPImage fields; construction failure/Delete must not dispose it. Declared XPImage/array parameters remain deferred; ordinary class-method object signatures are not enabled. See `class-constructor-reference.xps`, the narrow concrete-class normal parity fixture and `image-constructor-reference.xps`.

Experimental AST supports `Sub New(Optional ByRef value As Long = 1)` and implicit ByRef `Optional value As Long = 1`. `New Record()` gives fresh omitted storage; `New Record(value)` aliases exact-type writable caller storage. Later defaults may mutate earlier omitted/supplied references; repeated calls never share omitted cells. Required parameters may precede the Optional suffix, and ByRef/ByVal may mix. The same applies to Parent.New and implicit all-Optional bases. Omit a trailing suffix, not empty slots; use explicit ByVal for snapshots. Default errors reach the caller before body entry and preserve old targets while retaining supplied reference writes. XPImage/array constructor signatures remain deferred. This is AST capability, not legacy ByRef/omission parity. See `class-constructor-optional-byref.xps`, `constructor-optional-byref-zero.xps` and `image-constructor-optional-byref.xps`.

Experimental AST also allows required scalar ByRef parameters followed by Optional ByVal: `Sub New(ByRef seed As Long, Optional ByVal extra As Long = NextValue(seed))`. Use `New Record(seed)` or `Call Parent.New(seed)`. Defaults execute once after supplied arguments and can read/change earlier required references; duplicate aliases and writes survive default errors. Supplied ByVal values remain snapshots. Optional ByRef (including implicit ByRef) is also supported as described above. See `class-constructor-mixed.xps`, `image-constructor-mixed.xps` and the mixed-constructor contract in `docs/ast-csharp-emitter.md`.

Experimental AST supports required scalar ByRef constructors: `Sub New(ByRef seed As Long)` and `Dim item As New Record(seed)`. Required parameters without a modifier are also ByRef. Supply exact-type writable storage: locals, parameters, user-class fields, typed initializer-free Private/Public module scalars or typed array/List elements/ForAll aliases. Duplicate arguments share the same location, with once-only ordered receiver/index/tag evaluation; changes survive errors in constructors/base arguments. Parent.New forwards references directly; passing a child's ByVal parameter by reference changes only that child's snapshot. Failed construction preserves an old target and cleans image fields/locals without user Delete. Do not pass literals/accessor results or assume type coercion for ByRef. XPImage/array parameters remain deferred. Normal compilation currently fails simple ByRef construction at runtime, so this is AST capability, not legacy parity. See `class-constructor-byref.xps`, `image-constructor-byref.xps` and the AST constructor contract.

Experimental AST supports trailing Optional scalar ByVal constructor parameters: `Sub New(ByVal seed As Long, Optional ByVal label As String = "default")`, called with `New Record(4)` or `Call Parent.New(4)`. Omit a trailing suffix, not explicit empty slots. Defaults run once in order after supplied arguments, before bodies, and may read earlier parameters/module routines, not Me, fields, self/later parameters or locals. Missing scalar defaults are zero/False/empty String. An all-Optional base can be called implicitly. Handle default errors in the caller, not the not-yet-entered constructor body; an old target survives. Image-owning routines called by defaults keep ordinary local cleanup. XPImage/array constructor parameters remain deferred. Normal compilation still requires full constructor arity despite Optional; omission support here is AST-specific. See `class-constructor-optional.xps`, `image-constructor-optional.xps` and the Optional constructor contract in `docs/ast-csharp-emitter.md`.

Experimental AST also supports scalar ByVal constructors with required or trailing Optional parameters in derived classes. Put `Call Parent.New(...)` first in a derived Sub New when passing base arguments; otherwise the base must have an accessible parameterless constructor. Forwarding arguments can use constructor parameters/module routines, not Me, fields or body locals. Late/nested/duplicate Parent.New and root-class use are invalid; private base constructors remain inaccessible. Base bodies run before derived bodies, once, and bodies support own private fields and inherited public helpers/fields. A failure cleans typed image fields at all completed levels without user Delete and preserves an existing target. Ordinary derived methods/accessors, XPImage/array constructor signatures remain deferred. See `class-constructor-derived.xps`, its normal/AST parity fixture and `image-constructor-derived.xps`.

Experimental AST supports root-class `Sub New(ByVal value As Long)` (or parameterless New), with required or trailing Optional scalar ByRef/ByVal parameters. Write `Dim item As New Record(4)` or `Set item = New Record(4)` to construct it; plain `Dim item As Record` starts as Nothing. Unmodified required constructor parameters are ByRef; these references can precede trailing Optional ByVal parameters. Me, private helper calls, Exit Sub and On Error work in the body. On construction failure typed image fields are cleaned without calling user Delete, and a previous assignment target survives. Successful objects still need explicit Delete/field cleanup. XPImage/array parameters remain XPS3001. See the root-constructor contract in `docs/ast-csharp-emitter.md` and `class-constructor.xps`/`image-constructor.xps`.

Experimental AST supports one parameterless `Sub Delete()` per class, including private declarations and derived classes. Use `Delete item` explicitly: the most-derived body runs first, its typed image fields are cleared, then the base chain runs automatically, even after Exit Sub or an error. Do not add Parent.Delete forwarding. Each class's body runs at most once per instance, including re-entry and surviving aliases. Derived bodies can use their own private fields and inherited public fields/properties/helpers, including Me/With and scalar ByRef; base-private members remain inaccessible. Ordinary methods/accessors declared in derived classes are still deferred. Caller storage is cleared and images are recycled even on error. Standard finally precedence applies: a later base error replaces an earlier derived error. Other AST aliases still reference the object; do not assume LSRef alias invalidation or reuse deleted objects. GC/scope-exit cleanup remains pending. See `class-destructor-derived.xps`, `class-destructor-derived-parity.xps`, `image-destructor-derived.xps` and `docs/ast-csharp-emitter.md`.

AST indexed Get/Let also accepts matching typed array/List elements and List ForAll aliases as ByRef indexes, for example `counter.Item(values(0))`. Passing the same element twice preserves one shared location; mutations remain visible when the accessor raises an error. Each receiver/index/tag is evaluated once in order. Declared fixed/dynamic/multidimensional arrays and non-zero lower bounds are supported. A missing List tag or out-of-range array subscript fails before later arguments execute. If an entry is erased and recreated after its reference is captured, the old reference remains detached; it does not recreate the deleted entry by copy-back. This is indexed-accessor support, not a promise of shared aliases in all ordinary procedure call wrappers.

Typed AST module storage currently supports Boolean, Byte, String, Integer/Long and Single/Double/Currency. Date and other module types are not yet supported as indexed ByRef targets.

For AST indexed properties, an explicit `ByRef index As Long` can alias a matching scalar local, parameter, user-class field or scalar module variable declared with Private/Public without an initializer. Module scalars start at zero/False/empty String. Qualified fields such as `holder.Index` and implicit With field access are supported. Getter and setter declarations must use the same index mode. Changes are visible in the caller, including when the accessor raises an error; passing the same field or variable to two indexes preserves one shared location. Pass writable storage, not a literal or accessor-backed property. Initialized/reference-type module targets remain unsupported. ByVal and unmodified indexes retain snapshot behavior.

Experimental AST supports scalar indexed Get/Let: `store.Item(index) = value` and `Print store.Item(index)`, including multiple typed indexes, internal access and With. Trailing Optional indexes can be omitted, including `store.Item()` when all are Optional. The setter's final incoming-value parameter remains required. Paired accessors must agree on index types, modes and Optional positions. Defaults run once, left to right at accessor entry, after supplied arguments and setter RHS; they may read instance members and earlier indexes. Omitted ByRef defaults get fresh storage; supplied ByRef locations remain aliases. Missing scalar defaults use zero/False/empty String. Use parentheses for an all-Optional indexed call. Explicit empty argument slots, accessor-backed ByRef locations, other native-family accessors and derived accessor bodies remain deferred. Private/read-only/write-only restrictions are enforced. See `indexed-optional-runtime.xps` and `docs/ast-csharp-emitter.md`.

Experimental AST executes scalar Property Get/Let and Object/user-class Property Get/Set, including indexed reference values. Get returns the stored property-name result after the entire body runs; an unassigned reference result is Nothing. Parameterless `Property Let Name As String` exposes the incoming value as `Name`; parameterless `Property Set Name As Box` exposes the incoming reference the same way. Use `Set store.Item(index) = value` for Set accessors and ordinary assignment for Let; wrong modes and scalar/incompatible class values are diagnosed. Getter types are preserved, allowing `store.Item(index).Value`. Supplied ByRef scalar indexes remain aliases; the assigned value is a snapshot reference, so replacing an incoming ByRef value parameter does not replace the source variable. Object mutation remains shared. Read-only/write-only and private accessor permissions are enforced. Typed JSON/XML/CSV/spreadsheet/HTTP reference accessors and snapshot-owned XPImage accessors are supported; other native families, derived accessor bodies and complete lifecycle dispatch remain deferred. See `indexed-object-runtime.xps` and `docs/ast-csharp-emitter.md`.

Experimental AST now executes scalar typed instance Sub/Function bodies, private sibling calls, Me, recursion and scalar ByRef, preserving per-instance fields and parameter shadowing. Initialize class objects with parameterless New as supported; String fields start empty. Names are case-insensitive in AST (mixed-case class calls still fail in normal compilation). Private fields and methods are not externally accessible. Object/array/List method signatures, Optional/Static method-local storage and method bodies authored in derived classes remain explicitly deferred; accessor, advanced constructor and destructor migration is not complete. See docs/ast-csharp-emitter.md and class-methods-native.xps fixtures.

`Dim value As Object` inside a procedure starts as Nothing on every call. `Static value As Object` starts as Nothing once and keeps its reference between calls. Neither declaration creates an object; use `Set value = New Object()` explicitly.

Experimental AST also supports `Dim value As Object = New Object()` and `Static value As Object = New Object()`. Dim executes the initializer each time the declaration is reached; Static executes it once at the first reached declaration, using that call's parameters and earlier locals. Clearing a Static reference does not rerun its initializer. To recreate an object after clearing it, use `Static value As Object` without an initializer and `If value Is Nothing Then` with an explicit Set. Recursive initialization and simultaneous first calls are not covered by the current contract.

All XPscript classes are instance-based. Create class objects with `New`.

```xpscript
p = New Path("src/test/data.json")
```

Do not model classes as global singletons or static namespaces when writing XPscript. This is important for APIs such as XPDB and XPAI because multiple independent instances must be able to exist at the same time.

Example pattern:

```xpscript
Dim first As Variant
Dim second As Variant

first = New SomeClass(...)
second = New SomeClass(...)
```

Each instance must keep its own state.

For class inheritance, use `Class Child Extend ParentType`. When a child constructor must initialize a base class that requires constructor arguments, put `Call Parent.New(...)` first in the child `Sub New`. Do not invent C#-style `: base(...)` syntax in XPscript.

Experimental AST supports implicit user-class reference upcasts through direct/transitive ancestry in supported value assignments, initializers, properties and ByVal calls. A property declared As Root can store a Leaf instance when Leaf Extends Root; the reference still shares identity and mutations. This does not make ByRef parameters covariant: pass an exact declared type to ByRef parameters, and do not assume array/List covariance. Implicit downcasts and sibling conversions remain invalid. Unknown bases and inheritance cycles are diagnosed early. Ordinary method/accessor bodies authored in derived classes, advanced constructor signatures and complete lifecycle dispatch remain deferred in AST; do not infer full inheritance migration from reference conversion support. See `inherited-reference-runtime.xps` and `docs/ast-csharp-emitter.md`.


When using XPDB or XPAI, follow the exact constructor/member names shown by the current database/AI docs and samples. Never infer those signatures from older examples or another language.

For XPAi structured output, prefer defining the desired result shape as an XPscript class and passing an instance to `SetResultClass` when the contract maps naturally to a class. Public fields and public readable properties form the generated schema; private backing state is excluded. Use `SystemPrompt` and `UserPrompt` for the common two-part prompt shape. Use `SetJsonSchema` for an explicit schema; it accepts `XPJsonSchema` directly as well as `XPJsonObject` or `XPJsonDocument` with an object root. Prefer `XPJsonSchema` over hand-building raw JSON when XPscript's schema API can express the required contract. See `docs/xpai-structured-output.md` and `samples/xpai-structured-output.xps`.

## Native JSON in experimental AST

AST properties may use XPJsonDocument, XPJsonObject, XPJsonArray, XPJsonElement and XPXml reference types as Get results and Set incoming values, including indexed accessors with scalar Optional/ByRef indexes. Use `Set store.Current = value`; getter member chains keep their native type. References and mutations are shared, while replacing the setter's incoming parameter does not replace the caller's variable. XML element-to-node conversion is allowed for values, not ordinary ByRef storage. Do not generalize shared-reference Get/Set to XPImage: image Get factories transfer ownership, while image Set uses snapshot-owned field storage. See `native-reference-properties.xps`, `native-reference-signatures.xps` and `docs/ast-csharp-emitter.md`.

Use `New XPJsonObject` or `New XPJsonArray`, and `XPJsonDocument.Parse(text)` or `JsonParse`/`JsonDecode` to read JSON. Obtain an object or array from a document using `document.Root.AsObject()` or `document.Root.AsArray()`. Use `Get`, `Set`, `Add`, `Remove`/`RemoveAt`, `Contains` and `Count` as documented in `docs/api-reference.md`. JSON arrays support `ForAll`. `JsonEncode` and `JsonStringify` use the shared native serializer; invalid JSON and array indexes raise XPscript errors. JSON references can be typed parameters and function results, including ByRef replacement. `Dim value As New XPJsonObject` constructs the object; `Dim value As XPJsonObject` starts as Nothing.

## Native XML

The XML API also compiles directly through AST using the existing native runtime, with no XML NuGet addon. XML code is emitted only on actual use; `XmlEscape` alone does not include the DOM/DTD engine. Typed XML parameters/results, Static references and ByRef replacement are supported. ByVal shares the object; `LoadXml` mutates that document and remains visible through existing aliases. `XPXmlElement` can flow into a ByVal `XPXmlNode` parameter. XML nodes containing elements support runtime-checked element operations; other node kinds raise error 13. Use documented native methods, not arbitrary dynamic members. Both `Call .Method(...)` and `.Method(...)` work inside With.

Use the native XML classes documented in `docs/native-xml.md`; do not infer MSXML, browser DOM, or .NET `XPXmlDocument` members that XPscript does not expose.

Create documents with `New XPXmlDocument`, parse with `XPXmlDocument.Parse`, `XmlParse`, or a constructor string, and replace an existing document with `LoadXml`:

```xpscript
Dim doc As New XPXmlDocument
Dim root As XPXmlElement

Set root = doc.AddRoot("people")
Call root.SetAttribute("version", 1)
Call root.AddElement("person", "Alice")
```

Use `XPXmlNode` navigation for `Parent`, `OwnerDocument`, `FirstChild`, `LastChild`, `PreviousSibling`, `NextSibling`, `ChildNodes`, `Clone`, `InsertBefore`, `InsertAfter`, `ReplaceWith`, `Remove` and `Delete`.

Use `XPXmlElement` for element-specific operations such as `AddElement`, `PrependElement`, `AppendChild`, `PrependChild`, `GetElement`, `GetElements`, `GetDescendants`, `RemoveElement`, `RemoveElements`, `RemoveChildren`, `RemoveAll`, `SetAttribute`, `GetAttributeNode`, `RemoveAttribute` and `RemoveAllAttributes`.

Attributes are first-class objects:

```xpscript
Dim attr As XPXmlAttribute
Set attr = root.GetAttributeNode("version")
attr.Value = "2"
Call attr.Delete()
```

`XPXmlNodeCollection`, `XPXmlAttributeCollection` and `XPXmlValidationErrorCollection` support `ForAll`. XML collection indexes are zero-based.

Normal XML parsing prohibits DTD processing and external entity resolution. Do not weaken that boundary. Internal DTD validation is available through `ValidateDTD`/`IsValidDTD`, but external `SYSTEM` and `PUBLIC` identifiers remain unsupported.

## Native Notes/Domino

For native Notes/Domino code, use `docs/notes-c-api.md` as the primary object-model reference and `docs/notes-mime-entity.md` for MIME. Do not infer undocumented LotusScript compatibility merely because a method has the same name.

Only `NotesSession` is constructed with `New`. Obtain databases, views, documents, items, names, date/time values, MIME entities, collections and agent results from their owning Notes objects.

Use `New NotesSession()` for platform autodetection. On Windows it resolves the HCL Notes/Domino installer registry entries for the program directory, then the shared runtime resolves `notes.ini` and its `Directory=` data path. Pass an explicit program directory only when the documented installation is intentionally outside that configuration.

`NotesSession.EffectiveUserName` returns the active native session user name, and `NotesSession.UserNameObject` creates a `NotesName` wrapper for that value. Recycle the returned `NotesName` when it is no longer needed.

Run agents through `db.GetAgent(name)`, then `agent.Run()` or `agent.RunWithDocumentContext(document)`. Read `agent.ReturnMessage` for captured output and recycle the agent afterwards. `NotesDatabase.RunAgent` and `NotesAgentResult` were removed; do not generate those obsolete forms.

Use `item.Values = Array("one", "two")` for a Notes text list; homogeneous numeric and NotesDateTime arrays are also supported through AST and legacy. Mixed-type arrays raise error 13. Empty arrays write empty text. See `samples/notes-item-array-runtime-test.xps`.

`NotesDocument.Save()` is a void native save that raises failures. Boolean overloads are also available:

```xpscript
If Not doc.Save(True, False, True) Then
    Error 5, "Document was not saved"
End If
```

For these overloads, `force` applies native force-update behavior. `markRead=True` marks the successfully saved document read for the current Notes user. `createResponse` is currently accepted for compatibility but does not create a conflict response document; do not write code that depends on that side effect.

View key lookup supports scalar keys and one-dimensional arrays. An array key targets successive sorted columns. Arrays containing numeric values or `NotesDateTime` use native `NIFFindByKey`; text-only arrays use the managed comparison path. Use the documented `exactMatch` overload rather than inventing a different search mode.

`NotesName` exposes canonical/abbreviated forms, hierarchical components, RFC821/RFC822 address fields and comments. `NotesDateTime` supports wildcard date/time values, `SetNow`, adjustments, `TimeDifference`/`TimeDifferenceDouble`, and `ConvertToZone`. Do not call `TimeDifference` with wildcard date/time values.

## Notes MIME

For DXL, obtain the importer with `session.CreateDXLImporter()` and call `importer.Import(path, database)` with an open NotesDatabase (a Variant-held reference is accepted). Read `importer.Log` after import or a handled error; repeated reads leave the importer alive. Recycle the importer explicitly when finished. See `samples/notes-dxl-log-runtime-test.xps`.

Use `NotesMIMEEntity` only with the currently documented native MIME subset in `docs/notes-mime-entity.md`.

Set `NotesSession.ConvertMIME = False` before opening documents whose MIME `Body` must remain a native MIME item. The current document-level native MIME implementation supports the `Body` item only.

For root content mutation, create or open the root entity, write content to a `NotesStream`, rewind the stream, and call `SetContentFromText` or `SetContentFromBytes` with an explicit content type and charset when text encoding matters:

```xpscript
Dim stream As NotesStream
Dim mime As NotesMIMEEntity

Set mime = doc.CreateMIMEEntity("Body")
Set stream = session.CreateStream()
stream.Charset = "UTF-8"
Call stream.WriteText("Hello")
stream.Position = 0
Call mime.SetContentFromText(stream, "text/plain; charset=UTF-8", 1725)
```

Root content readback supports `ContentAsText`, `GetContentAsText`, `GetContentAsBytes`, and `GetEntityAsText`. Use `ContentAsText` for decoded root text. Use the stream methods when callers need `NotesStream` output. Use `GetEntityAsText` only when the complete root RFC822 entity including headers is needed.

For a normal `multipart/mixed` message, call `CreateChildEntity()` on the root. The first call promotes a non-multipart root to `multipart/mixed`. Multipart child entities can create nested children as well. Root, direct-child and nested entities support the documented content read/write, navigation, header and parameter surfaces. `CreateChildEntity(nextSibling)` requires `nextSibling` to share the same parent.

Use encoding `1727` for a base64 attachment and set `Content-Disposition` explicitly:

```xpscript
Dim attachment As NotesMIMEEntity
Dim disposition As NotesMIMEHeader

Set attachment = mime.CreateChildEntity()
stream.Position = 0
Call attachment.SetContentFromBytes(stream, "application/octet-stream; name=""file.pdf""", 1727)
Set disposition = attachment.CreateHeader("Content-Disposition")
Call disposition.SetHeaderVal("attachment; filename=""file.pdf""")
```

For real binary attachments, load the binary bytes into the `NotesStream`, rewind it, then use `SetContentFromBytes`. Do not pre-base64 the stream when using encoding `1727`; XPscript performs the MIME base64 transfer encoding.

Root, direct-child and nested `GetNthHeader(name, occurrence)` support standard content headers and arbitrary header names; occurrence is one-based. `NotesMIMEHeader.GetParamVal` and `SetParamVal` support MIME parameters such as `name`, `charset`, and `filename`. Values resolve through native MIME-directory state and serialized entity headers, so callers can read and update transfer encodings, dispositions and nested custom headers after mutation and directory reopen. `Headers`, `HeaderObjects`, `GetSomeHeaders`, `GetHeaders` and `RemoveHeaders` are part of the documented header surface. `InputStream`, `GetInputStream`, `Reader`, `Preamble`, `EncodeContent` and `DecodeContent` are also available as documented. `CloseMIMEEntities`, save, and document recycle invalidate MIME directory-backed entity handles. The wrapper performing a successful supported MIME mutation is rebound to the corresponding native entity so metadata can be read immediately.

## HTTP client security

AST user-class properties support Get/Set with XPHttpClient, XPHttpRequest, XPHttpResponse, XPHttpPart, XPHttpFile, XPHttpPartCollection and XPHttpFileCollection. Use Set for reference assignment. Typed getter chains call the real native API; scalar Optional/ByRef indexes keep their usual aliases, and replacing the incoming setter parameter does not replace the caller's reference. Reference results default to Nothing. Clients are shared, not cloned or automatically disposed by accessors: Dispose explicitly after all users are finished, and never use any alias afterward. Security/read-only guards remain active. GetFile returns XPHttpPart; the distinct static XPHttpFile name needs an existing Variant runtime cast when bridging that value, not a new implicit Part-to-File conversion. Do not use HTTP references as indexes or apply HTTP shared-reference ownership to XPImage. See `http-reference-properties.xps`, `http-property-signature-only.xps` and `docs/http-client.md`.

Native HTTP client/request/response APIs compile directly through AST and use the same secure transport/TLS runtime as normal compilation. `CertificateValidation` defaults to `Strict`; `AllowSelfSigned` is only for deliberately trusted self-signed endpoints and still validates hostname and expiry. Keep `Insecure` out of production code. Fractional `Timeout` values are supported. ByVal shares HTTP references; ByRef can replace them. Call `Dispose()` when a client is no longer needed. HTTP code is included only on actual use without external NuGet addons. Async callbacks and HTTP/UIForm helpers retain whole-file legacy compilation until migrated. See `docs/http-client.md`.

`XPHttpClient` blocks private and local network destinations by default. Leave this protection enabled when a URL may contain request data or other untrusted input.

For a trusted application-controlled intranet or local endpoint, opt in on that client instance:

```xpscript
Dim http As New XPHttpClient
http.AllowPrivateNetwork = True
Set response = http.Get("http://127.0.0.1:8080/health")
```

Do not enable `AllowPrivateNetwork` merely to make a user-supplied URL work. Request framing and transport headers such as `Host`, `Content-Length`, and `Transfer-Encoding` are runtime-managed and must not be set with `SetHeader`.

## Arrays, Lists, and ForAll

XPscript arrays use XPscript array semantics and support helpers such as `Array`, `ReDim`, `LBound`, `UBound`, `Join`, `Explode`, and array helper functions documented in the language reference.

Experimental AST recognizes ranked array parameter syntax (`values()` / `values(,)`) but still rejects array constructor/procedure/method signatures with XPS3001. Parser recognition is not executable support; do not generate such signatures for AST programs yet. Passing individual typed array elements to supported scalar/reference ByRef parameters remains supported.

AST fixed bounds accept signed constants (`Dim values(-3 To -1) As Long`). Variant sharing and matching typed-array assignments preserve physical bounds and shared element mutations; ask LBound/UBound after replacement rather than assuming the original declaration's bounds. Typed Variant array conversion requires exact element type/rank and leaves an old target unchanged on error. String arrays and Erase retain empty-string defaults, including nonzero/multidimensional bounds. These storage rules do not enable whole-array constructor parameters.

`ForAll` is the single first-class iteration model for supported iterable values. Use the same syntax for one-dimensional arrays, Lists, JSON arrays/enumerables, XML collections, database row/result collections, filesystem arrays, other runtime enumerable values, and XPscript classes that expose an iterator.

```xpscript
files = Files("src", "*.xps", True)

ForAll file In files
    Print file
End ForAll
```

Only one-dimensional arrays can be used with `ForAll`. Multidimensional arrays are not flattened and raise runtime error 13; use explicit nested `For` loops for their dimensions.

Lists retain their tag/value alias semantics under `ForAll`. Do not treat an XPscript List as an ordinary array when code relies on `ListTag`, `IsElement`, or keyed list access.

A user class can participate in `ForAll` by exposing a public parameterless `Iterator()` function that returns another supported iterable value:

```xpscript
Class Words
    Public Function Iterator() As Variant
        Iterator = Array("one", "two", "three")
    End Function
End Class

Sub Main()
    Dim words As Words
    Set words = New Words()

    ForAll word In words
        Print word
    End ForAll
End Sub
```

`Iterator()` must not return the object itself. Do not invent collection-specific loop constructs when the runtime value can integrate with `ForAll`.

Do not redeclare the `ForAll` iteration alias immediately before the loop unless a current sample specifically requires it.

## Filesystem APIs

Prefer modern convenience functions for new code unless compatibility with legacy `Dir()` behavior is specifically required.

### Existence and type

```xpscript
If IsFile("config.json") Then
    Print "file"
End If

If IsDir("data") Then
    Print "directory"
End If
```

Existing compatibility functions may include `FileExists` and `DirExists`; prefer `IsFile`/`IsDir` when the distinction between file and directory matters.

### Dir

```xpscript
name = Dir("data/*")
name = Dir("data/*", 1)      ' files only
name = Dir("data/*", 2)      ' directories only
name = Dir("data/*", 3)      ' recursive files, default maxDepth 3
name = Dir("data/*", 3, 1)   ' recursive files, max one level down
```

`Dir()` with no arguments continues the current enumeration. `.` and `..` are always excluded. Recursive mode is bounded: default depth is `3`, valid depth is `0..32`, and link/reparse directories are not traversed.

Prefer `Files()` or `Directories()` when stateful `Dir()` continuation is unnecessary:

```xpscript
files = Files("src", "*.xps", True)
dirs = Directories("src")
```

These return XPscript arrays suitable for `ForAll`.

### FileInfo

```xpscript
info = FileInfo("archive.zip")

Print info.Name
Print info.FullPath
Print info.Extension
Print info.Length
Print info.Created
Print info.Modified
Print info.Accessed
Print info.IsFile
Print info.IsDirectory
Print info.IsLink
Print info.Attributes
```

### File hashes and equality

```xpscript
hash = FileHash("archive.zip")
hash = FileHash("archive.zip", "SHA256")
hash = FileHash("archive.zip", "SHA384")
hash = FileHash("archive.zip", "SHA512")
```

SHA256 is the default. SHA1 and MD5 may exist for legacy compatibility; do not recommend them for security-sensitive integrity checks.

```xpscript
If FileEquals("a.bin", "b.bin") Then
    Print "same content"
End If
```

### CopyFile and MoveFile

Both return Boolean. The optional action defaults to `1`.

```xpscript
ok = CopyFile("a.bin", "b.bin")
ok = CopyFile("a.bin", "b.bin", 2)
ok = MoveFile("b.bin", "archive/b.bin", 3)
```

Actions:

- `1` = fail if the target exists.
- `2` = overwrite the target.
- `3` = skip if the target exists.

A successful transfer returns `True`; a failed/skipped transfer returns `False` according to the documented policy. Legacy `FileCopy` remains available for compatibility.

### Whole-file text I/O

```xpscript
content = ReadFile("config.json")
content = ReadFile("config.json", "utf-8")

WriteFile "config.json", content
WriteFile "config.json", content, "utf-8"
AppendFile "app.log", "done"
```

Reads and file metadata/hash/equality operations raise a runtime error when their required file is missing. Writes, appends, line/byte output, and copy/move destinations raise a runtime error when the destination directory is missing; create the directory explicitly before writing.

Supported charset names include `utf-8`, UTF-16 variants, ISO encodings, and code-page names supported by the runtime encoding provider. UTF-8 is the normal default.

### Lines

```xpscript
lines = ReadLines("users.txt")
WriteLines "copy.txt", lines

ForAll line In lines
    Print line
End ForAll
```

### Bytes

```xpscript
data = ReadBytes("image.png")
WriteBytes "copy.png", data
```

Use these instead of handle-based binary I/O when the whole file comfortably fits in memory. Use `Open`/`Get`/`Put` for streaming, random access, locking, or very large files.

## Path class

`Path` is an instance class. The stored path is supplied once in the constructor.

```xpscript
p = New Path("src/test/data.json")

Print p.FileName()
Print p.FileNameWithoutExtension()
Print p.Extension()
Print p.Directory()
Print p.Root()
Print p.Normalize()
Print p.Absolute()
Print p.Relative("src/test/archive.json")
Print p.ChangeExtension(".xml")
Print p.IsAbsolute()
Print p.Exists()
Print p.Combine("child.txt")
```

Do not write static forms such as `Path.Absolute(path)` unless the language documentation explicitly adds such an API in the future.

`ChangeExtension()` returns the changed path string; it does not rename a file.

## GetFileAttr

`GetFileAttr(path)` returns a bit mask. Test attributes with bitwise `And`.

```xpscript
If (GetFileAttr(fullPath) And 16) <> 0 Then
    Print "directory"
End If
```

Common bits include `ReadOnly=1`, `Hidden=2`, `System=4`, `Directory=16`, `Archive=32`, `Normal=128`, `Temporary=256`, `SparseFile=512`, `ReparsePoint=1024`, `Compressed=2048`, `Offline=4096`, `NotContentIndexed=8192`, and `Encrypted=16384`.

Windows exposes native Windows attributes. On macOS/Linux XPscript synthesizes `Hidden=2` for names beginning with `.`; other bits depend on what the runtime/filesystem exposes. Do not assume Windows-only metadata exists on Unix-like systems.

## Process execution

Use `ShellExecute` when XPscript code needs to wait for a child process, inspect its exit code, or capture stdout/stderr. Pass arguments as an XPscript array/list rather than concatenating a command string.

```xpscript
Dim args(1) As String
Dim result As Variant

args(0) = "--version"
args(1) = "--verbose"
Set result = ShellExecute("tool", args)

If result.ExitCode = 0 Then
    Print result.Output
Else
    Print result.Error
End If
```

`ShellExecute(executable, arguments [, timeoutMilliseconds])` returns an `XPShellResult` with `ExitCode`, `Output`, `Error`, and `TimedOut`. Timeout `0` waits indefinitely. A timed-out process tree is terminated and returns `TimedOut=True` and `ExitCode=-1`. Non-zero child exit codes are normal results. Missing/unstartable executables raise an XPscript runtime error. Prefer this structured API over command-string concatenation whenever values can vary.

## Error handling

Use XPscript error handling, not C#/Java exception syntax:

```xpscript
On Error GoTo Handler

content = ReadFile("config.json")
Exit Sub

Handler:
Print "Error " & CStr(Err) & ": " & Error$
```

Use `On Error Resume Next` only when the program intentionally handles an expected failure immediately afterward.

Experimental `ast-compile` supports `On Error Resume Next`, `On Error GoTo label`, `On Error GoTo 0`, `Resume`, `Resume Next` and `Resume label`. Handling applies after activation in the current procedure. `Resume` retries the failing statement; repair its cause first. `Resume Next` continues after that statement, including a failing procedure call. `Resume label` continues at a label in the same procedure. A failure in an active handler propagates to the caller; `Resume` without a handled error fails at runtime. CLI and MCP share this compilation path.

## Cross-platform rules

Experimental AST compilation uses the same scalar runtime for `CStr`, `CInt`, `CLng`, `CDbl`, `CBool`, byte/single/currency/date conversions, printing, Null/Empty, string helpers and `Environ` as normal compilation. `CInt` checks the Integer range; overflow can be handled with ordinary XPscript `On Error` handling. `XPScriptRuntime` is internal generated support, not a user-facing XPscript object.

XPscript targets Windows, Linux, and macOS. When writing portable programs:

- Prefer `Path` operations over manual separator manipulation when paths are non-trivial.
- Do not assume drive letters outside Windows.
- Do not assume filesystem case sensitivity is the same on every OS.
- Treat symlinks/reparse points carefully in recursive traversal.
- Use documented charset names instead of platform-default text encodings.
- Avoid hard-coded Windows paths in examples intended to be portable.

## LLM coding workflow

When asked to write an XPscript program:

1. Identify whether the request needs plain language syntax, a runtime class, filesystem I/O, database access, AI, UI, web APIs, XML, or native interop.
2. Read the relevant current reference/sample if any API name or signature is uncertain.
3. Prefer `Option Declare` and explicit types where practical.
4. Instantiate every class with `New`; preserve independent per-instance state.
5. Prefer modern convenience APIs (`Files`, `ReadFile`, `FileInfo`, `Path`, etc.) over legacy/stateful APIs unless compatibility is requested.
6. Use XPscript arrays/Lists rather than CLR/.NET collection assumptions.
7. Use `ForAll` as the common loop for supported iterable values; do not create type-specific loop models.
8. Keep code cross-platform unless the user explicitly requests one OS.
9. Do not silently invent unsupported overloads, properties, or optional arguments.
10. If modifying the language or runtime, add/update an executable `.xps` regression sample and documentation in the same change.
11. If the change affects how an LLM should write XPscript, update this skill in the same PR.

## Verification checklist

When consuming generated OpenAPI models, copy type names from the generated
declarations. Do not Pascalize schema keys yourself. Server generation preserves
valid keys such as `clear_actor_limits` and maps converted names consistently
across `$ref` uses. See `docs/rest-api.md` for generation and initial import.
Generated model array fields use `XPJsonArray`; do not replace them with
`Public items() As Model`, which the CLI class parser does not support.
Copy enum member names from the generated declaration too; collisions can add
an enum-name prefix or numeric suffix.

Before presenting code as valid XPscript, check:

- Every variable required by `Option Declare` is declared.
- Every class construction uses `New`.
- Optional arguments match documented signatures.
- `ForAll` is used only with supported iterables and never with multidimensional arrays.
- Iterable user classes expose public parameterless `Iterator()` returning another iterable.
- Function return values use XPscript semantics.
- File/directory recursion is bounded where appropriate.
- Path behavior is portable where the program claims to be portable.
- Charset names are explicit when encoding matters.
- XML code uses the documented native DOM API rather than assumed MSXML/browser/.NET members.
- XML parsing keeps DTD/external-entity resolution disabled outside the bounded internal-DTD validation API.
- The code does not confuse legacy `FileCopy` with Boolean `CopyFile`.
- The code does not use static `Path.*` calls for the instance-based `Path` class.

## Maintenance rule

This file is a living LLM contract for XPscript. Any PR that changes user-visible language syntax, built-ins, runtime classes, constructors, object lifecycle, function signatures, filesystem behavior, database/AI/XML APIs, iteration behavior, or recommended idioms should review and update this file in the same PR when the change affects code generation guidance.

Keep this skill concise and operational. Link to authoritative docs for exhaustive reference instead of duplicating the entire language manual.


## Structured web logging

Application has native AST support and shares normal compilation's runtime. It is predefined: do not create it with New. Use Application.ArgCount and Application.Args(index) for arguments; bare Application.Args returns an independent one-dimensional snapshot. The property is read-only; indexes outside the process arguments raise error 9. Use Application.NewLine for platform line endings. Application.State and Process/Session/Request.State preserve their separate scopes. With Application and With Application.State are supported. Debug is activated by --appdebug/--debug; Log/Audit require the web host and Secrets requires Application.Id plus the OS credential store. Linux Registry.User respects an absolute XDG_CONFIG_HOME, otherwise ~/.config. Literal Application.Executable metadata goes through the existing build/packaging backend. See docs/application-reference.md.

Web hosts always emit protected JSONL access, security and error logs. Use `Application.Log.Trace/Debug/Info/Warning/Error/Critical(eventName, message [, attributes])` for application events and `Application.Audit.Write(eventName, message [, attributes])` for security/audit events. The optional attributes value must be an `XPJsonObject`. Never place credentials, tokens, cookies, session identifiers, personal data or request bodies in log messages or attributes. The runtime rejects sensitive attribute names and reserves protocol fields. Log directories must remain outside the web root. See `docs/web-logging.md` and `samples/application-web-logging.xps`.

For targeted web troubleshooting, set `Application.Log.CaptureExchange = True` only in the affected request. It writes a redacted, 256 KiB-per-body exchange record. Use the automatic hashed `session.id` to group requests from one browser or client and `request.id` for one request. Never log the raw `XPSLOGID` cookie.


## Native CSV in AST

AST user properties can declare XPCsvDocument, CSV header/row/column collection/item types, XPSpreadsheet, XPWorksheet, XPCell and XPRange as Get results and Set values. Use `Set store.Csv = csv` or `Set store.Book(index) = book`; native member chains stay typed and share mutations. Scalar Optional/ByRef indexes retain aliases and incoming setter references are snapshots, even on error. Unassigned results are Nothing. FromBytes still requires a document variable, not `store.Csv.FromBytes(...)`: read the property into a local, call FromBytes, then Set the property to that replacement. Prior aliases keep the old document. XPImage uses snapshot-owned field storage instead of shared-reference storage. See `tabular-reference-properties.xps`, `tabular-reference-signatures.xps` and `docs/ast-csharp-emitter.md`.

`XPCsvDocument` and its header/row/column types use the shared native CSV engine in AST. ByVal shares the document; ByRef and `FromBytes` can replace the reference. `FromBytes` leaves existing aliases pointing to the previous document. Use `Load`, `Save` or `SaveFile` for encoded files, and `Get`/`Set` or row indexes for cells. CSV implementation code is selected only on use; `CsvEscape` alone does not include document/file runtime. CSV-to-spreadsheet interop compiles directly through AST using the shared helpers. See `docs/native-csv.md` and `tests/ast-compile-probe/csv-*.xps`.

`XPSpreadsheet`, `XPWorksheet`, `XPCell` and `XPRange` compile directly through AST without external packages. Construct the workbook only; obtain worksheets/cells/ranges from its methods. ByVal shares references and ByRef can replace them; worksheet Name/Index are read-only (use RenameWorksheet). XLSX relative paths are script-relative. External/unmarked workbooks cannot be overwritten; SaveAsSimple explicitly converts them. Formulas are stored, not evaluated. Range.Values currently returns a rank-one LSArray; use LBound/UBound and ForAll. Use csv.ToSpreadsheet, sheet.ToCsv or XPCsvDocument.FromWorksheet for native conversion. Only actual use includes the relevant engines. See `docs/xpspreadsheet.md`.

## XPImage

In experimental AST, `Delete owner` releases typed XPImage fields, including private and inherited fields, through the existing Delete chain. It clears the named caller variable; other aliases still see the object but its image fields are Nothing. Source images copied into fields and borrowed Variant fields remain untouched. Repeated Delete is safe. Use `Set backing = Nothing` for early field release or Delete the owner when finished. Merely leaving scope or assigning owner Nothing does not invoke cleanup; GC finalizers are not enabled. See `image-field-delete.xps`.

AST Property Set supports XPImage with snapshot-owned typed fields. A setter can use `Set backing = incoming` where backing is declared As XPImage: storage receives an independent clone. ByVal incoming values clone at entry; ByRef incoming values borrow a reference snapshot. Replacing or clearing the ByRef parameter cannot replace/recycle the caller's variable, but explicit mutation or Recycle still affects the shared source. Replacing a field recycles the old field image; Nothing clears it and failed cloning preserves its old value. Clear fields with `Set backing = Nothing` for early release, or Delete their owner when finished; GC/scope-exit cleanup is not implemented. Variant fields are not owned image storage. Getters exposing retained backing images must return `backing.Clone()` (guard Nothing); capture returned getters in typed locals. See `image-property-set.xps` and the XPImage documentation. Do not apply the ordinary shared-reference native property contract to images.

AST supports XPImage Property Get factories with scalar Optional/ByRef indexes. Capture the owning result in a typed local, for example `Dim picture As XPImage = factory.Fresh`; do not discard it in a member chain. Returned images survive accessor exit; unused locals, replaced results and failed results are recycled by the existing image scope. An unassigned getter returns Nothing. There is no automatic getter clone: Clone explicitly before returning a borrowed field/static image when the original owner must keep it. XPImage Property Set uses snapshot-owned typed fields; GC/scope-exit cleanup is not added; do not treat image getters as shared-reference Get/Set. See `image-property-get.xps`, `image-property-signature-only.xps` and `docs/xpimage/index.md`.

XPImage also compiles directly through AST, without legacy fallback. `Dim image As New XPImage(...)` retains its constructor arguments; member names are case-insensitive and information properties are read-only. ByVal parameters clone the image; ByRef can replace it. AST recycles local images on normal, early and exceptional exits, while preserving returned images, including Variant returns. Static images survive across calls. Use `Recycle()` for explicit disposal; do not keep a borrowed alias to a procedure-local image after that procedure exits. Image dependencies/native assets and notices are included only when image support is used.

Use `XPImage` for cross-platform raster creation and processing. Construct images with `New XPImage(width, height [, background])`, or use the documented file/byte/base64 loaders. Editing methods mutate the current image unless documented otherwise; `Clone()` creates independent mutable state and `Difference(other)` returns a new image. Use `ChangeGamma(value)` to modify gamma and the read-only `Gamma` property to inspect it. `Compare(other)` returns normalized RMS difference from 0 through 100 and is not a literal percentage of changed pixels. `Statistics()` and `Histogram([maxColors])` return XPJson values and describe pixel/channel data, not semantic objects. Keep Magick.NET types out of XPscript code. See `docs/xpimage/index.md` and `docs/api-reference.md`; the executable regression is `samples/xpimage-runtime.xps`.

Initialized `XPImage` and native JSON module variables are typed ByRef storage in the experimental AST compiler. Other initialized compatibility-module forms and array parameter signatures remain deferred.

Derived public methods and properties dispatch virtually through base-typed references in the experimental AST compiler, including scalar and indexed getter/setter accessors. Keep base and derived accessor signatures compatible.
