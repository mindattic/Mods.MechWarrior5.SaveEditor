namespace Mech5.Shared.Unreal.Properties;

/// <summary>
/// An object reference. MW5 inlines whole sub-objects after the path (e.g. each persistent
/// model), which are exposed through <see cref="Inline"/> when they parse cleanly.
/// </summary>
public sealed class ObjectProperty(string name, FString path = default) : UProperty(name)
{
    public override string TypeName => "ObjectProperty";

    public FString Path { get; set; } = path;

    public PropertyList? Inline { get; set; }

    /// <summary>Bytes after <see cref="Path"/> (and after <see cref="Inline"/> when present).</summary>
    public byte[] Trailing { get; set; } = [];

    protected override UProperty CloneCore() => new ObjectProperty(Name, Path)
    {
        Inline = Inline?.DeepClone(),
        Trailing = (byte[])Trailing.Clone(),
    };
}

/// <summary>Localized text. Kept as raw bytes; <see cref="FText"/> decodes the common forms.</summary>
public sealed class TextProperty(string name, byte[] data) : UProperty(name)
{
    public override string TypeName => "TextProperty";

    public byte[] Data { get; set; } = data;

    public string? DisplayString => FText.TryGetDisplayString(Data);

    protected override UProperty CloneCore() => new TextProperty(Name, (byte[])Data.Clone());
}

/// <summary>Any property this editor never needs to edit: its payload is preserved verbatim.</summary>
public sealed class RawProperty(string name, string typeName, byte[] tagData, byte[] value) : UProperty(name)
{
    public override string TypeName => typeName;

    /// <summary>Type-specific tag bytes between the array index and the property-GUID flag.</summary>
    public byte[] TagData { get; set; } = tagData;

    public byte[] Value { get; set; } = value;

    protected override UProperty CloneCore() =>
        new RawProperty(Name, TypeName, (byte[])TagData.Clone(), (byte[])Value.Clone());
}
