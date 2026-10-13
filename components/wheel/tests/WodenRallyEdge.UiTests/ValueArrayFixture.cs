namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

// Model the shipped interop indexer's copy semantics, not reference mutation.
internal sealed class Il2CppReferenceArray<T> where T:struct
{
    private readonly T[] _values;
    internal Il2CppReferenceArray(T[] values) => _values = values;
    public static implicit operator Il2CppReferenceArray<T>(T[] values) => new(values);
    internal int Length => _values.Length;
    internal T this[int index] { get => _values[index]; set => _values[index] = value; }
}
