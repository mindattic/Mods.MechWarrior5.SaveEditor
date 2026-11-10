namespace Mech5.Shared.Unreal.Properties;

/// <summary>A tagged Unreal property: name, type and value as stored in a property stream.</summary>
public abstract class UProperty
{
    protected UProperty(string name)
    {
        Name = name;
    }

    public string Name { get; set; }

    public abstract string TypeName { get; }

    public int ArrayIndex { get; set; }

    /// <summary>Optional per-property GUID (tag flag <c>HasPropertyGuid</c>); almost always absent.</summary>
    public Guid? PropertyGuid { get; set; }

    /// <summary>
    /// The UE 5.4+ tag (type tree, flags, extensions) when the property came from, or is meant for,
    /// a UE5 stream; <see langword="null"/> for UE4 streams.
    /// </summary>
    public Ue5Tag? Ue5 { get; set; }

    public UProperty DeepClone()
    {
        var clone = CloneCore();
        clone.Name = Name;
        clone.ArrayIndex = ArrayIndex;
        clone.PropertyGuid = PropertyGuid;
        clone.Ue5 = Ue5;
        return clone;
    }

    protected abstract UProperty CloneCore();

    public override string ToString() => $"{Name}: {TypeName}";
}
