using Mech5.Shared.Unreal.Properties;

namespace Mech5.Shared.Unreal;

/// <summary>
/// Reads and writes UE 5.4+ tagged-property streams (MechWarrior 5: Clans) into the same property
/// model as <see cref="PropertySerializer"/>. Tags carry a <see cref="PropertyTypeName"/> tree and
/// flags instead of UE4's per-type tag data; each property keeps its <see cref="Ue5Tag"/> so a
/// read followed by a write reproduces the input exactly.
/// </summary>
public static class Ue5PropertySerializer
{
    private static readonly Dictionary<string, int> FixedSizeNativeStructs = new(StringComparer.Ordinal)
    {
        ["Guid"] = 16,
        ["DateTime"] = 8,
        ["Timespan"] = 8,
        ["Vector"] = 24,
        ["Vector2D"] = 16,
        ["Vector4"] = 32,
        ["Rotator"] = 24,
        ["Quat"] = 32,
        ["LinearColor"] = 16,
        ["Color"] = 4,
        ["IntPoint"] = 8,
        ["IntVector"] = 12,
        ["FrameNumber"] = 4,
    };

    private static readonly HashSet<string> VariableSizeNativeStructs = new(StringComparer.Ordinal)
    {
        "GameplayTagContainer",
        "GameplayTag",
        "SoftObjectPath",
        "SoftClassPath",
        "PerPlatformFloat",
        "PerPlatformInt",
    };

    public static PropertyList ReadList(UnrealReader reader)
    {
        var list = new PropertyList();
        while (true)
        {
            var nameAt = reader.Position;
            var name = reader.ReadFString().Value
                ?? throw new SaveFormatException("Property name is empty", nameAt);
            if (name == PropertySerializer.Terminator)
            {
                return list;
            }

            list.Add(ReadProperty(reader, name));
        }
    }

    public static void WriteList(UnrealWriter writer, PropertyList list)
    {
        foreach (var property in list)
        {
            WriteProperty(writer, property);
        }

        writer.WriteFString(PropertySerializer.Terminator);
    }

    private static UProperty ReadProperty(UnrealReader reader, string name)
    {
        var tagAt = reader.Position;
        var type = PropertyTypeName.Read(reader);
        var size = reader.ReadInt32();
        if (size < 0)
        {
            throw new SaveFormatException($"Property '{name}' has negative size {size}", tagAt);
        }

        var flags = reader.ReadByte();
        var arrayIndex = (flags & Ue5Tag.HasArrayIndex) != 0 ? reader.ReadInt32() : 0;
        Guid? guid = (flags & Ue5Tag.HasPropertyGuid) != 0 ? reader.ReadGuid() : null;
        byte[]? extensions = null;
        if ((flags & Ue5Tag.HasPropertyExtensions) != 0)
        {
            var ext = reader.ReadByte();
            extensions = (ext & 0x02) != 0 ? [ext, reader.ReadByte()] : [ext];
        }

        var tag = new Ue5Tag(type, flags, extensions);
        var value = reader.Slice(size);
        var property = ReadValue(name, tag, value);
        property.ArrayIndex = arrayIndex;
        property.PropertyGuid = guid;
        property.Ue5 = tag;
        return property;
    }

    private static UProperty ReadValue(string name, Ue5Tag tag, UnrealReader value)
    {
        var type = tag.Type;
        switch (type.Name.Value)
        {
            case "BoolProperty":
                return new BoolProperty(name, (tag.Flags & Ue5Tag.BoolTrue) != 0);
            case "StructProperty":
            {
                var structName = type.StructName ?? string.Empty;
                return new StructProperty(name, structName, ReadStructValue(value, structName, tag.IsBinaryOrNative));
            }

            case "ArrayProperty":
                return new ArrayProperty(name, type.First?.Name ?? FString.Null, ReadArray(value, type.First));
            case "MapProperty":
                return new MapProperty(name, type.First?.Name ?? FString.Null, Param(type, 1), value.ReadBytes(value.Remaining));
            case "SetProperty":
                return new SetProperty(name, type.First?.Name ?? FString.Null, value.ReadBytes(value.Remaining));
            case "EnumProperty":
            {
                var property = new EnumProperty(name, type.First?.Name ?? FString.Null, value.ReadFString());
                ExpectConsumed(value, name);
                return property;
            }

            case "ByteProperty":
            {
                var property = new ByteProperty(name) { EnumType = type.First?.Name ?? "None" };
                if (value.Remaining == 1)
                {
                    property.RawValue = value.ReadByte();
                }
                else
                {
                    property.HoldsName = true;
                    property.NameValue = value.ReadFString();
                }

                ExpectConsumed(value, name);
                return property;
            }

            case "ObjectProperty":
                return ReadObject(name, value);
            default:
            {
                UProperty property = type.Name.Value switch
                {
                    "IntProperty" => new IntProperty(name, value.ReadInt32()),
                    "UInt32Property" => new UInt32Property(name, value.ReadUInt32()),
                    "Int64Property" => new Int64Property(name, value.ReadInt64()),
                    "FloatProperty" => new FloatProperty(name, value.ReadSingle()),
                    "DoubleProperty" => new DoubleProperty(name, value.ReadDouble()),
                    "StrProperty" => new StrProperty(name, value.ReadFString()),
                    "NameProperty" => new NameProperty(name, value.ReadFString()),
                    "TextProperty" => new TextProperty(name, value.ReadBytes(value.Remaining)),
                    _ => new RawProperty(name, type.Name.ToString(), [], value.ReadBytes(value.Remaining)),
                };
                ExpectConsumed(value, name);
                return property;
            }
        }
    }

