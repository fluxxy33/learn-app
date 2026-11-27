using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Dialogs;

/// <summary>Fenêtre de dialogue au style de l'application : titre, contenu, boutons.</summary>
public class DialogWindow : Window
{
    protected readonly StackPanel Body = new();
    protected readonly StackPanel Buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    protected Button? OkButton;
    private readonly TextBlock _error = new() { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };

    public DialogWindow(string title, double width = 440)
    {
        Title = title;
        Width = width;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        if (Application.Current.MainWindow is { IsLoaded: true } main && main != this) Owner = main;
        SetResourceReference(BackgroundProperty, "Content");
        SetResourceReference(ForegroundProperty, "Fg");
        FontFamily = (FontFamily)FindResource("UiFont");
        FontSize = 14;

        var root = new DockPanel { Margin = new Thickness(24, 20, 24, 20) };
        var header = Ui.Text(title, "H2");
        header.Margin = new Thickness(0, 0, 0, 14);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        var footer = new StackPanel();
        footer.Children.Add(_error);
        Buttons.Margin = new Thickness(0, 18, 0, 0);
        footer.Children.Add(Buttons);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(Body);
        Content = root;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; } };
    }

    protected void AddOkCancel(string okText = "OK", string cancelText = "Annuler")
    {
        var cancel = new Button { Content = cancelText, Style = Ui.Style("SecondaryButton"), IsCancel = true, Margin = new Thickness(0, 0, 8, 0), MinWidth = 90 };
        OkButton = new Button { Content = okText, Style = Ui.Style("AccentButton"), IsDefault = true, MinWidth = 90 };
        OkButton.Click += (_, _) =>
        {
            var err = Validate();
            if (err != null) { ShowError(err); return; }
            DialogResult = true;
        };
        Buttons.Children.Add(cancel);
        Buttons.Children.Add(OkButton);
    }

    protected virtual string? Validate() => null;

    protected void ShowError(string message)
    {
        _error.Text = message;
        _error.Visibility = Visibility.Visible;
    }

    protected static TextBlock FieldLabel(string text)
    {
        var t = Ui.Text(text, "Label");
        t.Margin = new Thickness(0, 10, 0, 5);
        return t;
    }

    // -------------------------------------------------------------- Raccourcis

    public static string? Ask(string title, string label, string initial = "", string okText = "OK")
    {
        var d = new InputDialog(title, label, initial, okText);
        return d.ShowDialog() == true ? d.Value : null;
    }

    public static bool Confirm(string title, string message, string okText = "Supprimer")
    {
        var d = new DialogWindow(title);
        d.Body.Children.Add(Ui.Text(message));
        d.AddOkCancel(okText);
        return d.ShowDialog() == true;
    }

    public static void Info(string title, string message)
    {
        var d = new DialogWindow(title);
        d.Body.Children.Add(Ui.Text(message));
        var ok = new Button { Content = "OK", Style = Ui.Style("AccentButton"), IsDefault = true, IsCancel = true, MinWidth = 90 };
        ok.Click += (_, _) => d.DialogResult = true;
        d.Buttons.Children.Add(ok);
        d.ShowDialog();
    }
}

public sealed class InputDialog : DialogWindow
{
    private readonly TextBox _box;
    public string Value => _box.Text.Trim();

    public InputDialog(string title, string label, string initial, string okText) : base(title)
    {
        Body.Children.Add(FieldLabel(label));
        _box = new TextBox { Text = initial, Padding = new Thickness(6, 5, 6, 5) };
        Body.Children.Add(_box);
        AddOkCancel(okText);
        Loaded += (_, _) => { _box.Focus(); _box.SelectAll(); };
    }

    protected override string? Validate() => Value.Length == 0 ? "Ce champ ne peut pas être vide." : null;
}

/// <summary>Créer ou modifier une matière : nom, couleur, icône.</summary>
public sealed class SubjectDialog : DialogWindow
{
    private readonly TextBox _name = new() { Padding = new Thickness(6, 5, 6, 5) };
    public string SubjectName => _name.Text.Trim();
    public string ColorHex { get; private set; }
    public string Glyph { get; private set; }

