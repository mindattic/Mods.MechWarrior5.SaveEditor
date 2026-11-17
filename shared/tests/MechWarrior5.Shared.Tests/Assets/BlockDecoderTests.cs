using MechWarrior5.Shared.Assets;

namespace MechWarrior5.Shared.Tests.Assets;

[TestFixture]
public class BlockDecoderTests
{
    private static readonly byte[] RedOverBlue = [0x00, 0xF8, 0x1F, 0x00];

    [Test]
    public void Bc1_four_color_block_decodes_its_endpoints()
    {
        // Index 0 (red) for the top two rows, index 1 (blue) for the bottom two.
        byte[] block = [.. RedOverBlue, 0x00, 0x00, 0x55, 0x55];
        var bgra = BlockDecoder.Bc1(block, 4, 4)!;

        Assert.Multiple(() =>
        {
            Assert.That(Pixel(bgra, 4, 0, 0), Is.EqualTo((0, 0, 255, 255)), "top-left is red");
            Assert.That(Pixel(bgra, 4, 3, 3), Is.EqualTo((255, 0, 0, 255)), "bottom-right is blue");
        });
    }

    [Test]
    public void Bc1_three_color_block_has_transparent_black()
    {
        // c0 <= c1 selects three-color mode; index 3 is transparent black.
        byte[] block = [0x1F, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF];
        var bgra = BlockDecoder.Bc1(block, 4, 4)!;

        Assert.That(Pixel(bgra, 4, 2, 1), Is.EqualTo((0, 0, 0, 0)));
    }

    [Test]
    public void Bc3_reads_its_alpha_block()
    {
        // Alpha endpoints 255 and 0; every texel uses index 1 (alpha 0) except the first, index 0 (alpha 255).
        byte[] alpha = [255, 0, 0b0100_1000, 0b1001_0010, 0b0010_0100, 0b0100_1001, 0b1001_0010, 0b0010_0100];
        byte[] color = [.. RedOverBlue, 0, 0, 0, 0];
        var bgra = BlockDecoder.Bc3([.. alpha, .. color], 4, 4)!;

        Assert.Multiple(() =>
        {
            Assert.That(Pixel(bgra, 4, 0, 0).A, Is.EqualTo(255));
            Assert.That(Pixel(bgra, 4, 1, 0).A, Is.EqualTo(0));
            Assert.That(Pixel(bgra, 4, 3, 3).A, Is.EqualTo(0));
            Assert.That(Pixel(bgra, 4, 0, 0).R, Is.EqualTo(255), "BC3 colors are always four-color");
        });
    }

    [Test]
    public void Sizes_that_are_not_a_multiple_of_four_are_cropped()
    {
        byte[] blocks = [.. Enumerable.Repeat<byte[]>([.. RedOverBlue, 0, 0, 0, 0], 4).SelectMany(b => b)];
        var bgra = BlockDecoder.Bc1(blocks, 6, 5)!;

        Assert.That(bgra, Has.Length.EqualTo(6 * 5 * 4));
    }

    [Test]
    public void Short_data_is_refused_rather_than_misread()
    {
        Assert.That(BlockDecoder.Bc3(new byte[15], 4, 4), Is.Null);
    }

    private static (int B, int G, int R, int A) Pixel(byte[] bgra, int width, int x, int y)
    {
        var at = ((y * width) + x) * 4;
        return (bgra[at], bgra[at + 1], bgra[at + 2], bgra[at + 3]);
    }
}
