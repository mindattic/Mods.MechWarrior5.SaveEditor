using MechWarrior5.Shared.Unreal;

namespace MechWarrior5.Shared.Assets;

/// <summary>A decoded texture: 32-bit BGRA pixels, top row first, straight (not premultiplied) alpha.</summary>
public sealed record DecodedTexture(int Width, int Height, byte[] Bgra);

/// <summary>
/// Reads the largest mip of a cooked UE4 <c>Texture2D</c> whose pixels the editor can decode: DXT1 (BC1),
/// DXT5 (BC3) and uncompressed B8G8R8A8, stored inline or in the package's <c>.ubulk</c>. Anything else
/// (BC7, compressed bulk data, streaming-only mips) yields null rather than a wrong picture.
/// </summary>
public static class TextureDecoder
{
    private const int BulkPayloadAtEndOfFile = 0x01;
    private const int BulkUnused = 0x20;
    private const int BulkSeparateFile = 0x100;
    private const int BulkSize64 = 0x2000;
    private const int BulkCompressedZlib = 0x02;

    public static DecodedTexture? Decode(byte[] uasset, byte[] uexp, byte[]? ubulk)
    {
        CookedPackage package;
        try
        {
            package = CookedPackage.Parse(uasset, uexp);
        }
        catch (Exception e) when (e is SaveFormatException or ArgumentException or IndexOutOfRangeException)
        {
            return null;
        }

        if (package.FindExport("Texture2D") is not { } export)
        {
            return null;
        }

        try
        {
            return DecodeNative(package.NameAt, package.ReadNativeData(export), ubulk);
        }
        catch (Exception e) when (e is SaveFormatException or ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// A texture from a UE5 zen package (MechWarrior 5: Clans), its largest mip stored inline. The package's export data
    /// follows its header, so the platform data is found the same way: by its pixel-format FName.
    /// </summary>
    public static DecodedTexture? DecodeZen(byte[] package)
    {
        try
        {
            var names = ZenPackage.Names(package);
            return DecodeNative(i => i >= 0 && i < names.Count ? names[i] : null, package, ubulk: null);
        }
        catch (Exception e) when (e is SaveFormatException or ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            return null;
        }
    }

    private static DecodedTexture? DecodeNative(Func<int, string?> nameAt, byte[] data, byte[]? ubulk)
    {
        // The platform data starts at its pixel-format FName (PF_*); the strip flags and cooked flag before it vary.
        for (var at = 0; at + 8 <= data.Length; at++)
        {
            var format = nameAt(BitConverter.ToInt32(data, at));
            if (format is null || !format.StartsWith("PF_", StringComparison.Ordinal) || BitConverter.ToInt32(data, at + 4) != 0)
            {
                continue;
            }

            // UE4: offset to the next platform, then the sizes. UE5 adds a 16-byte placeholder before them.
            foreach (var placeholder in new[] { 0, 16 })
            {
                if (TryLayout(data, at + 8 + 8 + placeholder, format) is not { } layout)
                {
                    continue;
                }

                return placeholder == 0
                    ? ReadUe4Mips(new UnrealReader(data, layout.MipsAt), format, layout.MipCount, ubulk)
                    : ReadInlineMip(data, layout.MipsAt, format, layout.Width, layout.Height);
            }
        }

        return null;
    }

    /// <summary>The sizes, format string and mip count at <paramref name="at"/>, or null when they don't read as such.</summary>
    private static (int Width, int Height, int MipCount, int MipsAt)? TryLayout(byte[] data, int at, string format)
    {
        try
        {
            var r = new UnrealReader(data, at);
            var width = r.ReadInt32();
            var height = r.ReadInt32();
            r.ReadInt32(); // packed slices and flags
            var length = r.ReadInt32();
            if (width <= 0 || height <= 0 || length != format.Length + 1)
            {
                return null;
            }

            var r2 = new UnrealReader(data, at + 12);
            if (r2.ReadFString().Value?.TrimEnd('\0') != format)
            {
                return null;
            }

            r2.ReadInt32(); // first mip to serialize
            return (width, height, r2.ReadInt32(), r2.Position);
        }
        catch (Exception e) when (e is SaveFormatException or ArgumentException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private static DecodedTexture? ReadUe4Mips(UnrealReader r, string format, int mipCount, byte[]? ubulk)
    {
        for (var i = 0; i < mipCount; i++)
        {
            if (ReadMip(r, format, ubulk) is { } picture)
            {
                return picture;
            }
        }

        return null;
    }

    /// <summary>
    /// UE5's cooked mip: a short bulk-data header, the pixels inline, then the mip's SizeX, SizeY and SizeZ. The header's
    /// length is found by where the trailing sizes land after the pixels this format and size need.
    /// </summary>
    private static DecodedTexture? ReadInlineMip(byte[] data, int at, string format, int width, int height)
    {
        var size = format switch
        {
            "PF_DXT1" => Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 8,
            "PF_DXT5" => Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 16,
            "PF_B8G8R8A8" => width * height * 4,
            _ => 0,
        };
        if (size <= 0 || width <= 0 || height <= 0)
        {
            return null;
        }

        for (var header = 0; header <= 48; header += 4)
        {
            var end = at + header + size;
            if (end + 8 <= data.Length && BitConverter.ToInt32(data, end) == width && BitConverter.ToInt32(data, end + 4) == height)
            {
                var pixels = data.AsSpan(at + header, size).ToArray();
                var bgra = format switch
                {
                    "PF_DXT1" => BlockDecoder.Bc1(pixels, width, height),
                    "PF_DXT5" => BlockDecoder.Bc3(pixels, width, height),
                    _ => pixels,
                };
                return bgra is null ? null : new DecodedTexture(width, height, bgra);
            }
        }

        return null;
    }

    /// <summary>Mips come largest first; returns the first one that has decodable pixels.</summary>
    private static DecodedTexture? ReadMip(UnrealReader r, string format, byte[]? ubulk)
    {
        r.ReadInt32(); // bCooked
        var flags = r.ReadInt32();
        if ((flags & BulkSize64) != 0)
        {
            r.ReadInt64();
        }
        else
        {
            r.ReadInt32();
        }

        var size = (int)((flags & BulkSize64) != 0 ? r.ReadInt64() : r.ReadInt32());
        var offset = r.ReadInt64();
        byte[]? pixels = null;
        // A payload stored at the end of the file isn't inline: reading it here would knock every later field out of step.
        if ((flags & BulkSeparateFile) == 0 && (flags & BulkUnused) == 0 && (flags & BulkPayloadAtEndOfFile) == 0 && size > 0)
        {
            pixels = r.ReadBytes(size);
        }
        else if ((flags & BulkSeparateFile) != 0 && ubulk is not null && offset >= 0 && offset + size <= ubulk.Length && size > 0)
        {
            pixels = ubulk.AsSpan((int)offset, size).ToArray();
        }

        var width = r.ReadInt32();
        var height = r.ReadInt32();
        r.ReadInt32(); // SizeZ
        if (pixels is null || (flags & BulkCompressedZlib) != 0 || width <= 0 || height <= 0)
        {
            return null;
        }

        var bgra = format switch
        {
            "PF_DXT1" => BlockDecoder.Bc1(pixels, width, height),
            "PF_DXT5" => BlockDecoder.Bc3(pixels, width, height),
            "PF_B8G8R8A8" when pixels.Length >= width * height * 4 => pixels[..(width * height * 4)],
            _ => null,
        };
        return bgra is null ? null : new DecodedTexture(width, height, bgra);
    }
}