    private static FString Param(PropertyTypeName type, int index) =>
        type.Parameters.Count > index ? type.Parameters[index].Name : FString.Null;

    /// <summary>
    /// An object path, optionally followed by an inline object: a zero byte, then its properties.
    /// </summary>
    private static ObjectProperty ReadObject(string name, UnrealReader value)
    {
        var property = new ObjectProperty(name, value.ReadFString());
        if (value.AtEnd)
        {
            return property;
        }

        var attempt = value.Fork();
        try
        {
            if (attempt.ReadByte() == 0)
            {
                property.Inline = ReadList(attempt);
                property.Trailing = attempt.ReadBytes(attempt.Remaining);
                value.Slice(value.Remaining);
                return property;
            }
        }
        catch (SaveFormatException)
        {
        }

        property.Inline = null;
        property.Trailing = value.ReadBytes(value.Remaining);
        return property;
    }

    private static IStructValue ReadStructValue(UnrealReader value, string structName, bool binary)
    {
        if (structName == "Guid" && value.Remaining == 16)
        {
            return new GuidValue(value.ReadGuid());
        }

        if (binary || FixedSizeNativeStructs.ContainsKey(structName) || VariableSizeNativeStructs.Contains(structName))
        {
            return new RawStructValue(value.ReadBytes(value.Remaining));
        }

        var attempt = value.Fork();
        try
        {
            var list = ReadList(attempt);
            if (attempt.AtEnd)
            {
                value.Slice(value.Remaining);
                return list;
            }
        }
        catch (SaveFormatException)
        {
        }

        return new RawStructValue(value.ReadBytes(value.Remaining));
    }

    private static ArrayStorage ReadArray(UnrealReader value, PropertyTypeName? inner)
    {
        var attempt = value.Fork();
        try
        {
            ArrayStorage? parsed = inner?.Name.Value switch
            {
                "ByteProperty" when inner.Parameters.Count == 0 => ReadByteArray(attempt),
                "StructProperty" => ReadStructArray(attempt, inner.StructName ?? string.Empty),
                _ => null,
            };

            if (parsed is not null && attempt.AtEnd)
            {
                value.Slice(value.Remaining);
                return parsed;
            }
        }
        catch (SaveFormatException)
        {
        }

        return new RawArrayStorage(value.ReadBytes(value.Remaining));
    }

    private static ByteArrayStorage ReadByteArray(UnrealReader reader)
    {
        var count = reader.ReadInt32();
        return new ByteArrayStorage(reader.ReadBytes(count));
    }

    private static StructArrayStorage? ReadStructArray(UnrealReader reader, string structName)
    {
        if (VariableSizeNativeStructs.Contains(structName))
        {
            return null;
        }

        var count = reader.ReadInt32();
        FixedSizeNativeStructs.TryGetValue(structName, out var fixedSize);
        var storage = new StructArrayStorage(FString.Null, structName);
        for (var i = 0; i < count; i++)
        {
            IStructValue element = fixedSize switch
            {
                16 when structName == "Guid" => new GuidValue(reader.ReadGuid()),
                > 0 => new RawStructValue(reader.ReadBytes(fixedSize)),
                _ => ReadList(reader),
            };
            storage.Elements.Add(element);
        }

        return storage;
    }

    private static void ExpectConsumed(UnrealReader value, string name)
    {
        if (!value.AtEnd)
        {
            throw new SaveFormatException($"Property '{name}' has {value.Remaining} unread byte(s)", value.Position);
        }
    }

