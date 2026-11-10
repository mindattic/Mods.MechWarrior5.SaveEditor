using Mw5SaveEditor.Core.Unreal;
using Mw5SaveEditor.Core.Unreal.Properties;

namespace Mw5SaveEditor.Core.Saves;

/// <summary>
/// An MW5 Mercenaries campaign save: a header-less property stream whose
/// <c>PersistentModelData</c> byte array embeds every game model (inventory, roster, ...).
/// </summary>
public sealed class Mw5SaveFile
{
    private const string ModelDataName = "PersistentModelData";
    private const string NotACampaignSave =
        "This file is not an MW5 Mercenaries campaign save. Campaign saves are the GUID-named .sav files inside a campaign folder.";

    private readonly byte[] _rootTrailing;
    private readonly byte[] _modelsTrailing;
    private readonly ArrayProperty _modelData;

    private Mw5SaveFile(PropertyList root, byte[] rootTrailing, ArrayProperty modelData, PropertyList models, byte[] modelsTrailing)
    {
        Root = root;
        _rootTrailing = rootTrailing;
        _modelData = modelData;
        Models = models;
        _modelsTrailing = modelsTrailing;
    }

    public PropertyList Root { get; }

    public PropertyList Models { get; }

    public string? ProjectVersion => Root.Find<StrProperty>("ProjectVersion")?.Value.Value;

    public static Mw5SaveFile Load(string path) => Parse(File.ReadAllBytes(path));

    /// <summary>
    /// Parses a save and proves the parse is lossless by re-serializing it; a save this editor
    /// cannot reproduce byte-for-byte is rejected rather than risked.
    /// </summary>
    public static Mw5SaveFile Parse(byte[] data)
    {
        PropertyList root;
        byte[] rootTrailing;
        try
        {
            root = PropertySerializer.ReadList(data, out rootTrailing);
        }
        catch (SaveFormatException e)
        {
            throw new SaveFormatException(NotACampaignSave, inner: e);
        }

        var modelData = root.Find<ArrayProperty>(ModelDataName)
            ?? throw new SaveFormatException(NotACampaignSave);
        var models = PropertySerializer.ReadList(modelData.Bytes.Data, out var modelsTrailing);

        var save = new Mw5SaveFile(root, rootTrailing, modelData, models, modelsTrailing);
        var roundTrip = save.Serialize();
        if (!roundTrip.AsSpan().SequenceEqual(data))
        {
            var mismatch = roundTrip.AsSpan().CommonPrefixLength(data);
            throw new SaveFormatException("The save uses a layout this editor cannot reproduce exactly; refusing to edit it.", mismatch);
        }

        return save;
    }

    public PropertyList GetModel(string name) =>
        TryGetModel(name) ?? throw new SaveFormatException($"The save has no {name}.");

    public PropertyList? TryGetModel(string name) => Models.Find<ObjectProperty>(name)?.Inline;

    public byte[] Serialize()
    {
        _modelData.Bytes.Data = PropertySerializer.WriteList(Models, _modelsTrailing);
        return PropertySerializer.WriteList(Root, _rootTrailing);
    }

    /// <summary>
    /// Writes atomically via a temp file; when <paramref name="backup"/> is set and the target
    /// exists, the previous file is kept as <c>&lt;name&gt;.bak</c>.
    /// </summary>
    public void Save(string path, bool backup = true)
    {
        var bytes = Serialize();
        Parse(bytes);

        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        if (File.Exists(path))
        {
            File.Replace(temp, path, backup ? path + ".bak" : null);
        }
        else
        {
            File.Move(temp, path);
        }
    }
}
