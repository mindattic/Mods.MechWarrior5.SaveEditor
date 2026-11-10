using System.IO;
using System.Text.Json;

namespace Mech5.Mercs.SaveEditor.App.Services;

public sealed class AppSettings
{
    public string? LastSavePath { get; set; }

    /// <summary>"Mercs" or "Clans".</summary>
    public string Theme { get; set; } = "Mercs";

    public bool DownloadMechImages { get; set; } = true;

    /// <summary>MW5 install folder; empty means detect it (Steam libraries, Epic, common folders).</summary>
    public string? GameFolder { get; set; }

    public bool OwnedDlcOnly { get; set; } = true;
}

/// <summary>Settings as a plain <c>settings.json</c> beside the program; nothing else is written.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public SettingsService(string? folder = null)
    {
        FilePath = Path.Combine(folder ?? AppContext.BaseDirectory, "settings.json");
        Current = Load();
    }

    public string FilePath { get; }

    public AppSettings Current { get; }

    /// <summary>Best effort: a read-only program folder just means settings aren't remembered.</summary>
    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private AppSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }
}
