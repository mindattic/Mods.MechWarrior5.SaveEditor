namespace MechWarrior5.Shared.Unreal;

/// <summary>
/// A UE 5.4+ property type as written in tags: a name with nested parameters, e.g.
/// <c>ArrayProperty(StructProperty(Guid(/Script/CoreUObject)))</c>.
/// </summary>
public sealed record PropertyTypeName(FString Name, IReadOnlyList<PropertyTypeName> Parameters)
{
    public PropertyTypeName(FString name, params PropertyTypeName[] parameters)
        : this(name, (IReadOnlyList<PropertyTypeName>)parameters)
    {
    }

    /// <summary>The first parameter, e.g. the inner type of an array or the struct of a struct property.</summary>
    public PropertyTypeName? First => Parameters.Count > 0 ? Parameters[0] : null;

    /// <summary>For <c>StructProperty(X(...))</c>, the struct name X.</summary>
    public string? StructName => Name.Value == "StructProperty" ? First?.Name.Value : null;

    public static PropertyTypeName Read(UnrealReader reader)
    {
        var at = reader.Position;
        var name = reader.ReadFString();
        var count = reader.ReadInt32();
        if (count is < 0 or > 16)
        {
            throw new SaveFormatException($"Implausible type parameter count {count} for '{name}'", at);
        }

        var parameters = new PropertyTypeName[count];
        for (var i = 0; i < count; i++)
        {
            parameters[i] = Read(reader);
        }

        return new PropertyTypeName(name, parameters);
    }

    public void Write(UnrealWriter writer)
    {
        writer.WriteFString(Name);
        writer.WriteInt32(Parameters.Count);
        foreach (var parameter in Parameters)
        {
            parameter.Write(writer);
        }
    }

    public bool Equals(PropertyTypeName? other) =>
        other is not null && Name == other.Name && Parameters.SequenceEqual(other.Parameters);

    public override int GetHashCode() => HashCode.Combine(Name, Parameters.Count);

    public override string ToString() =>
        Parameters.Count == 0 ? Name.ToString() : $"{Name}({string.Join(",", Parameters)})";
}

/// <summary>
/// The UE 5.4+ tag fields beyond name/size: the type tree, <c>EPropertyTagFlags</c>, and any
/// property-extension bytes. Kept per property so a rewrite reproduces the input.
/// </summary>
public sealed record Ue5Tag(PropertyTypeName Type, byte Flags = 0, byte[]? Extensions = null)
{
    public const byte HasArrayIndex = 0x01;
    public const byte HasPropertyGuid = 0x02;
    public const byte HasPropertyExtensions = 0x04;
    public const byte HasBinaryOrNativeSerialize = 0x08;
    public const byte BoolTrue = 0x10;
    public const byte SkippedSerialize = 0x20;

    public bool IsBinaryOrNative => (Flags & HasBinaryOrNativeSerialize) != 0;

    /// <summary>A simple tag for a new property of a parameterless type, e.g. <c>StrProperty</c>.</summary>
    public static Ue5Tag Simple(string typeName) => new(new PropertyTypeName(typeName));
}
