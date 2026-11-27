using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;

namespace StudyDesk.Helpers;

/// <summary>Styles de paragraphe des documents. Un titre = texte en gras d'au moins 17 points.</summary>
public static class DocStyles
{
    public static readonly Color H1Color = Color.FromRgb(0x1D, 0x3D, 0x7A);
    public static readonly Color H2Color = Color.FromRgb(0x2F, 0x62, 0xD9);
    public static readonly Color H3Color = Color.FromRgb(0x1B, 0x22, 0x30);
    public static readonly Color TextColor = Color.FromRgb(0x1B, 0x1B, 0x1B);
    public static readonly Color BoxBack = Color.FromRgb(0xE8, 0xEF, 0xFD);
    public static readonly Color BoxLine = Color.FromRgb(0x2F, 0x62, 0xD9);

    public static double SizeFor(int level) => level switch { 1 => 28, 2 => 21, 3 => 17, _ => 15 };

    public static void ApplyHeading(Paragraph p, int level, double bodySize)
    {
        if (level == 0)
        {
            p.FontSize = bodySize;
            p.FontWeight = FontWeights.Normal;
            p.Foreground = new SolidColorBrush(TextColor);
            p.Margin = new Thickness(0, 0, 0, 10);
            foreach (var r in p.Inlines.OfType<Run>()) { r.ClearValue(TextElement.FontSizeProperty); r.ClearValue(TextElement.FontWeightProperty); r.ClearValue(TextElement.ForegroundProperty); }
            return;
        }
        p.FontSize = SizeFor(level);
        p.FontWeight = level == 3 ? FontWeights.SemiBold : FontWeights.Bold;
        p.Foreground = new SolidColorBrush(level == 1 ? H1Color : level == 2 ? H2Color : H3Color);
        p.Margin = new Thickness(0, level == 1 ? 6 : 16, 0, 8);
        foreach (var r in p.Inlines.OfType<Run>()) { r.ClearValue(TextElement.FontSizeProperty); r.ClearValue(TextElement.FontWeightProperty); r.ClearValue(TextElement.ForegroundProperty); }
    }

    /// <summary>Niveau de titre d'un paragraphe (0 = texte normal).</summary>
    public static int HeadingLevel(Paragraph p)
    {
        if (p.Parent is ListItem) return 0;
        var size = p.FontSize;
        var bold = p.FontWeight.ToOpenTypeWeight() >= 600;
        // si tout le texte du paragraphe est en gras et plus grand
        var runs = p.Inlines.OfType<Run>().Where(r => r.Text.Trim().Length > 0).ToList();
        if (runs.Count > 0)
        {
            size = runs.Max(r => r.FontSize);
            bold = runs.All(r => r.FontWeight.ToOpenTypeWeight() >= 600);
        }
        if (!bold || size < 16.5) return 0;
        return size >= 25 ? 1 : size >= 19.5 ? 2 : 3;
    }

    public static Section CreateBox(string? text = null)
    {
        var s = new Section
        {
            Background = new SolidColorBrush(BoxBack),
            BorderBrush = new SolidColorBrush(BoxLine),
            BorderThickness = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(14, 8, 12, 8),
            Margin = new Thickness(0, 6, 0, 12)
        };
        s.Blocks.Add(new Paragraph(new Run(text ?? "À retenir : ")) { Margin = new Thickness(0) });
        return s;
    }
}

/// <summary>Conversion du « texte simple » (modèles, tutoriels) en FlowDocument.</summary>
public static class MarkupRenderer
{
    private static readonly Regex InlineRegex = new(@"(\*\*[^*]+\*\*|\*[^*]+\*)", RegexOptions.Compiled);

    /// <param name="themed">Vrai : couleurs de l'interface (tutoriels). Faux : couleurs de la page (documents).</param>
    public static void Render(FlowDocument doc, string markup, bool themed, double bodySize = 15)
    {
        var lines = markup.Replace("\r", "").Split('\n');
        List? currentList = null;
        Section? box = null;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) { currentList = null; box = null; continue; }

