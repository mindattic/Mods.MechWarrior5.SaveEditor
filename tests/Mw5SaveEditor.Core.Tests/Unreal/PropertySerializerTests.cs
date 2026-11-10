using Mw5SaveEditor.Core.Game;
using Mw5SaveEditor.Core.Unreal;
using Mw5SaveEditor.Core.Unreal.Properties;

namespace Mw5SaveEditor.Core.Tests.Unreal;

[TestFixture]
public class PropertySerializerTests
{
    private static PropertyList SampleList() =>
    [
        new IntProperty("Reputation", 2966),
        new Int64Property("StartingDateTicks", 951_252_408_000_000_000),
        new FloatProperty("Head", 18f),
        new BoolProperty("bIsNewGame", true),
        new StrProperty("CustomName", "Fire Hazard"),
        new NameProperty("HardpointSlotID", "Torso_Left_MH1_srm6"),
        Ue.Enum("HealthState", "EMWHealthState", "Healthy"),
        new ByteProperty("SkillCap") { RawValue = 10 },
        Ue.ItemId("ItemId", Guid.Parse("05767524-e59d-d344-83cd-5f04c8be0bbf")),
        Ue.AssetId("MechDataAssetId", "MechDataAssetId", "MWMechDataAsset", "AS7-D_MDA"),
        new ArrayProperty("ByteData", "ByteProperty", new ByteArrayStorage([1, 2, 3, 4])),
        Ue.StructArray("InstalledWeapons", "MechLoadoutWeapons",
        [
            [new NameProperty("HardpointSlotID", "Arm_Right_EH1"), Ue.Healthy()],
            [new NameProperty("HardpointSlotID", "Arm_Left_EH1"), Ue.Healthy()],
        ]),
        Ue.StructArray("PilotTraits", "PilotTraitAssetId", []),
    ];

    [Test]
    public void Write_then_read_reproduces_the_same_bytes()
    {
        var bytes = PropertySerializer.WriteList(SampleList());

        var reread = PropertySerializer.ReadList(bytes, out var trailing);

        Assert.Multiple(() =>
        {
            Assert.That(trailing, Is.Empty);
            Assert.That(PropertySerializer.WriteList(reread), Is.EqualTo(bytes));
        });
    }

    [Test]
    public void Typed_values_survive_a_round_trip()
    {
        var reread = PropertySerializer.ReadList(PropertySerializer.WriteList(SampleList()), out _);

        Assert.Multiple(() =>
        {
            Assert.That(reread.Get<IntProperty>("Reputation").Value, Is.EqualTo(2966));
            Assert.That(reread.Get<BoolProperty>("bIsNewGame").Value, Is.True);
            Assert.That(reread.Get<ByteProperty>("SkillCap").RawValue, Is.EqualTo(10));
            Assert.That(Ue.AssetName(reread.GetStruct("MechDataAssetId")), Is.EqualTo("AS7-D_MDA"));
            Assert.That(Ue.ItemGuid(reread.GetStruct("ItemId")), Is.EqualTo(Guid.Parse("05767524-e59d-d344-83cd-5f04c8be0bbf")));
            Assert.That(reread.Get<ArrayProperty>("InstalledWeapons").Structs.Elements, Has.Count.EqualTo(2));
            Assert.That(reread.Get<ArrayProperty>("PilotTraits").Structs.Elements, Is.Empty);
        });
    }

    [Test]
    public void Trailing_bytes_after_the_terminator_are_preserved()
    {
        var bytes = PropertySerializer.WriteList([new IntProperty("A", 1)], [0, 0, 0, 0]);

        var list = PropertySerializer.ReadList(bytes, out var trailing);

        Assert.That(PropertySerializer.WriteList(list, trailing), Is.EqualTo(bytes));
    }

    [Test]
    public void Unknown_property_types_are_kept_verbatim()
    {
        PropertyList list = [new RawProperty("OnChanged", "MulticastInlineDelegateProperty", [], [9, 8, 7])];
        var bytes = PropertySerializer.WriteList(list);

        var reread = PropertySerializer.ReadList(bytes, out _);

        Assert.Multiple(() =>
        {
            Assert.That(reread[0], Is.InstanceOf<RawProperty>());
            Assert.That(PropertySerializer.WriteList(reread), Is.EqualTo(bytes));
        });
    }

    [Test]
    public void Opaque_native_struct_is_kept_as_raw_bytes()
    {
        PropertyList list = [new StructProperty("Attributes", "GameplayTagContainer", new RawStructValue([0, 0, 0, 0]))];
        var bytes = PropertySerializer.WriteList(list);

        var reread = PropertySerializer.ReadList(bytes, out _);

        Assert.That(reread.Get<StructProperty>("Attributes").Value, Is.InstanceOf<RawStructValue>());
    }

    [Test]
    public void Text_property_display_string_can_be_rewritten()
    {
        var writer = new UnrealWriter();
        writer.WriteInt32(2);
        writer.WriteByte(0xFF);
        writer.WriteInt32(1);
        writer.WriteFString("Old Name");
        var text = new TextProperty("CompanyName", writer.ToArray());

        text.Data = FText.WithDisplayString(text.Data, "Kell Hounds");

        Assert.That(text.DisplayString, Is.EqualTo("Kell Hounds"));
    }

    [Test]
    public void Deep_clone_is_independent()
    {
        var original = SampleList();
        var clone = original.DeepClone();

        clone.Get<IntProperty>("Reputation").Value = 1;

        Assert.That(original.Get<IntProperty>("Reputation").Value, Is.EqualTo(2966));
    }
}
