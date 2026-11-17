namespace MechWarrior5.Shared.Assets;

/// <summary>BC1 (DXT1) and BC3 (DXT5) block decompression to 32-bit BGRA.</summary>
public static class BlockDecoder
{
    public static byte[]? Bc1(byte[] blocks, int width, int height) => Decode(blocks, width, height, 8, alphaBlock: false);

    public static byte[]? Bc3(byte[] blocks, int width, int height) => Decode(blocks, width, height, 16, alphaBlock: true);

    private static byte[]? Decode(byte[] blocks, int width, int height, int blockSize, bool alphaBlock)
    {
        var across = (width + 3) / 4;
        var down = (height + 3) / 4;
        if (blocks.Length < (long)across * down * blockSize)
        {
            return null;
        }

        var output = new byte[width * height * 4];
        Span<byte> colors = stackalloc byte[16];
        Span<byte> alphas = stackalloc byte[8];
        for (var by = 0; by < down; by++)
        {
            for (var bx = 0; bx < across; bx++)
            {
                var at = ((by * across) + bx) * blockSize;
                ulong alphaBits = 0;
                if (alphaBlock)
                {
                    Alphas(blocks[at], blocks[at + 1], alphas);
                    for (var i = 0; i < 6; i++)
                    {
                        alphaBits |= (ulong)blocks[at + 2 + i] << (8 * i);
                    }

                    at += 8;
                }

                var c0 = (ushort)(blocks[at] | (blocks[at + 1] << 8));
                var c1 = (ushort)(blocks[at + 2] | (blocks[at + 3] << 8));
                var indices = BitConverter.ToUInt32(blocks, at + 4);
                Colors(c0, c1, fourColor: alphaBlock || c0 > c1, colors);

                for (var py = 0; py < 4; py++)
                {
                    var y = (by * 4) + py;
                    if (y >= height)
                    {
                        break;
                    }

                    for (var px = 0; px < 4; px++)
                    {
                        var x = (bx * 4) + px;
                        if (x >= width)
                        {
                            continue;
                        }

                        var texel = (py * 4) + px;
                        var index = (int)((indices >> (2 * texel)) & 3);
                        var o = ((y * width) + x) * 4;
                        output[o] = colors[(index * 4) + 0];
                        output[o + 1] = colors[(index * 4) + 1];
                        output[o + 2] = colors[(index * 4) + 2];
                        output[o + 3] = alphaBlock ? alphas[(int)((alphaBits >> (3 * texel)) & 7)] : colors[(index * 4) + 3];
                    }
                }
            }
        }

        return output;
    }

    /// <summary>The block's four BGRA colors; in three-color mode the fourth is transparent black.</summary>
    private static void Colors(ushort c0, ushort c1, bool fourColor, Span<byte> bgra)
    {
        Expand(c0, bgra[..4]);
        Expand(c1, bgra.Slice(4, 4));
        for (var ch = 0; ch < 3; ch++)
        {
            int a = bgra[ch], b = bgra[4 + ch];
            if (fourColor)
            {
                bgra[8 + ch] = (byte)(((2 * a) + b) / 3);
                bgra[12 + ch] = (byte)((a + (2 * b)) / 3);
            }
            else
            {
                bgra[8 + ch] = (byte)((a + b) / 2);
                bgra[12 + ch] = 0;
            }
        }

        bgra[11] = 255;
        bgra[15] = fourColor ? (byte)255 : (byte)0;
    }

    private static void Expand(ushort rgb565, Span<byte> bgra)
    {
        var r = (rgb565 >> 11) & 31;
        var g = (rgb565 >> 5) & 63;
        var b = rgb565 & 31;
        bgra[0] = (byte)((b << 3) | (b >> 2));
        bgra[1] = (byte)((g << 2) | (g >> 4));
        bgra[2] = (byte)((r << 3) | (r >> 2));
        bgra[3] = 255;
    }

    private static void Alphas(byte a0, byte a1, Span<byte> alphas)
    {
        alphas[0] = a0;
        alphas[1] = a1;
        if (a0 > a1)
        {
            for (var i = 1; i < 7; i++)
            {
                alphas[i + 1] = (byte)((((7 - i) * a0) + (i * a1)) / 7);
            }
        }
        else
        {
            for (var i = 1; i < 5; i++)
            {
                alphas[i + 1] = (byte)((((5 - i) * a0) + (i * a1)) / 5);
            }

            alphas[6] = 0;
            alphas[7] = 255;
        }
    }
}
