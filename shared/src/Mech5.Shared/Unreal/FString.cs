namespace Mech5.Shared.Unreal;

/// <summary>
/// An Unreal FString as stored on disk. <see cref="Value"/> is null for the zero-length encoding
/// (no terminator), which differs on disk from an empty string (a lone terminator).
/// </summary>
public readonly record struct FString(string? Value, bool IsWide = false)
{
    public static FString Null => default;

    public bool IsNull => Value is null;

    public static FString From(string? value) =>
        new(value, value is not null && value.Any(static c => c > 0xFF));

    public static implicit operator FString(string? value) => From(value);

    public override string ToString() => Value ?? string.Empty;
}
