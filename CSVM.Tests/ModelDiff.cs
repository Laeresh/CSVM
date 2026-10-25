using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Deep comparison of two loader models: the one built from the extraction and the one a reader
/// builds from the original's own files. It walks every instance field, public or private, so
/// state kept behind accessors counts, and names the first difference by path
/// (<c>GameZ.Meshes[3].Vertices[7].Y</c>). Floats and doubles compare bit for bit, NaN payload
/// and the sign of zero included. Lists and arrays compare by count, then in order; dictionaries
/// and sets by their exact keys, order-free, and by their comparer.
/// </summary>
public static class ModelDiff
{
    // Built lazily on first query, so comparing them reports only which side was asked first.
    private static readonly string[] DefaultSkips =
    {
        "GameZ._parent", "GameZ._placed", "GameZ._markerGizmo",
    };

    private static readonly ConcurrentDictionary<Type, (FieldInfo Field, string Name)[]> FieldCache = new();

    /// <summary>Fails with the first difference's path and both values. Otherwise returns how many
    /// leaf values were compared, so a caller can assert the walk saw something. A
    /// <paramref name="skip"/> entry names a member as <c>Type.member</c>, or one whole path.</summary>
    public static int AssertEqual(object? expected, object? actual, params string[] skip)
    {
        var walk = new Walk(skip);
        if (walk.Diff(expected, actual, RootName(expected ?? actual)) is { } difference)
        {
            Assert.Fail("model diff, first difference at " + difference);
        }

        return walk.Compared;
    }

    /// <summary>The first difference as <c>path: expected …, actual …</c>, or null when equal.</summary>
    public static string? FirstDifference(object? expected, object? actual, params string[] skip) =>
        new Walk(skip).Diff(expected, actual, RootName(expected ?? actual));

    private static string RootName(object? value) => value?.GetType().Name.Split('`')[0] ?? "null";

    private static bool IsLeaf(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);

    private static bool SameLeaf(object e, object a) => e switch
    {
        float f => BitConverter.SingleToInt32Bits(f) == BitConverter.SingleToInt32Bits((float)a),
        double d => BitConverter.DoubleToInt64Bits(d) == BitConverter.DoubleToInt64Bits((double)a),
        _ => e.Equals(a),
    };

    private static string Show(object? value) => value switch
    {
        null => "null",
        float f => $"{f.ToString("R", CultureInfo.InvariantCulture)} (0x{BitConverter.SingleToInt32Bits(f):X8})",
        double d => $"{d.ToString("R", CultureInfo.InvariantCulture)} (0x{BitConverter.DoubleToInt64Bits(d):X16})",
        string s => $"\"{s}\"",
        IFormattable x => x.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? value.GetType().Name,
    };

