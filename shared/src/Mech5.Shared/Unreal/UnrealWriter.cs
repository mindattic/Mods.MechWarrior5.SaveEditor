using System.Buffers.Binary;
using System.Text;

namespace Mech5.Shared.Unreal;

/// <summary>Little-endian growable writer with back-patching for length prefixes.</summary>
public sealed class UnrealWriter
{
    private byte[] _buffer = new byte[4096];

    public int Position { get; private set; }

    public void WriteByte(byte value)
    {
        Ensure(1);
        _buffer[Position++] = value;
    }

    public void WriteInt32(int value)
    {
        Ensure(4);
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(Position), value);
        Position += 4;
    }

    public void WriteUInt32(uint value)
    {
        Ensure(4);
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(Position), value);
        Position += 4;
    }

    public void WriteInt64(long value)
    {
        Ensure(8);
        BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(Position), value);
        Position += 8;
    }

    public void WriteSingle(float value)
    {
        Ensure(4);
        BinaryPrimitives.WriteSingleLittleEndian(_buffer.AsSpan(Position), value);
        Position += 4;
    }

    public void WriteDouble(double value)
    {
        Ensure(8);
        BinaryPrimitives.WriteDoubleLittleEndian(_buffer.AsSpan(Position), value);
        Position += 8;
    }

    public void WriteGuid(Guid value)
    {
        Ensure(16);
        value.TryWriteBytes(_buffer.AsSpan(Position));
        Position += 16;
    }

    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        Ensure(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(Position));
        Position += bytes.Length;
    }

    public void WriteFString(FString value)
    {
        if (value.Value is null)
        {
            WriteInt32(0);
            return;
        }

        if (value.IsWide)
        {
            var bytes = Encoding.Unicode.GetBytes(value.Value);
            WriteInt32(-(bytes.Length / 2 + 1));
            WriteBytes(bytes);
            WriteByte(0);
            WriteByte(0);
            return;
        }

        var latin = Encoding.Latin1.GetBytes(value.Value);
        WriteInt32(latin.Length + 1);
        WriteBytes(latin);
        WriteByte(0);
    }

    /// <summary>Reserves an int32 slot and returns its offset for <see cref="PatchInt32"/>.</summary>
    public int ReserveInt32()
    {
        var at = Position;
        WriteInt32(0);
        return at;
    }

    public void PatchInt32(int at, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(at), value);

    public byte[] ToArray() => _buffer.AsSpan(0, Position).ToArray();

    private void Ensure(int count)
    {
        var needed = Position + count;
        if (needed <= _buffer.Length)
        {
            return;
        }

        var size = _buffer.Length;
        while (size < needed)
        {
            size *= 2;
        }

        Array.Resize(ref _buffer, size);
    }
}
