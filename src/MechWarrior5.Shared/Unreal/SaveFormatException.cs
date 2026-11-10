namespace MechWarrior5.Shared.Unreal;

public sealed class SaveFormatException : Exception
{
    public SaveFormatException(string message, long offset = -1, Exception? inner = null)
        : base(offset >= 0 ? $"{message} (at byte 0x{offset:X})" : message, inner)
    {
        Offset = offset;
    }

    public long Offset { get; }
}
