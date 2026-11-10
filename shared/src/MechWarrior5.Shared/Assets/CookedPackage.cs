using MechWarrior5.Shared.Unreal;
using MechWarrior5.Shared.Unreal.Properties;

namespace MechWarrior5.Shared.Assets;

/// <summary>An export of a cooked package: its name, class and serialized data range.</summary>
public sealed record PackageExport(string Name, string ClassName, long Offset, long Size);

/// <summary>
/// Read-only reader for cooked UE4.26 packages (<c>.uasset</c> + <c>.uexp</c>): name map, imports,
/// exports, and an export's tagged properties. Properties land in the same <see cref="PropertyList"/>
/// model as save data (names resolved from the name map; object references become their target's
/// name), so code that reads save structures reads game assets too.
/// </summary>
public sealed class CookedPackage
{
    private readonly byte[] _data;
    private readonly List<string> _names = [];
    private readonly List<string> _imports = [];
    private readonly List<PackageExport> _exports = [];

    private CookedPackage(byte[] data)
    {
        _data = data;
    }

    public IReadOnlyList<PackageExport> Exports => _exports;

    public static CookedPackage Parse(byte[] uasset, byte[] uexp)
    {
        var package = new CookedPackage([.. uasset, .. uexp]);
        package.ReadSummary(new UnrealReader(uasset));
        return package;
    }

    /// <summary>The first export of the given class, e.g. <c>MWMechLoadoutAsset</c>.</summary>
    public PackageExport? FindExport(string className) =>
        _exports.FirstOrDefault(e => string.Equals(e.ClassName, className, StringComparison.Ordinal));

    public PropertyList ReadProperties(PackageExport export)
    {
        var reader = new UnrealReader(_data, (int)export.Offset, (int)(export.Offset + export.Size));
        return ReadList(reader);
    }

    private void ReadSummary(UnrealReader r)
    {
        r.ReadUInt32();
        var legacy = r.ReadInt32();
        if (legacy != -4)
        {
            r.ReadInt32();
        }

        r.ReadInt32();
        r.ReadInt32();
        for (int versions = r.ReadInt32(), i = 0; i < versions; i++)
        {
            r.ReadBytes(20);
        }

        r.ReadInt32();
        r.ReadFString();
        r.ReadUInt32();
        var nameCount = r.ReadInt32();
        var nameOffset = r.ReadInt32();
        r.ReadInt32();
        r.ReadInt32();
        var exportCount = r.ReadInt32();
        var exportOffset = r.ReadInt32();
        var importCount = r.ReadInt32();
        var importOffset = r.ReadInt32();

        var names = new UnrealReader(_data, nameOffset);
        for (var i = 0; i < nameCount; i++)
        {
            _names.Add(names.ReadFString().Value ?? string.Empty);
            names.ReadUInt32();
        }

        var imports = new UnrealReader(_data, importOffset);
        for (var i = 0; i < importCount; i++)
        {
            ReadName(imports);
            ReadName(imports);
            imports.ReadInt32();
            _imports.Add(ReadName(imports));
        }

        // Exports can name each other as their class, so resolve classes once the table is complete.
        var classIndices = new List<int>();
        var exports = new UnrealReader(_data, exportOffset);
        for (var i = 0; i < exportCount; i++)
        {
            classIndices.Add(exports.ReadInt32());
            exports.ReadInt32();
            exports.ReadInt32();
            exports.ReadInt32();
            var name = ReadName(exports);
            exports.ReadUInt32();
            var size = exports.ReadInt64();
            var offset = exports.ReadInt64();
            exports.ReadBytes((4 * 3) + 16 + 4 + 8 + 20);
            _exports.Add(new PackageExport(name, string.Empty, offset, size));
        }

        for (var i = 0; i < _exports.Count; i++)
        {
            _exports[i] = _exports[i] with { ClassName = Resolve(classIndices[i]) };
        }
    }

    private string ReadName(UnrealReader r)
    {
        var index = r.ReadInt32();
        var number = r.ReadInt32();
        var name = index >= 0 && index < _names.Count ? _names[index] : $"#{index}";
        return number == 0 ? name : $"{name}_{number - 1}";
    }

    private string Resolve(int index) => index switch
    {
        < 0 when -index - 1 < _imports.Count => _imports[-index - 1],
        > 0 when index - 1 < _exports.Count => _exports[index - 1].Name,
        _ => "None",
    };

    private PropertyList ReadList(UnrealReader r)
    {
        var list = new PropertyList();
        while (!r.AtEnd)
        {
            var name = ReadName(r);
            if (name == PropertySerializer.Terminator)
            {
                break;
            }

            var type = ReadName(r);
            var size = r.ReadInt32();
            r.ReadInt32();
            string? extra = null;
            var boolValue = false;
            switch (type)
            {
                case "StructProperty":
                    extra = ReadName(r);
                    r.ReadBytes(16);
                    break;
                case "BoolProperty":
                    boolValue = r.ReadByte() != 0;
                    break;
                case "ByteProperty" or "EnumProperty" or "ArrayProperty" or "SetProperty":
                    extra = ReadName(r);
                    break;
                case "MapProperty":
                    extra = ReadName(r);
                    ReadName(r);
                    break;
            }

            if (r.ReadByte() != 0)
            {
                r.ReadBytes(16);
            }

            var value = r.Slice(size);
            list.Add(ReadValue(name, type, extra, boolValue, value));
        }

        return list;
    }