    public SubjectDialog(Subject? existing) : base(existing == null ? "Nouvelle matière" : "Modifier la matière", 480)
    {
        ColorHex = existing?.ColorHex ?? SubjectIcons.Colors[AppState.Store.Library.Subjects.Count % SubjectIcons.Colors.Count];
        Glyph = existing?.Icon ?? SubjectIcons.All[0].Glyph;
        _name.Text = existing?.Name ?? "";

        Body.Children.Add(FieldLabel("Nom"));
        Body.Children.Add(_name);

        Body.Children.Add(FieldLabel("Couleur"));
        var colors = new WrapPanel();
        foreach (var c in SubjectIcons.Colors)
        {
            var rb = new RadioButton { GroupName = "color", IsChecked = c == ColorHex, Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand, ToolTip = c };
            rb.Template = SwatchTemplate();
            rb.Background = Ui.BrushFromHex(c);
            var hex = c;
            rb.Checked += (_, _) => ColorHex = hex;
            colors.Children.Add(rb);
        }
        Body.Children.Add(colors);

        Body.Children.Add(FieldLabel("Icône"));
        var icons = new WrapPanel();
        foreach (var (glyph, name) in SubjectIcons.All)
        {
            var rb = new RadioButton { Style = Ui.Style("Chip"), IsChecked = glyph == Glyph, Margin = new Thickness(0, 0, 6, 6), ToolTip = name, Content = Ui.Icon(glyph, 16) };
            var g = glyph;
            rb.Checked += (_, _) => Glyph = g;
            icons.Children.Add(rb);
        }
        Body.Children.Add(icons);
        AddOkCancel(existing == null ? "Créer" : "Enregistrer");
        Loaded += (_, _) => _name.Focus();
    }

    private static ControlTemplate SwatchTemplate()
    {
        var t = new ControlTemplate(typeof(RadioButton));
        var border = new FrameworkElementFactory(typeof(Border), "Bd");
        border.SetValue(WidthProperty, 30.0);
        border.SetValue(HeightProperty, 30.0);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(15));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(3));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetResourceReference(Border.BorderBrushProperty, "Content");
        t.VisualTree = border;
        var trig = new Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
        trig.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("Fg"), "Bd"));
        t.Triggers.Add(trig);
        return t;
    }

    protected override string? Validate() => SubjectName.Length == 0 ? "Donne un nom à la matière." : null;
}

/// <summary>Choisir une matière et un chapitre.</summary>
public class SubjectPicker : StackPanel
{
    private readonly ComboBox _subject = new() { DisplayMemberPath = "Name" };
    private readonly ComboBox _chapter = new() { DisplayMemberPath = "Name" };

    public Subject? SelectedSubject => _subject.SelectedItem as Subject;
    public string? SelectedChapterId => (_chapter.SelectedItem as Chapter)?.Id is { Length: > 0 } id ? id : null;

    public SubjectPicker(string? subjectId, string? chapterId)
    {
        var lbl = Ui.Text("Matière", "Label");
        lbl.Margin = new Thickness(0, 10, 0, 5);
        Children.Add(lbl);
        _subject.ItemsSource = AppState.Store.Library.Subjects.OrderBy(s => s.Order).ToList();
        Children.Add(_subject);
        var lbl2 = Ui.Text("Chapitre", "Label");
        lbl2.Margin = new Thickness(0, 10, 0, 5);
        Children.Add(lbl2);
        Children.Add(_chapter);
        _subject.SelectionChanged += (_, _) => FillChapters(null);
        _subject.SelectedItem = AppState.Store.GetSubject(subjectId ?? "") ?? AppState.Store.Library.Subjects.OrderBy(s => s.Order).FirstOrDefault();
        FillChapters(chapterId);
    }

    private void FillChapters(string? chapterId)
    {
        var list = new List<Chapter> { new() { Id = "", Name = "(Aucun chapitre)" } };
        if (SelectedSubject != null) list.AddRange(SelectedSubject.Chapters.OrderBy(c => c.Order));
        _chapter.ItemsSource = list;
        _chapter.SelectedItem = list.FirstOrDefault(c => c.Id == chapterId) ?? list[0];
    }
}

/// <summary>Créer un document, un diaporama, un paquet de fiches ou un quiz.</summary>
public sealed class NewItemDialog : DialogWindow
{
    private readonly TextBox _title = new() { Padding = new Thickness(6, 5, 6, 5) };
    private readonly SubjectPicker _picker;
    private readonly ComboBox _template = new() { DisplayMemberPath = "Name" };
    private readonly TextBlock _templateHint = Ui.Text("", "Muted");
    public ItemKind Kind { get; private set; }
    public string ItemTitle => _title.Text.Trim();
    public Subject? Subject => _picker.SelectedSubject;
    public string? ChapterId => _picker.SelectedChapterId;
    public Templates.DocTemplate? SelectedTemplate => _template.SelectedItem as Templates.DocTemplate;
    public bool UseExposeTemplate { get; private set; }

