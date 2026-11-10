namespace MechWarrior5.Mercs.SaveEditor.Core.Saves;

public sealed record MissingAsset(string AssetType, string AssetName, int References);

/// <summary>
/// The closest thing to loading a save in game without running it: every <c>PrimaryAssetId</c> the
/// save references (mechs, weapons, equipment, skins, traits, portraits, voices, ...) is checked against
/// the asset names in the installed game's and enabled mods' paks. Untouched game saves resolve fully
/// except for content from mods that aren't enabled.
/// </summary>
public static class SaveValidator
{
    public static IReadOnlyList<MissingAsset> FindMissing(Mw5SaveFile save, IReadOnlySet<string> assetNames)
    {
        var missing = new Dictionary<(string Type, string Name), int>();
        Walk(save.Models, assetNames, missing);
        return [.. missing.Select(m => new MissingAsset(m.Key.Type, m.Key.Name, m.Value)).OrderBy(m => m.AssetType).ThenBy(m => m.AssetName)];
    }

    /// <summary>References missing now that weren't missing in <paramref name="baseline"/>.</summary>
    public static IReadOnlyList<MissingAsset> Introduced(IReadOnlyList<MissingAsset> now, IReadOnlyList<MissingAsset> baseline)
    {
        var before = baseline.Select(m => (m.AssetType, m.AssetName)).ToHashSet();
        return [.. now.Where(m => !before.Contains((m.AssetType, m.AssetName)))];
    }

    private static void Walk(PropertyList list, IReadOnlySet<string> names, Dictionary<(string, string), int> missing)
    {
        foreach (var property in list)
        {
            switch (property)
            {
                case StructProperty { StructType.Value: "PrimaryAssetId", Value: PropertyList id }:
                    Check(id, names, missing);
                    break;
                case StructProperty { Value: PropertyList nested }:
                    Walk(nested, names, missing);
                    break;
                case ArrayProperty { Storage: StructArrayStorage array }:
                    foreach (var element in array.PropertyElements)
                    {
                        Walk(element, names, missing);
                    }

                    break;
                case ArrayProperty { Name: "ByteData", Storage: ByteArrayStorage bytes }:
                    try
                    {
                        Walk(PropertySerializer.ReadList(bytes.Data, out _), names, missing);
                    }
                    catch (SaveFormatException)
                    {
                    }

                    break;
                case ObjectProperty { Inline: { } inline }:
                    Walk(inline, names, missing);
                    break;
            }
        }
    }

    private static void Check(PropertyList id, IReadOnlySet<string> names, Dictionary<(string, string), int> missing)
    {
        var name = id.Find<NameProperty>("PrimaryAssetName")?.Value.Value;
        if (name is null or "None" || names.Contains(name))
        {
            return;
        }

        var type = (id.Find<StructProperty>("PrimaryAssetType")?.Value as PropertyList)?.Find<NameProperty>("Name")?.Value.Value ?? "?";
        missing[(type, name)] = missing.GetValueOrDefault((type, name)) + 1;
    }
}
