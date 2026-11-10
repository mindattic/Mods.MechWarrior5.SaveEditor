using System.IO;
using System.Text.Json;

namespace Mw5SaveEditor.App.Services;

public sealed class AppSettings
{
    public string? LastSavePath { get; set; }

    public bool DownloadMechImages { get; set; } = true;

    public bool OwnedDlcOnly { get; set; } = true;
}

/// <summary>Per-user settings in <c>%APPDATA%\MindAttic\MW5SaveEditor\settings.json</c>.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public SettingsService(string folder)
    {
        Folder = folder;
        Directory.CreateDirectory(folder);
        Current = Load();
    }

    public string Folder { get; }

    public AppSettings Current { get; }

    private string FilePath => Path.Combine(Folder, "settings.json");

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, Json));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
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
