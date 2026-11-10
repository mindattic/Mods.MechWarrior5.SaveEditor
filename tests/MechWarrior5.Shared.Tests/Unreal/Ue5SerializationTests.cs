using MechWarrior5.Shared.Saves;
using MechWarrior5.Shared.Unreal;
using MechWarrior5.Shared.Unreal.Properties;

namespace MechWarrior5.Shared.Tests.Unreal;

public class Ue5SerializationTests
{
    private static readonly PropertyTypeName CoreUObject = new("/Script/CoreUObject");

    [Test]
    public void Type_names_round_trip_with_nested_parameters()
    {
        var type = new PropertyTypeName("ArrayProperty", new PropertyTypeName("StructProperty", new PropertyTypeName("Guid", CoreUObject)));
        var writer = new UnrealWriter();
        type.Write(writer);

        var read = PropertyTypeName.Read(new UnrealReader(writer.ToArray()));

        Assert.That(read, Is.EqualTo(type));
        Assert.That(read.ToString(), Is.EqualTo("ArrayProperty(StructProperty(Guid(/Script/CoreUObject)))"));
        Assert.That(read.First!.StructName, Is.EqualTo("Guid"));
    }

    [Test]
    public void Property_list_round_trips_byte_for_byte()
    {
        var original = Serialize(SampleList());

        var list = Ue5PropertySerializer.ReadList(new UnrealReader(original));
        var again = Serialize(list);

        Assert.That(again, Is.EqualTo(original));
    }

    [Test]
    public void Bools_live_in_the_tag_flags()
    {
        var list = Ue5PropertySerializer.ReadList(new UnrealReader(Serialize(SampleList())));

        Assert.That(list.Get<BoolProperty>("bReady").Value, Is.True);
    }

    [Test]
    public void Edits_change_only_the_value_and_its_sizes()
    {
        var list = Ue5PropertySerializer.ReadList(new UnrealReader(Serialize(SampleList())));
        list.Get<IntProperty>("Quantity").Value = 1_000_000;
        list.Get<StructProperty>("ItemData").Properties.Get<StrProperty>("Label").Value = "a much longer label than before";

        var read = Ue5PropertySerializer.ReadList(new UnrealReader(Serialize(list)));

        Assert.That(read.Get<IntProperty>("Quantity").Value, Is.EqualTo(1_000_000));
        Assert.That(read.Get<StructProperty>("ItemData").Properties.Get<StrProperty>("Label").Value.Value, Is.EqualTo("a much longer label than before"));
    }

    [Test]
    public void Object_blobs_keep_their_class_and_marker()
    {
        var blob = new Ue5ObjectBlob("/Script/MechWarrior.ActorSaveDataArray", SampleList(), []);

        var read = Ue5ObjectBlob.Read(blob.ToArray());

        Assert.That(read.ClassPath.Value, Is.EqualTo("/Script/MechWarrior.ActorSaveDataArray"));
        Assert.That(read.ToArray(), Is.EqualTo(blob.ToArray()));
    }

    [Test]
    public void Gzip_archive_round_trips_multi_chunk_payloads()
    {
        var payload = new byte[GzipChunkArchive.ChunkSize * 2 + 1234];
        new Random(7).NextBytes(payload.AsSpan(0, 5000));

        var packed = GzipChunkArchive.Pack(payload);

        Assert.That(GzipChunkArchive.IsArchive(packed), Is.True);
        Assert.That(GzipChunkArchive.Unpack(packed), Is.EqualTo(payload));
    }

    [Test]
    public void Editor_stamp_gets_a_ue5_tag_and_reads_back()
    {
        var list = SampleList();
        EditorStamp.Apply(list, "Tests", "1.0", "SOURCE", ue5: true);

        var read = Ue5PropertySerializer.ReadList(new UnrealReader(Serialize(list)));
        var stamp = EditorStamp.Read(read);

        Assert.That(stamp, Is.Not.Null);
        Assert.That(stamp!.Tool, Is.EqualTo("Tests"));
        Assert.That(stamp.Source, Is.EqualTo("SOURCE"));
    }

    private static PropertyList SampleList()
    {
        var mechWarrior = new PropertyTypeName("/Script/MechWarrior");
        var itemData = new StructProperty("ItemData", "KelItemData", new PropertyList(
        [
            new StructProperty("InventoryId", "Guid", new GuidValue(Guid.NewGuid()))
            {
                Ue5 = new Ue5Tag(new PropertyTypeName("StructProperty", new PropertyTypeName("Guid", CoreUObject)), Ue5Tag.HasBinaryOrNativeSerialize),
            },
            new StrProperty("Label", "ER Medium Laser") { Ue5 = Ue5Tag.Simple("StrProperty") },
        ]))
        {
            Ue5 = new Ue5Tag(new PropertyTypeName("StructProperty", new PropertyTypeName("KelItemData", mechWarrior))),
        };

        return new PropertyList(
        [
            new IntProperty("Quantity", 12) { Ue5 = Ue5Tag.Simple("IntProperty") },
            new BoolProperty("bReady", true) { Ue5 = Ue5Tag.Simple("BoolProperty") },
            new EnumProperty("HealthState", "EMWHealthState", "EMWHealthState::Healthy")
            {
                Ue5 = new Ue5Tag(new PropertyTypeName("EnumProperty", new PropertyTypeName("EMWHealthState", mechWarrior), new PropertyTypeName("ByteProperty"))),
            },
            itemData,
        ]);
    }

    private static byte[] Serialize(PropertyList list)
    {
        var writer = new UnrealWriter();
        Ue5PropertySerializer.WriteList(writer, list);
        return writer.ToArray();
    }
}