    private UProperty ReadValue(string name, string type, string? extra, bool boolValue, UnrealReader value)
    {
        var start = value.Fork();
        try
        {
            return type switch
            {
                "BoolProperty" => new BoolProperty(name, boolValue),
                "IntProperty" => new IntProperty(name, value.ReadInt32()),
                "Int64Property" => new Int64Property(name, value.ReadInt64()),
                "FloatProperty" => new FloatProperty(name, value.ReadSingle()),
                "StrProperty" => new StrProperty(name, value.ReadFString()),
                "NameProperty" => new NameProperty(name, ReadName(value)),
                "EnumProperty" => new EnumProperty(name, extra, ReadName(value)),
                "ByteProperty" when value.Remaining == 1 => new ByteProperty(name) { EnumType = extra, RawValue = value.ReadByte() },
                "ByteProperty" => new ByteProperty(name) { EnumType = extra, HoldsName = true, NameValue = ReadName(value) },
                "ObjectProperty" => new ObjectProperty(name, Resolve(value.ReadInt32())),
                "TextProperty" => new TextProperty(name, value.ReadBytes(value.Remaining)),
                "StructProperty" => new StructProperty(name, extra, ReadStruct(extra, value)),
                "ArrayProperty" when extra == "StructProperty" => new ArrayProperty(name, extra, ReadStructArray(value)),
                _ => new RawProperty(name, type, [], value.ReadBytes(value.Remaining)),
            };
        }
        catch (SaveFormatException)
        {
            // Natively serialized structs (e.g. a curve's RichCurveKey array) aren't tagged property lists: keep the
            // bytes so a caller that knows the layout can still read them.
            return new RawProperty(name, type, [], start.ReadBytes(start.Remaining));
        }
    }

    /// <summary>
    /// The keys of a <c>CurveFloat</c> export (<c>FloatCurve.Keys</c>, natively serialized <c>RichCurveKey</c>s) as
    /// (time, value) pairs; empty when the export has no readable keys.
    /// </summary>
    public IReadOnlyList<(float Time, float Value)> ReadCurveKeys(PackageExport export)
    {
        if ((ReadProperties(export).Find<StructProperty>("FloatCurve")?.Value as PropertyList)?.Find<RawProperty>("Keys")?.Value is not { Length: > 4 } raw)
        {
            return [];
        }

        try
        {
            // int32 count, then the inner struct tag (name, type, size, index, struct name, GUID, GUID flag), then the keys.
            var reader = new UnrealReader(raw);
            var count = reader.ReadInt32();
            ReadName(reader);
            ReadName(reader);
            var size = reader.ReadInt32();
            reader.ReadInt32();
            ReadName(reader);
            reader.ReadBytes(16);
            if (reader.ReadByte() != 0)
            {
                reader.ReadBytes(16);
            }

            // Each key: interp, tangent and tangent-weight modes (one byte each), then time, value and four tangent floats.
            const int KeySize = 3 + (6 * 4);
            if (count <= 0 || size != count * KeySize || reader.Remaining < size)
            {
                return [];
            }

            var keys = new List<(float, float)>(count);
            for (var i = 0; i < count; i++)
            {
                reader.ReadBytes(3);
                var time = reader.ReadSingle();
                var value = reader.ReadSingle();
                reader.ReadBytes(16);
                keys.Add((time, value));
            }

            return keys;
        }
        catch (SaveFormatException)
        {
            return [];
        }
    }

    private IStructValue ReadStruct(string? structType, UnrealReader value)
    {
        if (structType is "Guid" && value.Remaining == 16)
        {
            return new GuidValue(value.ReadGuid());
        }

        if (structType is "Vector" or "Vector2D" or "Vector4" or "Rotator" or "Quat" or "LinearColor" or "Color"
            or "IntPoint" or "DateTime" or "Timespan" or "Box" or "GameplayTagContainer" or "SoftObjectPath")
        {
            return new RawStructValue(value.ReadBytes(value.Remaining));
        }

        return ReadList(value);
    }

    private StructArrayStorage ReadStructArray(UnrealReader value)
    {
        var count = value.ReadInt32();
        var innerName = ReadName(value);
        ReadName(value);
        var innerSize = value.ReadInt32();
        value.ReadInt32();
        var structType = ReadName(value);
        value.ReadBytes(16);
        if (value.ReadByte() != 0)
        {
            value.ReadBytes(16);
        }

        var storage = new StructArrayStorage(innerName, structType);
        var elements = value.Slice(innerSize);
        for (var i = 0; i < count && !elements.AtEnd; i++)
        {
            storage.Elements.Add(structType == "Guid" ? new GuidValue(elements.ReadGuid()) : ReadList(elements));
        }

        return storage;
    }
}
