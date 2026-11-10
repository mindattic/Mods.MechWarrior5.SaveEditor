namespace Mech5.Shared.Unreal;

/// <summary>
/// Decodes the two FText history forms MechWarrior 5 uses for player-visible names, <c>None</c>
/// (culture-invariant string) and <c>Base</c> (namespace, key, source string), and writes edits as
/// culture-invariant text: a Base text keeps its localization key, so the game would keep showing the
/// localized original instead of the edit.
/// </summary>
public static class FText
{
    private const byte HistoryNone = 0xFF;
    private const byte HistoryBase = 0x00;
    private const int CultureInvariantFlag = 2;

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

    /// <summary>A culture-invariant text showing <paramref name="text"/> (empty text when null or empty).</summary>
    public static byte[] Invariant(string? text)
    {
        var writer = new UnrealWriter();
        writer.WriteInt32(CultureInvariantFlag);
        writer.WriteByte(HistoryNone);
        if (string.IsNullOrEmpty(text))
        {
            writer.WriteInt32(0);
        }
        else
        {
            writer.WriteInt32(1);
            writer.WriteFString(FString.From(text));
        }

        return writer.ToArray();
    }

    /// <summary>Returns <paramref name="data"/> unchanged when it already shows <paramref name="text"/>, else an invariant text.</summary>
    public static byte[] WithDisplayString(byte[] data, string text) =>
        TryGetDisplayString(data) == text ? data : Invariant(text);
}
