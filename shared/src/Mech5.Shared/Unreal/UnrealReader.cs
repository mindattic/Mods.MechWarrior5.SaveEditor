using System.Buffers.Binary;
using System.Text;

namespace Mech5.Shared.Unreal;

/// <summary>Little-endian cursor over a bounded window of a byte buffer.</summary>
public sealed class UnrealReader
{
    private const int MaxStringLength = 1 << 24;

    private readonly byte[] _buffer;
    private readonly int _end;

    public UnrealReader(byte[] buffer, int start = 0, int? end = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;
        _end = end ?? buffer.Length;
        if (start < 0 || _end > buffer.Length || start > _end)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        Position = start;
    }

    public int Position { get; private set; }

    public int End => _end;

    public int Remaining => _end - Position;

    public bool AtEnd => Position >= _end;

    public byte ReadByte()
    {
        Require(1);
        return _buffer[Position++];
    }

    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));

    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));

    public float ReadSingle() => BinaryPrimitives.ReadSingleLittleEndian(Take(4));

    public double ReadDouble() => BinaryPrimitives.ReadDoubleLittleEndian(Take(8));

    public Guid ReadGuid() => new(Take(16));

    public byte[] ReadBytes(int count) => Take(count).ToArray();

    public FString ReadFString()
    {
        var start = Position;
        var length = ReadInt32();
        if (length == 0)
        {
            return FString.Null;
        }

        if (length is > MaxStringLength or < -MaxStringLength)
        {
            throw new SaveFormatException($"Implausible string length {length}", start);
        }

        if (length > 0)
        {
            var bytes = Take(length);
            if (bytes[^1] != 0)
            {
                throw new SaveFormatException("String is missing its terminator", start);
            }

            return new FString(Encoding.Latin1.GetString(bytes[..^1]), IsWide: false);
        }

        var wide = Take(-length * 2);
        if (wide[^1] != 0 || wide[^2] != 0)
        {
            throw new SaveFormatException("Wide string is missing its terminator", start);
        }

        return new FString(Encoding.Unicode.GetString(wide[..^2]), IsWide: true);
    }

    /// <summary>An independent reader at the same position, for retrying a parse from here.</summary>
    public UnrealReader Fork() => new(_buffer, Position, _end);

    /// <summary>Returns a reader over the next <paramref name="length"/> bytes and skips past them.</summary>
    public UnrealReader Slice(int length)
    {
        Require(length);
        var slice = new UnrealReader(_buffer, Position, Position + length);
        Position += length;
        return slice;
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        Require(count);
        var span = new ReadOnlySpan<byte>(_buffer, Position, count);
        Position += count;
        return span;
    }

    private void Require(int count)
    {
        if (count < 0 || Position + count > _end)
        {
            throw new SaveFormatException($"Unexpected end of data reading {count} byte(s)", Position);
        }
    }
}