            if (line.StartsWith("- ") || Regex.IsMatch(line, @"^\d+\. "))
            {
                var numbered = !line.StartsWith("- ");
                var text = numbered ? Regex.Replace(line, @"^\d+\. ", "") : line[2..];
                if (currentList == null || (currentList.MarkerStyle == TextMarkerStyle.Decimal) != numbered)
                {
                    currentList = new List { MarkerStyle = numbered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(24, 0, 0, 0) };
                    AddBlock(doc, box, currentList);
                }
                var p = new Paragraph { Margin = new Thickness(0, 0, 0, 3) };
                AddInlines(p, text, themed);
                currentList.ListItems.Add(new ListItem(p));
                continue;
            }
            currentList = null;

            if (line == "---")
            {
                box = null;
                var sep = new Paragraph { BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 6, 0, 14), FontSize = 4 };
                if (themed) sep.SetResourceReference(Block.BorderBrushProperty, "Line");
                else sep.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xD2, 0xDC));
                doc.Blocks.Add(sep);
                continue;
            }

            if (line.StartsWith("> "))
            {
                if (box == null)
                {
                    box = DocStyles.CreateBox("");
                    box.Blocks.Clear();
                    if (themed)
                    {
                        box.SetResourceReference(Block.BackgroundProperty, "AccentSoft");
                        box.SetResourceReference(Block.BorderBrushProperty, "Accent");
                    }
                    doc.Blocks.Add(box);
                }
                var p = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                AddInlines(p, line[2..], themed);
                box.Blocks.Add(p);
                continue;
            }
            box = null;

            int level = line.StartsWith("### ") ? 3 : line.StartsWith("## ") ? 2 : line.StartsWith("# ") ? 1 : 0;
            var para = new Paragraph();
            AddInlines(para, level > 0 ? line[(level + 1)..] : line, themed);
            DocStyles.ApplyHeading(para, level, bodySize);
            if (themed)
            {
                para.ClearValue(TextElement.ForegroundProperty);
                if (level == 1 || level == 2) para.SetResourceReference(TextElement.ForegroundProperty, level == 2 ? "Accent" : "Fg");
                else para.SetResourceReference(TextElement.ForegroundProperty, "Fg");
            }
            doc.Blocks.Add(para);
        }
    }

    private static void AddBlock(FlowDocument doc, Section? box, Block b)
    {
        if (box != null) box.Blocks.Add(b);
        else doc.Blocks.Add(b);
    }

    private static void AddInlines(Paragraph p, string text, bool themed)
    {
        foreach (var part in InlineRegex.Split(text))
        {
            if (part.Length == 0) continue;
            if (part.StartsWith("**") && part.EndsWith("**") && part.Length > 4)
                p.Inlines.Add(new Run(part[2..^2]) { FontWeight = FontWeights.Bold });
            else if (part.StartsWith('*') && part.EndsWith('*') && part.Length > 2)
                p.Inlines.Add(new Run(part[1..^1]) { FontStyle = FontStyles.Italic });
            else
                p.Inlines.Add(new Run(part));
        }
    }
}

/// <summary>Lecture / écriture des documents (.sdoc = XamlPackage WPF avec images), export HTML et texte.</summary>
public static class DocumentIO
{
    public static FlowDocument NewDocument(ProfileSettings s) => new()
    {
        FontFamily = new FontFamily(s.DefaultFontFamily),
        FontSize = s.DefaultFontSize,
        Foreground = new SolidColorBrush(DocStyles.TextColor),
        PagePadding = new Thickness(0),
        LineHeight = double.NaN
    };

    public static FlowDocument Load(ProfileStore store, LibraryItem item)
    {
        var doc = NewDocument(store.Library.Settings);
        var path = store.ContentPath(item);
        if (File.Exists(path))
        {
            using var fs = File.OpenRead(path);
            var range = new TextRange(doc.ContentStart, doc.ContentEnd);
            range.Load(fs, DataFormats.XamlPackage);
        }
        else
        {
            var seed = DefaultContent.SeedPath(store, item.Id);
            if (File.Exists(seed)) MarkupRenderer.Render(doc, File.ReadAllText(seed), themed: false, store.Library.Settings.DefaultFontSize);
        }
        return doc;
    }

