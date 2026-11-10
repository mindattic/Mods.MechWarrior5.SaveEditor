using Mech5.Shared.Unreal.Properties;

namespace Mech5.Shared.Unreal;

/// <summary>
/// Reads and writes Unreal tagged-property streams. Anything not understood is kept as raw
/// bytes, so a read followed by a write reproduces the input exactly.
/// </summary>
public static class PropertySerializer
{
    public const string Terminator = "None";

    private static readonly Dictionary<string, int> FixedSizeNativeStructs = new(StringComparer.Ordinal)
    {
        ["Guid"] = 16,
        ["DateTime"] = 8,
        ["Timespan"] = 8,
        ["Vector"] = 12,
        ["Vector2D"] = 8,
        ["Vector4"] = 16,
        ["Rotator"] = 12,
        ["Quat"] = 16,
        ["LinearColor"] = 16,
        ["Color"] = 4,
        ["IntPoint"] = 8,
        ["IntVector"] = 12,
        ["Box"] = 25,
        ["Box2D"] = 17,
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
            if (name == Terminator)
            {
                return list;
            }

            list.Add(ReadProperty(reader, name));
        }
    }

    public static PropertyList ReadList(byte[] data, out byte[] trailing)
    {
        var reader = new UnrealReader(data);
        var list = ReadList(reader);
        trailing = reader.ReadBytes(reader.Remaining);
        return list;
    }

    public static void WriteList(UnrealWriter writer, PropertyList list)
    {
        foreach (var property in list)
        {
            WriteProperty(writer, property);
        }

        writer.WriteFString(Terminator);
    }

    public static byte[] WriteList(PropertyList list, ReadOnlySpan<byte> trailing = default)
    {
        var writer = new UnrealWriter();
        WriteList(writer, list);
        writer.WriteBytes(trailing);
        return writer.ToArray();
    }

    private static UProperty ReadProperty(UnrealReader reader, string name)
    {
        var typeAt = reader.Position;
        var type = reader.ReadFString().Value ?? throw new SaveFormatException($"Property '{name}' has no type", typeAt);
        var size = reader.ReadInt32();
        var arrayIndex = reader.ReadInt32();
        if (size < 0)
        {
            throw new SaveFormatException($"Property '{name}' has negative size {size}", typeAt);
        }

        UProperty property;
        switch (type)
        {
            case "StructProperty":
            {
                var structType = reader.ReadFString();
                var structGuid = reader.ReadGuid();
                var guid = ReadPropertyGuid(reader);
                property = new StructProperty(name, structType, ReadStructValue(reader.Slice(size), structType.ToString()))
                {
                    StructGuid = structGuid,
                    PropertyGuid = guid,
                };
                break;
            }

            case "BoolProperty":
            {
                var value = reader.ReadByte() != 0;
                var guid = ReadPropertyGuid(reader);
                reader.Slice(size);
                property = new BoolProperty(name, value) { PropertyGuid = guid };
                break;
            }

            case "ByteProperty":
            {
                var enumType = reader.ReadFString();
                var guid = ReadPropertyGuid(reader);
                var value = reader.Slice(size);
                var byteProperty = new ByteProperty(name) { EnumType = enumType, PropertyGuid = guid };
                if (size == 1)
                {
                    byteProperty.RawValue = value.ReadByte();
                }
                else
                {
                    byteProperty.HoldsName = true;
                    byteProperty.NameValue = value.ReadFString();
                    ExpectConsumed(value, name);
                }

                property = byteProperty;
                break;
            }

            case "EnumProperty":
            {
                var enumType = reader.ReadFString();
                var guid = ReadPropertyGuid(reader);
                var value = reader.Slice(size);
                property = new EnumProperty(name, enumType, value.ReadFString()) { PropertyGuid = guid };
                ExpectConsumed(value, name);
                break;
            }

            case "ArrayProperty":
            {
                var innerType = reader.ReadFString();
                var guid = ReadPropertyGuid(reader);
                property = new ArrayProperty(name, innerType, ReadArray(reader.Slice(size), innerType.ToString()))
                {
                    PropertyGuid = guid,
                };
                break;
            }

            case "SetProperty":
            {
                var innerType = reader.ReadFString();
                var guid = ReadPropertyGuid(reader);
                property = new SetProperty(name, innerType, reader.ReadBytes(size)) { PropertyGuid = guid };
                break;
            }

            case "MapProperty":
            {
                var keyType = reader.ReadFString();
                var valueType = reader.ReadFString();
                var guid = ReadPropertyGuid(reader);
                property = new MapProperty(name, keyType, valueType, reader.ReadBytes(size)) { PropertyGuid = guid };
                break;
            }

            default:
            {
                var guid = ReadPropertyGuid(reader);
                property = ReadSimpleValue(name, type, reader.Slice(size));
                property.PropertyGuid = guid;
                break;
            }
        }

        property.ArrayIndex = arrayIndex;
        return property;
    }

    private static UProperty ReadSimpleValue(string name, string type, UnrealReader value)
    {
        UProperty property = type switch
        {
            "IntProperty" => new IntProperty(name, value.ReadInt32()),
            "UInt32Property" => new UInt32Property(name, value.ReadUInt32()),
            "Int64Property" => new Int64Property(name, value.ReadInt64()),
            "FloatProperty" => new FloatProperty(name, value.ReadSingle()),
            "DoubleProperty" => new DoubleProperty(name, value.ReadDouble()),
            "StrProperty" => new StrProperty(name, value.ReadFString()),
            "NameProperty" => new NameProperty(name, value.ReadFString()),
            "TextProperty" => new TextProperty(name, value.ReadBytes(value.Remaining)),
            "ObjectProperty" => ReadObject(name, value),
            _ => new RawProperty(name, type, [], value.ReadBytes(value.Remaining)),
        };

        ExpectConsumed(value, name);
        return property;
    }

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
            property.Inline = ReadList(attempt);
            property.Trailing = attempt.ReadBytes(attempt.Remaining);
        }
        catch (SaveFormatException)
        {
            property.Inline = null;
            property.Trailing = value.Fork().ReadBytes(value.Remaining);
        }

        value.Slice(value.Remaining);
        return property;
    }

    private static IStructValue ReadStructValue(UnrealReader value, string structType)
    {
        if (structType == "Guid" && value.Remaining == 16)
        {
            return new GuidValue(value.ReadGuid());
        }

        if (FixedSizeNativeStructs.ContainsKey(structType) || VariableSizeNativeStructs.Contains(structType))
        {
            return new RawStructValue(value.ReadBytes(value.Remaining));
        }

        var attempt = value.Fork();
        try
        {
            var list = ReadList(attempt);
            if (attempt.AtEnd)
            {
                return list;
            }
        }
        catch (SaveFormatException)
        {
        }

        return new RawStructValue(value.ReadBytes(value.Remaining));
    }

    private static ArrayStorage ReadArray(UnrealReader value, string innerType)
    {
        var attempt = value.Fork();
        try
        {
            ArrayStorage? parsed = innerType switch
            {
                "ByteProperty" => ReadByteArray(attempt),
                "StructProperty" => ReadStructArray(attempt),
                _ => null,
            };

            if (parsed is not null && attempt.AtEnd)
            {
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

    private static StructArrayStorage? ReadStructArray(UnrealReader reader)
    {
        var count = reader.ReadInt32();
        var innerName = reader.ReadFString();
        var tagType = reader.ReadFString();
        if (tagType.Value != "StructProperty")
        {
            return null;
        }

        var innerSize = reader.ReadInt32();
        var innerIndex = reader.ReadInt32();
        var structType = reader.ReadFString();
        var storage = new StructArrayStorage(innerName, structType)
        {
            StructGuid = reader.ReadGuid(),
            InnerArrayIndex = innerIndex,
        };
        storage.InnerPropertyGuid = ReadPropertyGuid(reader);

        var elements = reader.Slice(innerSize);
        var type = structType.ToString();
        FixedSizeNativeStructs.TryGetValue(type, out var fixedSize);
        if (VariableSizeNativeStructs.Contains(type))
        {
            return null;
        }

        for (var i = 0; i < count; i++)
        {
            IStructValue element = fixedSize switch
            {
                16 when type == "Guid" => new GuidValue(elements.ReadGuid()),
                > 0 => new RawStructValue(elements.ReadBytes(fixedSize)),
                _ => ReadList(elements),
            };
            storage.Elements.Add(element);
        }

        return elements.AtEnd ? storage : null;
    }

    private static Guid? ReadPropertyGuid(UnrealReader reader) =>
        reader.ReadByte() != 0 ? reader.ReadGuid() : null;

    private static void ExpectConsumed(UnrealReader value, string name)
    {
        if (!value.AtEnd)
        {
            throw new SaveFormatException($"Property '{name}' has {value.Remaining} unread byte(s)", value.Position);
        }
    }

    private static void WriteProperty(UnrealWriter writer, UProperty property)
    {
        writer.WriteFString(property.Name);
        writer.WriteFString(property.TypeName);
        var sizeAt = writer.ReserveInt32();
        writer.WriteInt32(property.ArrayIndex);

        switch (property)
        {
            case StructProperty s:
                writer.WriteFString(s.StructType);
                writer.WriteGuid(s.StructGuid);
                break;
            case BoolProperty b:
                writer.WriteByte(b.Value ? (byte)1 : (byte)0);
                break;
            case ByteProperty b:
                writer.WriteFString(b.EnumType);
                break;
            case EnumProperty e:
                writer.WriteFString(e.EnumType);
                break;
            case ArrayProperty a:
                writer.WriteFString(a.InnerType);
                break;
            case SetProperty s:
                writer.WriteFString(s.InnerType);
                break;
            case MapProperty m:
                writer.WriteFString(m.KeyType);
                writer.WriteFString(m.ValueType);
                break;
            case RawProperty r:
                writer.WriteBytes(r.TagData);
                break;
        }

        WritePropertyGuid(writer, property.PropertyGuid);

        var valueStart = writer.Position;
        WriteValue(writer, property);
        writer.PatchInt32(sizeAt, writer.Position - valueStart);
    }

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
                writer.WriteFString(s.InnerName);
                writer.WriteFString("StructProperty");
                var sizeAt = writer.ReserveInt32();
                writer.WriteInt32(s.InnerArrayIndex);
                writer.WriteFString(s.StructType);
                writer.WriteGuid(s.StructGuid);
                WritePropertyGuid(writer, s.InnerPropertyGuid);
                var start = writer.Position;
                foreach (var element in s.Elements)
                {
                    WriteStructValue(writer, element);
                }

                writer.PatchInt32(sizeAt, writer.Position - start);
                break;
            default:
                throw new InvalidOperationException($"No writer for {storage.GetType().Name}");
        }
    }

    private static void WritePropertyGuid(UnrealWriter writer, Guid? guid)
    {
        if (guid is { } value)
        {
            writer.WriteByte(1);
            writer.WriteGuid(value);
        }
        else
        {
            writer.WriteByte(0);
        }
    }
}
