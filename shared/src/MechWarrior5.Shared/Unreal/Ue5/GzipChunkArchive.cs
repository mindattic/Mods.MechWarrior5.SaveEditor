using System.Buffers.Binary;
using System.IO.Compression;

namespace MechWarrior5.Shared.Unreal;

/// <summary>
/// The MW5 Clans save container: <c>!GZIP</c>, then Unreal compressed-archive blocks (package tag,
/// 128 KiB chunk size, compressor id, summary, per-chunk sizes, gzip data) until end of file.
/// </summary>
public static class GzipChunkArchive
{
    public const ulong PackageFileTag = 0x222222229E2A83C1;
    public const int ChunkSize = 0x20000;
    private const byte GzipCompressor = 4;
    private static ReadOnlySpan<byte> Magic => "!GZIP"u8;

    public static bool IsArchive(ReadOnlySpan<byte> data) => data.StartsWith(Magic);

    public static byte[] Unpack(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!IsArchive(data))
        {
            throw new SaveFormatException("Not a !GZIP save archive", 0);
        }

        using var output = new MemoryStream();
        var position = Magic.Length;
        while (position < data.Length)
        {
            var blockAt = position;
            var tag = ReadUInt64(data, ref position);
            var chunkSize = ReadUInt64(data, ref position);
            if (tag != PackageFileTag || chunkSize != ChunkSize)
            {
                throw new SaveFormatException($"Bad archive block header (tag {tag:X}, chunk {chunkSize})", blockAt);
            }

            position++; // compressor id
            ReadUInt64(data, ref position); // total compressed
            var uncompressed = ReadUInt64(data, ref position);
            // Each chunk has a 16-byte size entry, so a count the rest of the file can't hold is corruption.
            var chunkCount = (uncompressed + ChunkSize - 1) / ChunkSize;
            if (chunkCount > (ulong)(data.Length - position) / 16)
            {
                throw new SaveFormatException($"Bad archive block size ({uncompressed} bytes)", blockAt);
            }

            var chunks = (int)chunkCount;
            var sizes = new (long Compressed, long Uncompressed)[chunks];
            for (var i = 0; i < chunks; i++)
            {
                sizes[i] = ((long)ReadUInt64(data, ref position), (long)ReadUInt64(data, ref position));
            }

            foreach (var (compressed, expected) in sizes)
            {
                if (compressed < 0 || position + compressed > data.Length)
                {
                    throw new SaveFormatException("Archive chunk runs past end of file", position);
                }

                var before = output.Length;
                try
                {
                    using var gzip = new GZipStream(new MemoryStream(data, position, (int)compressed), CompressionMode.Decompress);
                    gzip.CopyTo(output);
                }
                catch (InvalidDataException e)
                {
                    throw new SaveFormatException($"Corrupt archive chunk: {e.Message}", position);
                }

                if (output.Length - before != expected)
                {
                    throw new SaveFormatException($"Chunk inflated to {output.Length - before} bytes, expected {expected}", position);
                }

                position += (int)compressed;
            }
        }

        return output.ToArray();
    }

    public static byte[] Pack(ReadOnlySpan<byte> payload)
    {
        using var output = new MemoryStream();
        output.Write(Magic);
        Span<byte> header = stackalloc byte[49];
        for (var offset = 0; offset < payload.Length || offset == 0; offset += ChunkSize)
        {
            var part = payload.Slice(offset, Math.Min(ChunkSize, payload.Length - offset));
            var compressed = Compress(part);
            BinaryPrimitives.WriteUInt64LittleEndian(header, PackageFileTag);
            BinaryPrimitives.WriteUInt64LittleEndian(header[8..], ChunkSize);
            header[16] = GzipCompressor;
            BinaryPrimitives.WriteUInt64LittleEndian(header[17..], (ulong)compressed.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(header[25..], (ulong)part.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(header[33..], (ulong)compressed.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(header[41..], (ulong)part.Length);
            output.Write(header);
            output.Write(compressed);
            if (payload.Length == 0)
            {
                break;
            }
        }

        return output.ToArray();
    }

    private static byte[] Compress(ReadOnlySpan<byte> part)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(part);
        }

        return buffer.ToArray();
    }

    private static ulong ReadUInt64(byte[] data, ref int position)
    {
        if (position + 8 > data.Length)
        {
            throw new SaveFormatException("Unexpected end of archive", position);
        }

        var value = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(position, 8));
        position += 8;
        return value;
    }
}