    public static void Save(ProfileStore store, LibraryItem item, FlowDocument doc)
    {
        var path = store.ContentPath(item);
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
        {
            var range = new TextRange(doc.ContentStart, doc.ContentEnd);
            range.Save(fs, DataFormats.XamlPackage, true);
        }
        File.Move(tmp, path, true);
        File.WriteAllText(store.DocumentHtmlPath(item.Id), ToHtml(doc));
        File.WriteAllText(store.DocumentTextPath(item.Id), PlainText(doc));
        var seed = DefaultContent.SeedPath(store, item.Id);
        if (File.Exists(seed)) File.Delete(seed);
        store.Touch(item);
    }

    public static string PlainText(FlowDocument doc) => new TextRange(doc.ContentStart, doc.ContentEnd).Text;

    public static int WordCount(string text) => Regex.Matches(text, @"[\p{L}\p{N}’']+").Count;

    // ---------------------------------------------------------------- Plan (pour la conversion en fiches / quiz)

    public static List<OutlineBlock> Outline(FlowDocument doc)
    {
        var list = new List<OutlineBlock>();
        foreach (var b in doc.Blocks) Walk(b, list, false);
        return list;
    }

    private static void Walk(Block b, List<OutlineBlock> list, bool inList)
    {
        switch (b)
        {
            case Paragraph p:
                var ob = new OutlineBlock { Level = inList ? 0 : DocStyles.HeadingLevel(p), IsListItem = inList };
                foreach (var (text, bold) in Segments(p.Inlines)) ob.Segments.Add((text, bold));
                if (ob.Text.Length > 0) list.Add(ob);
                break;
            case List l:
                foreach (var li in l.ListItems)
                    foreach (var inner in li.Blocks) Walk(inner, list, true);
                break;
            case Section s:
                foreach (var inner in s.Blocks) Walk(inner, list, inList);
                break;
            case Table t:
                foreach (var row in t.RowGroups.SelectMany(g => g.Rows))
                {
                    var cells = row.Cells.Select(c => new TextRange(c.ContentStart, c.ContentEnd).Text.Trim()).ToList();
                    if (cells.Count >= 2 && cells[0].Length > 0 && cells[1].Length > 0)
                        list.Add(OutlineBlock.Plain(cells[0] + " : " + string.Join(" ", cells.Skip(1))));
                }
                break;
        }
    }

    private static IEnumerable<(string, bool)> Segments(InlineCollection inlines)
    {
        foreach (var i in inlines)
        {
            switch (i)
            {
                case Run r:
                    yield return (r.Text, r.FontWeight.ToOpenTypeWeight() >= 600);
                    break;
                case Span s:
                    foreach (var x in Segments(s.Inlines)) yield return x;
                    break;
                case LineBreak:
                    yield return (" ", false);
                    break;
            }
        }
    }

    // ---------------------------------------------------------------- HTML (lecture sur téléphone)

    public static string ToHtml(FlowDocument doc)
    {
        var sb = new StringBuilder();
        foreach (var b in doc.Blocks) BlockHtml(b, sb);
        return sb.ToString();
    }

    private static string E(string s) => WebUtility.HtmlEncode(s);

