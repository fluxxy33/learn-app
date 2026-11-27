using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Views;

/// <summary>Barre d'édition commune : retour, titre, onglets du ruban compact et état d'enregistrement.</summary>
public class EditorShell : UserControl
{
    protected readonly DockPanel Root = new();
    protected readonly TextBlock TitleText = Ui.Text("", "H3");
    protected readonly TextBlock StatusText = Ui.Text("", "Muted");
    protected readonly StackPanel TabsPanel = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 6, 0, 0) };
    protected readonly ContentControl RibbonHost = new();
    protected readonly StackPanel RightActions = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly Dictionary<string, FrameworkElement> _ribbons = new();

    protected EditorShell(string title, string kindGlyph)
    {
        var top = new StackPanel { Margin = new Thickness(12, 10, 12, 0) };
        var titleRow = new DockPanel();
        var back = new Button { Style = Ui.Style("IconButton"), Content = Icons.Back, ToolTip = "Retour (Alt + ←)" };
        back.Click += (_, _) => AppState.Main.GoBack();
        DockPanel.SetDock(back, Dock.Left);
        titleRow.Children.Add(back);
        DockPanel.SetDock(RightActions, Dock.Right);
        titleRow.Children.Add(RightActions);
        var kind = Ui.Icon(kindGlyph, 15);
        kind.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        kind.Margin = new Thickness(6, 0, 8, 0);
        DockPanel.SetDock(kind, Dock.Left);
        titleRow.Children.Add(kind);
        TitleText.Text = title;
        TitleText.VerticalAlignment = VerticalAlignment.Center;
        TitleText.TextTrimming = TextTrimming.CharacterEllipsis;
        TitleText.TextWrapping = TextWrapping.NoWrap;
        TitleText.Cursor = Cursors.Hand;
        TitleText.ToolTip = "Cliquer pour renommer";
        StatusText.Margin = new Thickness(12, 0, 0, 0);
        StatusText.VerticalAlignment = VerticalAlignment.Center;
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal };
        titleStack.Children.Add(TitleText);
        titleStack.Children.Add(StatusText);
        titleRow.Children.Add(titleStack);
        top.Children.Add(titleRow);
        top.Children.Add(TabsPanel);
        var ribbonBorder = new Border { Style = Ui.Style("Toolbar"), Margin = new Thickness(0, 2, 0, 8), Child = RibbonHost, MinHeight = 42 };
        top.Children.Add(ribbonBorder);
        DockPanel.SetDock(top, Dock.Top);
        Root.Children.Add(top);
        Content = Root;
    }

    protected void AddRibbonTab(string name, FrameworkElement content, bool selected = false)
    {
        _ribbons[name] = content;
        var rb = new RadioButton { Style = Ui.Style("RibbonTab"), Content = name, GroupName = "ribbon" + GetHashCode(), IsChecked = selected };
        rb.Checked += (_, _) => RibbonHost.Content = content;
        TabsPanel.Children.Add(rb);
        if (selected) RibbonHost.Content = content;
    }

    protected static WrapPanel RibbonRow(params UIElement[] children)
    {
        var w = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        foreach (var c in children)
        {
            if (c is FrameworkElement fe && fe.Margin == default) fe.Margin = new Thickness(1, 2, 1, 2);
            w.Children.Add(c);
        }
        return w;
    }

    protected static StackPanel Group(params UIElement[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var c in children) sp.Children.Add(c);
        return sp;
    }

    /// <summary>Petit sélecteur de couleur en menu.</summary>
    protected static ContextMenu ColorMenu(FrameworkElement target, Action<Color?> pick, bool allowNone, string noneLabel = "Aucune")
    {
        var colors = new[]
        {
            "#1B1B1B", "#5F6878", "#C43C3C", "#D9731A", "#B8860B", "#1F8A52", "#1B8FA6", "#2F62D9", "#7A4FC4", "#C2417A",
            "#FFFFFF", "#FDE68A", "#FECACA", "#BBF7D0", "#BFDBFE", "#E9D5FF", "#FBCFE8", "#FED7AA"
        };
        var m = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        var grid = new UniformGrid { Columns = 6, Margin = new Thickness(4) };
        foreach (var hex in colors)
        {
            var b = new Button
            {
                Width = 26, Height = 26, Margin = new Thickness(2), Background = Ui.BrushFromHex(hex), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), ToolTip = hex,
                Template = SwatchTemplate()
            };
            var c = Ui.ColorFromHex(hex, Colors.Black);
            b.Click += (_, _) => { m.IsOpen = false; pick(c); };
            grid.Children.Add(b);
        }
        m.Items.Add(new MenuItem { Header = grid, StaysOpenOnClick = true });
        if (allowNone)
        {
            var none = new MenuItem { Header = noneLabel };
            none.Click += (_, _) => pick(null);
            m.Items.Add(none);
        }
        return m;
    }

    private static ControlTemplate SwatchTemplate()
    {
        var t = new ControlTemplate(typeof(Button));
        var f = new FrameworkElementFactory(typeof(Border));
        f.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        f.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        f.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        f.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        t.VisualTree = f;
        return t;
    }

    protected static ComboBox Combo(IEnumerable<object> items, double width, string tooltip, bool editable = false)
    {
        var cb = new ComboBox { Width = width, ToolTip = tooltip, IsEditable = editable, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
        foreach (var i in items) cb.Items.Add(i);
        return cb;
    }
}