    public NewItemDialog(ItemKind kind, string? subjectId, string? chapterId, string? templateId = null) : base("Nouveau", 500)
    {
        Kind = kind;
        var tl = FieldLabel("Modèle");
        Body.Children.Add(FieldLabel("Type"));
        var kinds = new WrapPanel();
        foreach (var (k, glyph, name) in new[] { (ItemKind.Document, Icons.Document, "Document"), (ItemKind.Slideshow, Icons.Slideshow, "Diaporama"), (ItemKind.Deck, Icons.Cards, "Fiches"), (ItemKind.Quiz, Icons.Quiz, "Quiz") })
        {
            var rb = new RadioButton { Style = Ui.Style("Chip"), Content = Ui.IconText(glyph, name), IsChecked = k == kind, Margin = new Thickness(0, 0, 6, 6) };
            var kk = k;
            rb.Checked += (_, _) => { Kind = kk; UpdateTemplates(); };
            kinds.Children.Add(rb);
        }
        Body.Children.Add(kinds);
        Body.Children.Add(FieldLabel("Titre"));
        Body.Children.Add(_title);
        _picker = new SubjectPicker(subjectId, chapterId);
        Body.Children.Add(_picker);
        Body.Children.Add(tl);
        Body.Children.Add(_template);
        _templateHint.Margin = new Thickness(0, 6, 0, 0);
        Body.Children.Add(_templateHint);
        _template.SelectionChanged += (_, _) => _templateHint.Text = (_template.SelectedItem as Templates.DocTemplate)?.Description ?? "";
        UpdateTemplates();
        AddOkCancel("Créer");
        Loaded += (_, _) => _title.Focus();

        void UpdateTemplates()
        {
            var visible = Kind is ItemKind.Document or ItemKind.Slideshow;
            tl.Visibility = _template.Visibility = _templateHint.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (Kind == ItemKind.Document) _template.ItemsSource = Templates.Documents;
            else if (Kind == ItemKind.Slideshow)
                _template.ItemsSource = new[]
                {
                    new Templates.DocTemplate("vide", "Diaporama vide", "Une diapositive de titre.", ""),
                    new Templates.DocTemplate("expose", "Exposé (10 diapositives)", "Introduction, plan, parties, conclusion et sources, avec des conseils dans les notes.", "")
                };
            _template.SelectedIndex = 0;
            if (templateId != null && _template.ItemsSource is IEnumerable<Templates.DocTemplate> all)
                _template.SelectedItem = all.FirstOrDefault(t => t.Id == templateId) ?? _template.SelectedItem;
        }
    }

    protected override string? Validate()
    {
        if (ItemTitle.Length == 0) return "Donne un titre.";
        if (Subject == null) return "Crée d'abord une matière dans la Bibliothèque.";
        UseExposeTemplate = Kind == ItemKind.Slideshow && SelectedTemplate?.Id == "expose";
        return null;
    }
}

/// <summary>Déplacer un élément vers une autre matière / un autre chapitre.</summary>
public sealed class MoveDialog : DialogWindow
{
    private readonly SubjectPicker _picker;
    public Subject? Subject => _picker.SelectedSubject;
    public string? ChapterId => _picker.SelectedChapterId;

    public MoveDialog(LibraryItem item) : base("Déplacer « " + item.Title + " »")
    {
        _picker = new SubjectPicker(item.SubjectId, item.ChapterId);
        Body.Children.Add(_picker);
        AddOkCancel("Déplacer");
    }
}

/// <summary>Choisir des options avant une conversion automatique.</summary>
public sealed class ChoiceDialog : DialogWindow
{
    public int SelectedIndex { get; private set; }

    public ChoiceDialog(string title, string message, params string[] options) : base(title, 480)
    {
        Body.Children.Add(Ui.Text(message));
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        for (int i = 0; i < options.Length; i++)
        {
            var rb = new RadioButton { Content = options[i], IsChecked = i == 0, Margin = new Thickness(0, 6, 0, 0), GroupName = "choice" };
            var idx = i;
            rb.Checked += (_, _) => SelectedIndex = idx;
            panel.Children.Add(rb);
        }
        Body.Children.Add(panel);
        AddOkCancel("Continuer");
    }
}
