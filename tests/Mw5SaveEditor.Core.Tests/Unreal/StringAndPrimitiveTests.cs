using Mw5SaveEditor.Core.Unreal;

namespace Mw5SaveEditor.Core.Tests.Unreal;

[TestFixture]
public class StringAndPrimitiveTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("GRF-1N_MDA")]
    [TestCase("Jägermech")]
    [TestCase("赤い竜")]
    public void FString_round_trips_every_encoding(string? text)
    {
        var writer = new UnrealWriter();
        writer.WriteFString(FString.From(text));

        var read = new UnrealReader(writer.ToArray()).ReadFString();

        Assert.That(read.Value, Is.EqualTo(text));
    }

    [Test]
    public void Null_and_empty_strings_encode_differently()
    {
        var nullWriter = new UnrealWriter();
        nullWriter.WriteFString(FString.Null);
        var emptyWriter = new UnrealWriter();
        emptyWriter.WriteFString("");

        Assert.That(nullWriter.ToArray(), Is.EqualTo(new byte[] { 0, 0, 0, 0 }));
        Assert.That(emptyWriter.ToArray(), Is.EqualTo(new byte[] { 1, 0, 0, 0, 0 }));
    }

    [Test]
    public void Wide_flag_is_kept_for_ascii_text_stored_as_utf16()
    {
        var writer = new UnrealWriter();
        writer.WriteFString(new FString("abc", IsWide: true));

        var read = new UnrealReader(writer.ToArray()).ReadFString();

        Assert.That(read, Is.EqualTo(new FString("abc", IsWide: true)));
    }

    [Test]
    public void Reading_past_the_end_reports_the_offset()
    {
        var reader = new UnrealReader([1, 2]);

        var error = Assert.Throws<SaveFormatException>(() => reader.ReadInt32());

        Assert.That(error!.Offset, Is.Zero);
    }

    [Test]
    public void Implausible_string_length_is_rejected()
    {
        var reader = new UnrealReader([0xFF, 0xFF, 0xFF, 0x7F]);

        Assert.Throws<SaveFormatException>(() => reader.ReadFString());
    }

    [Test]
    public void Patched_length_prefix_is_written_in_place()
    {
        var writer = new UnrealWriter();
        var at = writer.ReserveInt32();
        writer.WriteInt64(42);
        writer.PatchInt32(at, 8);

        var reader = new UnrealReader(writer.ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(reader.ReadInt32(), Is.EqualTo(8));
            Assert.That(reader.ReadInt64(), Is.EqualTo(42));
        });
    }
}