/// <summary>Éditeur de document : texte mis en forme, images, tableaux, dessins, encadrés.</summary>
public sealed class DocumentEditorView : EditorShell, IPage, IEditor
{
    public string NavKey => "library";
    private readonly LibraryItem _item;
    private readonly RichTextBox _rtb;
    private readonly DispatcherTimer _autosave = new() { Interval = TimeSpan.FromSeconds(20) };
    private bool _dirty;
    private bool _updatingUi;
    private readonly ComboBox _fontFamily;
    private readonly ComboBox _fontSize;
    private readonly ComboBox _paraStyle;
    private readonly ToggleButton _bold = Ui.ToolToggle(Icons.Bold, null, "Gras (Ctrl + B)");
    private readonly ToggleButton _italic = Ui.ToolToggle(Icons.Italic, null, "Italique (Ctrl + I)");
    private readonly ToggleButton _underline = Ui.ToolToggle(Icons.Underline, null, "Souligné (Ctrl + U)");
    private readonly TextBlock _wordCount = Ui.Text("", "Muted");

    private static readonly string[] Fonts = { "Segoe UI", "Calibri", "Arial", "Verdana", "Georgia", "Times New Roman", "Cambria", "Garamond", "Consolas", "Comic Sans MS", "Segoe Print" };
    private static readonly double[] Sizes = { 9, 10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24, 28, 32, 36, 44, 54, 72 };

