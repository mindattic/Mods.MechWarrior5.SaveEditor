using System.Diagnostics;

namespace MechWarrior5.Shared.Wpf.Services;

/// <summary>
/// Links to a 'Mech's page on Sarna.net (the BattleTech wiki, the fullest technical reference). The editor only builds
/// the address; the player's own browser opens it, and only when they click. Nothing is downloaded.
/// </summary>
public static class SarnaLink
{
    private const string Host = "www.sarna.net";

    /// <summary>The page for a known Sarna title, else a Sarna search for the chassis and variant code.</summary>
    public static Uri For(string? sarnaTitle, string chassis, string? code = null) =>
        !string.IsNullOrWhiteSpace(sarnaTitle)
            ? new Uri($"https://{Host}/wiki/{Uri.EscapeDataString(sarnaTitle.Replace(' ', '_'))}")
            : new Uri($"https://{Host}/wiki/Special:Search?go=Go&search={Uri.EscapeDataString($"{chassis} {code}".Trim())}");

    /// <summary>Opens a Sarna.net address in the default browser; anything else is ignored.</summary>
    public static void Open(Uri? uri)
    {
        if (uri is { Scheme: "https", Host: Host })
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }
}
