using System.Globalization;
using System.Reflection;
using System.Text;

namespace MechWarrior5.Mercs.SaveEditor.Core.Saves;

/// <summary>
/// Marks a save as written by this editor: one extra top-level <c>StrProperty</c>. Unreal skips
/// tagged properties it doesn't recognize, so the game loads the save unchanged; a game re-save
/// drops the stamp, which correctly turns the file back into an original.
/// </summary>
public sealed record EditorStamp(string Tool, string Version, string? Source, DateTime WrittenUtc)
{
    public const string PropertyName = "Mech5SaveEditorStamp";
    public const string ToolName = "MechWarrior5.Mercs.SaveEditor";

    private static readonly byte[] NameBytes = Encoding.ASCII.GetBytes(PropertyName + "\0");

    public static EditorStamp? Read(PropertyList root)
    {
        if (root.Find<StrProperty>(PropertyName)?.Value.Value is not { } text)
        {
            return null;
        }

        // A source save name may itself contain ";tool=" or ";version=": the first of each key wins instead of throwing.
        var fields = text.Split(';')
            .Select(f => f.Split('=', 2))
            .Where(kv => kv.Length == 2)
            .GroupBy(kv => kv[0], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First()[1], StringComparer.Ordinal);
        return new EditorStamp(
            fields.GetValueOrDefault("tool", ToolName),
            fields.GetValueOrDefault("version", "?"),
            fields.GetValueOrDefault("source"),
            DateTime.TryParse(fields.GetValueOrDefault("written"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var when)
                ? when
                : DateTime.MinValue);
    }

    /// <summary>Adds or refreshes the stamp, keeping the original source save's name once set.</summary>
    public static void Apply(PropertyList root, string? sourceSave)
    {
        var source = Read(root)?.Source ?? sourceSave;
        var version = typeof(EditorStamp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? "2";
        var text = $"tool={ToolName};version={version};source={source};written={DateTime.UtcNow:O}";
        if (root.Find<StrProperty>(PropertyName) is { } existing)
        {
            existing.Value = text;
        }
        else
        {
            root.Add(new StrProperty(PropertyName, text));
        }
    }

    /// <summary>Cheap check without parsing: the stamp is the last property, so it sits near the end of the file.</summary>
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
