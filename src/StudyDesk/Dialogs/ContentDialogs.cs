using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Helpers;
using StudyDesk.Services;
using StudyDesk.Views;

namespace StudyDesk.Dialogs;

// ======================================================================= Recherche d'images

/// <summary>Recherche d'images libres de droits sur Wikimedia Commons.</summary>
public sealed class ImageSearchDialog : DialogWindow
{
    private readonly TextBox _query = new() { Padding = new Thickness(8, 6, 8, 6) };
    private readonly WrapPanel _results = new();
    private readonly TextBlock _state = Ui.Text("Tape un mot-clé, par exemple « prise de la Bastille » ou « cellule végétale ».", "Muted");
    private OnlineImage? _selected;
    private Border? _selectedBorder;
    private CancellationTokenSource? _cts;

    public byte[]? ResultBytes { get; private set; }
    public string? ResultCredit { get; private set; }

    public ImageSearchDialog(string? initial = null) : base("Image en ligne (Wikimedia Commons)", 820)
    {
        var row = new DockPanel();
        var go = Ui.Button("Rechercher", (_, _) => _ = Search(), "AccentButton", Icons.Search);
        go.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(go, Dock.Right);
        row.Children.Add(go);
        row.Children.Add(_query);
        _query.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; _ = Search(); } };
        Body.Children.Add(row);
        _state.Margin = new Thickness(0, 10, 0, 6);
        Body.Children.Add(_state);
        Body.Children.Add(new ScrollViewer { Content = _results, Height = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var note = Ui.Text("Les images de Wikimedia Commons sont libres. Leur auteur et leur licence seront ajoutés en légende.", "Muted");
        note.Margin = new Thickness(0, 8, 0, 0);
        Body.Children.Add(note);
        AddOkCancel("Insérer");
        OkButton!.IsEnabled = false;
        Loaded += (_, _) => { _query.Text = initial ?? ""; _query.Focus(); if (!string.IsNullOrWhiteSpace(initial)) _ = Search(); };
        Closing += (_, _) => _cts?.Cancel();
    }

    private async Task Search()
    {
        var q = _query.Text.Trim();
        if (q.Length == 0) return;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _results.Children.Clear();
        _selected = null;
        OkButton!.IsEnabled = false;
        _state.Text = "Recherche…";
        try
        {
            var images = await AppState.Online.SearchImagesAsync(q, ct);
            _state.Text = images.Count == 0 ? "Aucune image trouvée. Essaie un autre mot-clé (en anglais, il y a souvent plus de résultats)." : $"{images.Count} images. Clique pour choisir.";
            foreach (var img in images)
            {
                var image = new Image { Stretch = Stretch.UniformToFill };
                var border = new Border
                {
                    Width = 180, Height = 130, Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(3),
                    ClipToBounds = true, Child = image, Cursor = Cursors.Hand, ToolTip = img.CleanTitle + "\n" + img.Author + " · " + img.License
                };
                border.SetResourceReference(Border.BackgroundProperty, "Surface2");
                border.SetResourceReference(Border.BorderBrushProperty, "Surface2");
                var im = img;
                border.MouseLeftButtonUp += (_, _) => Select(im, border);
                border.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { Select(im, border); _ = Accept(); } };
                _results.Children.Add(border);
                _ = LoadThumb(image, img.ThumbUrl, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _state.Text = "Impossible de chercher en ligne. Vérifie ta connexion Internet. (" + ex.Message + ")";
        }
    }

    private static async Task LoadThumb(Image target, string url, CancellationToken ct)
    {
        try
        {
            var bytes = await AppState.Online.DownloadAsync(url, ct);
            target.Source = ImageHelper.FromBytes(bytes);
        }
        catch { /* vignette indisponible */ }
    }

    private void Select(OnlineImage img, Border border)
    {
        if (_selectedBorder != null) _selectedBorder.SetResourceReference(Border.BorderBrushProperty, "Surface2");
        border.SetResourceReference(Border.BorderBrushProperty, "Accent");
        _selectedBorder = border;
        _selected = img;
        OkButton!.IsEnabled = true;
    }

    protected override string? Validate()
    {
        if (_selected == null) return "Choisis une image.";
        if (ResultBytes == null) { _ = Accept(); return "Téléchargement de l'image…"; }
        return null;
    }

    private async Task Accept()
    {
        if (_selected == null) return;
        OkButton!.IsEnabled = false;
        _state.Text = "Téléchargement de l'image…";
        try
        {
            byte[] bytes;
            var large = _selected.ThumbUrl.Contains("/320px-") ? _selected.ThumbUrl.Replace("/320px-", "/1024px-") : _selected.Url;
            try { bytes = await AppState.Online.DownloadAsync(large); }
            catch { bytes = await AppState.Online.DownloadAsync(_selected.Url); }
            var (b, _) = ImageHelper.Prepare(bytes, System.IO.Path.GetExtension(_selected.Url).ToLowerInvariant());
            ResultBytes = b;
            ResultCredit = _selected.Credit;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            _state.Text = "Téléchargement impossible : " + ex.Message;
            OkButton.IsEnabled = true;
        }
    }
}

// ======================================================================= Wikipédia

public sealed class WikipediaDialog : DialogWindow
{
    private readonly TextBox _query = new() { Padding = new Thickness(8, 6, 8, 6) };
    private readonly ListBox _titles = new() { Height = 300 };
    private readonly TextBlock _extract = Ui.Text("", "Body");
    private readonly Image _thumb = new() { MaxHeight = 160, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8) };
    private readonly CheckBox _withImage = new() { Content = "Insérer aussi l'image", IsChecked = true, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _state = Ui.Text("Cherche un sujet : un personnage, un événement, une notion…", "Muted");
    private byte[]? _thumbBytes;

    public WikiSummary? Result { get; private set; }
    public byte[]? ResultImage => _withImage.IsChecked == true ? _thumbBytes : null;

    public WikipediaDialog() : base("Wikipédia", 820)
    {
        var row = new DockPanel();
        var go = Ui.Button("Rechercher", (_, _) => _ = Search(), "AccentButton", Icons.Search);
        go.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(go, Dock.Right);
        row.Children.Add(go);
        row.Children.Add(_query);
        _query.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; _ = Search(); } };
        Body.Children.Add(row);
        _state.Margin = new Thickness(0, 10, 0, 8);
        Body.Children.Add(_state);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(_titles);
        var preview = new StackPanel();
        preview.Children.Add(_thumb);
        preview.Children.Add(_extract);
        preview.Children.Add(_withImage);
        var sv = new ScrollViewer { Content = preview, Height = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(sv, 2);
        grid.Children.Add(sv);
        Body.Children.Add(grid);
        var note = Ui.Text("Le résumé est inséré avec sa source. Reformule-le avec tes mots : c'est comme ça qu'on apprend.", "Muted");
        note.Margin = new Thickness(0, 8, 0, 0);
        Body.Children.Add(note);
        _titles.SelectionChanged += (_, _) => { if (_titles.SelectedItem is string t) _ = LoadSummary(t); };
        AddOkCancel("Insérer");
        Loaded += (_, _) => _query.Focus();
    }

    private async Task Search()
    {
        var q = _query.Text.Trim();
        if (q.Length == 0) return;
        _state.Text = "Recherche…";
        try
        {
            var titles = await AppState.Online.SearchWikipediaAsync(q);
            _titles.ItemsSource = titles;
            _state.Text = titles.Count == 0 ? "Aucun article trouvé." : "Choisis un article.";
            if (titles.Count > 0) _titles.SelectedIndex = 0;
        }
        catch (Exception ex) { _state.Text = "Impossible de chercher en ligne : " + ex.Message; }
    }

    private async Task LoadSummary(string title)
    {
        _extract.Text = "Chargement…";
        _thumb.Source = null;
        _thumbBytes = null;
        _withImage.Visibility = Visibility.Collapsed;
        try
        {
            Result = await AppState.Online.GetWikipediaSummaryAsync(title);
            _extract.Text = Result?.Extract is { Length: > 0 } e ? e : "Pas de résumé pour cet article.";
            if (Result?.ThumbnailUrl != null)
            {
                _thumbBytes = await AppState.Online.DownloadAsync(Result.ThumbnailUrl);
                _thumb.Source = ImageHelper.FromBytes(_thumbBytes);
                _withImage.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex) { _extract.Text = "Erreur : " + ex.Message; }
    }

    protected override string? Validate() => Result == null || Result.Extract.Length == 0 ? "Choisis un article qui a un résumé." : null;
}

// ======================================================================= Dessin

/// <summary>Surface de dessin : crayon, surligneur, formes, flèches et texte. Le résultat est inséré comme image.</summary>
public sealed class DrawingDialog : DialogWindow
{
    private enum Tool { Pen, Marker, Eraser, Line, Arrow, Rectangle, Ellipse, Text }

    private readonly InkCanvas _ink = new() { Background = Brushes.Transparent };
    private readonly Canvas _shapes = new() { Background = Brushes.Transparent };
    private readonly Grid _surface;
    private Tool _tool = Tool.Pen;
    private Color _color = Color.FromRgb(0x1B, 0x22, 0x30);
    private double _thickness = 3;
    private Point _start;
    private Shape? _current;
    private readonly Stack<object> _undo = new();
    private readonly CheckBox _fill = new() { Content = "Remplir", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };

    public byte[]? ResultPng { get; private set; }

    public DrawingDialog(int width = 900, int height = 520) : base("Dessin et schéma", width + 60)
    {
        // Barre d'outils
        var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var (tool, glyph, name) in new[]
                 {
                     (Tool.Pen, Icons.Pen, "Crayon"), (Tool.Marker, Icons.Highlight, "Surligneur"), (Tool.Eraser, Icons.Eraser, "Gomme"),
                     (Tool.Line, Icons.Remove, "Ligne"), (Tool.Arrow, Icons.Forward, "Flèche"), (Tool.Rectangle, Icons.Shapes, "Rectangle"),
                     (Tool.Ellipse, "", "Ellipse"), (Tool.Text, Icons.TextBox, "Texte")
                 })
        {
            var rb = new RadioButton { Style = Ui.Style("Chip"), Content = Ui.IconText(glyph, name, 13), IsChecked = tool == Tool.Pen, GroupName = "tool", Margin = new Thickness(0, 0, 4, 4) };
            var t = tool;
            rb.Checked += (_, _) => SetTool(t);
            tools.Children.Add(rb);
        }
        Body.Children.Add(tools);

        var opts = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var hex in new[] { "#1B2230", "#C43C3C", "#D9731A", "#E0B000", "#1F8A52", "#1B8FA6", "#2F62D9", "#7A4FC4", "#FFFFFF" })
        {
            var b = new Button { Width = 24, Height = 24, Margin = new Thickness(0, 0, 4, 0), Background = Ui.BrushFromHex(hex), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Cursor = Cursors.Hand, ToolTip = hex };
            var c = Ui.ColorFromHex(hex, Colors.Black);
            b.Click += (_, _) => { _color = c; UpdateInk(); };
            opts.Children.Add(b);
        }
        opts.Children.Add(new TextBlock { Text = "Épaisseur", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) });
        var slider = new Slider { Minimum = 1, Maximum = 20, Value = _thickness, Width = 120, VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += (_, e) => { _thickness = e.NewValue; UpdateInk(); };
        opts.Children.Add(slider);
        opts.Children.Add(_fill);
        var undo = Ui.Button("Annuler", (_, _) => Undo(), "SubtleButton", Icons.Undo);
        undo.Margin = new Thickness(12, 0, 0, 0);
        opts.Children.Add(undo);
        opts.Children.Add(Ui.Button("Tout effacer", (_, _) => { _ink.Strokes.Clear(); _shapes.Children.Clear(); _undo.Clear(); }, "SubtleButton", Icons.Delete));
        Body.Children.Add(opts);

        _surface = new Grid { Width = width, Height = height, Background = Brushes.White, ClipToBounds = true };
        _surface.Children.Add(_ink);
        _surface.Children.Add(_shapes);
        Body.Children.Add(new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), Child = _surface, HorizontalAlignment = HorizontalAlignment.Center });

        _ink.StrokeCollected += (_, e) => _undo.Push(e.Stroke);
        _shapes.MouseLeftButtonDown += OnDown;
        _shapes.MouseMove += OnMove;
        _shapes.MouseLeftButtonUp += OnUp;
        SetTool(Tool.Pen);
        AddOkCancel("Insérer le dessin");
    }

    private void SetTool(Tool t)
    {
        _tool = t;
        var inkTool = t is Tool.Pen or Tool.Marker or Tool.Eraser;
        _shapes.IsHitTestVisible = !inkTool;
        _ink.EditingMode = t == Tool.Eraser ? InkCanvasEditingMode.EraseByStroke : inkTool ? InkCanvasEditingMode.Ink : InkCanvasEditingMode.None;
        _shapes.Cursor = t == Tool.Text ? Cursors.IBeam : Cursors.Cross;
        UpdateInk();
    }

    private void UpdateInk()
    {
        var marker = _tool == Tool.Marker;
        _ink.DefaultDrawingAttributes = new System.Windows.Ink.DrawingAttributes
        {
            Color = marker ? Color.FromArgb(110, _color.R, _color.G, _color.B) : _color,
            Width = marker ? _thickness * 4 : _thickness,
            Height = marker ? _thickness * 4 : _thickness,
            IsHighlighter = marker,
            FitToCurve = true,
            StylusTip = marker ? System.Windows.Ink.StylusTip.Rectangle : System.Windows.Ink.StylusTip.Ellipse
        };
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(_shapes);
        var brush = new SolidColorBrush(_color);
        if (_tool == Tool.Text)
        {
            var box = new TextBox { MinWidth = 120, FontSize = 12 + _thickness * 2, Foreground = brush, Background = Brushes.White, BorderThickness = new Thickness(1) };
            Canvas.SetLeft(box, _start.X);
            Canvas.SetTop(box, _start.Y);
            _shapes.Children.Add(box);
            box.Loaded += (_, _) => box.Focus();
            box.LostKeyboardFocus += (_, _) =>
            {
                _shapes.Children.Remove(box);
                if (box.Text.Trim().Length == 0) return;
                var tb = new TextBlock { Text = box.Text, FontSize = box.FontSize, Foreground = brush, FontWeight = FontWeights.SemiBold };
                Canvas.SetLeft(tb, _start.X + 3);
                Canvas.SetTop(tb, _start.Y + 2);
                _shapes.Children.Add(tb);
                _undo.Push(tb);
            };
            e.Handled = true;
            return;
        }
        _current = _tool switch
        {
            Tool.Rectangle => new Rectangle { RadiusX = 4, RadiusY = 4 },
            Tool.Ellipse => new Ellipse(),
            Tool.Line => new Line(),
            Tool.Arrow => new Path(),
            _ => null
        };
        if (_current == null) return;
        _current.Stroke = brush;
        _current.StrokeThickness = _thickness;
        _current.StrokeStartLineCap = _current.StrokeEndLineCap = PenLineCap.Round;
        _current.StrokeLineJoin = PenLineJoin.Round;
        if (_fill.IsChecked == true && _tool is Tool.Rectangle or Tool.Ellipse) _current.Fill = new SolidColorBrush(Color.FromArgb(70, _color.R, _color.G, _color.B));
        _shapes.Children.Add(_current);
        _shapes.CaptureMouse();
        Update(_start);
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_current != null && e.LeftButton == MouseButtonState.Pressed) Update(e.GetPosition(_shapes));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_current == null) return;
        _shapes.ReleaseMouseCapture();
        _undo.Push(_current);
        _current = null;
    }

    private void Update(Point p)
    {
        switch (_current)
        {
            case Line l:
                l.X1 = _start.X; l.Y1 = _start.Y; l.X2 = p.X; l.Y2 = p.Y;
                break;
            case Path path:
                path.Data = ShapeGeometry.Arrow(_start, p, 8 + _thickness * 2);
                break;
            case Shape s:
                var x = Math.Min(_start.X, p.X);
                var y = Math.Min(_start.Y, p.Y);
                Canvas.SetLeft(s, x);
                Canvas.SetTop(s, y);
                s.Width = Math.Abs(p.X - _start.X);
                s.Height = Math.Abs(p.Y - _start.Y);
                break;
        }
    }

    private void Undo()
    {
        if (_undo.Count == 0) return;
        var last = _undo.Pop();
        if (last is System.Windows.Ink.Stroke st) _ink.Strokes.Remove(st);
        else if (last is UIElement el) _shapes.Children.Remove(el);
    }

    protected override string? Validate()
    {
        if (_ink.Strokes.Count == 0 && _shapes.Children.Count == 0) return "Le dessin est vide.";
        var w = (int)_surface.Width;
        var h = (int)_surface.Height;
        _surface.Measure(new Size(w, h));
        _surface.Arrange(new Rect(0, 0, w, h));
        var rtb = new RenderTargetBitmap(w * 2, h * 2, 192, 192, PixelFormats.Pbgra32);
        rtb.Render(_surface);
        ResultPng = ImageHelper.ToPng(Crop(rtb));
        return null;
    }

    /// <summary>Enlève les marges blanches autour du dessin.</summary>
    private static BitmapSource Crop(BitmapSource src)
    {
        int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
        var px = new byte[h * stride];
        src.CopyPixels(px, stride, 0);
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                if (px[i] < 245 || px[i + 1] < 245 || px[i + 2] < 245)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            }
        if (maxX < 0) return src;
        int pad = 24;
        minX = Math.Max(0, minX - pad); minY = Math.Max(0, minY - pad);
        maxX = Math.Min(w - 1, maxX + pad); maxY = Math.Min(h - 1, maxY + pad);
        var c = new CroppedBitmap(src, new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
        c.Freeze();
        return c;
    }
}

