using System.Globalization;
using System.Text;
using Mech5.Shared.Unreal;
using Mech5.Shared.Unreal.Properties;

namespace Mech5.Shared.Saves;

/// <summary>
/// Marks a save as written by a Mech5 save editor: one extra top-level <c>StrProperty</c>. Unreal
/// skips tagged properties it doesn't recognize, so the game loads the save unchanged; a game
/// re-save drops the stamp, which correctly turns the file back into an original. Editors only
/// ever overwrite stamped files; an unstamped (original) save is always written as a new file.
/// </summary>
public sealed record EditorStamp(string Tool, string Version, string? Source, DateTime WrittenUtc)
{
    public const string PropertyName = "Mech5SaveEditorStamp";

    private static readonly byte[] NameBytes = Encoding.ASCII.GetBytes(PropertyName + "\0");

    public static EditorStamp? Read(PropertyList root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root.Find<StrProperty>(PropertyName)?.Value.Value is not { } text)
        {
            return null;
        }

        var fields = text.Split(';')
            .Select(f => f.Split('=', 2))
            .Where(kv => kv.Length == 2)
            .ToDictionary(kv => kv[0], kv => kv[1], StringComparer.Ordinal);
        return new EditorStamp(
            fields.GetValueOrDefault("tool", "?"),
            fields.GetValueOrDefault("version", "?"),
            fields.GetValueOrDefault("source"),
            DateTime.TryParse(fields.GetValueOrDefault("written"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var when)
                ? when
                : DateTime.MinValue);
    }

    /// <summary>
    /// Adds or refreshes the stamp, keeping the original source save's name once set. For UE5
    /// streams pass <paramref name="ue5"/> so the new property gets a UE5 tag.
    /// </summary>
    public static void Apply(PropertyList root, string tool, string version, string? sourceSave, bool ue5 = false)
    {
        ArgumentNullException.ThrowIfNull(root);
        var source = Read(root)?.Source ?? sourceSave;
        var text = $"tool={tool};version={version};source={source};written={DateTime.UtcNow:O}";
        if (root.Find<StrProperty>(PropertyName) is { } existing)
        {
            existing.Value = text;
        }
        else
        {
            root.Add(new StrProperty(PropertyName, text) { Ue5 = ue5 ? Ue5Tag.Simple("StrProperty") : null });
        }
    }

    /// <summary>
    /// Cheap check for uncompressed saves: the stamp is the last property, so it sits near the end
    /// of the file. Compressed (Clans) saves must be parsed and checked with <see cref="Read"/>.
    /// </summary>
    public static bool FileHasStamp(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        using var stream = File.OpenRead(path);
        var length = (int)Math.Min(stream.Length, 4096);
        stream.Seek(-length, SeekOrigin.End);
        var tail = new byte[length];
        stream.ReadExactly(tail);
        return tail.AsSpan().IndexOf(NameBytes) >= 0;
    }
}
