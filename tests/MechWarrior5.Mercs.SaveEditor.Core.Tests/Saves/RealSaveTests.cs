using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;

namespace MechWarrior5.Mercs.SaveEditor.Core.Tests.Saves;

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
    public void Editing_an_original_writes_a_new_stamped_save_and_leaves_the_original_untouched(string path)
    {
        var work = Path.Combine(Path.GetTempPath(), "mw5-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var copy = Path.Combine(work, Path.GetFileName(path));
        File.Copy(path, copy);
        var original = File.ReadAllBytes(copy);
        var catalog = MechCatalog.LoadBuiltIn();
        try
        {
            var doc = Mw5Document.Open(copy, catalog);
            var before = doc.Hangar.Mechs.Count(m => m.Location == MechLocation.ColdStorage);
            var picks = catalog.Variants.Where(v => v.Dlc is null).Take(3).ToList();

            var result = doc.Hangar.AddToColdStorage(picks);
            var outcome = doc.Save(displayName: "Test Edit");
            var reopened = Mw5Document.Open(outcome.Path, catalog);

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Has.Count.EqualTo(3));
                Assert.That(outcome.CreatedNewFile, Is.True);
                Assert.That(outcome.Path, Is.Not.EqualTo(copy));
                Assert.That(File.ReadAllBytes(copy), Is.EqualTo(original));
                Assert.That(reopened.IsOriginalGameSave, Is.False);
                Assert.That(reopened.Hangar.Mechs.Count(m => m.Location == MechLocation.ColdStorage), Is.EqualTo(before + 3));
            });
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [TestCaseSource(nameof(Saves))]
    public void Saving_over_an_original_game_save_is_refused(string path)
    {
        var work = Path.Combine(Path.GetTempPath(), "mw5-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var copy = Path.Combine(work, Path.GetFileName(path));
        File.Copy(path, copy);
        try
        {
            var doc = Mw5Document.Open(copy, MechCatalog.LoadBuiltIn());

            Assert.Throws<SaveFormatException>(() => doc.Save(copy));
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }
}
