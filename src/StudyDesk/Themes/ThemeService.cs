using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using StudyDesk.Core.Models;

namespace StudyDesk.Themes;

/// <summary>
/// Couleurs de l'application (clair / sombre). Toutes les vues utilisent ces couleurs via DynamicResource,
/// et les contrôles standard utilisent le thème Fluent de Windows 11.
/// </summary>
public static class ThemeService
{
    public static bool IsDark { get; private set; }
    public static event Action? Changed;

    private static readonly Dictionary<string, (string Light, string Dark)> Tokens = new()
    {
        ["Bg"] = ("#EEF1F6", "#1C1F26"),
        ["Sidebar"] = ("#EEF1F6", "#1C1F26"),
        ["Content"] = ("#F9FAFC", "#22262E"),
        ["Surface"] = ("#FFFFFF", "#2A2F38"),
        ["Surface2"] = ("#EEF1F6", "#333944"),
        ["SurfaceHover"] = ("#E4E8F0", "#3A414D"),
        ["Line"] = ("#DFE3EA", "#3A414D"),
        ["Fg"] = ("#1B2230", "#E8ECF3"),
        ["FgMuted"] = ("#5F6878", "#A3ACBC"),
        ["Accent"] = ("#2F62D9", "#7EA2FF"),
        ["AccentFg"] = ("#FFFFFF", "#0C1226"),
        ["AccentSoft"] = ("#E3EBFC", "#2A3655"),
        ["Good"] = ("#1F8A52", "#5CCB93"),
        ["GoodSoft"] = ("#E1F3E8", "#1D3A2B"),
        ["Bad"] = ("#C43C3C", "#FF8585"),
        ["BadSoft"] = ("#FBE5E5", "#43242A"),
        ["Warn"] = ("#B8741A", "#F0B25A"),
        ["WarnSoft"] = ("#FBEFDC", "#3F3220"),
        ["PageBg"] = ("#E3E7EE", "#16181D"),
        ["Paper"] = ("#FFFFFF", "#FFFFFF"),
        ["PaperFg"] = ("#1B1B1B", "#1B1B1B"),
        ["Overlay"] = ("#66000000", "#99000000"),
    };

    public static void Apply(AppTheme theme)
    {
        IsDark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => SystemUsesDarkTheme()
        };
        var res = Application.Current.Resources;
        foreach (var (key, (light, dark)) in Tokens)
        {
            var color = (Color)ColorConverter.ConvertFromString(IsDark ? dark : light);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            res[key] = brush;
            res[key + "Color"] = color;
        }
        try
        {
            Application.Current.ThemeMode = IsDark ? ThemeMode.Dark : ThemeMode.Light;
        }
        catch
        {
            // Thème Fluent indisponible : les contrôles gardent le style classique.
        }
        Changed?.Invoke();
    }

    public static SolidColorBrush Brush(string key) =>
        Application.Current.Resources[key] as SolidColorBrush ?? Brushes.Gray;

    public static bool SystemUsesDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch
        {
            return false;
        }
    }
}
