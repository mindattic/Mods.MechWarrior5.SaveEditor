using System.Buffers.Binary;
using System.Text;

namespace MechWarrior5.Shared.Assets;

/// <summary>A UE5 zen (IoStore) package's name map: enough to find the FNames its exports serialize.</summary>
public static class ZenPackage
{
    public static IReadOnlyList<string> Names(byte[] d)
    {
        var at = 4 + 4 + 8 + 4 + 4 + (4 * 7);
        if (BinaryPrimitives.ReadUInt32LittleEndian(d) != 0)
        {
            at += 4 + 8 + 4;
            at += 4 + (BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(at)) * 20);
        }

        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(at));
        at += 16 + (count * 8);
        var lengths = new short[count];
        for (var i = 0; i < count; i++)
        {
            lengths[i] = BinaryPrimitives.ReadInt16BigEndian(d.AsSpan(at + (i * 2)));
        }

        at += count * 2;
        var names = new List<string>(count);
        foreach (var length in lengths)
        {
            if (length < 0)
            {
                names.Add(Encoding.Unicode.GetString(d, at, -length * 2));
                at += -length * 2;
            }
            else
            {
                names.Add(Encoding.Latin1.GetString(d, at, length));
                at += length;
            }
        }

        return names;
    }
}
