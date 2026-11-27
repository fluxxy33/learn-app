using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Views;

/// <summary>Éditeur de diaporama : vignettes à gauche, diapositive au centre, propriétés à droite, notes en bas.</summary>
public sealed class SlideEditorView : EditorShell, IPage, IEditor
{
    public string NavKey => "library";
    private readonly LibraryItem _item;
    private Slideshow _show;
    private int _slideIndex;
    private SlideElement? _selected;
    private bool _dirty;

    private readonly ListBox _thumbs = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Canvas _stage = new() { Width = SlideRenderer.W, Height = SlideRenderer.H, ClipToBounds = true };
    private readonly InkCanvas _ink = new() { Width = SlideRenderer.W, Height = SlideRenderer.H, Background = Brushes.Transparent };
    private readonly Grid _stageRoot = new() { Width = SlideRenderer.W, Height = SlideRenderer.H };
    private readonly TextBox _notes = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 70, Padding = new Thickness(8, 6, 8, 6), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly StackPanel _props = new();
    private readonly ToggleButton _drawToggle = Ui.ToolToggle(Icons.Pen, "Dessiner", "Dessiner à main levée sur la diapositive");
    private readonly ToggleButton _eraseToggle = Ui.ToolToggle(Icons.Eraser, "Gomme", "Effacer des traits dessinés");
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private SlideElement? _clipboard;
    private Color _inkColor = Color.FromRgb(0xC4, 0x3C, 0x3C);
    private bool _loading;

    private SlideTheme Theme => SlideThemes.Get(_show.ThemeId);
    private Slide CurrentSlide => _show.Slides[Math.Clamp(_slideIndex, 0, _show.Slides.Count - 1)];

