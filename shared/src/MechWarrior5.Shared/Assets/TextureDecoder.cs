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
            return DecodeNative(package, package.ReadNativeData(export), ubulk);
        }
        catch (Exception e) when (e is SaveFormatException or ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            return null;
        }
    }

    private static DecodedTexture? DecodeNative(CookedPackage package, byte[] data, byte[]? ubulk)
    {
        // The platform data starts at its pixel-format FName (PF_*); the strip flags and cooked flag before it vary.
        for (var at = 0; at + 8 <= data.Length; at++)
        {
            var format = package.NameAt(BitConverter.ToInt32(data, at));
            if (format is null || !format.StartsWith("PF_", StringComparison.Ordinal) || BitConverter.ToInt32(data, at + 4) != 0)
            {
                continue;
            }

            var r = new UnrealReader(data, at + 8);
            r.ReadInt64(); // offset to the next platform's data
            r.ReadInt32(); // SizeX
            r.ReadInt32(); // SizeY
            r.ReadInt32(); // packed slices and flags
            r.ReadFString();
            r.ReadInt32(); // first mip to serialize
            var mipCount = r.ReadInt32();
            for (var i = 0; i < mipCount; i++)
            {
                if (ReadMip(r, format, ubulk) is { } picture)
                {
                    return picture;
                }
            }

            return null;
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
        if ((flags & BulkSeparateFile) == 0 && (flags & BulkUnused) == 0 && size > 0)
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
