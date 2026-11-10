using Mw5SaveEditor.Core.Catalog;
using Mw5SaveEditor.Core.Game;
using Mw5SaveEditor.Core.Saves;

namespace Mw5SaveEditor.Core.Tests.Saves;

/// <summary>
/// Exercises real campaign saves. Point <c>MW5_SAVE_DIR</c> at a folder of copies (never your live
/// saves); every test works on a temp copy.
/// </summary>
[TestFixture]
[Explicit("Needs real MW5 saves via the MW5_SAVE_DIR environment variable")]
public class RealSaveTests
{
    private static IEnumerable<string> Saves()
    {
        var dir = Environment.GetEnvironmentVariable("MW5_SAVE_DIR");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.sav", SearchOption.AllDirectories)
                     .Where(f => Path.GetFileName(f).Length == 36))
        {
            yield return file;
        }
    }

    [TestCaseSource(nameof(Saves))]
    public void Save_round_trips_byte_for_byte(string path)
    {
        var original = File.ReadAllBytes(path);

        var save = Mw5SaveFile.Parse(original);

        Assert.That(save.Serialize(), Is.EqualTo(original));
    }

    [TestCaseSource(nameof(Saves))]
    public void Added_cold_storage_mechs_survive_save_and_reload(string path)
    {
        var work = Path.Combine(Path.GetTempPath(), "mw5-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var copy = Path.Combine(work, Path.GetFileName(path));
        File.Copy(path, copy);
        var catalog = MechCatalog.LoadBuiltIn();
        try
        {
            var doc = Mw5Document.Open(copy, catalog);
            var before = doc.Hangar.Mechs.Count(m => m.Location == MechLocation.ColdStorage);
            var picks = catalog.Variants.Where(v => v.Dlc is null).Take(3).ToList();

            var result = doc.Hangar.AddToColdStorage(picks);
            doc.Save();
            var reopened = Mw5Document.Open(copy, catalog);

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Has.Count.EqualTo(3));
                Assert.That(reopened.Hangar.Mechs.Count(m => m.Location == MechLocation.ColdStorage), Is.EqualTo(before + 3));
                Assert.That(File.Exists(copy + ".bak"), Is.True);
            });
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }
}