    private static void WriteProperty(UnrealWriter writer, UProperty property)
    {
        var tag = property.Ue5 ?? SynthesizeTag(property);
        var flags = tag.Flags;
        flags = Toggle(flags, Ue5Tag.BoolTrue, property is BoolProperty { Value: true });
        flags = Toggle(flags, Ue5Tag.HasArrayIndex, property.ArrayIndex != 0);
        flags = Toggle(flags, Ue5Tag.HasPropertyGuid, property.PropertyGuid is not null);

        writer.WriteFString(property.Name);
        tag.Type.Write(writer);
        var sizeAt = writer.ReserveInt32();
        writer.WriteByte(flags);
        if (property.ArrayIndex != 0)
        {
            writer.WriteInt32(property.ArrayIndex);
        }

        if (property.PropertyGuid is { } guid)
        {
            writer.WriteGuid(guid);
        }

        if ((flags & Ue5Tag.HasPropertyExtensions) != 0)
        {
            writer.WriteBytes(tag.Extensions ?? [0]);
        }

        var valueStart = writer.Position;
        WriteValue(writer, property);
        writer.PatchInt32(sizeAt, writer.Position - valueStart);
    }

    private static byte Toggle(byte flags, byte bit, bool on) => on ? (byte)(flags | bit) : (byte)(flags & ~bit);

    private static Ue5Tag SynthesizeTag(UProperty property) => property switch
    {
        IntProperty or UInt32Property or Int64Property or FloatProperty or DoubleProperty
            or BoolProperty or StrProperty or NameProperty or TextProperty => Ue5Tag.Simple(property.TypeName),
        ObjectProperty { Inline: null } => Ue5Tag.Simple(property.TypeName),
        _ => throw new InvalidOperationException(
            $"Property '{property.Name}' ({property.TypeName}) has no UE5 tag; copy one from an existing property of the same type."),
    };

    private static void WriteValue(UnrealWriter writer, UProperty property)
    {
        switch (property)
        {
            case IntProperty p: writer.WriteInt32(p.Value); break;
            case UInt32Property p: writer.WriteUInt32(p.Value); break;
            case Int64Property p: writer.WriteInt64(p.Value); break;
            case FloatProperty p: writer.WriteSingle(p.Value); break;
            case DoubleProperty p: writer.WriteDouble(p.Value); break;
            case BoolProperty: break;
            case StrProperty p: writer.WriteFString(p.Value); break;
            case NameProperty p: writer.WriteFString(p.Value); break;
            case EnumProperty p: writer.WriteFString(p.Value); break;
            case TextProperty p: writer.WriteBytes(p.Data); break;
            case RawProperty p: writer.WriteBytes(p.Value); break;
            case MapProperty p: writer.WriteBytes(p.Payload); break;
            case SetProperty p: writer.WriteBytes(p.Payload); break;
            case ByteProperty p:
                if (p.HoldsName)
                {
                    writer.WriteFString(p.NameValue);
                }
                else
                {
                    writer.WriteByte(p.RawValue);
                }

                break;
            case ObjectProperty p:
                writer.WriteFString(p.Path);
                if (p.Inline is not null)
                {
                    writer.WriteByte(0);
                    WriteList(writer, p.Inline);
                }

                writer.WriteBytes(p.Trailing);
                break;
            case StructProperty p:
                WriteStructValue(writer, p.Value);
                break;
            case ArrayProperty p:
                WriteArray(writer, p.Storage);
                break;
            default:
                throw new InvalidOperationException($"No writer for {property.GetType().Name}");
        }
    }

    private static void WriteStructValue(UnrealWriter writer, IStructValue value)
    {
        switch (value)
        {
            case GuidValue g: writer.WriteGuid(g.Value); break;
            case RawStructValue r: writer.WriteBytes(r.Data); break;
            case PropertyList list: WriteList(writer, list); break;
            default: throw new InvalidOperationException($"No writer for {value.GetType().Name}");
        }
    }

    private static void WriteArray(UnrealWriter writer, ArrayStorage storage)
    {
        switch (storage)
        {
            case ByteArrayStorage b:
                writer.WriteInt32(b.Data.Length);
                writer.WriteBytes(b.Data);
                break;
            case RawArrayStorage r:
                writer.WriteBytes(r.Payload);
                break;
            case StructArrayStorage s:
                writer.WriteInt32(s.Elements.Count);
                foreach (var element in s.Elements)
                {
                    WriteStructValue(writer, element);
                }

                break;
            default:
                throw new InvalidOperationException($"No writer for {storage.GetType().Name}");
        }
    }
}
