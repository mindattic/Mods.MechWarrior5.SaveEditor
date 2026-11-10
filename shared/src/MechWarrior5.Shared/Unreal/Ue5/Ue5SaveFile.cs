namespace MechWarrior5.Shared.Unreal;

/// <summary>
/// A MW5 Clans <c>.sav</c>: a <see cref="GzipChunkArchive"/> whose payload is
/// <c>int32 length</c>, an optional <c>GVAS</c> header (the <c>UserSaveMetaData</c> files have
/// one, campaign saves don't), then the save object as a <see cref="Ue5ObjectBlob"/>.
/// Loading verifies that writing it back reproduces the payload exactly.
/// </summary>
public sealed class Ue5SaveFile
{
    private Ue5SaveFile(byte[] gvasHeader, Ue5ObjectBlob root, byte[] trailing)
    {
        GvasHeader = gvasHeader;
        Root = root;
        Trailing = trailing;
    }

    /// <summary>The raw GVAS header (magic, versions, custom versions); empty when absent.</summary>
    public byte[] GvasHeader { get; }

    public Ue5ObjectBlob Root { get; }

    public PropertyList Properties => Root.Properties;

    /// <summary>Bytes after the length-prefixed body (zero padding in practice).</summary>
    public byte[] Trailing { get; }

    public static Ue5SaveFile Load(string path) => Parse(File.ReadAllBytes(path));

    public static Ue5SaveFile Parse(byte[] fileBytes)
    {
        var payload = GzipChunkArchive.Unpack(fileBytes);
        var reader = new UnrealReader(payload);
        var length = reader.ReadInt32();
        var body = reader.Slice(length);
        var header = body.Remaining >= 4 && payload.AsSpan(body.Position, 4).SequenceEqual("GVAS"u8)
            ? ReadGvasHeader(body)
            : [];
        var root = Ue5ObjectBlob.Read(body.ReadBytes(body.Remaining));
        var file = new Ue5SaveFile(header, root, reader.ReadBytes(reader.Remaining));

        if (!file.ToPayload().AsSpan().SequenceEqual(payload))
        {
            throw new SaveFormatException("This save does not round-trip through the reader; refusing to edit it", 0);
        }

        return file;
    }

    public byte[] ToPayload()
    {
        var blob = Root.ToArray();
        var writer = new UnrealWriter();
        writer.WriteInt32(GvasHeader.Length + blob.Length);
        writer.WriteBytes(GvasHeader);
        writer.WriteBytes(blob);
        writer.WriteBytes(Trailing);
        return writer.ToArray();
    }

    public byte[] ToFileBytes() => GzipChunkArchive.Pack(ToPayload());

    private static byte[] ReadGvasHeader(UnrealReader body)
    {
        var start = body.Fork();
        body.ReadBytes(4 + 4 + 4 + 4); // magic, save-game version, UE4 package version, UE5 package version
        body.ReadBytes(2 + 2 + 2 + 4); // engine major, minor, patch, changelist
        body.ReadFString(); // engine branch
        body.ReadInt32(); // custom version format
        var customVersions = body.ReadInt32();
        if (customVersions is < 0 or > 4096)
        {
            throw new SaveFormatException($"Implausible custom version count {customVersions}", body.Position);
        }

        body.ReadBytes(customVersions * 20);
        return start.ReadBytes(body.Position - start.Position);
    }
}
