# XPScript runtime reference implementation TODO

(c) xpagedeveloper.com 2026

Tracks implementation against the standalone XPScript runtime reference.

Development note: GitHub Actions verification is enabled. Features are marked `[x]` only after their applicable compiler/runtime regression gates pass.

Status:
- `[x]` implemented and verified
- `[-]` partially implemented
- `[>]` implemented/in progress, awaiting explicit verification
- `[ ]` not implemented

## 1. Core language and declarations

- [x] `Sub`, `Function`, `Call`, `Exit Sub`, `Exit Function`
- [x] scalar types: Variant, Boolean, Byte, Integer, Long, Single, Double, Currency, String, Date, Object
- [x] `Dim`, `Static`, `ByVal`, explicit `ByRef`, `Set`, `New`, `Delete`
- [x] `Optional` parameters, defaults, omitted trailing arguments and omitted slots
- [x] module-level `Public` scalar variables
- [x] module-level `Private` scalar variables
- [x] module-level fixed/dynamic arrays with `ReDim`, `ReDim Preserve`, indexed reads/writes, bounds and `Erase`; source: `samples/module-arrays.xps`
- [x] module-level custom `Type` values; source: `samples/module-type-values.xps`
- [x] module-level class/object references with `Set`, `New`, aliases, `Nothing`, identity, member access and `Delete`; source: `samples/module-object-references.xps`
- [x] `Type ... End Type`: scalar fields, auto initialization and scalar value-copy; source: `samples/type-value-copy.xps`
- [x] `Type` array fields: fixed/dynamic fields, indexing, `ReDim`, `Erase`, bounds and deep array-copy; source: `samples/type-array-members.xps`
- [x] nested `Type` deep-copy recursively clones nested values and nested array storage; source: `samples/type-nested-value-copy.xps`
- [x] cyclic nested `Type` copy graphs produce an explicit compiler diagnostic instead of unbounded clone generation; source: `samples/type-cycle-error.xps`
- [x] implicit lower bounds in `ReDim typeValue.arrayField(n)` honor active `Option Base`; source: `samples/type-array-option-base.xps`
- [x] nested `Type` copy into module-level `Type` values uses detached copy-then-commit semantics and handles self-assignment; source: `samples/module-nested-type-value-copy.xps`
- [x] `Enum ... End Enum`: explicit values, auto increment, qualified/unqualified members

## 2. Classes and properties

- [x] classes, methods, constructors, destructors, `Me`
- [x] parameterless `Property Get`
- [x] parameterless object `Property Set`
- [x] scalar `Property Let`
- [x] parameterized/indexed `Property Get`
- [x] parameterized/indexed `Property Let/Set`
- [x] indexed properties lower to typed methods so normal parameter diagnostics apply
- [x] indexed object getters/setters preserve `Set` reference semantics, including object-returning Function assignment; sources: `samples/indexed-properties.xps`, `samples/indexed-object-properties.xps`
- [x] positive scalar source: `samples/indexed-properties.xps`
- [x] negative type source: `samples/indexed-properties-error.xps`
- [x] class `Function`/`Sub` overload resolution for distinct scalar/object typed signatures, different arity, `Optional` specificity, typed object fallback, `Me`, explicit `Call` and bare member calls; source: `samples/class-method-overloads.xps`
- [x] overload diagnostics for duplicate effective signatures, no matching overload and ambiguous calls; sources: `samples/class-method-overloads-duplicate.xps`, `samples/class-method-overloads-no-match.xps`, `samples/class-method-overloads-ambiguous.xps`
- [x] class overload follow-ups for array/scalar overload end-to-end coverage and scalar `ByRef`; detailed checklist: `todo/function-sub-overloading-todo.md`

## 3. Control flow and error handling

- [x] `If`, `ElseIf`, `Else`, `Select Case`
- [ ] support all valid `If` statement layouts consistently:
  - [ ] single-line `If condition Then statement`
  - [ ] single-line branches such as `If condition Then statement Else statement` and applicable `ElseIf condition Then statement` forms
  - [ ] `If condition Then` followed by statement(s) and `End If` on a later line
  - [ ] fully multiline block form with `If`, `Then`, body and `End If` on separate lines
  - [ ] ensure Date/comparison lowering and other preprocessors preserve single-line `If ... Then ...` syntax instead of producing `Unsupported statement` diagnostics; regression discovered by `examples/date-comparisons.xps` testing
- [ ] audit all documented control-flow/declaration statement layouts for the same line-shape assumption:
  - [ ] verify `_` line continuation remains accepted for long expressions, argument lists, procedure headers and control-flow expressions
  - [ ] verify `ElseIf` / `Else` supported layouts and nested single-line/block combinations
  - [ ] verify `Select Case`, `Case`, `With`, `For/Next`, `ForAll`, `Do/Loop`, `While/Wend`, procedure/property/class headers and native declarations do not produce false `Unsupported statement` errors for documented/valid multiline layouts
  - [ ] add regression samples for every newly identified valid alternate layout; do not add arbitrary unsupported grammar forms merely because keywords can physically be split
- [x] `For/Next/Step`, `Do/Loop`, `Do While`, `Do Until`, `While/Wend`, `ForAll`
- [x] `GoTo`, `GoSub`, labels, `Return`
- [x] `On Error`, `Resume`, `Resume Next`, `Err`, `Error`, `Error$`, `Erl`
- [x] physical source-line accuracy for `Erl`; source: `samples/erl-physical-source-line.xps`
- [x] deeply nested `Resume` targets use stacked per-error-context resume frames so nested procedure calls preserve the innermost failing statement; source: `samples/nested-resume-targets.xps`

## 4. Operators

- [x] comparisons, `Like`, object identity `Is`
- [x] `And`, `Or`, `Not`, `Xor`, `Eqv`, `Imp`
- [x] arithmetic operators and `Mod`, `^`
- [x] `&` and forgiving `+`
- [x] line continuation `_`

## 5. String functions

- [x] `Asc`, `Chr`, `Instr`, `LCase`, `UCase`, `Left`, `Right`, `Mid`, `Len`, `LenB`
- [x] `LTrim`, `RTrim`, `Trim`, `Replace`, `Space`, `String`, `Str`, `StrCompare`
- [x] `InstrB`, `LeftB`, `RightB`, `MidB`
- [x] `StrConv`: upper/lower/proper case
- [x] `StrLeft`, `StrLeftBack`, `StrRight`, `StrRightBack`, `StrToken`
- [x] `LSet`, `RSet`, `UChr`, `Uni`
