namespace Mech5.Shared.Unreal;

/// <summary>
/// A UE5 object serialized into a byte array (MW5 Clans' <c>PersistentModelData</c>,
/// <c>InventoryData</c>, <c>DeploymentData</c> and each actor's <c>ByteData</c>):
/// an optional class path, a zero byte, then the object's properties.
/// </summary>
public sealed class Ue5ObjectBlob
{
    public Ue5ObjectBlob(FString classPath, PropertyList properties, byte[] trailing)
    {
        ClassPath = classPath;
        Properties = properties;
        Trailing = trailing;
    }

    /// <summary>e.g. <c>/Script/MechWarrior.ActorSaveDataArray</c>; null when the stream has none.</summary>
    public FString ClassPath { get; set; }

    public PropertyList Properties { get; }

    public byte[] Trailing { get; set; }

    public static Ue5ObjectBlob Read(byte[] data)
    {
        var reader = new UnrealReader(data);
        var classPath = data.Length > 0 && data[0] != 0 ? reader.ReadFString() : FString.Null;
        var marker = reader.ReadByte();
        if (marker != 0)
        {
            throw new SaveFormatException($"Object stream marker is {marker}, expected 0", reader.Position - 1);
        }

        var properties = Ue5PropertySerializer.ReadList(reader);
        return new Ue5ObjectBlob(classPath, properties, reader.ReadBytes(reader.Remaining));
    }

    public byte[] ToArray()
    {
        var writer = new UnrealWriter();
        if (!ClassPath.IsNull)
        {
            writer.WriteFString(ClassPath);
        }

        writer.WriteByte(0);
        Ue5PropertySerializer.WriteList(writer, Properties);
        writer.WriteBytes(Trailing);
        return writer.ToArray();
    }
}