    public SlideEditorView(LibraryItem item) : base(item.Title, Icons.Slideshow)
    {
        _item = item;
        _show = AppState.Store.LoadSlideshow(item.Id);
        if (_show.Slides.Count == 0) _show.Slides.Add(SlideshowFactory.CreateSlide(SlideLayout.TitleSlide, item.Title));
        TitleText.MouseLeftButtonUp += (_, _) =>
        {
            var n = DialogWindow.Ask("Renommer", "Nom du diaporama", _item.Title, "Renommer");
            if (n == null) return;
            _item.Title = n; TitleText.Text = n; AppState.Store.Touch(_item);
        };

        // ---------------------------------------------- Ruban
        Button newSlide = null!, shapes = null!, themeBtn = null!, bgBtn = null!, transBtn = null!, inkColorBtn = null!;
        newSlide = Ui.Tool(Icons.Add, "Nouvelle diapositive", "Ajouter une diapositive", (_, _) => LayoutMenu(newSlide).IsOpen = true);
        var home = RibbonRow(
            newSlide,
            Ui.Tool(Icons.Copy, null, "Dupliquer la diapositive", (_, _) => DuplicateSlide()),
            Ui.Tool(Icons.Delete, null, "Supprimer la diapositive", (_, _) => DeleteSlide()),
            Ui.Separator(),
            Ui.Tool(Icons.Undo, null, "Annuler (Ctrl + Z)", (_, _) => Undo()),
            Ui.Tool(Icons.Redo, null, "Rétablir (Ctrl + Y)", (_, _) => Redo()),
            Ui.Separator(),
            Ui.Tool(Icons.TextBox, "Zone de texte", "Ajouter une zone de texte", (_, _) => AddText(false)),
            Ui.Tool(Icons.Header, "Titre", "Ajouter un titre", (_, _) => AddText(true)),
            Ui.Separator(),
            Ui.Tool(Icons.Bold, null, "Gras", (_, _) => EditSelected(e => e.Bold = !e.Bold)),
            Ui.Tool(Icons.Italic, null, "Italique", (_, _) => EditSelected(e => e.Italic = !e.Italic)),
            Ui.Tool(Icons.Bullets, null, "Puces", (_, _) => EditSelected(e => e.Bullets = !e.Bullets)),
            Ui.Tool(Icons.AlignLeft, null, "Aligner à gauche", (_, _) => EditSelected(e => e.Align = TextAlign.Left)),
            Ui.Tool(Icons.AlignCenter, null, "Centrer", (_, _) => EditSelected(e => e.Align = TextAlign.Center)),
            Ui.Tool(Icons.AlignRight, null, "Aligner à droite", (_, _) => EditSelected(e => e.Align = TextAlign.Right)),
            Ui.Tool(Icons.Font, "A+", "Agrandir le texte", (_, _) => EditSelected(e => e.FontSize = Math.Min(160, e.FontSize + 4))),
            Ui.Tool(Icons.Font, "A−", "Réduire le texte", (_, _) => EditSelected(e => e.FontSize = Math.Max(10, e.FontSize - 4))));

        shapes = Ui.Tool(Icons.Shapes, "Formes", "Rectangle, cercle, flèche…", (_, _) => ShapeMenu(shapes).IsOpen = true);
        inkColorBtn = Ui.Tool(Icons.Color, null, "Couleur du crayon", (_, _) => ColorMenu(inkColorBtn, c => { if (c != null) { _inkColor = c.Value; UpdateInkMode(); } }, false).IsOpen = true);
        var insert = RibbonRow(
            Ui.Tool(Icons.Picture, "Image", "Image depuis le PC (ou glisser-déposer, ou Ctrl + V)", (_, _) => AddImageFromFile()),
            Ui.Tool(Icons.Globe, "Image en ligne", "Image libre de droits (Wikimedia Commons)", (_, _) => AddOnlineImage()),
            shapes,
            Ui.Separator(),
            _drawToggle, _eraseToggle, inkColorBtn,
            Ui.Tool(Icons.Delete, "Effacer le dessin", "Effacer tous les traits de cette diapositive", (_, _) => { PushUndo(); _ink.Strokes.Clear(); SaveInk(); }));
        _drawToggle.Click += (_, _) => { if (_drawToggle.IsChecked == true) _eraseToggle.IsChecked = false; UpdateInkMode(); };
        _eraseToggle.Click += (_, _) => { if (_eraseToggle.IsChecked == true) _drawToggle.IsChecked = false; UpdateInkMode(); };

        themeBtn = Ui.Tool(Icons.Color, "Thème", "Changer le thème de tout le diaporama", (_, _) => ThemeMenu(themeBtn).IsOpen = true);
        bgBtn = Ui.Tool(Icons.Brightness, "Fond", "Couleur de fond de cette diapositive", (_, _) =>
            ColorMenu(bgBtn, c => { PushUndo(); CurrentSlide.BackgroundHex = c == null ? null : Ui.ToHex(c.Value); Changed(true); }, true, "Fond du thème").IsOpen = true);
        transBtn = Ui.Tool(Icons.Slideshow, "Transition", "Animation entre les diapositives", (_, _) => TransitionMenu(transBtn).IsOpen = true);
        var design = RibbonRow(themeBtn, bgBtn, transBtn);

        var review = RibbonRow(
            Ui.Tool(Icons.Cards, "Créer des fiches", "Le titre de chaque diapositive devient une question, son texte la réponse", (_, _) => MakeCards()),
            Ui.Tool(Icons.Info, "Conseils", "Conseils pour un bon diaporama", (_, _) => AppState.Main.Navigate(new TutorialsView("expose"))));

        AddRibbonTab("Accueil", home, true);
        AddRibbonTab("Insertion", insert);
        AddRibbonTab("Création", design);
        AddRibbonTab("Réviser", review);
        RightActions.Children.Add(Ui.Button("Présenter", (_, _) => Present(_slideIndex), "AccentButton", Icons.Play, "F5 : depuis le début · Maj + F5 : depuis cette diapositive"));

        // ---------------------------------------------- Disposition
        var grid = new Grid { Margin = new Thickness(12, 0, 12, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });

        var thumbsScroll = new ScrollViewer { Content = _thumbs, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        ScrollViewer.SetHorizontalScrollBarVisibility(_thumbs, ScrollBarVisibility.Disabled);
        grid.Children.Add(thumbsScroll);
        _thumbs.SelectionChanged += (_, _) => { if (!_loading && _thumbs.SelectedIndex >= 0 && _thumbs.SelectedIndex != _slideIndex) GoToSlide(_thumbs.SelectedIndex); };

        var center = new DockPanel { Margin = new Thickness(12, 0, 12, 0) };
        var notesPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        notesPanel.Children.Add(Ui.Text("Notes de l'orateur (visibles seulement par toi pendant la présentation, touche N)", "Label"));
        notesPanel.Children.Add(_notes);
        DockPanel.SetDock(notesPanel, Dock.Bottom);
        center.Children.Add(notesPanel);
        _stageRoot.Children.Add(_stage);
        _stageRoot.Children.Add(_ink);
        var frame = new Border { Child = _stageRoot, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 1, Opacity = 0.2 } };
        var viewbox = new Viewbox { Child = frame, Stretch = Stretch.Uniform, Margin = new Thickness(4) };
        var stageArea = new Grid { Children = { viewbox } };
        stageArea.SetResourceReference(Panel.BackgroundProperty, "PageBg");
        center.Children.Add(stageArea);
        Grid.SetColumn(center, 1);
        grid.Children.Add(center);

        var propsCard = new Border { Style = Ui.Style("Card"), Padding = new Thickness(14), Child = new ScrollViewer { Content = _props, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        Grid.SetColumn(propsCard, 2);
        grid.Children.Add(propsCard);
        Root.Children.Add(grid);

        // ---------------------------------------------- Événements
        _stage.Background = Brushes.Transparent;
        _stage.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == _stage) Select(null); };
        _stage.AllowDrop = true;
        _stage.DragOver += (_, e) => { e.Effects = ImageHelper.FirstImageFile(e.Data) != null ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        _stage.Drop += (_, e) =>
        {
            var f = ImageHelper.FirstImageFile(e.Data);
            if (f == null) return;
            var (bytes, ext) = ImageHelper.Prepare(File.ReadAllBytes(f), Path.GetExtension(f).ToLowerInvariant());
            AddImage(AppState.Store.ImportMedia(bytes, ext), null, e.GetPosition(_stage));
        };
        _ink.StrokeCollected += (_, _) => SaveInk();
        _ink.StrokeErased += (_, _) => SaveInk();
        _notes.TextChanged += (_, _) => { if (_loading) return; CurrentSlide.Notes = _notes.Text; Changed(false); };
        PreviewKeyDown += OnKey;
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(); };

        UpdateInkMode();
        RefreshThumbs();
        GoToSlide(0);
        StatusText.Text = Ui.Plural(_show.Slides.Count, "diapositive");
    }

    public void OnLeave() => Save();

    private void Save()
    {
        _saveTimer.Stop();
        if (!_dirty) return;
        AppState.Store.SaveSlideshow(_show);
        AppState.Store.Touch(_item);
        _dirty = false;
        StatusText.Text = $"{Ui.Plural(_show.Slides.Count, "diapositive")} · enregistré";
    }

