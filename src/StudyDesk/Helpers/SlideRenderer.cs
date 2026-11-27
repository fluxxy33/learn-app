using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Shapes;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Services;
using Path = System.Windows.Shapes.Path;

namespace StudyDesk.Helpers;

/// <summary>Dessine une diapositive (1280 × 720) avec son thème.</summary>
public static class SlideRenderer
{
    public const double W = SlideshowFactory.Width;
    public const double H = SlideshowFactory.Height;

    public static Brush Background(Slide slide, SlideTheme theme)
    {
        var c1 = Ui.ColorFromHex(slide.BackgroundHex ?? theme.Background, Colors.White);
        if (slide.BackgroundHex == null && theme.Background2 != null)
        {
            var c2 = Ui.ColorFromHex(theme.Background2, c1);
            var g = new LinearGradientBrush(c1, c2, new Point(0, 0), new Point(1, 1));
            g.Freeze();
            return g;
        }
        var b = new SolidColorBrush(c1);
        b.Freeze();
        return b;
    }

    /// <summary>Crée la diapositive complète (fond, éléments, dessin) dans un Canvas de 1280 × 720.</summary>
    public static Canvas Render(Slide slide, SlideTheme theme, bool includeInk = true)
    {
        var canvas = new Canvas { Width = W, Height = H, Background = Background(slide, theme), ClipToBounds = true };
        foreach (var e in slide.Elements)
        {
            var visual = ElementVisual(e, theme);
            visual.Width = Math.Max(1, e.Width);
            visual.Height = Math.Max(1, e.Height);
            Canvas.SetLeft(visual, e.X);
            Canvas.SetTop(visual, e.Y);
            if (e.Rotation != 0)
            {
                visual.RenderTransformOrigin = new Point(0.5, 0.5);
                visual.RenderTransform = new RotateTransform(e.Rotation);
            }
            canvas.Children.Add(visual);
        }
        if (includeInk && LoadInk(slide) is { Count: > 0 } strokes)
        {
            var ink = new InkPresenter { Strokes = strokes, Width = W, Height = H, IsHitTestVisible = false };
            canvas.Children.Add(ink);
        }
        return canvas;
    }

    public static FrameworkElement ElementVisual(SlideElement e, SlideTheme theme)
    {
        switch (e.Type)
        {
            case SlideElementType.Text:
                return TextVisual(e, theme);
            case SlideElementType.Image:
                var grid = new Grid();
                var src = e.ImageFile == null ? null : ImageHelper.Load(AppState.Store.MediaPath(e.ImageFile), 1600);
                if (src != null) grid.Children.Add(new Image { Source = src, Stretch = Stretch.Uniform });
                else grid.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), Child = new TextBlock { Text = "Image introuvable", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
                if (!string.IsNullOrWhiteSpace(e.Caption))
                {
                    var cap = new TextBlock
                    {
                        Text = e.Caption, FontSize = 13, Foreground = Ui.BrushFromHex(theme.TextColor), Opacity = 0.8, TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, -20)
                    };
                    grid.Children.Add(cap);
                }
                return grid;
            default:
                return ShapeVisual(e, theme);
        }
    }

    private static FrameworkElement TextVisual(SlideElement e, SlideTheme theme)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = Math.Max(6, e.FontSize),
            FontFamily = new FontFamily(e.IsTitle ? theme.TitleFont : theme.TextFont),
            FontWeight = e.Bold ? FontWeights.Bold : e.IsTitle ? FontWeights.SemiBold : FontWeights.Normal,
            FontStyle = e.Italic ? FontStyles.Italic : FontStyles.Normal,
            Foreground = Ui.BrushFromHex(e.ColorHex ?? (e.IsTitle ? theme.TitleColor : theme.TextColor)),
            TextAlignment = e.Align switch { TextAlign.Center => TextAlignment.Center, TextAlign.Right => TextAlignment.Right, _ => TextAlignment.Left },
            LineHeight = e.FontSize * 1.3,
        };
        var lines = e.Text.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (e.Bullets && lines[i].Trim().Length > 0)
                tb.Inlines.Add(new Run("•  ") { Foreground = Ui.BrushFromHex(theme.Accent) });
            tb.Inlines.Add(new Run(lines[i]));
            if (i < lines.Length - 1) tb.Inlines.Add(new LineBreak());
        }
        return new Border { Child = tb, Background = Brushes.Transparent, ClipToBounds = false };
    }

    private static FrameworkElement ShapeVisual(SlideElement e, SlideTheme theme)
    {
        var fill = e.FillHex == null ? null : Ui.BrushFromHex(e.FillHex);
        var stroke = e.StrokeHex == null ? null : Ui.BrushFromHex(e.StrokeHex);
        var w = Math.Max(1, e.Width);
        var h = Math.Max(1, e.Height);
        Shape s = e.Shape switch
        {
            ShapeKind.Rectangle => new Rectangle(),
            ShapeKind.RoundedRectangle => new Rectangle { RadiusX = Math.Min(w, h) * 0.12, RadiusY = Math.Min(w, h) * 0.12 },
            ShapeKind.Ellipse => new Ellipse(),
            ShapeKind.Line => new Path { Data = new LineGeometry(new Point(0, h / 2), new Point(w, h / 2)) },
            ShapeKind.Arrow => new Path { Data = ShapeGeometry.Arrow(new Point(0, h / 2), new Point(w, h / 2), 10 + e.StrokeThickness * 2.5) },
            ShapeKind.Triangle => new Path { Data = ShapeGeometry.Triangle(w, h), Stretch = Stretch.Fill },
            _ => new Rectangle()
        };
        if (e.Shape is ShapeKind.Line or ShapeKind.Arrow)
        {
            s.Stroke = stroke ?? fill ?? Ui.BrushFromHex(theme.Accent);
            s.StrokeThickness = Math.Max(1, e.StrokeThickness);
            s.StrokeStartLineCap = s.StrokeEndLineCap = PenLineCap.Round;
            s.StrokeLineJoin = PenLineJoin.Round;
        }
        else
        {
            s.Fill = fill;
            s.Stroke = stroke;
            s.StrokeThickness = stroke == null ? 0 : e.StrokeThickness;
        }
        return new Grid { Children = { s }, Background = Brushes.Transparent };
    }

    public static StrokeCollection? LoadInk(Slide slide)
    {
        if (string.IsNullOrEmpty(slide.Ink)) return null;
        try
        {
            using var ms = new MemoryStream(Convert.FromBase64String(slide.Ink));
            return new StrokeCollection(ms);
        }
        catch { return null; }
    }

    public static string? SaveInk(StrokeCollection strokes)
    {
        if (strokes.Count == 0) return null;
        using var ms = new MemoryStream();
        strokes.Save(ms, true);
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>Petite vignette de la diapositive.</summary>
    public static Viewbox Thumbnail(Slide slide, SlideTheme theme, double width)
    {
        return new Viewbox { Width = width, Height = width * H / W, Child = Render(slide, theme), Stretch = Stretch.Uniform };
    }
}
