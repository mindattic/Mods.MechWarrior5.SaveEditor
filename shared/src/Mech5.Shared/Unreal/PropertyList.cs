using System.Collections;
using Mech5.Shared.Unreal.Properties;

namespace Mech5.Shared.Unreal;

/// <summary>An ordered property stream (terminated by <c>None</c> on disk).</summary>
public sealed class PropertyList : IStructValue, IEnumerable<UProperty>
{
    private readonly List<UProperty> _items = [];

    public PropertyList()
    {
    }

    public PropertyList(IEnumerable<UProperty> properties)
    {
        _items.AddRange(properties);
    }

    public int Count => _items.Count;

    public UProperty this[int index] => _items[index];

    public UProperty? Find(string name) =>
        _items.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));

    public T? Find<T>(string name)
        where T : UProperty => Find(name) as T;

    public T Get<T>(string name)
        where T : UProperty =>
        Find(name) as T ?? throw new SaveFormatException($"Expected {typeof(T).Name} '{name}' was not found");

    /// <summary>Walks nested structs, e.g. <c>Get("Item", "ItemData", "MechDataAssetId")</c>.</summary>
    public PropertyList GetStruct(params string[] path)
    {
        var current = this;
        foreach (var segment in path)
        {
            current = current.Get<StructProperty>(segment).Properties;
        }

        return current;
    }

    public int IndexOf(string name) =>
        _items.FindIndex(p => string.Equals(p.Name, name, StringComparison.Ordinal));

    public void Add(UProperty property) => _items.Add(property);

    public void Insert(int index, UProperty property) => _items.Insert(index, property);

    public bool Remove(string name)
    {
        var index = IndexOf(name);
        if (index < 0)
        {
            return false;
        }

        _items.RemoveAt(index);
        return true;
    }

    /// <summary>Replaces the property with the same name in place, or appends it.</summary>
    public void Set(UProperty property)
    {
        var index = IndexOf(property.Name);
        if (index < 0)
        {
            _items.Add(property);
        }
        else
        {
            _items[index] = property;
        }
    }

    public PropertyList DeepClone() => new(_items.Select(static p => p.DeepClone()));

    public IStructValue DeepCloneValue() => DeepClone();

    public IEnumerator<UProperty> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