    public DocumentEditorView(LibraryItem item) : base(item.Title, Icons.Document)
    {
        _item = item;
        TitleText.MouseLeftButtonUp += (_, _) => Rename();

        _rtb = new RichTextBox
        {
            AcceptsTab = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(72, 56, 72, 72),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            SpellCheck = { IsEnabled = true },
            Language = XmlLanguage.GetLanguage("fr-FR"),
            AllowDrop = true,
            FontSize = AppState.Settings.DefaultFontSize,
        };
        _rtb.Template = (ControlTemplate)XamlReader.Parse(
            "<ControlTemplate TargetType=\"RichTextBox\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
            "<ScrollViewer x:Name=\"PART_ContentHost\" Padding=\"{TemplateBinding Padding}\" Background=\"Transparent\" Focusable=\"False\" />" +
            "</ControlTemplate>");
        _rtb.Foreground = new SolidColorBrush(DocStyles.TextColor);
        _rtb.CaretBrush = Brushes.Black;
        _rtb.SelectionBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0x62, 0xD9));

        try { _rtb.Document = DocumentIO.Load(AppState.Store, item); }
        catch (Exception ex)
        {
            AppState.LogError(ex);
            _rtb.Document = DocumentIO.NewDocument(AppState.Settings);
            DialogWindow.Info("Document illisible", "Ce document n'a pas pu être ouvert : " + ex.Message);
        }

        // ------------------------------------------------- Ruban : Accueil
        _fontFamily = Combo(Fonts.Cast<object>(), 140, "Police");
        _fontSize = Combo(Sizes.Select(s => (object)s.ToString()), 64, "Taille", editable: true);
        _paraStyle = Combo(new object[] { "Texte", "Titre 1", "Titre 2", "Titre 3" }, 96, "Style du paragraphe (les titres deviennent des questions de fiches)");
        _fontFamily.SelectionChanged += (_, _) => { if (!_updatingUi && _fontFamily.SelectedItem is string f) ApplyFormat(TextElement.FontFamilyProperty, new FontFamily(f)); };
        _fontSize.SelectionChanged += (_, _) => { if (!_updatingUi && _fontSize.SelectedItem is string s && double.TryParse(s, out var v)) ApplyFormat(TextElement.FontSizeProperty, v); };
        _fontSize.LostKeyboardFocus += (_, _) => { if (!_updatingUi && double.TryParse(_fontSize.Text, out var v) && v is >= 4 and <= 200) ApplyFormat(TextElement.FontSizeProperty, v); };
        _paraStyle.SelectionChanged += (_, _) => { if (!_updatingUi) SetHeading(_paraStyle.SelectedIndex); };
        _bold.Click += (_, _) => { EditingCommands.ToggleBold.Execute(null, _rtb); _rtb.Focus(); };
        _italic.Click += (_, _) => { EditingCommands.ToggleItalic.Execute(null, _rtb); _rtb.Focus(); };
        _underline.Click += (_, _) => { EditingCommands.ToggleUnderline.Execute(null, _rtb); _rtb.Focus(); };

        Button colorBtn = null!, highlightBtn = null!;
        colorBtn = Ui.Tool(Icons.FontColor, null, "Couleur du texte", (_, _) =>
            ColorMenu(colorBtn, c => ApplyFormat(TextElement.ForegroundProperty, new SolidColorBrush(c ?? DocStyles.TextColor)), true, "Couleur automatique").IsOpen = true);
        highlightBtn = Ui.Tool(Icons.Highlight, null, "Surligner", (_, _) =>
            ColorMenu(highlightBtn, c => ApplyFormat(TextElement.BackgroundProperty, c == null ? null : new SolidColorBrush(c.Value)), true, "Pas de surlignage").IsOpen = true);

        var home = RibbonRow(
            Ui.Tool(Icons.Undo, null, "Annuler (Ctrl + Z)", (_, _) => _rtb.Undo()),
            Ui.Tool(Icons.Redo, null, "Rétablir (Ctrl + Y)", (_, _) => _rtb.Redo()),
            Ui.Separator(),
            _paraStyle, _fontFamily, _fontSize,
            Ui.Separator(),
            _bold, _italic, _underline, colorBtn, highlightBtn,
            Ui.Separator(),
            Ui.Tool(Icons.Bullets, null, "Liste à puces", (_, _) => { EditingCommands.ToggleBullets.Execute(null, _rtb); _rtb.Focus(); }),
            Ui.Tool(Icons.Numbers, null, "Liste numérotée", (_, _) => { EditingCommands.ToggleNumbering.Execute(null, _rtb); _rtb.Focus(); }),
            Ui.Tool(Icons.AlignLeft, null, "Aligner à gauche", (_, _) => { EditingCommands.AlignLeft.Execute(null, _rtb); _rtb.Focus(); }),
            Ui.Tool(Icons.AlignCenter, null, "Centrer", (_, _) => { EditingCommands.AlignCenter.Execute(null, _rtb); _rtb.Focus(); }),
            Ui.Tool(Icons.AlignRight, null, "Aligner à droite", (_, _) => { EditingCommands.AlignRight.Execute(null, _rtb); _rtb.Focus(); }),
            Ui.Separator(),
            Ui.Tool(Icons.Remove, "Effacer la mise en forme", "Remettre le texte sélectionné en texte normal", (_, _) => ClearFormatting()));

        // ------------------------------------------------- Ruban : Insertion
        var insert = RibbonRow(
            Ui.Tool(Icons.Picture, "Image", "Insérer une image depuis le PC (ou glisser-déposer, ou Ctrl + V)", (_, _) => InsertImageFromFile()),
            Ui.Tool(Icons.Globe, "Image en ligne", "Chercher une image libre de droits (Wikimedia Commons)", (_, _) => InsertOnlineImage()),
            Ui.Tool(Icons.Pen, "Dessin / schéma", "Dessiner à main levée, des formes et des flèches", (_, _) => InsertDrawing()),
            Ui.Separator(),
            Ui.Tool(Icons.Table, "Tableau", "Insérer un tableau", (s, _) => TableMenu((FrameworkElement)s).IsOpen = true),
            Ui.Tool(Icons.Lightbulb, "Encadré « À retenir »", "Un encadré coloré pour l'essentiel", (_, _) => InsertBox()),
            Ui.Tool(Icons.Remove, "Ligne", "Ligne de séparation", (_, _) => InsertSeparator()),
            Ui.Separator(),
            Ui.Tool(Icons.Dictionary, "Wikipédia", "Chercher un résumé sur Wikipédia et l'insérer avec sa source", (_, _) => InsertWikipedia()),
            Ui.Tool(Icons.Calendar, "Date", "Insérer la date du jour", (_, _) => InsertText(DateTime.Now.ToString("dddd d MMMM yyyy", new System.Globalization.CultureInfo("fr-FR")))));

        // ------------------------------------------------- Ruban : Réviser
        var review = RibbonRow(
            Ui.Tool(Icons.Cards, "Créer des fiches", "Les titres deviennent des questions, le texte qui suit la réponse. Les lignes « mot : définition » aussi.", (_, _) => MakeCards()),
            Ui.Tool(Icons.Quiz, "Créer un quiz", "Les mots en gras deviennent des textes à trous, les titres des QCM", (_, _) => MakeQuiz()),
            Ui.Separator(),
            Ui.Tool(Icons.Info, "Comment ça marche ?", "Explications", (_, _) => DialogWindow.Info("Transformer un cours en fiches",
                "• Chaque Titre 1, 2 ou 3 devient le recto d'une fiche, et le texte qui suit en devient le verso.\n" +
                "• Chaque ligne « Terme : définition » devient aussi une fiche.\n" +
                "• Pour les quiz, les mots en gras deviennent des textes à trous.\n\n" +
                "Tu peux relire et décocher les fiches avant de les créer.")));

        AddRibbonTab("Accueil", home, true);
        AddRibbonTab("Insertion", insert);
        AddRibbonTab("Réviser", review);

        // ------------------------------------------------- Page
        var page = new Border
        {
            Width = 860, Margin = new Thickness(0, 4, 0, 0), CornerRadius = new CornerRadius(4, 4, 0, 0),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 1, Opacity = 0.18 },
            Child = _rtb
        };
        page.SetResourceReference(Border.BackgroundProperty, "Paper");
        var status = new DockPanel { Margin = new Thickness(16, 4, 16, 6) };
        status.Children.Add(_wordCount);
        DockPanel.SetDock(status, Dock.Bottom);
        Root.Children.Add(status);
        var area = new Grid { Children = { page } };
        area.SetResourceReference(Panel.BackgroundProperty, "PageBg");
        Root.Children.Add(area);

        RightActions.Children.Add(Ui.Button("Enregistrer", (_, _) => Save(), "SecondaryButton", Icons.Save, "Ctrl + S"));

        // ------------------------------------------------- Événements
        _rtb.TextChanged += (_, _) => { _dirty = true; StatusText.Text = "Modifications non enregistrées"; };
        _rtb.SelectionChanged += (_, _) => UpdateToolbar();
        _rtb.PreviewKeyDown += OnKeyDown;
        _rtb.AddHandler(CommandManager.PreviewExecutedEvent, new ExecutedRoutedEventHandler(OnPreviewCommand), true);
        _rtb.PreviewDragOver += (_, e) => { if (ImageHelper.FirstImageFile(e.Data) != null) { e.Effects = DragDropEffects.Copy; e.Handled = true; } };
        _rtb.PreviewDrop += OnDrop;
        _rtb.ContextMenuOpening += OnContextMenu;
        _autosave.Tick += (_, _) => { if (_dirty) Save(); };
        _autosave.Start();
        Unloaded += (_, _) => _autosave.Stop();
        Loaded += (_, _) => { _autosave.Start(); _rtb.Focus(); UpdateToolbar(); UpdateWordCount(); };
        _dirty = false;
        StatusText.Text = "Enregistré";
    }

    public void OnLeave()
    {
        if (_dirty) Save();
    }

    private void Save()
    {
        try
        {
            DocumentIO.Save(AppState.Store, _item, _rtb.Document);
            _dirty = false;
            StatusText.Text = "Enregistré à " + DateTime.Now.ToString("HH:mm");
            UpdateWordCount();
        }
        catch (Exception ex)
        {
            AppState.LogError(ex);
            StatusText.Text = "Erreur d'enregistrement : " + ex.Message;
        }
    }

    private void Rename()
    {
        var n = DialogWindow.Ask("Renommer", "Titre du document", _item.Title, "Renommer");
        if (n == null) return;
        _item.Title = n;
        TitleText.Text = n;
        AppState.Store.Touch(_item);
    }

    private void UpdateWordCount()
    {
        var text = DocumentIO.PlainText(_rtb.Document);
        var words = DocumentIO.WordCount(text);
        _wordCount.Text = $"{Ui.Plural(words, "mot")} · {Ui.Plural(text.Count(c => !char.IsWhiteSpace(c)), "caractère")} · lecture {Math.Max(1, (int)Math.Ceiling(words / 200.0))} min";
    }

    // ------------------------------------------------- Clavier et commandes

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { Save(); e.Handled = true; }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && e.Key is Key.D1 or Key.NumPad1) { SetHeading(1); e.Handled = true; }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && e.Key is Key.D2 or Key.NumPad2) { SetHeading(2); e.Handled = true; }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && e.Key is Key.D3 or Key.NumPad3) { SetHeading(3); e.Handled = true; }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && e.Key is Key.D0 or Key.NumPad0) { SetHeading(0); e.Handled = true; }
        if (e.Key == Key.Space || e.Key == Key.Enter) Dispatcher.BeginInvoke(UpdateWordCountLazy, DispatcherPriority.Background);
    }

    private DateTime _lastCount;
    private void UpdateWordCountLazy()
    {
        if ((DateTime.Now - _lastCount).TotalSeconds < 2) return;
        _lastCount = DateTime.Now;
        UpdateWordCount();
    }

    private void OnPreviewCommand(object sender, ExecutedRoutedEventArgs e)
    {
        // Coller une image (capture d'écran, fichier copié)
        if (e.Command == ApplicationCommands.Paste && !Clipboard.ContainsText() && ImageHelper.FromClipboard() is { } img)
        {
            InsertImageBytes(img.Bytes, null);
            e.Handled = true;
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var file = ImageHelper.FirstImageFile(e.Data);
        if (file == null) return;
        e.Handled = true;
        var pos = _rtb.GetPositionFromPoint(e.GetPosition(_rtb), true);
        if (pos != null) _rtb.CaretPosition = pos;
        var (bytes, _) = ImageHelper.Prepare(File.ReadAllBytes(file), Path.GetExtension(file).ToLowerInvariant());
        InsertImageBytes(bytes, null);
    }

    // ------------------------------------------------- Mise en forme

    private void ApplyFormat(DependencyProperty prop, object? value)
    {
        if (_rtb.Selection.IsEmpty)
        {
            // Sans sélection : on applique au mot courant pour éviter les surprises
            _rtb.Selection.ApplyPropertyValue(prop, value);
        }
        else _rtb.Selection.ApplyPropertyValue(prop, value);
        _rtb.Focus();
        _dirty = true;
    }

    private void ClearFormatting()
    {
        var sel = _rtb.Selection;
        sel.ClearAllProperties();
        foreach (var p in SelectedParagraphs()) DocStyles.ApplyHeading(p, 0, AppState.Settings.DefaultFontSize);
        _rtb.Focus();
    }

    private IEnumerable<Paragraph> SelectedParagraphs()
    {
        var start = _rtb.Selection.Start.Paragraph;
        var end = _rtb.Selection.End.Paragraph;
        if (start == null) yield break;
        var p = start;
        while (p != null)
        {
            yield return p;
            if (p == end) yield break;
            var next = p.NextBlock as Paragraph;
            p = next;
        }
    }

    private void SetHeading(int level)
    {
        foreach (var p in SelectedParagraphs().ToList())
            DocStyles.ApplyHeading(p, level, AppState.Settings.DefaultFontSize);
        _dirty = true;
        _rtb.Focus();
        UpdateToolbar();
    }

    private void UpdateToolbar()
    {
        _updatingUi = true;
        try
        {
            var sel = _rtb.Selection;
            _bold.IsChecked = sel.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight w && w.ToOpenTypeWeight() >= 600;
            _italic.IsChecked = sel.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle s && s == FontStyles.Italic;
            _underline.IsChecked = sel.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection d && d.Count > 0;
            var ff = sel.GetPropertyValue(TextElement.FontFamilyProperty) as FontFamily;
            _fontFamily.SelectedItem = ff == null ? null : Fonts.FirstOrDefault(f => f == ff.Source);
            if (sel.GetPropertyValue(TextElement.FontSizeProperty) is double size)
            {
                var pt = Math.Round(size).ToString();
                _fontSize.SelectedItem = _fontSize.Items.Contains(pt) ? pt : null;
                _fontSize.Text = pt;
            }
            else _fontSize.Text = "";
            var para = sel.Start.Paragraph;
            _paraStyle.SelectedIndex = para == null ? -1 : DocStyles.HeadingLevel(para);
        }
        finally { _updatingUi = false; }
    }

    // ------------------------------------------------- Insertion

    private void InsertText(string text)
    {
        _rtb.Selection.Text = text;
        _rtb.CaretPosition = _rtb.Selection.End;
        _rtb.Focus();
    }

    private void InsertImageFromFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = ImageHelper.Filter };
        if (dlg.ShowDialog() != true) return;
        var (bytes, _) = ImageHelper.Prepare(File.ReadAllBytes(dlg.FileName), Path.GetExtension(dlg.FileName).ToLowerInvariant());
        InsertImageBytes(bytes, null);
    }

    private void InsertOnlineImage()
    {
        if (!OnlineCheck()) return;
        var d = new ImageSearchDialog();
        if (d.ShowDialog() != true || d.ResultBytes == null) return;
        InsertImageBytes(d.ResultBytes, d.ResultCredit);
    }

    private void InsertDrawing()
    {
        var d = new DrawingDialog();
        if (d.ShowDialog() != true || d.ResultPng == null) return;
        InsertImageBytes(d.ResultPng, null);
    }

    private void InsertImageBytes(byte[] bytes, string? caption)
    {
        var bmp = ImageHelper.FromBytes(bytes);
        if (bmp == null) { DialogWindow.Info("Image", "Ce fichier n'est pas une image lisible."); return; }
        var width = Math.Min(bmp.PixelWidth, 600);
        var image = new Image { Source = bmp, Width = width, Stretch = Stretch.Uniform };
        var container = new BlockUIContainer(image) { Margin = new Thickness(0, 6, 0, caption == null ? 12 : 2) };
        var current = _rtb.CaretPosition.Paragraph;
        var blocks = current?.Parent is FlowDocument ? _rtb.Document.Blocks : null;
        if (current != null && blocks != null)
        {
            var isEmpty = new TextRange(current.ContentStart, current.ContentEnd).Text.Trim().Length == 0;
            blocks.InsertAfter(current, container);
            if (isEmpty) blocks.Remove(current);
        }
        else
        {
            _rtb.Document.Blocks.Add(container);
        }
        Block last = container;
        if (caption != null)
        {
            var cap = new Paragraph(new Run(caption)) { FontSize = 11, FontStyle = FontStyles.Italic, Foreground = new SolidColorBrush(Color.FromRgb(0x5F, 0x68, 0x78)), Margin = new Thickness(0, 0, 0, 12) };
            _rtb.Document.Blocks.InsertAfter(container, cap);
            last = cap;
        }
        var after = new Paragraph();
        _rtb.Document.Blocks.InsertAfter(last, after);
        _rtb.CaretPosition = after.ContentStart;
        _rtb.Focus();
        _dirty = true;
    }

    private void InsertBox()
    {
        var box = DocStyles.CreateBox();
        var p = _rtb.CaretPosition.Paragraph;
        if (p != null && p.Parent is FlowDocument) _rtb.Document.Blocks.InsertAfter(p, box);
        else _rtb.Document.Blocks.Add(box);
        var first = (Paragraph)box.Blocks.FirstBlock;
        first.Inlines.Clear();
        first.Inlines.Add(new Run("À retenir : ") { FontWeight = FontWeights.Bold });
        first.Inlines.Add(new Run(""));
        _rtb.CaretPosition = first.ContentEnd;
        _rtb.Focus();
    }

    private void InsertSeparator()
    {
        var sep = new Paragraph { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xD2, 0xDC)), Margin = new Thickness(0, 6, 0, 14), FontSize = 4 };
        var p = _rtb.CaretPosition.Paragraph;
        if (p != null && p.Parent is FlowDocument) _rtb.Document.Blocks.InsertAfter(p, sep);
        else _rtb.Document.Blocks.Add(sep);
        var after = new Paragraph();
        _rtb.Document.Blocks.InsertAfter(sep, after);
        _rtb.CaretPosition = after.ContentStart;
        _rtb.Focus();
    }

    private ContextMenu TableMenu(FrameworkElement target)
    {
        var m = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        foreach (var (r, c) in new[] { (2, 2), (3, 2), (3, 3), (4, 3), (5, 4), (6, 2) })
        {
            var mi = new MenuItem { Header = $"{r} lignes × {c} colonnes" };
            mi.Click += (_, _) => InsertTable(r, c);
            m.Items.Add(mi);
        }
        return m;
    }

    private void InsertTable(int rows, int cols)
    {
        var line = new SolidColorBrush(Color.FromRgb(0xB8, 0xC0, 0xCC));
        var table = new Table { CellSpacing = 0, BorderBrush = line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 6, 0, 12) };
        for (int c = 0; c < cols; c++) table.Columns.Add(new TableColumn());
        var group = new TableRowGroup();
        for (int r = 0; r < rows; r++)
        {
            var row = new TableRow();
            if (r == 0) row.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xEF, 0xFD));
            for (int c = 0; c < cols; c++)
            {
                var p = new Paragraph(new Run(r == 0 ? $"Titre {c + 1}" : "")) { Margin = new Thickness(0) };
                if (r == 0) p.FontWeight = FontWeights.SemiBold;
                row.Cells.Add(new TableCell(p) { BorderBrush = line, BorderThickness = new Thickness(0.5), Padding = new Thickness(6, 4, 6, 4) });
            }
            group.Rows.Add(row);
        }
        table.RowGroups.Add(group);
        var cur = _rtb.CaretPosition.Paragraph;
        if (cur != null && cur.Parent is FlowDocument) _rtb.Document.Blocks.InsertAfter(cur, table);
        else _rtb.Document.Blocks.Add(table);
        _rtb.Document.Blocks.InsertAfter(table, new Paragraph());
        _rtb.CaretPosition = ((Paragraph)group.Rows[0].Cells[0].Blocks.FirstBlock).ContentEnd;
        _rtb.Focus();
        _dirty = true;
    }

    private void InsertWikipedia()
    {
        if (!OnlineCheck()) return;
        var d = new WikipediaDialog();
        if (d.ShowDialog() != true || d.Result == null) return;
        var w = d.Result;
        var cur = _rtb.CaretPosition.Paragraph;
        var title = new Paragraph(new Run(w.Title));
        DocStyles.ApplyHeading(title, 3, AppState.Settings.DefaultFontSize);
        var text = new Paragraph(new Run(w.Extract)) { Margin = new Thickness(0, 0, 0, 4) };
        var source = new Paragraph(new Run($"Source : Wikipédia, article « {w.Title} », consulté le {DateTime.Now:dd/MM/yyyy} — {w.PageUrl}"))
        {
            FontSize = 11, FontStyle = FontStyles.Italic, Foreground = new SolidColorBrush(Color.FromRgb(0x5F, 0x68, 0x78)), Margin = new Thickness(0, 0, 0, 12)
        };
        var blocks = _rtb.Document.Blocks;
        if (cur != null && cur.Parent is FlowDocument)
        {
            blocks.InsertAfter(cur, title);
            blocks.InsertAfter(title, text);
            blocks.InsertAfter(text, source);
        }
        else { blocks.Add(title); blocks.Add(text); blocks.Add(source); }
        if (d.ResultImage != null)
        {
            var bmp = ImageHelper.FromBytes(d.ResultImage);
            if (bmp != null) blocks.InsertAfter(title, new BlockUIContainer(new Image { Source = bmp, Width = Math.Min(260, bmp.PixelWidth), HorizontalAlignment = HorizontalAlignment.Left }) { Margin = new Thickness(0, 0, 0, 6) });
        }
        var after = new Paragraph();
        blocks.InsertAfter(source, after);
        _rtb.CaretPosition = after.ContentStart;
        _rtb.Focus();
        _dirty = true;
    }

    public static bool OnlineCheck()
    {
        if (AppState.Settings.OnlineEnabled) return true;
        DialogWindow.Info("Fonctions en ligne désactivées", "Active les fonctions en ligne dans les Réglages pour chercher des images et des articles Wikipédia.");
        return false;
    }

    // ------------------------------------------------- Menu contextuel des images

    private void OnContextMenu(object sender, ContextMenuEventArgs e)
    {
        var image = (e.OriginalSource as DependencyObject) is { } src ? FindImage(src) : null;
        if (image == null) { _rtb.ContextMenu = null; return; }
        var m = new ContextMenu();
        foreach (var (label, w) in new[] { ("Petite (200 px)", 200.0), ("Moyenne (400 px)", 400.0), ("Grande (700 px)", 700.0), ("Taille réelle", double.NaN) })
        {
            var mi = new MenuItem { Header = label };
            var width = w;
            mi.Click += (_, _) =>
            {
                image.Width = double.IsNaN(width) && image.Source is BitmapSource bs ? Math.Min(bs.PixelWidth, 716) : width;
                _dirty = true;
            };
            m.Items.Add(mi);
        }
        m.Items.Add(new Separator());
        foreach (var (label, align) in new[] { ("Aligner à gauche", HorizontalAlignment.Left), ("Centrer", HorizontalAlignment.Center), ("Aligner à droite", HorizontalAlignment.Right) })
        {
            var mi = new MenuItem { Header = label };
            var a = align;
            mi.Click += (_, _) => { image.HorizontalAlignment = a; _dirty = true; };
            m.Items.Add(mi);
        }
        m.Items.Add(new Separator());
        var del = new MenuItem { Header = "Supprimer l'image", Icon = Ui.Icon(Icons.Delete, 14) };
        del.Click += (_, _) =>
        {
            if (image.Parent is BlockUIContainer bc && bc.Parent is FlowDocument fd) fd.Blocks.Remove(bc);
            else if (image.Parent is BlockUIContainer bc2 && bc2.Parent is Section sec) sec.Blocks.Remove(bc2);
            else if (image.Parent is InlineUIContainer ic && ic.Parent is Paragraph p) p.Inlines.Remove(ic);
            _dirty = true;
        };
        m.Items.Add(del);
        _rtb.ContextMenu = m;
    }

    private static Image? FindImage(DependencyObject d)
    {
        while (d != null)
        {
            if (d is Image img) return img;
            d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }

    // ------------------------------------------------- Conversion

    private void MakeCards()
    {
        Save();
        var cards = Converters.CardsFromOutline(DocumentIO.Outline(_rtb.Document));
        if (cards.Count == 0)
        {
            DialogWindow.Info("Aucune fiche trouvée", "Utilise les styles Titre 1, Titre 2 ou Titre 3 pour tes titres, ou écris des lignes « mot : définition ». Ensuite, recommence.");
            return;
        }
        CardsPreviewDialog.CreateDeckFrom(cards, _item);
    }

    private void MakeQuiz()
    {
        Save();
        var questions = Converters.QuizFromOutline(DocumentIO.Outline(_rtb.Document));
        if (questions.Count == 0)
        {
            DialogWindow.Info("Aucune question trouvée", "Mets en gras les mots importants de ton cours (ils deviendront des textes à trous) ou utilise des titres. Ensuite, recommence.");
            return;
        }
        QuestionsPreviewDialog.CreateQuizFrom(questions, _item);
    }
}
