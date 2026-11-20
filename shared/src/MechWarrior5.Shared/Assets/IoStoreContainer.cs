using System.Buffers.Binary;
using System.Text;
using MechWarrior5.Shared.Unreal;

namespace MechWarrior5.Shared.Assets;

/// <summary>
/// Read-only UE5 IoStore container (<c>.utoc</c> + <c>.ucas</c>): the directory index and chunk reads. Blocks stored
/// uncompressed are copied; compressed ones go through <see cref="Decompress"/> (Oodle in MechWarrior 5: Clans).
/// </summary>
public sealed class IoStoreContainer
{
    private const int BlockEntrySize = 12;
    private const byte FlagEncrypted = 2;
    private const byte FlagSigned = 4;

    private readonly byte[] _offsetLengths;
    private readonly byte[] _blocks;
    private readonly int _blockSize;
    private readonly string[] _methods;
    private readonly string _ucas;

    private IoStoreContainer(string ucas, byte[] offsetLengths, byte[] blocks, int blockSize, string[] methods, IReadOnlyDictionary<string, int> files)
    {
        _ucas = ucas;
        _offsetLengths = offsetLengths;
        _blocks = blocks;
        _blockSize = blockSize;
        _methods = methods;
        Files = files;
    }

    /// <summary>Every file path (mount point included) and its chunk index.</summary>
    public IReadOnlyDictionary<string, int> Files { get; }

    public static IoStoreContainer Open(string utoc)
    {
        var b = File.ReadAllBytes(utoc);
        if (b.Length < 100 || Encoding.ASCII.GetString(b, 0, 16) != "-==--==--==--==-")
        {
            throw new SaveFormatException($"{Path.GetFileName(utoc)} is not an IoStore table of contents");
        }

        int U32(int at) => (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
        var headerSize = U32(20);
        var entries = U32(24);
        var blockCount = U32(28);
        var blockEntrySize = U32(32);
        var methodCount = U32(36);
        var methodLength = U32(40);
        var blockSize = U32(44);
        var directorySize = U32(48);
        var flags = b[80];
        var seeds = U32(84);
        var withoutHash = U32(96);
        if ((flags & FlagEncrypted) != 0 || blockEntrySize != BlockEntrySize)
        {
            throw new SaveFormatException($"{Path.GetFileName(utoc)} is encrypted or uses an unknown block layout");
        }

        var at = headerSize;
        at += entries * 12; // chunk ids
        var offsetLengths = b[at..(at + (entries * 10))];
        at += (entries * 10) + (seeds * 4) + (withoutHash * 4);
        var blocks = b[at..(at + (blockCount * blockEntrySize))];
        at += blockCount * blockEntrySize;
        var methods = new string[methodCount];
        for (var i = 0; i < methodCount; i++)
        {
            methods[i] = Encoding.ASCII.GetString(b, at + (i * methodLength), methodLength).TrimEnd('\0');
        }

        at += methodCount * methodLength;
        if ((flags & FlagSigned) != 0)
        {
            var hashSize = U32(at);
            at += 4 + (hashSize * 2) + (blockCount * 20);
        }

        var files = ReadDirectory(b.AsSpan(at, directorySize).ToArray());
        return new IoStoreContainer(Path.ChangeExtension(utoc, ".ucas"), offsetLengths, blocks, blockSize, methods, files);
    }

    /// <summary>A chunk's bytes; <paramref name="decompress"/> gets (method, compressed bytes, uncompressed size).</summary>
    public byte[] Read(int chunk, Func<string, byte[], int, byte[]> decompress)
    {
        var offset = Be40(_offsetLengths, chunk * 10);
        var length = Be40(_offsetLengths, (chunk * 10) + 5);
        var first = offset / _blockSize;
        var last = (offset + length - 1) / _blockSize;
        var data = new MemoryStream((int)((last - first + 1) * _blockSize));
        using var file = new FileStream(_ucas, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
        for (var k = first; k <= last; k++)
        {
            var e = _blocks.AsSpan((int)k * BlockEntrySize, BlockEntrySize);
            var blockOffset = (long)e[0] | ((long)e[1] << 8) | ((long)e[2] << 16) | ((long)e[3] << 24) | ((long)e[4] << 32);
            var compressed = e[5] | (e[6] << 8) | (e[7] << 16);
            var uncompressed = e[8] | (e[9] << 8) | (e[10] << 16);
            var method = e[11];
            if (method > _methods.Length)
            {
                throw new SaveFormatException($"Block compression method {method} is past the container's {_methods.Length} declared method(s)", (int)k);
            }

            var raw = new byte[compressed];
            file.Seek(blockOffset, SeekOrigin.Begin);
            file.ReadExactly(raw);
            data.Write(method == 0 ? raw.AsSpan(0, uncompressed) : decompress(_methods[method - 1], raw, uncompressed));
        }

        return data.GetBuffer().AsSpan((int)(offset - (first * _blockSize)), (int)length).ToArray();
    }

    private static long Be40(byte[] b, int at) =>
        ((long)b[at] << 32) | ((long)b[at + 1] << 24) | ((long)b[at + 2] << 16) | ((long)b[at + 3] << 8) | b[at + 4];

    private static Dictionary<string, int> ReadDirectory(byte[] d)
    {
        var files = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (d.Length < 4)
        {
            return files;
        }

        var r = new UnrealReader(d);
        var mount = r.ReadFString().Value ?? string.Empty;
        var directories = new (uint Name, uint Child, uint Sibling, uint File)[r.ReadInt32()];
        for (var i = 0; i < directories.Length; i++)
        {
            directories[i] = (r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32());
        }

        var entries = new (uint Name, uint Next, uint Chunk)[r.ReadInt32()];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = (r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32());
        }

        var strings = new string[r.ReadInt32()];
        for (var i = 0; i < strings.Length; i++)
        {
            strings[i] = r.ReadFString().Value ?? string.Empty;
        }

        const uint None = uint.MaxValue;
        var stack = new Stack<(uint Index, string Prefix)>();
        if (directories.Length > 0)
        {
            stack.Push((0, string.Empty));
        }

        while (stack.Count > 0)
        {
            var (i, prefix) = stack.Pop();
            while (i != None)
            {
                var dir = directories[i];
                var here = prefix + (dir.Name != None ? strings[dir.Name] + "/" : string.Empty);
                for (var f = dir.File; f != None; f = entries[f].Next)
                {
                    files[mount + here + strings[entries[f].Name]] = (int)entries[f].Chunk;
                }

                if (dir.Child != None)
                {
                    stack.Push((dir.Child, here));
                }

                i = dir.Sibling;
            }
        }

        return files;
    }
}
