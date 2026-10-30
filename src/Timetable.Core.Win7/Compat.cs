namespace Timetable.Core;

/// <summary>Small helpers that .NET 8 has built in and .NET Framework lacks, so the shared Core code compiles unchanged.</summary>
internal static class Compat
{
    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key) where TKey : notnull =>
        dictionary.TryGetValue(key, out var value) ? value : default;

    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }
}
