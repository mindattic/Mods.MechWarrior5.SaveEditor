namespace MechWarrior5.Shared.Unreal;

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
        var classPath = HasClassPath(data) ? reader.ReadFString() : FString.Null;
        var marker = reader.ReadByte();
        if (marker != 0)
        {
            throw new SaveFormatException($"Object stream marker is {marker}, expected 0", reader.Position - 1);
        }

        var properties = Ue5PropertySerializer.ReadList(reader);
        return new Ue5ObjectBlob(classPath, properties, reader.ReadBytes(reader.Remaining));
    }

    /// <summary>
    /// The stream opens with the 0 marker (no class path) or a class path's FString length. A nonzero first byte can
    /// only be a length, but a zero one is also the low byte of a length that is a multiple of 256: then it's a class
    /// path only when one reads cleanly there, a "/..." path followed by the 0 marker.
    /// </summary>
    private static bool HasClassPath(byte[] data)
    {
        if (data.Length == 0)
        {
            return false;
        }

        if (data[0] != 0)
        {
            return true;
        }

        try
        {
            var probe = new UnrealReader(data);
            return probe.ReadFString().Value is { Length: > 0 } path && path[0] == '/' && !probe.AtEnd && probe.ReadByte() == 0;
        }
        catch (SaveFormatException)
        {
            return false;
        }
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
