namespace MechWarrior5.Shared.Unreal;

/// <summary>
/// An Unreal FString as stored on disk. <see cref="Value"/> is null for the zero-length encoding
/// (no terminator), which differs on disk from an empty string (a lone terminator).
/// </summary>
public readonly record struct FString(string? Value, bool IsWide = false)
{
    public static FString Null => default;

    public bool IsNull => Value is null;

    // Unreal only writes a string as narrow when it is pure ASCII; on load it maps narrow bytes
    // above 0x7F to '?', so any non-ASCII character needs the UTF-16 encoding.
    public static FString From(string? value) =>
        new(value, value is not null && value.Any(static c => c > 0x7F));

    public static implicit operator FString(string? value) => From(value);

    public override string ToString() => Value ?? string.Empty;
}
