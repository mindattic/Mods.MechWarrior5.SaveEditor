namespace Mech5.Shared.Unreal.Properties;

public abstract class ArrayStorage
{
    public abstract ArrayStorage DeepClone();
}

/// <summary><c>TArray&lt;uint8&gt;</c>. MW5 uses these to embed whole serialized objects.</summary>
public sealed class ByteArrayStorage(byte[] data) : ArrayStorage
{
    public byte[] Data { get; set; } = data;

    public override ArrayStorage DeepClone() => new ByteArrayStorage((byte[])Data.Clone());
}

/// <summary>An array of structs, including the inner tag Unreal writes ahead of the elements.</summary>
public sealed class StructArrayStorage(FString innerName, FString structType) : ArrayStorage
{
    public FString InnerName { get; set; } = innerName;

    public FString StructType { get; set; } = structType;

    public Guid StructGuid { get; set; }

    public int InnerArrayIndex { get; set; }

    public Guid? InnerPropertyGuid { get; set; }

    public List<IStructValue> Elements { get; } = [];

    public IEnumerable<PropertyList> PropertyElements => Elements.OfType<PropertyList>();

    public override ArrayStorage DeepClone()
    {
        var clone = new StructArrayStorage(InnerName, StructType)
        {
            StructGuid = StructGuid,
            InnerArrayIndex = InnerArrayIndex,
            InnerPropertyGuid = InnerPropertyGuid,
        };
        clone.Elements.AddRange(Elements.Select(static e => e.DeepCloneValue()));
        return clone;
    }
}

/// <summary>Any other array, preserved verbatim (payload includes the element count).</summary>
public sealed class RawArrayStorage(byte[] payload) : ArrayStorage
{
    public byte[] Payload { get; set; } = payload;

    public override ArrayStorage DeepClone() => new RawArrayStorage((byte[])Payload.Clone());
}

public sealed class ArrayProperty(string name, FString innerType, ArrayStorage storage) : UProperty(name)
{
    public override string TypeName => "ArrayProperty";

    public FString InnerType { get; set; } = innerType;

    public ArrayStorage Storage { get; set; } = storage;

    public StructArrayStorage Structs => Storage as StructArrayStorage
        ?? throw new InvalidOperationException($"Array '{Name}' is not a parsed struct array.");

    public ByteArrayStorage Bytes => Storage as ByteArrayStorage
        ?? throw new InvalidOperationException($"Array '{Name}' is not a byte array.");

    protected override UProperty CloneCore() => new ArrayProperty(Name, InnerType, Storage.DeepClone());
}

/// <summary>Maps and sets are never edited; their payload is preserved verbatim.</summary>
public sealed class MapProperty(string name, FString keyType, FString valueType, byte[] payload) : UProperty(name)
{
    public override string TypeName => "MapProperty";

    public FString KeyType { get; set; } = keyType;

    public FString ValueType { get; set; } = valueType;

    public byte[] Payload { get; set; } = payload;

    protected override UProperty CloneCore() =>
        new MapProperty(Name, KeyType, ValueType, (byte[])Payload.Clone());
}

public sealed class SetProperty(string name, FString innerType, byte[] payload) : UProperty(name)
{
    public override string TypeName => "SetProperty";

    public FString InnerType { get; set; } = innerType;

    public byte[] Payload { get; set; } = payload;

    protected override UProperty CloneCore() => new SetProperty(Name, InnerType, (byte[])Payload.Clone());
}
