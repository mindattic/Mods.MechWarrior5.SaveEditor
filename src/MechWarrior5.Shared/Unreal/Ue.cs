using MechWarrior5.Shared.Unreal.Properties;

namespace MechWarrior5.Shared.Unreal;

/// <summary>Builders for the Unreal struct shapes MechWarrior 5 writes, matching the game's layout field for field.</summary>
public static class Ue
{
    public static StructProperty Struct(string name, string structType, params UProperty[] fields) =>
        new(name, structType, new PropertyList(fields));

    public static StructProperty GuidStruct(string name, Guid value) => new(name, "Guid", new GuidValue(value));

    public static StructProperty ItemId(string name, Guid value) =>
        Struct(name, "ItemId", GuidStruct("Value", value));

    public static StructProperty PrimaryAssetId(string assetType, string assetName) =>
        Struct("ID", "PrimaryAssetId",
            Struct("PrimaryAssetType", "PrimaryAssetType", new NameProperty("Name", assetType)),
            new NameProperty("PrimaryAssetName", assetName));

    /// <summary>A typed asset id such as <c>MechDataAssetId { ID: PrimaryAssetId }</c>.</summary>
    public static StructProperty AssetId(string name, string structType, string assetType, string assetName) =>
        Struct(name, structType, PrimaryAssetId(assetType, assetName));

    public static EnumProperty Enum(string name, string enumType, string value) =>
        new(name, enumType, $"{enumType}::{value}");

    public static EnumProperty Healthy() => Enum("HealthState", "EMWHealthState", "Healthy");

    public static ArrayProperty StructArray(string name, string structType, IEnumerable<PropertyList> elements)
    {
        var storage = new StructArrayStorage(name, structType);
        storage.Elements.AddRange(elements);
        return new ArrayProperty(name, "StructProperty", storage);
    }

    /// <summary>Reads <c>X.ID.PrimaryAssetName</c> from a typed asset id struct (saves spell it ID, cooked assets Id).</summary>
    public static string? AssetName(PropertyList? assetIdStruct) =>
        IdOf(assetIdStruct)?.Find<NameProperty>("PrimaryAssetName")?.Value.Value;

    public static string? AssetType(PropertyList? assetIdStruct) =>
        IdOf(assetIdStruct)?.Find<StructProperty>("PrimaryAssetType")?.Value is PropertyList type
            ? type.Find<NameProperty>("Name")?.Value.Value
            : null;

    private static PropertyList? IdOf(PropertyList? assetIdStruct) =>
        (assetIdStruct?.Find<StructProperty>("ID") ?? assetIdStruct?.Find<StructProperty>("Id"))?.Value as PropertyList;

    public static Guid? ItemGuid(PropertyList? itemIdStruct) =>
        itemIdStruct?.Find<StructProperty>("Value")?.Value is GuidValue g ? g.Value : null;

    /// <summary>Strips the <c>EnumType::</c> prefix from a stored enum value.</summary>
    public static string EnumValue(FString value)
    {
        var text = value.ToString();
        var at = text.LastIndexOf("::", StringComparison.Ordinal);
        return at >= 0 ? text[(at + 2)..] : text;
    }
}
