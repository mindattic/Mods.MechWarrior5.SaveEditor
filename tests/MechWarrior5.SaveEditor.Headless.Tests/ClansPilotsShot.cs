using MechWarrior5.SaveEditor.App.Clans.ViewModels;
using ClansPage = MechWarrior5.SaveEditor.App.Clans.ViewModels.Page;

namespace MechWarrior5.SaveEditor.Headless.Tests;

public partial class UiHeadlessTests
{
    /// <summary>Renders the Clans Pilots page off-screen to MW5_SHOT_DIR (default: the test's work folder).</summary>
    [Test]
    [Explicit("Writes a PNG; run on purpose to look at the Pilots page.")]
    [Category("Screenshots")]
    public void Render_the_clans_pilots_page()
    {
        var folder = Environment.GetEnvironmentVariable("MW5_SHOT_DIR") is { Length: > 0 } dir ? dir : TestContext.CurrentContext.WorkDirectory;
        var (clansFolder, clansSave) = SaveFixtures.ClansFolderCopy();
        var (shell, window, _) = Launch(clansFolder);
        Size(window);
        shell.IsClansGame = true;
        shell.OpenPath(clansSave);
        shell.Clans.NavigateCommand.Execute(ClansPage.Pilots);
        var page = (PilotsViewModel)shell.Clans.CurrentView!;
        Assert.That(page.Pilots, Is.Not.Empty);
        Shot(window, folder, "clans-pilots");

        var card = page.Pilots[0];
        card.SkillToAdd = card.SkillChoices[0];
        card.AddSkillCommand.Execute(null);
        card.Skills[0].Rank = 5;
        Assert.That(shell.Clans.IsDirty, Is.True);
        Shot(window, folder, "clans-pilots-edited");
    }
}
