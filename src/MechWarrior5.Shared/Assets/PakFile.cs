using System.IO.Compression;
using System.Text;
using MechWarrior5.Shared.Unreal;

namespace MechWarrior5.Shared.Assets;

/// <summary>
/// Read-only UE4 <c>.pak</c> reader (versions 8 to 11, the full directory index): lists files and
/// reads uncompressed or zlib-compressed entries. Encrypted entries are reported, not read.
/// </summary>
public sealed class PakFile : IDisposable
{
    private const uint Magic = 0x5A6F12E1;

    private readonly FileStream _stream;
    private readonly byte[] _encodedEntries;
    private readonly string[] _compression;
    private readonly Dictionary<string, int> _entries = new(StringComparer.OrdinalIgnoreCase);

    private PakFile(string path, FileStream stream, byte[] encodedEntries, string[] compression)
    {
        Path = path;
        _stream = stream;
        _encodedEntries = encodedEntries;
        _compression = compression;
    }

    public string Path { get; }

    /// <summary>Every file in the pak, mount point included (e.g. <c>../../../MW5Mercs/Content/...</c>).</summary>
    public IReadOnlyCollection<string> Files => _entries.Keys;

    public static PakFile Open(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
        try
        {
            return Load(path, stream);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public bool Contains(string file) => _entries.ContainsKey(file);

    public byte[] Read(string file)
    {
        if (!_entries.TryGetValue(file, out var at))
        {
            throw new FileNotFoundException($"{file} is not in {System.IO.Path.GetFileName(Path)}");
        }

        var entry = Entry.Decode(_encodedEntries, at);
        if (entry.Encrypted)
        {
            throw new SaveFormatException($"{file} is encrypted");
        }

        // Each entry's data is preceded by a copy of its FPakEntry header.
        var header = ReadAt(entry.Offset, 53 + (entry.Method != 0 ? 4 + 16 * entry.BlockSizes.Length : 0) + 64);
        var reader = new UnrealReader(header);
        reader.ReadInt64();
        reader.ReadInt64();
        reader.ReadInt64();
        var method = reader.ReadUInt32();
        reader.ReadBytes(20);
        if (method != 0)
        {
            var blocks = reader.ReadUInt32();
            reader.ReadBytes((int)blocks * 16);
        }

        reader.ReadByte();
        reader.ReadUInt32();
        var dataAt = entry.Offset + reader.Position;

        if (entry.Method == 0)
        {
            return ReadAt(dataAt, (int)entry.UncompressedSize);
        }

        var name = _compression.ElementAtOrDefault(entry.Method - 1);
        if (!string.Equals(name, "Zlib", StringComparison.OrdinalIgnoreCase))
        {
            throw new SaveFormatException($"{file} uses unsupported compression '{name}'");
        }

        var output = new MemoryStream((int)entry.UncompressedSize);
        var position = dataAt;
        foreach (var size in entry.BlockSizes)
        {
            using var block = new ZLibStream(new MemoryStream(ReadAt(position, (int)size)), CompressionMode.Decompress);
            block.CopyTo(output);
            position += size;
        }

        return output.ToArray();
    }

    public void Dispose() => _stream.Dispose();

    private static PakFile Load(string path, FileStream stream)
    {
        // The footer sits in the last ~220 bytes; its exact size varies by version.
        var tailLength = (int)Math.Min(stream.Length, 256);
        stream.Seek(-tailLength, SeekOrigin.End);
        var tail = new byte[tailLength];
        stream.ReadExactly(tail);
        var magicAt = -1;
        for (var i = tailLength - 4; i >= 0; i--)
        {
            if (BitConverter.ToUInt32(tail, i) == Magic)
            {
                magicAt = i;
                break;
            }
        }

        if (magicAt < 0)
        {
            throw new SaveFormatException($"{System.IO.Path.GetFileName(path)} is not a UE4 pak");
        }

        var footer = new UnrealReader(tail, magicAt + 4);
        var version = footer.ReadInt32();
        var indexOffset = footer.ReadInt64();
        var indexSize = footer.ReadInt64();
        footer.ReadBytes(20);
        var compression = new string[5];
        if (version >= 8)
        {
            for (var i = 0; i < compression.Length && footer.Remaining >= 32; i++)
            {
                compression[i] = Encoding.ASCII.GetString(footer.ReadBytes(32)).TrimEnd('\0');
            }
        }

        if (version < 10)
        {
            throw new SaveFormatException($"pak version {version} is not supported (needs 10 or later)");
        }

        stream.Seek(indexOffset, SeekOrigin.Begin);
        var index = new byte[indexSize];
        stream.ReadExactly(index);
        var r = new UnrealReader(index);
        var mount = r.ReadFString().Value ?? string.Empty;
        r.ReadInt32();
        r.ReadInt64();
        if (r.ReadInt32() != 0)
        {
            r.ReadInt64();
            r.ReadInt64();
            r.ReadBytes(20);
        }

        if (r.ReadInt32() == 0)
        {
            throw new SaveFormatException("pak has no full directory index");
        }

        var directoryOffset = r.ReadInt64();
        var directorySize = r.ReadInt64();
        r.ReadBytes(20);
        var encoded = r.ReadBytes(r.ReadInt32());

        var pak = new PakFile(path, stream, encoded, compression);
        stream.Seek(directoryOffset, SeekOrigin.Begin);
        var directory = new byte[directorySize];
        stream.ReadExactly(directory);
        var d = new UnrealReader(directory);
        for (int dirs = d.ReadInt32(), i = 0; i < dirs; i++)
        {
            var folder = d.ReadFString().Value ?? string.Empty;
            for (int files = d.ReadInt32(), j = 0; j < files; j++)
            {
                var name = d.ReadFString().Value ?? string.Empty;
                pak._entries[mount + folder + name] = d.ReadInt32();
            }
        }

        return pak;
    }

    private byte[] ReadAt(long offset, int count)
    {
        var buffer = new byte[(int)Math.Min(count, Math.Max(0L, _stream.Length - offset))];
        _stream.Seek(offset, SeekOrigin.Begin);
        _stream.ReadExactly(buffer);
        return buffer;
    }

    /// <summary>The bit-packed FPakEntry of the encoded entries blob (pak v10+).</summary>
    private readonly record struct Entry(long Offset, long UncompressedSize, int Method, bool Encrypted, long[] BlockSizes)
    {
        public static Entry Decode(byte[] blob, int at)
        {
            var r = new UnrealReader(blob, at);
            var flags = r.ReadUInt32();
            long offset = (flags & (1u << 31)) != 0 ? r.ReadUInt32() : r.ReadInt64();
            long uncompressed = (flags & (1u << 30)) != 0 ? r.ReadUInt32() : r.ReadInt64();
            var method = (int)((flags >> 23) & 0x3F);
            var blocks = (int)((flags >> 6) & 0xFFFF);
            long[] sizes = [];
            if (method != 0)
            {
                long compressed = (flags & (1u << 29)) != 0 ? r.ReadUInt32() : r.ReadInt64();
                sizes = blocks == 1 ? [compressed] : [.. Enumerable.Range(0, blocks).Select(_ => (long)r.ReadUInt32())];
            }

            return new Entry(offset, uncompressed, method, (flags & (1u << 22)) != 0, sizes);
        }
    }
}
