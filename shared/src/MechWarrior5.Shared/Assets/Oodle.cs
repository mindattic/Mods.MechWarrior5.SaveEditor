using System.Runtime.InteropServices;

namespace MechWarrior5.Shared.Assets;

/// <summary>
/// Oodle decompression through an <c>oo2core*.dll</c> already on the player's PC (an Unreal Engine install or another
/// game ships one). The editor never bundles or downloads it: Oodle is Epic's licensed library.
/// </summary>
public sealed class Oodle
{
    private readonly DecompressFn _decompress;

    private Oodle(string path, DecompressFn decompress)
    {
        Path = path;
        _decompress = decompress;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate long DecompressFn(
        byte[] compressed, long compressedSize, byte[] raw, long rawSize, int fuzzSafe, int checkCrc, int verbosity,
        IntPtr decodeBufferBase, long decodeBufferSize, IntPtr callback, IntPtr callbackData, IntPtr decoderMemory, long decoderMemorySize, int threadPhase);

    public string Path { get; }

    /// <summary>Loads the DLL; null (with <paramref name="error"/>) when it isn't a usable x64 Oodle core.</summary>
    public static Oodle? TryLoad(string path, out string? error)
    {
        error = null;
        if (!System.IO.Path.GetFileName(path).StartsWith("oo2core", StringComparison.OrdinalIgnoreCase)
            || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            error = "not an oo2core*.dll file";
            return null;
        }

        try
        {
            var library = NativeLibrary.Load(path);
            if (!NativeLibrary.TryGetExport(library, "OodleLZ_Decompress", out var export))
            {
                error = "the DLL has no OodleLZ_Decompress";
                return null;
            }

            return new Oodle(path, Marshal.GetDelegateForFunctionPointer<DecompressFn>(export));
        }
        catch (Exception e) when (e is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            error = e.Message;
            return null;
        }
    }

    public byte[] Decompress(byte[] compressed, int rawSize)
    {
        var raw = new byte[rawSize];
        var written = _decompress(compressed, compressed.Length, raw, rawSize, 1, 0, 0, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, 3);
        return written == rawSize ? raw : throw new InvalidDataException($"Oodle returned {written} bytes, expected {rawSize}");
    }
}
