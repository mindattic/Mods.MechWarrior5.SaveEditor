namespace Mech5.SaveEditor.App.Services;

/// <summary>Everything the editor remembers, kept as a plain <c>settings.json</c> beside the program.</summary>
public sealed class AppSettings
{
    /// <summary>The game shown at startup: "Mercs" or "Clans".</summary>
    public string Game { get; set; } = "Mercs";

    public string? LastSavePath { get; set; }

    /// <summary>UI language code (en, de, es, fr, ja, ru); empty means the Windows language.</summary>
    public string? Language { get; set; }

    /// <summary>"Mercs" or "Clans".</summary>
    public string Theme { get; set; } = "Mercs";

    public bool DownloadMechImages { get; set; } = true;

    /// <summary>MW5 Mercenaries install folder; empty means detect it (Steam libraries, Epic, common folders).</summary>
    public string? GameFolder { get; set; }

    public bool OwnedDlcOnly { get; set; } = true;

    public string? ClansLastSavePath { get; set; }

    /// <summary>MW5 Clans save folder; empty means %LOCALAPPDATA%\MW5Clans\Saved\SaveGames.</summary>
    public string? ClansSaveFolder { get; set; }
}