public static class ShapeGeometry
{
    public static Geometry Arrow(Point a, Point b, double head)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(a, false, false);
            ctx.LineTo(b, true, true);
            var v = a - b;
            if (v.Length > 0.1)
            {
                v.Normalize();
                var n = new Vector(-v.Y, v.X);
                var p1 = b + v * head + n * head * 0.6;
                var p2 = b + v * head - n * head * 0.6;
                ctx.BeginFigure(p1, false, false);
                ctx.LineTo(b, true, true);
                ctx.LineTo(p2, true, true);
            }
        }
        g.Freeze();
        return g;
    }

    public static Geometry Triangle(double w, double h)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(w / 2, 0), true, true);
            ctx.LineTo(new Point(w, h), true, true);
            ctx.LineTo(new Point(0, h), true, true);
        }
        g.Freeze();
        return g;
    }
}

// ======================================================================= Aperçu des fiches et quiz générés

public sealed class CardsPreviewDialog : DialogWindow
{
    private readonly List<(CheckBox Box, Flashcard Card)> _rows = new();
    private readonly RadioButton _newDeck = new() { Content = "Nouveau paquet :", IsChecked = true, GroupName = "dest", VerticalAlignment = VerticalAlignment.Center };
    private readonly RadioButton _existing = new() { Content = "Ajouter au paquet :", GroupName = "dest", VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _name = new() { Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(8, 0, 0, 0) };
    private readonly ComboBox _decks = new() { DisplayMemberPath = "Title", Margin = new Thickness(8, 0, 0, 0) };

    public CardsPreviewDialog(List<Flashcard> cards, LibraryItem source) : base($"{Ui.Plural(cards.Count, "fiche trouvée", "fiches trouvées")}", 720)
    {
        Body.Children.Add(Ui.Text("Décoche les fiches que tu ne veux pas garder. Tu pourras les modifier ensuite.", "Muted"));
        var list = new StackPanel();
        foreach (var c in cards)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = c.Front, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            var back = Ui.Text(c.Back.Length > 220 ? c.Back[..220] + "…" : c.Back, "Muted");
            sp.Children.Add(back);
            var cb = new CheckBox { IsChecked = true, Content = sp, Margin = new Thickness(0, 0, 0, 10) };
            _rows.Add((cb, c));
            list.Children.Add(cb);
        }
        var sv = new ScrollViewer { Content = list, MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 10, 0, 10) };
        Body.Children.Add(sv);

