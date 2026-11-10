using System.IO;
using System.Text.Json;

namespace Mech5.Shared.Wpf.Services;

/// <summary>
/// Settings as a plain <c>settings.json</c> beside the program; nothing else is written. A missing,
/// unreadable or read-only file just means defaults and settings that aren't remembered.
/// </summary>
public sealed class JsonSettings<T>
    where T : class, new()
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public JsonSettings(string? folder = null)
    {
        FilePath = Path.Combine(folder ?? AppContext.BaseDirectory, "settings.json");
        Current = Load();
    }

    public string FilePath { get; }

    public T Current { get; }

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

    private T Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<T>(File.ReadAllText(FilePath)) ?? new T()
                : new T();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new T();
        }
    }
}
