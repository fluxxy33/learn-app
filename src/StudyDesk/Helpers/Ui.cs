using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StudyDesk.Helpers;

/// <summary>Glyphes Segoe Fluent Icons utilisés dans l'interface.</summary>
public static class Icons
{
    public const string Home = "", Library = "", Education = "", Document = "", Page = "",
        Slideshow = "", Cards = "", Quiz = "", Stats = "", Phone = "", Settings = "",
        Add = "", Delete = "", Edit = "", Picture = "", Search = "", Bold = "",
        Italic = "", Underline = "", Bullets = "", Numbers = "", AlignLeft = "",
        AlignCenter = "", AlignRight = "", Undo = "", Redo = "", Font = "",
        FontColor = "", Highlight = "", Shapes = "▭", Pen = "", Eraser = "",
        Table = "", Globe = "", Play = "", Pause = "", Stop = "", Timer = "",
        Calendar = "", Clock = "", ChevronLeft = "", ChevronRight = "", ChevronDown = "",
        ChevronUp = "", Back = "", Person = "", People = "", Save = "", Folder = "",
        Copy = "", Paste = "", Star = "", StarFill = "", Check = "", Cancel = "",
        Sync = "", QrCode = "", Lightbulb = "", Import = "", Export = "", Share = "",
        More = "", Info = "", Help = "", Link = "", FullScreen = "", Up = "",
        Down = "", Rename = "", MoveTo = "", Dictionary = "", Generate = "",
        Checklist = "", Flag = "", Refresh = "", Remove = "", Header = "",
        Wifi = "", Color = "", TextBox = "", Brightness = "", Warning = "",
        History = "", Accept = "", Forward = "\uE72A", Download = "\uE896";
}

/// <summary>Petites fonctions pour construire l'interface en code.</summary>
public static class Ui
{
    public static Style Style(string key) => (Style)Application.Current.FindResource(key);

    public static TextBlock Text(string text, string style = "Body", double? size = null)
    {
        var t = new TextBlock { Text = text, Style = Style(style) };
        if (size != null) t.FontSize = size.Value;
        return t;
    }

    public static TextBlock Icon(string glyph, double size = 16, Brush? brush = null)
    {
        var t = new TextBlock { Text = glyph, Style = Style("Icon"), FontSize = size };
        if (brush != null) t.Foreground = brush;
        return t;
    }

    /// <summary>Contenu « icône + texte » pour un bouton.</summary>
    public static StackPanel IconText(string glyph, string text, double iconSize = 15)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock { Text = glyph, FontFamily = (FontFamily)Application.Current.FindResource("IconFont"), FontSize = iconSize, VerticalAlignment = VerticalAlignment.Center });
        if (!string.IsNullOrEmpty(text))
            sp.Children.Add(new TextBlock { Text = text, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return sp;
    }

    public static Button Button(string text, RoutedEventHandler onClick, string style = "SecondaryButton", string? glyph = null, string? tooltip = null)
    {
        var b = new Button { Style = Style(style), Content = glyph == null ? text : IconText(glyph, text), ToolTip = tooltip };
        b.Click += onClick;
        return b;
    }

    public static Button Tool(string glyph, string? text, string tooltip, RoutedEventHandler onClick)
    {
        var b = new Button { Style = Style("ToolButton"), Content = IconText(glyph, text ?? ""), ToolTip = tooltip };
        b.Click += onClick;
        return b;
    }

    public static ToggleButton ToolToggle(string glyph, string? text, string tooltip)
    {
        return new ToggleButton { Style = Style("ToolToggle"), Content = IconText(glyph, text ?? ""), ToolTip = tooltip };
    }

    public static Border Separator() => new() { Style = Style("Separator") };

    public static Border Card(UIElement child, Thickness? padding = null)
    {
        var b = new Border { Style = Style("Card"), Child = child };
        if (padding != null) b.Padding = padding.Value;
        return b;
    }

    public static SolidColorBrush BrushFromHex(string? hex, Brush? fallback = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex))
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                b.Freeze();
                return b;
            }
        }
        catch { /* couleur invalide */ }
        return fallback as SolidColorBrush ?? Brushes.Gray;
    }

    public static Color ColorFromHex(string? hex, Color fallback)
    {
        try { return string.IsNullOrWhiteSpace(hex) ? fallback : (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    public static string ToHex(Color c) => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Petit carré de couleur arrondi (pastille de matière).</summary>
    public static Border Dot(string hex, double size = 12) => new()
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size / 3), Background = BrushFromHex(hex), VerticalAlignment = VerticalAlignment.Center
    };

    public static string Plural(int n, string singular, string? plural = null) => $"{n} {(n > 1 ? plural ?? singular + "s" : singular)}";

    public static string RelativeDate(DateTime d)
    {
        var days = (DateTime.Today - d.Date).TotalDays;
        return days switch
        {
            0 => "aujourd'hui à " + d.ToString("HH:mm"),
            1 => "hier",
            < 7 => $"il y a {days} jours",
            _ => d.ToString("d MMM yyyy", new System.Globalization.CultureInfo("fr-FR"))
        };
    }
}

/// <summary>Chargement et conversion d'images.</summary>
public static class ImageHelper
{
    public static readonly string Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|Tous les fichiers|*.*";
    public static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff" };

    /// <summary>Charge une image sans verrouiller le fichier.</summary>
    public static BitmapImage? Load(string path, int decodeWidth = 0)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var bmp = new BitmapImage();
            using var fs = File.OpenRead(path);
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;
            bmp.StreamSource = fs;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public static BitmapImage? FromBytes(byte[] bytes)
    {
        try
        {
            var bmp = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public static byte[] ToPng(BitmapSource source)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Réduit une image trop grande (les photos de téléphone font souvent 4000 px).</summary>
    public static BitmapSource Downscale(BitmapSource src, int maxSide = 1920)
    {
        var max = Math.Max(src.PixelWidth, src.PixelHeight);
        if (max <= maxSide) return src;
        double s = maxSide / (double)max;
        var t = new TransformedBitmap(src, new ScaleTransform(s, s));
        t.Freeze();
        return t;
    }

    /// <summary>Prépare un fichier image pour l'enregistrer dans le profil : réduit et convertit en PNG ou JPEG.</summary>
    public static (byte[] Bytes, string Extension) Prepare(byte[] original, string extension)
    {
        var bmp = FromBytes(original);
        if (bmp == null) return (original, extension);
        if (Math.Max(bmp.PixelWidth, bmp.PixelHeight) <= 1920 && extension is ".png" or ".jpg" or ".jpeg") return (original, extension);
        var scaled = Downscale(bmp);
        if (extension is ".jpg" or ".jpeg")
        {
            var enc = new JpegBitmapEncoder { QualityLevel = 88 };
            enc.Frames.Add(BitmapFrame.Create(scaled));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return (ms.ToArray(), ".jpg");
        }
        return (ToPng(scaled), ".png");
    }

    /// <summary>Image du presse-papiers (capture d'écran) ou fichier image copié.</summary>
    public static (byte[] Bytes, string Extension)? FromClipboard()
    {
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                foreach (string? f in Clipboard.GetFileDropList())
                    if (f != null && Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        return Prepare(File.ReadAllBytes(f), Path.GetExtension(f).ToLowerInvariant());
            }
            if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } img)
                return (ToPng(Downscale(img)), ".png");
        }
        catch { /* presse-papiers occupé */ }
        return null;
    }

    public static string? FirstImageFile(IDataObject data)
    {
        if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] files)
            return files.FirstOrDefault(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        return null;
    }
}
