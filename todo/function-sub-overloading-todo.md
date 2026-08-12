# Function and Sub overloading TODO

(c) xpagedeveloper.com 2026

Goal: allow a class to declare multiple `Function` or `Sub` members with the same name when their parameter signatures differ. The compiler must resolve the correct overload from the arguments used at each call site.

## Language behavior

- [ ] allow multiple class `Function` declarations with the same name when their parameter signatures are distinct
- [ ] allow multiple class `Sub` declarations with the same name when their parameter signatures are distinct
- [ ] treat parameter count, parameter types, array/scalar shape and relevant `ByVal`/`ByRef` constraints as part of overload matching
- [ ] support overloads with different numbers of parameters
- [ ] support overloads with the same number of parameters but different parameter types
- [ ] support overloads that combine required and `Optional` parameters without ambiguous resolution
- [ ] define how `Variant`, `Object`, numeric widening and String/Date coercions participate in overload selection
- [ ] choose the most specific valid overload when more than one overload can accept the supplied arguments
- [ ] reject calls where no overload matches with a clear compiler diagnostic listing the supplied signature
- [ ] reject ambiguous calls where more than one overload is equally valid with a clear compiler diagnostic
- [ ] reject duplicate declarations with an identical effective signature
- [ ] preserve normal return-type checking for overloaded `Function` members
- [ ] preserve normal `ByRef` compatibility and write-back semantics for the selected overload
- [ ] support overload resolution for calls both with and without the explicit `Call` keyword
- [ ] support overload resolution when invoking a member on a typed class variable
- [ ] support overload resolution through `Me` inside the declaring class
- [ ] define behavior for overload lookup across class inheritance if/when inherited member lookup is enabled

## Regression coverage

- [ ] positive sample: same `Function` name with `Integer`, `String` and `Date` parameter variants
- [ ] positive sample: same `Sub` name with one-parameter and two-parameter variants
- [ ] positive sample: typed object overload versus `Object`/`Variant` fallback
- [ ] positive sample: numeric widening selects the most specific valid overload
- [ ] negative sample: duplicate identical signatures
- [ ] negative sample: no matching overload
- [ ] negative sample: ambiguous overload
- [ ] verify generated C# uses valid CLR method overloads or stable compiler-generated unique method names while preserving XPScript call semantics
- [ ] add GitHub Actions regression gate before marking this feature complete

## Example target syntax

```xpscript
Class Formatter
    Public Function FormatValue(value As Integer) As String
        FormatValue = "INTEGER=" & CStr(value)
    End Function

    Public Function FormatValue(value As String) As String
        FormatValue = "STRING=" & value
    End Function

    Public Sub SetValue(value As Integer)
        ' integer implementation
    End Sub

    Public Sub SetValue(value As String)
        ' string implementation
    End Sub
End Class
```

The call determines the selected implementation:

```xpscript
Dim formatter As New Formatter
Print formatter.FormatValue(42)
Print formatter.FormatValue("hello")
Call formatter.SetValue(10)
Call formatter.SetValue("text")
```