    private void Changed(bool redraw)
    {
        _dirty = true;
        StatusText.Text = "Modifications non enregistrées";
        _saveTimer.Stop();
        _saveTimer.Start();
        if (redraw) { RenderStage(); RefreshThumb(_slideIndex); }
    }

    // ---------------------------------------------- Annuler / rétablir (instantanés)

    private void PushUndo()
    {
        _undo.Push(JsonSerializer.Serialize(_show, Json.File));
        if (_undo.Count > 60) { var keep = _undo.Take(60).Reverse().ToList(); _undo.Clear(); foreach (var k in keep) _undo.Push(k); }
        _redo.Clear();
    }

    private void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(JsonSerializer.Serialize(_show, Json.File));
        Restore(_undo.Pop());
    }

    private void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(JsonSerializer.Serialize(_show, Json.File));
        Restore(_redo.Pop());
    }

    private void Restore(string json)
    {
        var selectedId = _selected?.Id;
        _show = JsonSerializer.Deserialize<Slideshow>(json, Json.File)!;
        _slideIndex = Math.Clamp(_slideIndex, 0, _show.Slides.Count - 1);
        _selected = CurrentSlide.Elements.FirstOrDefault(e => e.Id == selectedId);
        RefreshThumbs();
        GoToSlide(_slideIndex);
        Changed(false);
    }

    // ---------------------------------------------- Clavier

    private void OnKey(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var inText = e.OriginalSource is TextBox;
        if (e.Key == Key.F5) { Present(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? _slideIndex : 0); e.Handled = true; return; }
        if (ctrl && e.Key == Key.S) { _dirty = true; Save(); e.Handled = true; return; }
        if (inText) return;
        if (ctrl && e.Key == Key.Z) { Undo(); e.Handled = true; }
        else if (ctrl && e.Key == Key.Y) { Redo(); e.Handled = true; }
        else if (ctrl && e.Key == Key.C && _selected != null) { _clipboard = Clone(_selected); e.Handled = true; }
        else if (ctrl && e.Key == Key.D && _selected != null) { _clipboard = Clone(_selected); PasteElement(); e.Handled = true; }
        else if (ctrl && e.Key == Key.V)
        {
            if (ImageHelper.FromClipboard() is { } img) AddImage(AppState.Store.ImportMedia(img.Bytes, img.Extension), null, null);
            else if (_clipboard != null) PasteElement();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && _selected != null) { DeleteSelected(); e.Handled = true; }
        else if (_selected != null && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 20 : 4;
            PushUndo();
            if (e.Key == Key.Left) _selected.X -= step;
            if (e.Key == Key.Right) _selected.X += step;
            if (e.Key == Key.Up) _selected.Y -= step;
            if (e.Key == Key.Down) _selected.Y += step;
            Changed(true);
            e.Handled = true;
        }
        else if (_selected == null && e.Key is Key.PageDown) { GoToSlide(Math.Min(_slideIndex + 1, _show.Slides.Count - 1)); e.Handled = true; }
        else if (_selected == null && e.Key is Key.PageUp) { GoToSlide(Math.Max(_slideIndex - 1, 0)); e.Handled = true; }
    }

    private static SlideElement Clone(SlideElement e)
    {
        var c = JsonSerializer.Deserialize<SlideElement>(JsonSerializer.Serialize(e, Json.File), Json.File)!;
        c.Id = NewId.Create();
        return c;
    }

    private void PasteElement()
    {
        if (_clipboard == null) return;
        PushUndo();
        var c = Clone(_clipboard);
        c.X += 24; c.Y += 24;
        CurrentSlide.Elements.Add(c);
        _selected = c;
        Changed(true);
        ShowProps();
    }

    // ---------------------------------------------- Diapositives

    private void RefreshThumbs()
    {
        _loading = true;
        _thumbs.Items.Clear();
        for (int i = 0; i < _show.Slides.Count; i++) _thumbs.Items.Add(ThumbItem(i));
        _thumbs.SelectedIndex = Math.Clamp(_slideIndex, 0, _show.Slides.Count - 1);
        _loading = false;
        StatusText.Text = Ui.Plural(_show.Slides.Count, "diapositive");
    }

    private ListBoxItem ThumbItem(int i)
    {
        var sp = new DockPanel { Margin = new Thickness(0, 2, 0, 6) };
        var num = Ui.Text((i + 1).ToString(), "Muted");
        num.Width = 18;
        num.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(num, Dock.Left);
        sp.Children.Add(num);
        var border = new Border { BorderThickness = new Thickness(1), Child = SlideRenderer.Thumbnail(_show.Slides[i], Theme, 140) };
        border.SetResourceReference(Border.BorderBrushProperty, "Line");
        sp.Children.Add(border);
        var li = new ListBoxItem { Content = sp, Padding = new Thickness(4) };
        var m = new ContextMenu();
        void Add(string h, Action a) { var mi = new MenuItem { Header = h }; mi.Click += (_, _) => a(); m.Items.Add(mi); }
        var index = i;
        Add("Dupliquer", () => { GoToSlide(index); DuplicateSlide(); });
        Add("Monter", () => MoveSlide(index, -1));
        Add("Descendre", () => MoveSlide(index, 1));
        Add("Présenter à partir d'ici", () => Present(index));
        m.Items.Add(new Separator());
        Add("Supprimer", () => { GoToSlide(index); DeleteSlide(); });
        li.ContextMenu = m;
        return li;
    }

    private void RefreshThumb(int i)
    {
        if (i < 0 || i >= _thumbs.Items.Count) return;
        _loading = true;
        var selected = _thumbs.SelectedIndex;
        _thumbs.Items[i] = ThumbItem(i);
        _thumbs.SelectedIndex = selected;
        _loading = false;
    }

    private void GoToSlide(int i)
    {
        if (_show.Slides.Count == 0) return;
        if (_slideIndex != i) _selected = null;
        _slideIndex = Math.Clamp(i, 0, _show.Slides.Count - 1);
        _loading = true;
        _thumbs.SelectedIndex = _slideIndex;
        _notes.Text = CurrentSlide.Notes;
        _loading = false;
        _ink.Strokes = SlideRenderer.LoadInk(CurrentSlide) ?? new System.Windows.Ink.StrokeCollection();
        RenderStage();
        ShowProps();
    }

    private ContextMenu LayoutMenu(FrameworkElement target)
    {
        var m = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        foreach (var (layout, name) in new[]
                 {
                     (SlideLayout.TitleAndContent, "Titre et liste"), (SlideLayout.TitleAndImage, "Titre, texte et image"), (SlideLayout.TwoColumns, "Deux colonnes"),
                     (SlideLayout.SectionHeader, "Titre de partie"), (SlideLayout.TitleSlide, "Diapositive de titre"), (SlideLayout.Blank, "Vide")
                 })
        {
            var mi = new MenuItem { Header = name };
            var l = layout;
            mi.Click += (_, _) => AddSlide(l);
            m.Items.Add(mi);
        }
        return m;
    }

    private void AddSlide(SlideLayout layout)
    {
        PushUndo();
        var s = SlideshowFactory.CreateSlide(layout);
        _show.Slides.Insert(_slideIndex + 1, s);
        _slideIndex++;
        RefreshThumbs();
        GoToSlide(_slideIndex);
        Changed(false);
    }

    private void DuplicateSlide()
    {
        PushUndo();
        var json = JsonSerializer.Serialize(CurrentSlide, Json.File);
        var copy = JsonSerializer.Deserialize<Slide>(json, Json.File)!;
        copy.Id = NewId.Create();
        foreach (var e in copy.Elements) e.Id = NewId.Create();
        _show.Slides.Insert(_slideIndex + 1, copy);
        _slideIndex++;
        RefreshThumbs();
        GoToSlide(_slideIndex);
        Changed(false);
    }

    private void DeleteSlide()
    {
        if (_show.Slides.Count <= 1) { DialogWindow.Info("Supprimer", "Un diaporama doit garder au moins une diapositive."); return; }
        PushUndo();
        _show.Slides.RemoveAt(_slideIndex);
        _slideIndex = Math.Min(_slideIndex, _show.Slides.Count - 1);
        RefreshThumbs();
        GoToSlide(_slideIndex);
        Changed(false);
    }

    private void MoveSlide(int i, int delta)
    {
        var j = i + delta;
        if (j < 0 || j >= _show.Slides.Count) return;
        PushUndo();
        (_show.Slides[i], _show.Slides[j]) = (_show.Slides[j], _show.Slides[i]);
        _slideIndex = j;
        RefreshThumbs();
        GoToSlide(j);
        Changed(false);
    }

    // ---------------------------------------------- Scène et sélection

    private void RenderStage()
    {
        _stage.Children.Clear();
        _stageRoot.Background = SlideRenderer.Background(CurrentSlide, Theme);
        _stage.Children.Add(SlideRenderer.Decoration(Theme));
        foreach (var e in CurrentSlide.Elements)
            _stage.Children.Add(new ElementHost(this, e, e == _selected));
    }

    internal void Select(SlideElement? e)
    {
        if (_selected == e) return;
        _selected = e;
        RenderStage();
        ShowProps();
    }

    private void EditSelected(Action<SlideElement> edit)
    {
        if (_selected == null) { DialogWindow.Info("Aucun élément sélectionné", "Clique d'abord sur une zone de texte ou une forme de la diapositive."); return; }
        PushUndo();
        edit(_selected);
        Changed(true);
        ShowProps();
    }

    private void DeleteSelected()
    {
        if (_selected == null) return;
        PushUndo();
        CurrentSlide.Elements.Remove(_selected);
        _selected = null;
        Changed(true);
        ShowProps();
    }

    internal void BeginChange() => PushUndo();
    internal SlideTheme ThemeForHosts => Theme;
    internal ElementHost? HostFor(SlideElement e) => _stage.Children.OfType<ElementHost>().FirstOrDefault(h => h.Element == e);
    internal void EndChange() { Changed(true); ShowProps(); }
    internal Canvas Stage => _stage;

    private void AddText(bool title)
    {
        PushUndo();
        var e = title
            ? SlideshowFactory.Text(true, "Titre", 80, 50, 1120, 110, 48, TextAlign.Left, bold: true)
            : SlideshowFactory.Text(false, "Ton texte ici", 160, 260, 600, 120, 28, TextAlign.Left);
        CurrentSlide.Elements.Add(e);
        _selected = e;
        Changed(true);
        ShowProps();
        Dispatcher.BeginInvoke(() => _stage.Children.OfType<ElementHost>().FirstOrDefault(h => h.Element == e)?.StartEditing(), DispatcherPriority.Input);
    }

    private ContextMenu ShapeMenu(FrameworkElement target)
    {
        var m = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        foreach (var (kind, name) in new[] { (ShapeKind.Rectangle, "Rectangle"), (ShapeKind.RoundedRectangle, "Rectangle arrondi"), (ShapeKind.Ellipse, "Ellipse / cercle"), (ShapeKind.Triangle, "Triangle"), (ShapeKind.Arrow, "Flèche"), (ShapeKind.Line, "Ligne") })
        {
            var mi = new MenuItem { Header = name };
            var k = kind;
            mi.Click += (_, _) => AddShape(k);
            m.Items.Add(mi);
        }
        return m;
    }

    private void AddShape(ShapeKind kind)
    {
        PushUndo();
        var line = kind is ShapeKind.Arrow or ShapeKind.Line;
        var e = new SlideElement
        {
            Type = SlideElementType.Shape, Shape = kind, X = 480, Y = line ? 340 : 240, Width = line ? 320 : 300, Height = line ? 40 : 220,
            FillHex = line ? null : Theme.Accent, StrokeHex = line ? Theme.Accent : null, StrokeThickness = line ? 6 : 0
        };
        CurrentSlide.Elements.Add(e);
        _selected = e;
        Changed(true);
        ShowProps();
    }

    private void AddImageFromFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = ImageHelper.Filter };
        if (dlg.ShowDialog() != true) return;
        var (bytes, ext) = ImageHelper.Prepare(File.ReadAllBytes(dlg.FileName), Path.GetExtension(dlg.FileName).ToLowerInvariant());
        AddImage(AppState.Store.ImportMedia(bytes, ext), null, null);
    }

    private void AddOnlineImage()
    {
        if (!DocumentEditorView.OnlineCheck()) return;
        var title = CurrentSlide.Elements.FirstOrDefault(e => e.IsTitle)?.Text;
        var d = new ImageSearchDialog(title);
        if (d.ShowDialog() != true || d.ResultBytes == null) return;
        AddImage(AppState.Store.ImportMedia(d.ResultBytes, ".png"), d.ResultCredit, null);
    }

    private void AddImage(string file, string? caption, Point? at)
    {
        PushUndo();
        var bmp = ImageHelper.Load(AppState.Store.MediaPath(file), 400);
        double ratio = bmp == null || bmp.PixelHeight == 0 ? 0.75 : bmp.PixelHeight / (double)bmp.PixelWidth;
        // Si la diapositive a un cadre d'image (rectangle du modèle), l'image prend sa place
        var placeholder = CurrentSlide.Elements.FirstOrDefault(e => e.Type == SlideElementType.Shape && e.Shape == ShapeKind.RoundedRectangle && e.FillHex == "#22FFFFFF");
        SlideElement img;
        if (placeholder != null && at == null)
        {
            img = new SlideElement { Type = SlideElementType.Image, ImageFile = file, Caption = caption, X = placeholder.X, Y = placeholder.Y, Width = placeholder.Width, Height = placeholder.Height };
            CurrentSlide.Elements[CurrentSlide.Elements.IndexOf(placeholder)] = img;
        }
        else
        {
            double w = 560, h = w * ratio;
            if (h > 480) { h = 480; w = h / ratio; }
            var p = at ?? new Point(SlideRenderer.W / 2, SlideRenderer.H / 2);
            img = new SlideElement { Type = SlideElementType.Image, ImageFile = file, Caption = caption, X = p.X - w / 2, Y = p.Y - h / 2, Width = w, Height = h };
            CurrentSlide.Elements.Add(img);
        }
        _selected = img;
        Changed(true);
        ShowProps();
    }

    // ---------------------------------------------- Dessin

    private void UpdateInkMode()
    {
        var draw = _drawToggle.IsChecked == true;
        var erase = _eraseToggle.IsChecked == true;
        _ink.IsHitTestVisible = draw || erase;
        _ink.EditingMode = erase ? InkCanvasEditingMode.EraseByStroke : draw ? InkCanvasEditingMode.Ink : InkCanvasEditingMode.None;
        _ink.DefaultDrawingAttributes = new System.Windows.Ink.DrawingAttributes { Color = _inkColor, Width = 5, Height = 5, FitToCurve = true };
        _stage.IsHitTestVisible = !(draw || erase);
    }

    private void SaveInk()
    {
        CurrentSlide.Ink = SlideRenderer.SaveInk(_ink.Strokes);
        _dirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
        RefreshThumb(_slideIndex);
    }

    // ---------------------------------------------- Thème et transitions

    private ContextMenu ThemeMenu(FrameworkElement target)
    {
        var m = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        var panel = new StackPanel { Width = 520, Margin = new Thickness(4) };
        WrapPanel? wrap = null;
        string? category = null;
        foreach (var t in SlideThemes.All)
        {
            if (t.Category != category)
            {
                category = t.Category;
                var h = Ui.Text(category, "Label");
                h.Margin = new Thickness(4, panel.Children.Count == 0 ? 0 : 8, 0, 2);
                panel.Children.Add(h);
                wrap = new WrapPanel();
                panel.Children.Add(wrap);
            }
            var preview = new Slide();
            preview.Elements.Add(SlideshowFactory.Text(true, t.Name, 80, 220, 1120, 160, 110, TextAlign.Center, bold: true));
            preview.Elements.Add(new SlideElement { Type = SlideElementType.Shape, Shape = ShapeKind.Rectangle, X = 440, Y = 440, Width = 400, Height = 24, FillHex = t.Accent });
            var thumb = new Border { Child = SlideRenderer.Thumbnail(preview, t, 116), BorderThickness = new Thickness(t.Id == _show.ThemeId ? 3 : 1), Margin = new Thickness(3), Cursor = Cursors.Hand, ToolTip = t.Name };
            thumb.SetResourceReference(Border.BorderBrushProperty, t.Id == _show.ThemeId ? "Accent" : "Line");
            var theme = t;
            thumb.MouseLeftButtonUp += (_, _) =>
            {
                m.IsOpen = false;
                PushUndo();
                _show.ThemeId = theme.Id;
                foreach (var s in _show.Slides) s.BackgroundHex = null;
                RefreshThumbs();
                RenderStage();
                Changed(false);
            };
            wrap!.Children.Add(thumb);
        }
        m.Items.Add(new MenuItem { Header = new ScrollViewer { Content = panel, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, StaysOpenOnClick = true });
        return m;
    }

    private ContextMenu TransitionMenu(FrameworkElement target)
    {
        var m = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        foreach (var (t, name) in new[] { (SlideTransition.None, "Aucune"), (SlideTransition.Fade, "Fondu"), (SlideTransition.Slide, "Glissement"), (SlideTransition.Zoom, "Zoom") })
        {
            var mi = new MenuItem { Header = name, IsCheckable = true, IsChecked = _show.Transition == t };
            var tr = t;
            mi.Click += (_, _) => { _show.Transition = tr; Changed(false); };
            m.Items.Add(mi);
        }
        return m;
    }

    // ---------------------------------------------- Panneau de propriétés

    private TextBox? _propText;

    /// <summary>Place le curseur dans le champ « Texte » de l'élément sélectionné.</summary>
    internal void FocusTextProperty()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_propText == null) return;
            var ok = _propText.Focus();
            Keyboard.Focus(_propText);
            _propText.SelectAll();
            if (AppState.SelfTest) ElementHost.Diagnostics.Add($"focus champ Texte : {ok}, focus réel : {Keyboard.FocusedElement?.GetType().Name}");
        }, DispatcherPriority.Input);
    }

    private void ShowProps()
    {
        _props.Children.Clear();
        _propText = null;
        if (_selected == null)
        {
            _props.Children.Add(Ui.Text("Diapositive " + (_slideIndex + 1), "H3"));
            var hint = Ui.Text("Clique sur un élément pour le modifier. Double-clique sur un texte pour l'écrire dans le champ « Texte » de ce panneau. Fais glisser pour déplacer, et utilise la poignée en bas à droite pour redimensionner.", "Muted");
            hint.Margin = new Thickness(0, 6, 0, 14);
            _props.Children.Add(hint);
            _props.Children.Add(Ui.Text("Thème : " + Theme.Name, "Muted"));
            _props.Children.Add(Ui.Text("Transition : " + _show.Transition switch { SlideTransition.Fade => "fondu", SlideTransition.Slide => "glissement", SlideTransition.Zoom => "zoom", _ => "aucune" }, "Muted"));
            var words = CurrentSlide.Elements.Where(e => e.Type == SlideElementType.Text).Sum(e => DocumentIO.WordCount(e.Text));
            if (words > 60)
            {
                var warn = Ui.Text($"Cette diapositive contient {words} mots. Essaie de garder des mots-clés et de dire le reste à l'oral.", "Muted");
                warn.SetResourceReference(TextBlock.ForegroundProperty, "Warn");
                warn.Margin = new Thickness(0, 12, 0, 0);
                _props.Children.Add(warn);
            }
            return;
        }
        var e = _selected;
        _props.Children.Add(Ui.Text(e.Type switch { SlideElementType.Text => e.IsTitle ? "Titre" : "Zone de texte", SlideElementType.Image => "Image", _ => "Forme" }, "H3"));

        if (e.Type == SlideElementType.Text)
        {
            _props.Children.Add(PropLabel("Texte"));
            var text = new TextBox { Text = e.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(6) };
            _propText = text;
            var textUndo = false;
            text.GotKeyboardFocus += (_, _) => textUndo = false;
            text.TextChanged += (_, _) =>
            {
                if (e.Text == text.Text) return;
                if (!textUndo) { PushUndo(); textUndo = true; }
                e.Text = text.Text; Changed(false); RenderStageSoon();
            };
            _props.Children.Add(text);
            _props.Children.Add(PropLabel("Taille du texte"));
            var size = new Slider { Minimum = 10, Maximum = 140, Value = e.FontSize, TickFrequency = 2, IsSnapToTickEnabled = true };
            var sizeText = Ui.Text($"{e.FontSize:0} pt", "Muted");
            size.PreviewMouseDown += (_, _) => PushUndo();
            size.ValueChanged += (_, a) => { e.FontSize = a.NewValue; sizeText.Text = $"{a.NewValue:0} pt"; Changed(false); RenderStageSoon(); };
            _props.Children.Add(size);
            _props.Children.Add(sizeText);
            var checks = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            checks.Children.Add(Check("Gras", e.Bold, v => e.Bold = v));
            checks.Children.Add(Check("Italique", e.Italic, v => e.Italic = v));
            checks.Children.Add(Check("Puces", e.Bullets, v => e.Bullets = v));
            checks.Children.Add(Check("C'est un titre", e.IsTitle, v => e.IsTitle = v));
            _props.Children.Add(checks);
            _props.Children.Add(PropLabel("Couleur"));
            _props.Children.Add(ColorRow(e.ColorHex, true, c => e.ColorHex = c, "Thème"));
        }
        else if (e.Type == SlideElementType.Shape)
        {
            var line = e.Shape is ShapeKind.Arrow or ShapeKind.Line;
            if (!line)
            {
                _props.Children.Add(PropLabel("Remplissage"));
                _props.Children.Add(ColorRow(e.FillHex, true, c => e.FillHex = c, "Aucun"));
            }
            _props.Children.Add(PropLabel(line ? "Couleur" : "Contour"));
            _props.Children.Add(ColorRow(e.StrokeHex, !line, c => { e.StrokeHex = c; if (c != null && e.StrokeThickness == 0) e.StrokeThickness = 3; }, "Aucun"));
            _props.Children.Add(PropLabel("Épaisseur du trait"));
            var th = new Slider { Minimum = 0, Maximum = 24, Value = e.StrokeThickness };
            th.PreviewMouseDown += (_, _) => PushUndo();
            th.ValueChanged += (_, a) => { e.StrokeThickness = a.NewValue; Changed(false); RenderStageSoon(); };
            _props.Children.Add(th);
        }
        else if (e.Type == SlideElementType.Image)
        {
            _props.Children.Add(PropLabel("Légende / source"));
            var cap = new TextBox { Text = e.Caption ?? "", TextWrapping = TextWrapping.Wrap, Padding = new Thickness(6) };
            var capUndo = false;
            cap.GotKeyboardFocus += (_, _) => capUndo = false;
            cap.TextChanged += (_, _) =>
            {
                if (!capUndo) { PushUndo(); capUndo = true; }
                e.Caption = cap.Text.Length == 0 ? null : cap.Text; Changed(false); RenderStageSoon();
            };
            _props.Children.Add(cap);
        }

        _props.Children.Add(PropLabel("Rotation"));
        var rot = new Slider { Minimum = -180, Maximum = 180, Value = e.Rotation, TickFrequency = 15, IsSnapToTickEnabled = true };
        rot.PreviewMouseDown += (_, _) => PushUndo();
        rot.ValueChanged += (_, a) => { e.Rotation = a.NewValue; Changed(false); RenderStageSoon(); };
        _props.Children.Add(rot);

        _props.Children.Add(PropLabel("Disposition"));
        var order = new WrapPanel();
        order.Children.Add(Ui.Button("Premier plan", (_, _) => { PushUndo(); CurrentSlide.Elements.Remove(e); CurrentSlide.Elements.Add(e); Changed(true); }, "SubtleButton", Icons.Up));
        order.Children.Add(Ui.Button("Arrière-plan", (_, _) => { PushUndo(); CurrentSlide.Elements.Remove(e); CurrentSlide.Elements.Insert(0, e); Changed(true); }, "SubtleButton", Icons.Down));
        order.Children.Add(Ui.Button("Centrer", (_, _) => { PushUndo(); e.X = (SlideRenderer.W - e.Width) / 2; Changed(true); }, "SubtleButton", Icons.AlignCenter));
        order.Children.Add(Ui.Button("Dupliquer", (_, _) => { _clipboard = Clone(e); PasteElement(); }, "SubtleButton", Icons.Copy));
        _props.Children.Add(order);
        var del = Ui.Button("Supprimer", (_, _) => DeleteSelected(), "SecondaryButton", Icons.Delete);
        del.Margin = new Thickness(0, 12, 0, 0);
        del.HorizontalAlignment = HorizontalAlignment.Left;
        _props.Children.Add(del);
    }

    private DispatcherOperation? _pendingRender;
    private void RenderStageSoon()
    {
        if (_pendingRender != null) return;
        _pendingRender = Dispatcher.BeginInvoke(() =>
        {
            _pendingRender = null;
            RenderStage();
            RefreshThumb(_slideIndex);
        }, DispatcherPriority.Background);
    }

    private static TextBlock PropLabel(string t)
    {
        var l = Ui.Text(t, "Label");
        l.Margin = new Thickness(0, 14, 0, 6);
        return l;
    }

    private CheckBox Check(string label, bool value, Action<bool> set)
    {
        var cb = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 0, 12, 6) };
        cb.Click += (_, _) => { PushUndo(); set(cb.IsChecked == true); Changed(true); };
        return cb;
    }

    private WrapPanel ColorRow(string? current, bool allowNone, Action<string?> set, string noneLabel)
    {
        var wrap = new WrapPanel();
        var colors = new List<string> { Theme.TitleColor, Theme.TextColor, Theme.Accent, "#FFFFFF", "#1B2230", "#C43C3C", "#D9731A", "#E0B000", "#1F8A52", "#1B8FA6", "#2F62D9", "#7A4FC4" };
        foreach (var hex in colors.Distinct())
        {
            var b = new Button { Width = 24, Height = 24, Margin = new Thickness(0, 0, 4, 4), Background = Ui.BrushFromHex(hex), BorderThickness = new Thickness(string.Equals(current, hex, StringComparison.OrdinalIgnoreCase) ? 3 : 1), Cursor = Cursors.Hand, ToolTip = hex };
            b.SetResourceReference(BorderBrushProperty, string.Equals(current, hex, StringComparison.OrdinalIgnoreCase) ? "Accent" : "Line");
            var h = hex;
            b.Click += (_, _) => { PushUndo(); set(h); Changed(true); ShowProps(); };
            wrap.Children.Add(b);
        }
        if (allowNone)
        {
            var none = Ui.Button(noneLabel, (_, _) => { PushUndo(); set(null); Changed(true); ShowProps(); }, "SubtleButton");
            none.Padding = new Thickness(6, 2, 6, 2);
            wrap.Children.Add(none);
        }
        return wrap;
    }

    // ---------------------------------------------- Présentation et fiches

    private void Present(int start)
    {
        Save();
        _dirty = true;
        Save();
        var w = new PresenterWindow(_show, start);
        w.ShowDialog();
    }

    private void MakeCards()
    {
        Save();
        var cards = Converters.CardsFromSlideshow(_show);
        if (cards.Count == 0)
        {
            DialogWindow.Info("Aucune fiche trouvée", "Chaque diapositive doit avoir un titre et du texte pour devenir une fiche.");
            return;
        }
        CardsPreviewDialog.CreateDeckFrom(cards, _item);
    }
}

