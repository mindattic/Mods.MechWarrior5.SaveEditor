using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Markup;

namespace Mech5.Mercs.SaveEditor.App.Localization;

public sealed record LanguageOption(string Code, string Name);

/// <summary>
/// UI strings by key, from embedded <c>Localization/&lt;code&gt;.json</c> files, with English as the
/// fallback for any key a translation lacks. Bindings go through the indexer, so switching
/// <see cref="Language"/> re-labels the whole UI at once.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static readonly IReadOnlyList<LanguageOption> Languages =
    [
        new("en", "English"),
        new("de", "Deutsch"),
        new("es", "Español"),
        new("fr", "Français"),
        new("ja", "日本語"),
        new("ru", "Русский"),
    ];

    private readonly Dictionary<string, string> _english = Load("en");
    private Dictionary<string, string> _current;
    private string _language = "en";

    private Loc()
    {
        _current = _english;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static Loc Instance { get; } = new();

    public string Language
    {
        get => _language;
        set
        {
            var code = Languages.Any(l => l.Code == value) ? value : "en";
            if (code == _language)
            {
                return;
            }

            _language = code;
            _current = code == "en" ? _english : Load(code);
            // Empty name = every property, indexer included; "Item[]" alone was missed on the first switch.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public string this[string key] =>
        _current.GetValueOrDefault(key) ?? _english.GetValueOrDefault(key) ?? key;

    /// <summary>The OS UI language when the editor has it, else English.</summary>
    public static string DefaultLanguage()
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Languages.Any(l => l.Code == code) ? code : "en";
    }

    /// <summary>A translated, formatted message: <paramref name="english"/> is used when no file has the key.</summary>
    public static string Text(string key, string english, params object[] args)
    {
        var template = Instance._current.GetValueOrDefault(key) ?? english;
        return args.Length == 0 ? template : string.Format(CultureInfo.CurrentCulture, template, args);
    }

    private static Dictionary<string, string> Load(string code)
    {
        using var stream = typeof(Loc).Assembly.GetManifestResourceStream($"Mech5.Mercs.SaveEditor.App.Localization.{code}.json");
        return stream is null
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    }
}

/// <summary><c>{l:T key}</c>: a live-updating binding to a UI string.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
