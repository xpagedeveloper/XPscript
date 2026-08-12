# Function and Sub overloading TODO

(c) xpagedeveloper.com 2026

Goal: allow a class to declare multiple `Function` or `Sub` members with the same name when their parameter signatures differ. The compiler resolves the correct overload from the arguments used at each call site.

Status:
- `[x]` implemented and regression-verified
- `[>]` implemented or defined but still needs broader compatibility coverage
- `[ ]` not implemented

## Language behavior

- [x] allow multiple class `Function` declarations with the same name when their parameter signatures are distinct
- [x] allow multiple class `Sub` declarations with the same name when their parameter signatures are distinct
- [x] parameter count, parameter types and array/scalar shape participate in overload matching; end-to-end scalar-versus-array class overload selection is regression-verified in `samples/class-method-overloads.xps`
- [x] support overloads with different numbers of parameters
- [x] support overloads with the same number of parameters but different parameter types
- [x] support overloads that combine required and `Optional` parameters; omitted optional parameters carry a specificity penalty so an exact-arity overload wins
- [x] overload selection rules are defined: exact type wins; numeric candidates are ranked by widening distance; a typed class overload is preferred over `Object`; `Variant` is a fallback; String and Date do not cross-coerce solely for overload selection
- [x] choose the most specific valid overload when more than one overload can accept the supplied arguments
- [x] reject calls where no overload matches with a clear compiler diagnostic listing the supplied signature
- [x] reject ambiguous calls where more than one overload is equally valid with a clear compiler diagnostic
- [x] reject duplicate declarations with an identical effective signature; `Variant` and `Object` are treated as the same CLR-effective object signature for duplicate detection
- [>] preserve normal return-type checking for overloaded `Function` members through generated typed CLR methods; add an explicit negative return-type regression before marking complete
- [>] `ByRef` constraints are represented by the overload validator, but scalar `ByRef` remains a broader compiler limitation and is not considered complete in this feature
- [x] support overload resolution for calls both with and without the explicit `Call` keyword
- [x] support overload resolution when invoking a member on a typed class variable
- [x] support overload resolution through `Me` inside the declaring class
- [x] inherited overload lookup is explicitly not enabled yet; resolution is limited to methods declared on the statically known class until inherited-member lookup is implemented

## Regression coverage

- [x] positive sample: same `Function` name with `Integer`, `String` and `Date` parameter variants; source: `samples/class-method-overloads.xps`
- [x] positive sample: scalar and typed-array overloads with the same member name select the correct CLR overload; source: `samples/class-method-overloads.xps`
- [x] positive sample: same `Sub` name with one-parameter and two-parameter variants
- [x] positive sample: typed object overload versus `Object` fallback
- [x] positive sample: numeric specificity selects the exact/smallest valid overload
- [x] positive sample: exact arity is preferred over an overload that consumes omitted `Optional` parameters
- [x] positive sample: typed member invocation, explicit `Call`, bare member invocation and `Me` invocation
- [x] negative sample: duplicate identical/effective signatures; source: `samples/class-method-overloads-duplicate.xps`
- [x] negative sample: no matching overload; source: `samples/class-method-overloads-no-match.xps`
- [x] negative sample: ambiguous overload; source: `samples/class-method-overloads-ambiguous.xps`
- [x] generated C# uses valid CLR method overloads while the XPScript validator supplies language-specific overload diagnostics
- [x] GitHub Actions regression gate: `Class Properties and Overloads Compatibility`

## Example syntax

```xpscript
Class Formatter
    Public Function FormatValue(value As Integer) As String
        FormatValue = "INTEGER=" & CStr(value)
    End Function

    Public Function FormatValue(value As String) As String
        FormatValue = "STRING=" & value
    End Function

    Public Sub SetValue(value As Integer)
        Print "integer"
    End Sub

    Public Sub SetValue(value As Integer, label As String)
        Print label
    End Sub
End Class
```

The arguments determine the selected implementation:

```xpscript
Dim formatter As New Formatter
Print formatter.FormatValue(42)
Print formatter.FormatValue("hello")
Call formatter.SetValue(10)
formatter.SetValue(10, "ten")
```
