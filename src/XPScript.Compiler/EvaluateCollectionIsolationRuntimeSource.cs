namespace XPScript.Compiler;

internal static class EvaluateCollectionIsolationRuntimeSource
{
    public const string Code = """
internal static class XPScriptEvaluateCollectionRuntime
{
    private const long MaxCollectionElements = 100_000;
    private const long MaxEstimatedPayloadBytes = 16L * 1024L * 1024L;
    private const int MaxSnapshotDepth = 64;

    private sealed class SnapshotBudget
    {
        private long _elements;
        private long _bytes;

        public void AddElements(long count)
        {
            if (count < 0 || count > MaxCollectionElements - _elements)
                throw new XPScriptRuntimeException(5,
                    $"Evaluate collection snapshot exceeds the maximum element budget of {MaxCollectionElements}.");
            _elements += count;
        }

        public void AddBytes(long count)
        {
            if (count < 0 || count > MaxEstimatedPayloadBytes - _bytes)
                throw new XPScriptRuntimeException(5,
                    $"Evaluate collection snapshot exceeds the maximum estimated payload budget of {MaxEstimatedPayloadBytes} bytes.");
            _bytes += count;
        }

        public void AddString(string value) => AddBytes(Encoding.UTF8.GetByteCount(value));

        public void AddScalar(object value)
        {
            AddBytes(value switch
            {
                byte or sbyte or bool => 1,
                short or ushort or char => 2,
                int or uint or float => 4,
                long or ulong or double or DateTime => 8,
                decimal => 16,
                _ => 32
            });
        }
    }

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
        return SnapshotCore(value, visited, new SnapshotBudget(), 0, forReturn: false);
    }

    public static object? SnapshotReturn(object? value)
    {
        var visited = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        return SnapshotCore(value, visited, new SnapshotBudget(), 0, forReturn: true);
    }

    public static object? ReadIndexed(object? value, IReadOnlyList<object?> args)
    {
        if (value is ListSnapshot list)
        {
            if (args.Count != 1)
                throw new XPScriptRuntimeException(5, "Evaluate List callvar requires exactly one tag.");
            return list.Get(args[0]);
        }

        if (value is ILSList runtimeList)
        {
            if (args.Count != 1)
                throw new XPScriptRuntimeException(5, "Evaluate List callvar requires exactly one tag.");
            return runtimeList.GetValue(args[0]);
        }

        if (value is LSArray array)
        {
            if (args.Count != array.Rank)
                throw new XPScriptRuntimeException(5, "Evaluate array callvar received the wrong number of indexes.");
            return array.Get(args.ToArray());
        }

        if (value is Array clrArray)
        {
            if (args.Count != clrArray.Rank)
                throw new XPScriptRuntimeException(5, "Evaluate array callvar received the wrong number of indexes.");
            var indexes = args.Select(XPScriptRuntime.CInt).ToArray();
            return clrArray.GetValue(indexes);
        }

        throw new XPScriptRuntimeException(5, "callvar is not an indexed value.");
    }

    public static void WriteIndexed(object? value, IReadOnlyList<object?> args, object? newValue)
    {
        if (value is ListSnapshot list)
        {
            if (args.Count != 1)
                throw new XPScriptRuntimeException(5, "Evaluate List callvar requires exactly one tag.");
            list.Set(Convert.ToString(args[0], CultureInfo.CurrentCulture) ?? "", newValue);
            return;
        }

        if (value is ILSList runtimeList)
        {
            if (args.Count != 1)
                throw new XPScriptRuntimeException(5, "Evaluate List callvar requires exactly one tag.");
            runtimeList.SetValue(args[0], newValue);
            return;
        }

        if (value is LSArray array)
        {
            if (args.Count != array.Rank)
                throw new XPScriptRuntimeException(5, "Evaluate array callvar received the wrong number of indexes.");
            array.Set(newValue, args.ToArray());
            return;
        }

        if (value is Array clrArray)
        {
            if (args.Count != clrArray.Rank)
                throw new XPScriptRuntimeException(5, "Evaluate array callvar received the wrong number of indexes.");
            var indexes = args.Select(XPScriptRuntime.CInt).ToArray();
            clrArray.SetValue(newValue, indexes);
            return;
        }

        throw new XPScriptRuntimeException(5, "callvar is not an indexed value.");
    }

    public static bool IsListValue(object? value) => value is ListSnapshot or ILSList;

    private static long CountElements(LSArray array)
    {
        long total = 1;
        try
        {
            for (var i = 0; i < array.Rank; i++)
            {
                var length = checked((long)array.UpperBounds[i] - array.LowerBounds[i] + 1L);
                total = checked(total * length);
                if (total > MaxCollectionElements) return total;
            }
            return total;
        }
        catch (OverflowException)
        {
            throw new XPScriptRuntimeException(5,
                $"Evaluate collection snapshot exceeds the maximum element budget of {MaxCollectionElements}.");
        }
    }

    private static object? SnapshotCore(
        object? value,
        Dictionary<object, object> visited,
        SnapshotBudget budget,
        int depth,
        bool forReturn)
    {
        if (depth > MaxSnapshotDepth)
            throw new XPScriptRuntimeException(5,
                $"Evaluate collection snapshot exceeds the maximum nesting depth of {MaxSnapshotDepth}.");

        if (value is null)
            return null;

        if (value is string text)
        {
            budget.AddString(text);
            return text;
        }

        if (value.GetType().IsValueType)
        {
            budget.AddScalar(value);
            return value;
        }

        if (visited.TryGetValue(value, out var existing))
            return existing;

        if (value is LSArray sourceArray)
        {
            if (!sourceArray.IsAllocated)
                return new LSArray(sourceArray.ElementType, true);

            budget.AddElements(CountElements(sourceArray));
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
                        copy.Set(SnapshotCore(sourceArray.Get(indexes), visited, budget, depth + 1, forReturn), indexes);
                    }
                }
            }
        }

        if (value is ILSList sourceList)
        {
            if (forReturn)
            {
                var copy = new LSList<object?>();
                visited[value] = copy;
                foreach (var entry in sourceList.SnapshotEntries())
                {
                    budget.AddElements(1);
                    budget.AddString(entry.Key);
                    copy[entry.Key] = SnapshotCore(entry.Value, visited, budget, depth + 1, true);
                }
                return copy;
            }

            var snapshot = new ListSnapshot();
            visited[value] = snapshot;
            foreach (var entry in sourceList.SnapshotEntries())
            {
                budget.AddElements(1);
                budget.AddString(entry.Key);
                snapshot.Set(entry.Key, SnapshotCore(entry.Value, visited, budget, depth + 1, false));
            }
            return snapshot;
        }

        if (value is ListSnapshot sourceSnapshot)
        {
            if (forReturn)
            {
                var copy = new LSList<object?>();
                visited[value] = copy;
                foreach (var entry in sourceSnapshot.Entries)
                {
                    budget.AddElements(1);
                    budget.AddString(entry.Key);
                    copy[entry.Key] = SnapshotCore(entry.Value, visited, budget, depth + 1, true);
                }
                return copy;
            }

            var snapshot = new ListSnapshot();
            visited[value] = snapshot;
            foreach (var entry in sourceSnapshot.Entries)
            {
                budget.AddElements(1);
                budget.AddString(entry.Key);
                snapshot.Set(entry.Key, SnapshotCore(entry.Value, visited, budget, depth + 1, false));
            }
            return snapshot;
        }

        if (value is Array sourceClrArray)
        {
            budget.AddElements(sourceClrArray.LongLength);
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
                        copy.SetValue(SnapshotCore(sourceClrArray.GetValue(indexes), visited, budget, depth + 1, forReturn), indexes);
                }
            }
        }

        throw new XPScriptRuntimeException(5,
            "Evaluate callvar contains an unsupported mutable object type: " + value.GetType().Name);
    }
}
""";
}