    private static string Index(string path, object key) =>
        $"{path}[{(key is string s ? $"\"{s}\"" : Show(key))}]";

    private static bool IsSet(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISet<>));

    private static bool IsMemory(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(Memory<>)
            || type.GetGenericTypeDefinition() == typeof(ReadOnlyMemory<>));

    private static object? ComparerOf(object collection) =>
        collection.GetType().GetProperty("Comparer")?.GetValue(collection);

    // Base type first, each type's fields in declaration order; an auto-property's backing field
    // is shown under the property's name.
    private static (FieldInfo Field, string Name)[] FieldsOf(Type type) => FieldCache.GetOrAdd(type, t =>
    {
        var chain = new List<Type>();
        for (var c = t; c != null && c != typeof(object) && c != typeof(ValueType); c = c.BaseType)
        {
            chain.Insert(0, c);
        }

        const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        return chain
            .SelectMany(c => c.GetFields(Flags).OrderBy(f => f.MetadataToken))
            .Select(f => (f, f.Name.StartsWith('<') ? f.Name[1..f.Name.IndexOf('>')] : f.Name))
            .ToArray();
    });

    private sealed class Walk
    {
        private readonly HashSet<string> _skip;
        private readonly HashSet<(object, object)> _visited = new(PairComparer.Instance);

        public Walk(string[] skip) => _skip = new HashSet<string>(DefaultSkips.Concat(skip), StringComparer.Ordinal);

        public int Compared { get; private set; }

        public string? Diff(object? e, object? a, string path)
        {
            if (e is null || a is null)
            {
                Compared++;
                return e is null && a is null ? null : $"{path}: expected {Show(e)}, actual {Show(a)}";
            }

            var type = e.GetType();
            if (type != a.GetType())
            {
                return $"{path}: expected {Show(e)} ({type.Name}), actual {Show(a)} ({a.GetType().Name})";
            }

            if (IsLeaf(type))
            {
                Compared++;
                return SameLeaf(e, a) ? null : $"{path}: expected {Show(e)}, actual {Show(a)}";
            }

            // A pair met again is already compared or being compared above: a cycle or a shared node.
            if (!type.IsValueType && !_visited.Add((e, a)))
            {
                return null;
            }

            return e switch
            {
                byte[] bytes => Bytes(bytes, (byte[])a, path),
                Image image => ImageDiff(image, (Image)a, path),
                GodotObject => throw new NotSupportedException(
                    $"{path}: {type.Name} is an engine object; compare the data it was built from"),
                IDictionary dict => Dictionary(dict, (IDictionary)a, path),
                IEnumerable set when IsSet(type) => Set(set, (IEnumerable)a, path),
                IList list => List(list, (IList)a, path),
                IEnumerable seq => List(seq.Cast<object?>().ToList(), ((IEnumerable)a).Cast<object?>().ToList(), path),
                _ when IsMemory(type) => Diff(ToArray(e), ToArray(a), path),
                _ => Fields(e, a, type, path),
            };
        }

        private static object? ToArray(object memory) => memory.GetType().GetMethod("ToArray")!.Invoke(memory, null);

        private string? Bytes(byte[] e, byte[] a, string path)
        {
            if (e.Length != a.Length)
            {
                return $"{path}.Count: expected {e.Length}, actual {a.Length}";
            }

            Compared += e.Length;
            int i = e.AsSpan().CommonPrefixLength(a);
            return i == e.Length ? null : $"{path}[{i}]: expected {e[i]}, actual {a[i]}";
        }

        // Engine-only: an Image's accessors need a running engine, so a unit test compares the
        // engine-free buffer the Image is built from instead.
        private string? ImageDiff(Image e, Image a, string path) =>
            Diff(e.GetFormat(), a.GetFormat(), path + ".Format")
            ?? Diff(e.GetWidth(), a.GetWidth(), path + ".Width")
            ?? Diff(e.GetHeight(), a.GetHeight(), path + ".Height")
            ?? Diff(e.GetMipmapCount(), a.GetMipmapCount(), path + ".Mipmaps")
            ?? Diff(e.GetData(), a.GetData(), path + ".Data");

        private string? Dictionary(IDictionary e, IDictionary a, string path)
        {
            if (Diff(ComparerOf(e), ComparerOf(a), path + ".Comparer") is { } comparer)
            {
                return comparer;
            }

            // Default equality, so a key must match exactly even under a case-blind comparer.
            var actualKeys = new HashSet<object>(a.Keys.Cast<object>());
            foreach (DictionaryEntry entry in e)
            {
                var at = Index(path, entry.Key);
                if (!actualKeys.Contains(entry.Key))
                {
                    return $"{at}: missing from actual";
                }

                if (Diff(entry.Value, a[entry.Key], at) is { } difference)
                {
                    return difference;
                }
            }

            var expectedKeys = new HashSet<object>(e.Keys.Cast<object>());
            var extra = a.Keys.Cast<object>().FirstOrDefault(k => !expectedKeys.Contains(k));
            return extra == null ? null : $"{Index(path, extra)}: not in expected";
        }

        private string? Set(IEnumerable e, IEnumerable a, string path)
        {
            if (Diff(ComparerOf(e), ComparerOf(a), path + ".Comparer") is { } comparer)
            {
                return comparer;
            }

            var expected = new HashSet<object?>(e.Cast<object?>());
            var actual = new HashSet<object?>(a.Cast<object?>());
            Compared += expected.Count;
            if (expected.FirstOrDefault(x => !actual.Contains(x)) is { } missing)
            {
                return $"{path}: {Show(missing)} missing from actual";
            }

            var extra = actual.FirstOrDefault(x => !expected.Contains(x));
            return extra == null ? null : $"{path}: {Show(extra)} not in expected";
        }

        private string? List(IList e, IList a, string path)
        {
            Compared++;
            if (e.Count != a.Count)
            {
                return $"{path}.Count: expected {e.Count}, actual {a.Count}";
            }

            for (int i = 0; i < e.Count; i++)
            {
                if (Diff(e[i], a[i], $"{path}[{i}]") is { } difference)
                {
                    return difference;
                }
            }

            return null;
        }

        private string? Fields(object e, object a, Type type, string path)
        {
            foreach (var (field, name) in FieldsOf(type))
            {
                var at = path + "." + name;
                if (_skip.Contains(field.DeclaringType!.Name + "." + name) || _skip.Contains(at))
                {
                    continue;
                }

                if (Diff(field.GetValue(e), field.GetValue(a), at) is { } difference)
                {
                    return difference;
                }
            }

            return null;
        }
    }

    private sealed class PairComparer : IEqualityComparer<(object, object)>
    {
        public static readonly PairComparer Instance = new();

        public bool Equals((object, object) x, (object, object) y) =>
            ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);

        public int GetHashCode((object, object) pair) =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(pair.Item1), RuntimeHelpers.GetHashCode(pair.Item2));
    }
}
