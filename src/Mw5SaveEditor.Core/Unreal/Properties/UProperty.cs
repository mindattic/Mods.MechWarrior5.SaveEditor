namespace Mw5SaveEditor.Core.Unreal.Properties;

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

    public UProperty DeepClone()
    {
        var clone = CloneCore();
        clone.Name = Name;
        clone.ArrayIndex = ArrayIndex;
        clone.PropertyGuid = PropertyGuid;
        return clone;
    }

    protected abstract UProperty CloneCore();

    public override string ToString() => $"{Name}: {TypeName}";
}
