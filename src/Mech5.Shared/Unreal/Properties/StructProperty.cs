namespace Mech5.Shared.Unreal.Properties;

/// <summary>The payload of a struct: a nested property list, a GUID, or opaque native bytes.</summary>
public interface IStructValue
{
    IStructValue DeepCloneValue();
}

public sealed class GuidValue(Guid value) : IStructValue
{
    public Guid Value { get; set; } = value;

    public IStructValue DeepCloneValue() => new GuidValue(Value);
}

public sealed class RawStructValue(byte[] data) : IStructValue
{
    public byte[] Data { get; set; } = data;

    public IStructValue DeepCloneValue() => new RawStructValue((byte[])Data.Clone());
}

public sealed class StructProperty(string name, FString structType, IStructValue value) : UProperty(name)
{
    public override string TypeName => "StructProperty";

    public FString StructType { get; set; } = structType;

    public Guid StructGuid { get; set; }

    public IStructValue Value { get; set; } = value;

    /// <summary>The nested property list; throws when the struct is native/opaque.</summary>
    public PropertyList Properties => Value as PropertyList
        ?? throw new InvalidOperationException($"Struct '{Name}' ({StructType}) has no property list.");

    protected override UProperty CloneCore() =>
        new StructProperty(Name, StructType, Value.DeepCloneValue()) { StructGuid = StructGuid };
}