    private static void BlockHtml(Block b, StringBuilder sb)
    {
        switch (b)
        {
            case Paragraph p:
                var level = DocStyles.HeadingLevel(p);
                var inner = InlinesHtml(p.Inlines, level > 0);
                if (inner.Trim().Length == 0) { if (p.BorderThickness.Bottom > 0) sb.Append("<hr>"); return; }
                var align = p.TextAlignment switch { TextAlignment.Center => " style=\"text-align:center\"", TextAlignment.Right => " style=\"text-align:right\"", _ => "" };
                sb.Append(level > 0 ? $"<h{level}{align}>{inner}</h{level}>" : $"<p{align}>{inner}</p>");
                break;
            case List l:
                var tag = l.MarkerStyle is TextMarkerStyle.Decimal or TextMarkerStyle.LowerLatin or TextMarkerStyle.UpperLatin or TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperRoman ? "ol" : "ul";
                sb.Append('<').Append(tag).Append('>');
                foreach (var li in l.ListItems)
                {
                    sb.Append("<li>");
                    foreach (var inner2 in li.Blocks)
                    {
                        if (inner2 is Paragraph pp) sb.Append(InlinesHtml(pp.Inlines, false));
                        else BlockHtml(inner2, sb);
                    }
                    sb.Append("</li>");
                }
                sb.Append("</").Append(tag).Append('>');
                break;
            case Section s:
                var isBox = s.BorderThickness.Left > 0 || s.Background != null;
                sb.Append(isBox ? "<div class=\"box\">" : "<div>");
                foreach (var x in s.Blocks) BlockHtml(x, sb);
                sb.Append("</div>");
                break;
            case Table t:
                sb.Append("<table>");
                foreach (var row in t.RowGroups.SelectMany(g => g.Rows))
                {
                    sb.Append("<tr>");
                    foreach (var cell in row.Cells)
                    {
                        sb.Append("<td>");
                        foreach (var cb in cell.Blocks)
                        {
                            if (cb is Paragraph cp) sb.Append(InlinesHtml(cp.Inlines, false));
                            else BlockHtml(cb, sb);
                        }
                        sb.Append("</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
                break;
            case BlockUIContainer c when c.Child is Image img:
                sb.Append("<p>").Append(ImageHtml(img)).Append("</p>");
                break;
        }
    }

    private static string InlinesHtml(InlineCollection inlines, bool heading)
    {
        var sb = new StringBuilder();
        foreach (var i in inlines)
        {
            switch (i)
            {
                case Run r:
                    var text = E(r.Text);
                    if (!heading && r.FontWeight.ToOpenTypeWeight() >= 600) text = "<b>" + text + "</b>";
                    if (r.FontStyle == FontStyles.Italic) text = "<i>" + text + "</i>";
                    if (r.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Underline) == true) text = "<u>" + text + "</u>";
                    if (r.Background is SolidColorBrush bg && bg.Color.A > 0) text = $"<mark style=\"background:{Css(bg.Color)}\">{text}</mark>";
                    else if (r.Foreground is SolidColorBrush fg && fg.Color != DocStyles.TextColor && !heading && fg.Color != Colors.Black)
                        text = $"<span style=\"color:{Css(fg.Color)}\">{text}</span>";
                    sb.Append(text);
                    break;
                case LineBreak:
                    sb.Append("<br>");
                    break;
                case Span s:
                    sb.Append(InlinesHtml(s.Inlines, heading));
                    break;
                case InlineUIContainer c when c.Child is Image img:
                    sb.Append(ImageHtml(img));
                    break;
            }
        }
        return sb.ToString();
    }

    private static string Css(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static string ImageHtml(Image img)
    {
        try
        {
            if (img.Source is not BitmapSource bs) return "";
            var scaled = ImageHelper.Downscale(bs, 1000);
            var enc = new JpegBitmapEncoder { QualityLevel = 82 };
            var hasAlpha = scaled.Format == PixelFormats.Bgra32 || scaled.Format == PixelFormats.Pbgra32;
            byte[] bytes;
            string mime;
            if (hasAlpha) { bytes = ImageHelper.ToPng(scaled); mime = "image/png"; }
            else
            {
                enc.Frames.Add(BitmapFrame.Create(scaled));
                using var ms = new MemoryStream();
                enc.Save(ms);
                bytes = ms.ToArray();
                mime = "image/jpeg";
            }
            var width = double.IsNaN(img.Width) ? "" : $" width=\"{(int)img.Width}\"";
            return $"<img src=\"data:{mime};base64,{Convert.ToBase64String(bytes)}\"{width} alt=\"\">";
        }
        catch
        {
            return "";
        }
    }
}