/// <summary>Un élément de diapositive dans l'éditeur : déplacement, redimensionnement, édition du texte.</summary>
internal sealed class ElementHost : Grid
{
    private readonly SlideEditorView _editor;
    public SlideElement Element { get; }
    /// <summary>Journal des clics (utilisé par l'autotest).</summary>
    internal static readonly List<string> Diagnostics = new();
    private Point _dragStart;
    private double _origX, _origY, _origW, _origH;
    private bool _dragging, _resizing, _moved;
    private readonly Border _outline;

    public ElementHost(SlideEditorView editor, SlideElement e, bool selected)
    {
        _editor = editor;
        Element = e;
        Width = Math.Max(8, e.Width);
        Height = Math.Max(8, e.Height);
        Canvas.SetLeft(this, e.X);
        Canvas.SetTop(this, e.Y);
        if (e.Rotation != 0)
        {
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = new RotateTransform(e.Rotation);
        }
        Background = Brushes.Transparent;
        Cursor = Cursors.SizeAll;
        Children.Add(SlideRenderer.ElementVisual(e, editor.ThemeForHosts));
        _outline = new Border
        {
            BorderThickness = new Thickness(selected ? 2.5 : 1),
            BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(0x2F, 0x62, 0xD9)) : Brushes.Transparent,
            Margin = new Thickness(-4),
            IsHitTestVisible = false
        };
        Children.Add(_outline);
        Border? handle = null;
        if (selected)
        {
            handle = new Border
            {
                Width = 18, Height = 18, CornerRadius = new CornerRadius(4), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0x62, 0xD9)),
                BorderThickness = new Thickness(2.5), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -11, -11), Cursor = Cursors.SizeNWSE, ToolTip = "Redimensionner (Maj : garder les proportions)"
            };
            Children.Add(handle);
        }
        MouseEnter += (_, _) => { if (!selected) _outline.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0x2F, 0x62, 0xD9)); };
        MouseLeave += (_, _) => { if (!selected) _outline.BorderBrush = Brushes.Transparent; };
        PreviewMouseLeftButtonDown += (_, ev) =>
        {
            if (ev.OriginalSource is TextBox || (ev.OriginalSource as DependencyObject) is { } src && IsInside(src, typeof(TextBox))) return;
            if (handle != null && ev.OriginalSource is DependencyObject d && (d == handle || IsDescendant(d, handle)))
            {
                StartDrag(ev, resize: true);
                ev.Handled = true;
                return;
            }
            if (AppState.SelfTest) Diagnostics.Add($"clic n°{ev.ClickCount} (sélectionné : {selected})");
            if (ev.ClickCount == 2 && e.Type == SlideElementType.Text) { StartEditing(); ev.Handled = true; return; }
            if (!selected)
            {
                _editor.Select(e);
                _editor.HostFor(e)?.StartDrag(ev, resize: false);
            }
            else StartDrag(ev, resize: false);
            ev.Handled = true;
        };
        MouseMove += OnMove;
        MouseLeftButtonUp += (_, _) => EndDrag();
        LostMouseCapture += (_, _) => EndDrag();
    }

    private static bool IsDescendant(DependencyObject d, DependencyObject ancestor)
    {
        while (d != null)
        {
            if (d == ancestor) return true;
            d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    private static bool IsInside(DependencyObject d, Type type)
    {
        while (d != null)
        {
            if (type.IsInstanceOfType(d)) return true;
            d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    internal void StartDrag(MouseButtonEventArgs ev, bool resize)
    {
        _dragStart = ev.GetPosition(_editor.Stage);
        _origX = Element.X; _origY = Element.Y; _origW = Element.Width; _origH = Element.Height;
        _dragging = !resize;
        _resizing = resize;
        _moved = false;
        CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs ev)
    {
        if (!_dragging && !_resizing) return;
        var p = ev.GetPosition(_editor.Stage);
        var dx = p.X - _dragStart.X;
        var dy = p.Y - _dragStart.Y;
        if (!_moved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3) return;
        if (!_moved) { _editor.BeginChange(); _moved = true; }
        if (_dragging)
        {
            Element.X = Snap(_origX + dx);
            Element.Y = Snap(_origY + dy);
            Canvas.SetLeft(this, Element.X);
            Canvas.SetTop(this, Element.Y);
        }
        else
        {
            var w = Math.Max(20, _origW + dx);
            var h = Math.Max(Element.Shape is ShapeKind.Arrow or ShapeKind.Line && Element.Type == SlideElementType.Shape ? 10 : 20, _origH + dy);
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Element.Type == SlideElementType.Image)
                h = w * (_origH / Math.Max(1, _origW));
            Element.Width = Snap(w);
            Element.Height = Snap(h);
            Width = Element.Width;
            Height = Element.Height;
        }
    }

    private static double Snap(double v) => Math.Round(v / 4) * 4;

    private void EndDrag()
    {
        if (!_dragging && !_resizing) return;
        _dragging = _resizing = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (_moved) _editor.EndChange();
    }

    /// <summary>Double-clic sur un texte : on l'écrit dans le panneau de droite, la diapositive se met à jour en direct.</summary>
    public void StartEditing()
    {
        if (Element.Type != SlideElementType.Text) return;
        _editor.Select(Element);
        _editor.FocusTextProperty();
    }
}
