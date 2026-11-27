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
        canvas.Children.Add(Decoration(theme));
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

    /// <summary>Décoration du thème (bandeaux, cercles, motifs…), dessinée sous le contenu.</summary>
    public static Canvas Decoration(SlideTheme theme)
    {
        var c = new Canvas { Width = W, Height = H, IsHitTestVisible = false };
        var accent = Ui.ColorFromHex(theme.Accent, Colors.SteelBlue);
        var accent2 = Ui.ColorFromHex(theme.Accent2 ?? theme.Accent, accent);
        SolidColorBrush B(Color col, double opacity) { var b = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), col.R, col.G, col.B)); b.Freeze(); return b; }
        void Add(UIElement e, double x, double y) { Canvas.SetLeft(e, x); Canvas.SetTop(e, y); c.Children.Add(e); }
        Polygon Poly(Brush fill, params double[] pts)
        {
            var p = new Polygon { Fill = fill };
            for (int i = 0; i + 1 < pts.Length; i += 2) p.Points.Add(new Point(pts[i], pts[i + 1]));
            return p;
        }
        Rectangle Pattern(Drawing tile, double size)
        {
            var brush = new DrawingBrush(tile) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, size, size), ViewportUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, size, size), ViewboxUnits = BrushMappingMode.Absolute };
            brush.Freeze();
            return new Rectangle { Width = W, Height = H, Fill = brush };
        }
        GeometryDrawing Line(Color col, double opacity, double thickness, Point a, Point b) =>
            new(null, new Pen(B(col, opacity), thickness), new LineGeometry(a, b));

        switch (theme.Decoration)
        {
            case SlideDecoration.TopBar:
                Add(new Rectangle { Width = W, Height = 18, Fill = B(accent, 1) }, 0, 0);
                Add(new Rectangle { Width = W, Height = 5, Fill = B(accent2, 0.45) }, 0, 18);
                break;
            case SlideDecoration.SideBar:
                Add(new Rectangle { Width = 30, Height = H, Fill = B(accent, 1) }, 0, 0);
                Add(new Rectangle { Width = 8, Height = H, Fill = B(accent2, 0.7) }, 30, 0);
                break;
            case SlideDecoration.Circles:
                Add(new Ellipse { Width = 560, Height = 560, Fill = B(accent2, 0.10) }, -200, -230);
                Add(new Ellipse { Width = 420, Height = 420, Fill = B(accent2, 0.09) }, 1000, 430);
                Add(new Ellipse { Width = 150, Height = 150, Fill = B(accent2, 0.08) }, 1080, 50);
                break;
            case SlideDecoration.Wave:
            {
                var g = Geometry.Parse("M0,620 C320,560 640,700 960,640 C1120,610 1200,600 1280,610 L1280,720 L0,720 Z");
                var g2 = Geometry.Parse("M0,660 C300,620 700,720 1000,670 C1150,645 1230,650 1280,655 L1280,720 L0,720 Z");
                c.Children.Add(new Path { Data = g, Fill = B(accent2, 0.22) });
                c.Children.Add(new Path { Data = g2, Fill = B(accent2, 0.30) });
                break;
            }
            case SlideDecoration.Frame:
                Add(new Rectangle { Width = W - 48, Height = H - 48, Stroke = B(accent2, 0.7), StrokeThickness = 4, RadiusX = 6, RadiusY = 6 }, 24, 24);
                Add(new Rectangle { Width = W - 72, Height = H - 72, Stroke = B(accent2, 0.4), StrokeThickness = 1.5, RadiusX = 4, RadiusY = 4 }, 36, 36);
                break;
            case SlideDecoration.Dots:
            {
                var tile = new GeometryDrawing(B(accent2, 0.18), null, new EllipseGeometry(new Point(20, 20), 2.6, 2.6));
                c.Children.Add(Pattern(tile, 40));
                break;
            }
            case SlideDecoration.Diagonal:
                c.Children.Add(Poly(B(accent, 0.16), 900, 0, W, 0, W, H, 1080, H));
                c.Children.Add(Poly(B(accent2, 0.35), 990, 0, 1050, 0, 870, H, 810, H));
                break;
            case SlideDecoration.Corner:
                c.Children.Add(Poly(B(accent2, 0.9), W, H, W, 400, 960, H));
                c.Children.Add(Poly(B(accent, 0.25), 0, 0, 240, 0, 0, 170));
                break;
            case SlideDecoration.Notebook:
            {
                var tile = new DrawingGroup();
                tile.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 36, 36))));
                tile.Children.Add(Line(accent2, 0.55, 1.2, new Point(0, 35), new Point(36, 35)));
                c.Children.Add(Pattern(tile, 36));
                Add(new Rectangle { Width = 2.5, Height = H, Fill = B(accent, 0.6) }, 110, 0);
                break;
            }
            case SlideDecoration.Grid:
            {
                var tile = new DrawingGroup();
                tile.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 32, 32))));
                tile.Children.Add(Line(accent2, 0.45, 1, new Point(0, 31.5), new Point(32, 31.5)));
                tile.Children.Add(Line(accent2, 0.45, 1, new Point(31.5, 0), new Point(31.5, 32)));
                c.Children.Add(Pattern(tile, 32));
                break;
            }
            case SlideDecoration.Stripes:
            {
                var tile = new DrawingGroup();
                tile.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 40, 40))));
                tile.Children.Add(Line(accent2, 0.35, 6, new Point(-10, 50), new Point(50, -10)));
                tile.Children.Add(Line(accent2, 0.35, 6, new Point(-30, 30), new Point(30, -30)));
                tile.Children.Add(Line(accent2, 0.35, 6, new Point(10, 70), new Point(70, 10)));
                var r = Pattern(tile, 40);
                r.Height = 70;
                Canvas.SetTop(r, H - 70);
                c.Children.Add(r);
                break;
            }
        }
        return c;
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