        _name.Text = source.Title + " — fiches";
        var r1 = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };
        r1.Children.Add(_newDeck);
        r1.Children.Add(_name);
        Body.Children.Add(r1);
        var decks = AppState.Store.ItemsOf(ItemKind.Deck).OrderByDescending(d => d.SubjectId == source.SubjectId).ThenBy(d => d.Title).ToList();
        _decks.ItemsSource = decks;
        _decks.SelectedIndex = decks.Count > 0 ? 0 : -1;
        var r2 = new DockPanel();
        r2.Children.Add(_existing);
        r2.Children.Add(_decks);
        _existing.IsEnabled = decks.Count > 0;
        Body.Children.Add(r2);
        AddOkCancel("Créer les fiches");
    }

    public List<Flashcard> Selected => _rows.Where(r => r.Box.IsChecked == true).Select(r => r.Card).ToList();
    public bool ToExisting => _existing.IsChecked == true;
    public LibraryItem? ExistingDeck => _decks.SelectedItem as LibraryItem;
    public string NewName => _name.Text.Trim();

    protected override string? Validate() =>
        Selected.Count == 0 ? "Garde au moins une fiche." : !ToExisting && NewName.Length == 0 ? "Donne un nom au paquet." : null;

    public static void CreateDeckFrom(List<Flashcard> cards, LibraryItem source)
    {
        var d = new CardsPreviewDialog(cards, source);
        if (d.ShowDialog() != true) return;
        var store = AppState.Store;
        LibraryItem target;
        if (d.ToExisting && d.ExistingDeck != null) target = d.ExistingDeck;
        else target = store.CreateItem(ItemKind.Deck, d.NewName, source.SubjectId, source.ChapterId);
        var deck = store.LoadDeck(target.Id);
        deck.Cards.AddRange(d.Selected);
        store.SaveDeck(deck);
        store.Touch(target);
        if (DialogWindow.Confirm("Fiches créées", $"{Ui.Plural(d.Selected.Count, "fiche ajoutée", "fiches ajoutées")} dans « {target.Title} ». Les ouvrir maintenant ?", "Ouvrir"))
            ItemActions.Open(target);
    }
}

