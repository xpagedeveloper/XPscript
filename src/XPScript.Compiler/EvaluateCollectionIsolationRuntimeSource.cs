namespace XPScript.Compiler;

internal static class EvaluateCollectionIsolationRuntimeSource
{
    public const string Code = """
internal static class XPScriptEvaluateCollectionRuntime
{
    private sealed class ListSnapshot
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.CurrentCulture);

        public IEnumerable<KeyValuePair<string, object?>> Entries => _values;

        public void Set(string tag, object? value) => _values[tag] = value;

        public object? Get(object? tag)
        {
            var key = Convert.ToString(tag, CultureInfo.CurrentCulture) ?? "";
            if (!_values.TryGetValue(key, out var value))
                throw new XPScriptRuntimeException(9, "Evaluate List tag does not exist: " + key);
            return value;
        }
    }

    public static object? Snapshot(object? value)
    {
        var visited = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        return SnapshotCore(value, visited, 0);
    }

    public static object? ReadIndexed(object? value, IReadOnlyList<object?> args)
    {
        if (value is ListSnapshot list)
        {
            if (args.Count != 1)
                throw new XPScriptRuntimeException(5, "Evaluate List callvar requires exactly one tag.");
            return list.Get(args[0]);
        }

        if (value is LSArray array)
            return array.Get(args.ToArray());

        if (value is Array clrArray)
        {
            if (args.Count != clrArray.Rank)
                throw new XPScriptRuntimeException(5, "Evaluate array callvar received the wrong number of indexes.");
            var indexes = args.Select(XPScriptRuntime.CInt).ToArray();
            return clrArray.GetValue(indexes);
        }

        throw new XPScriptRuntimeException(5, "callvar is not an indexed value.");
    }

    public static bool IsListSnapshot(object? value) => value is ListSnapshot;

    private static object? SnapshotCore(object? value, Dictionary<object, object> visited, int depth)
    {
        if (value is null || value is string || value.GetType().IsValueType)
            return value;

        if (depth > 64)
            throw new XPScriptRuntimeException(5, "Evaluate collection snapshot exceeds the maximum nesting depth of 64.");

        if (visited.TryGetValue(value, out var existing))
            return existing;

        if (value is LSArray sourceArray)
        {
            if (!sourceArray.IsAllocated)
                return new LSArray(sourceArray.ElementType, true);

            var lower = sourceArray.LowerBounds.ToArray();
            var upper = sourceArray.UpperBounds.ToArray();
            var copy = new LSArray(sourceArray.ElementType, true, lower, upper);
            visited[value] = copy;
            var current = new int[sourceArray.Rank];
            CopyDimension(0);
            return copy;

            void CopyDimension(int dimension)
            {
                for (var i = lower[dimension]; i <= upper[dimension]; i++)
                {
                    current[dimension] = i;
                    if (dimension + 1 < current.Length)
                    {
                        CopyDimension(dimension + 1);
                    }
                    else
                    {
                        var indexes = current.Cast<object?>().ToArray();
                        copy.Set(SnapshotCore(sourceArray.Get(indexes), visited, depth + 1), indexes);
                    }
                }
            }
        }

        if (value is ILSList sourceList)
        {
            var copy = new ListSnapshot();
            visited[value] = copy;
            foreach (var entry in sourceList.SnapshotEntries())
                copy.Set(entry.Key, SnapshotCore(entry.Value, visited, depth + 1));
            return copy;
        }

        if (value is ListSnapshot sourceSnapshot)
        {
            var copy = new ListSnapshot();
            visited[value] = copy;
            foreach (var entry in sourceSnapshot.Entries)
                copy.Set(entry.Key, SnapshotCore(entry.Value, visited, depth + 1));
            return copy;
        }

        if (value is Array sourceClrArray)
        {
            var lengths = Enumerable.Range(0, sourceClrArray.Rank).Select(sourceClrArray.GetLength).ToArray();
            var lower = Enumerable.Range(0, sourceClrArray.Rank).Select(sourceClrArray.GetLowerBound).ToArray();
            var copy = Array.CreateInstance(typeof(object), lengths, lower);
            visited[value] = copy;
            var indexes = new int[sourceClrArray.Rank];
            CopyClrDimension(0);
            return copy;

            void CopyClrDimension(int dimension)
            {
                var start = sourceClrArray.GetLowerBound(dimension);
                var end = sourceClrArray.GetUpperBound(dimension);
                for (var i = start; i <= end; i++)
                {
                    indexes[dimension] = i;
                    if (dimension + 1 < indexes.Length)
                        CopyClrDimension(dimension + 1);
                    else
                        copy.SetValue(SnapshotCore(sourceClrArray.GetValue(indexes), visited, depth + 1), indexes);
                }
            }
        }

        // Arbitrary mutable objects are deliberately not bridged into Evaluate.
        throw new XPScriptRuntimeException(5,
            "Evaluate callvar contains an unsupported mutable object type: " + value.GetType().Name);
    }
}
""";
}
