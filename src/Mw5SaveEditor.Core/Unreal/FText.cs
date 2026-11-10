namespace Mw5SaveEditor.Core.Unreal;

/// <summary>
/// Decodes and rewrites the two FText history forms MW5 uses for player-visible names:
/// <c>None</c> (culture-invariant string) and <c>Base</c> (namespace, key, source string).
/// </summary>
public static class FText
{
    private const byte HistoryNone = 0xFF;
    private const byte HistoryBase = 0x00;

    public static string? TryGetDisplayString(byte[] data)
    {
        try
        {
            var reader = new UnrealReader(data);
            reader.ReadInt32();
            var history = reader.ReadByte();
            switch (history)
            {
                case HistoryNone:
                    return reader.Remaining >= 4 && reader.ReadInt32() != 0 ? reader.ReadFString().Value : string.Empty;
                case HistoryBase:
                    reader.ReadFString();
                    reader.ReadFString();
                    return reader.ReadFString().Value;
                default:
                    return null;
            }
        }
        catch (SaveFormatException)
        {
            return null;
        }
    }

    /// <summary>Returns <paramref name="data"/> re-encoded to show <paramref name="text"/>.</summary>
    public static byte[] WithDisplayString(byte[] data, string text)
    {
        var reader = new UnrealReader(data);
        var flags = reader.ReadInt32();
        var history = reader.ReadByte();
        var writer = new UnrealWriter();
        writer.WriteInt32(flags);

        if (history == HistoryBase)
        {
            var ns = reader.ReadFString();
            var key = reader.ReadFString();
            writer.WriteByte(HistoryBase);
            writer.WriteFString(ns);
            writer.WriteFString(key);
            writer.WriteFString(FString.From(text));
            return writer.ToArray();
        }

        writer.WriteByte(HistoryNone);
        writer.WriteInt32(1);
        writer.WriteFString(FString.From(text));
        return writer.ToArray();
    }
}