public sealed class QuestionsPreviewDialog : DialogWindow
{
    private readonly List<(CheckBox Box, QuizQuestion Q)> _rows = new();
    private readonly TextBox _name = new() { Padding = new Thickness(6, 4, 6, 4) };

    public QuestionsPreviewDialog(List<QuizQuestion> questions, string defaultName) : base($"{Ui.Plural(questions.Count, "question créée", "questions créées")}", 720)
    {
        Body.Children.Add(Ui.Text("Décoche les questions que tu ne veux pas garder. Tu pourras les modifier ensuite.", "Muted"));
        var list = new StackPanel();
        foreach (var q in questions)
        {
            var sp = new StackPanel();
            var type = q.Type switch
            {
                QuestionType.MultipleChoice => "QCM", QuestionType.FillBlanks => "Texte à trous", QuestionType.ShortAnswer => "Réponse courte",
                QuestionType.Matching => "Relier", _ => "Vrai / faux"
            };
            sp.Children.Add(new TextBlock { Text = type + " · " + (q.Type == QuestionType.FillBlanks ? q.ClozeText : q.Prompt), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(Ui.Text("Réponse : " + QuizGrader.CorrectAnswerText(q), "Muted"));
            var cb = new CheckBox { IsChecked = true, Content = sp, Margin = new Thickness(0, 0, 0, 10) };
            _rows.Add((cb, q));
            list.Children.Add(cb);
        }
        Body.Children.Add(new ScrollViewer { Content = list, MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 10, 0, 10) });
        Body.Children.Add(FieldLabel("Nom du quiz"));
        _name.Text = defaultName;
        Body.Children.Add(_name);
        AddOkCancel("Créer le quiz");
    }

    public List<QuizQuestion> Selected => _rows.Where(r => r.Box.IsChecked == true).Select(r => r.Q).ToList();
    public string QuizName => _name.Text.Trim();

    protected override string? Validate() => Selected.Count == 0 ? "Garde au moins une question." : QuizName.Length == 0 ? "Donne un nom au quiz." : null;

    public static void CreateQuizFrom(List<QuizQuestion> questions, LibraryItem source)
    {
        var d = new QuestionsPreviewDialog(questions, source.Title + " — quiz");
        if (d.ShowDialog() != true) return;
        var store = AppState.Store;
        var item = store.CreateItem(ItemKind.Quiz, d.QuizName, source.SubjectId, source.ChapterId);
        var quiz = store.LoadQuiz(item.Id);
        quiz.Questions.AddRange(d.Selected);
        store.SaveQuiz(quiz);
        if (DialogWindow.Confirm("Quiz créé", $"Le quiz « {item.Title} » contient {Ui.Plural(d.Selected.Count, "question")}. Le faire maintenant ?", "Faire le quiz"))
            QuizPlayerView.Start(item);
    }
}
