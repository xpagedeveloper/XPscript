# EMPTY, NULL, NOTHING and JSON

XPScript keeps `EMPTY`, `NULL` and `NOTHING` as distinct language states at runtime. JSON has only one null representation, so serialization must deliberately collapse these states at the JSON boundary.

## Serialization contract

`JsonStringify`, `JsonEncode`, `JsonObject.Set`, `JsonArray.Add` and `JsonArray.Set` use the following rules:

| XPScript value | JSON result |
| --- | --- |
| unassigned Variant `EMPTY` | `null` |
| Variant `NULL` | `null` |
| object reference `NOTHING` | `null` |

Example:

```xpscript
Dim emptyValue As Variant
Dim nullValue As Variant
Dim person As Person

nullValue = Null
Set person = Nothing

Print JsonStringify(emptyValue)
Print JsonStringify(nullValue)
Print JsonStringify(person)
```

Output:

```text
null
null
null
```

The private runtime representation used for Variant `NULL` is never passed to the CLR JSON serializer. The internal object-reference wrapper used for `NOTHING` is also never serialized.

## Bound object references

A bound XPScript class/object reference does not currently have a general JSON object-mapping contract. Passing such a reference directly to the JSON serializer is therefore rejected with controlled runtime error 5.

This prevents implementation details such as reference wrappers or internal object fields from becoming accidental public JSON APIs.

Build a `JsonObject` explicitly when an XPScript object must be returned as JSON:

```xpscript
Dim result As New JsonObject
Call result.Set("name", person.Name)
Call result.Set("active", True)
Print result.Stringify()
```

## Deserialization contract

JSON text cannot encode the distinction between XPScript `EMPTY`, `NULL` and `NOTHING`.

When JSON `null` is read through the native JSON API, its value maps to the normal empty Variant representation. It does not reconstruct the private XPScript `NULL` sentinel and cannot reconstruct an object reference in the `NOTHING` state.

If an application needs to preserve the distinction across an API boundary, encode it explicitly in the schema, for example:

```json
{
  "state": "null"
}
```

or use separate presence/state fields defined by the application protocol.

## Security property

JSON conversion must never expose internal type names or implementation objects such as the private NULL sentinel or the `LSRef<T>` object-reference wrapper. Cross-platform regression tests enforce this behavior on Windows, Ubuntu and macOS.
