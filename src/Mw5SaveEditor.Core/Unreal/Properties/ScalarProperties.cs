namespace Mw5SaveEditor.Core.Unreal.Properties;

public sealed class IntProperty(string name, int value = 0) : UProperty(name)
{
    public override string TypeName => "IntProperty";

    public int Value { get; set; } = value;

    protected override UProperty CloneCore() => new IntProperty(Name, Value);
}

public sealed class UInt32Property(string name, uint value = 0) : UProperty(name)
{
    public override string TypeName => "UInt32Property";

    public uint Value { get; set; } = value;

    protected override UProperty CloneCore() => new UInt32Property(Name, Value);
}

public sealed class Int64Property(string name, long value = 0) : UProperty(name)
{
    public override string TypeName => "Int64Property";

    public long Value { get; set; } = value;

    protected override UProperty CloneCore() => new Int64Property(Name, Value);
}

public sealed class FloatProperty(string name, float value = 0) : UProperty(name)
{
    public override string TypeName => "FloatProperty";

    public float Value { get; set; } = value;

    protected override UProperty CloneCore() => new FloatProperty(Name, Value);
}

public sealed class DoubleProperty(string name, double value = 0) : UProperty(name)
{
    public override string TypeName => "DoubleProperty";

    public double Value { get; set; } = value;

    protected override UProperty CloneCore() => new DoubleProperty(Name, Value);
}

/// <summary>Bool properties keep their value in the tag; the value payload is empty.</summary>
public sealed class BoolProperty(string name, bool value = false) : UProperty(name)
{
    public override string TypeName => "BoolProperty";

    public bool Value { get; set; } = value;

    protected override UProperty CloneCore() => new BoolProperty(Name, Value);
}

public sealed class StrProperty(string name, FString value = default) : UProperty(name)
{
    public override string TypeName => "StrProperty";

    public FString Value { get; set; } = value;

    protected override UProperty CloneCore() => new StrProperty(Name, Value);
}

public sealed class NameProperty(string name, FString value = default) : UProperty(name)
{
    public override string TypeName => "NameProperty";

    public FString Value { get; set; } = value;

    protected override UProperty CloneCore() => new NameProperty(Name, Value);
}

public sealed class EnumProperty(string name, FString enumType = default, FString value = default) : UProperty(name)
{
    public override string TypeName => "EnumProperty";

    public FString EnumType { get; set; } = enumType;

    /// <summary>Fully qualified value, e.g. <c>EMWHealthState::Healthy</c>.</summary>
    public FString Value { get; set; } = value;

    protected override UProperty CloneCore() => new EnumProperty(Name, EnumType, Value);
}

/// <summary>
/// A byte property is either a raw byte (enum type <c>None</c>) or an enum stored by name.
/// </summary>
public sealed class ByteProperty(string name) : UProperty(name)
{
    public override string TypeName => "ByteProperty";

    public FString EnumType { get; set; } = "None";

    public bool HoldsName { get; set; }

    public byte RawValue { get; set; }

    public FString NameValue { get; set; }

    protected override UProperty CloneCore() => new ByteProperty(Name)
    {
        EnumType = EnumType,
        HoldsName = HoldsName,
        RawValue = RawValue,
        NameValue = NameValue,
    };
}
