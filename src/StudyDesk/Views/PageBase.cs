using System.IO;
using System.Windows;
using System.Windows.Controls;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Views;

/// <summary>Page défilante avec un titre, une ligne d'actions à droite et un contenu.</summary>
public abstract class PageBase : UserControl, IPage
{
    protected readonly StackPanel Body = new();
    protected readonly StackPanel Actions = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _title;
    private readonly TextBlock _subtitle;
    protected readonly ScrollViewer Scroller;

    public abstract string NavKey { get; }

    protected PageBase(string title, string? subtitle = null, double maxWidth = 1100)
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 20) };
        DockPanel.SetDock(Actions, Dock.Right);
        header.Children.Add(Actions);
        var titles = new StackPanel();
        _title = Ui.Text(title, "H1");
        _subtitle = Ui.Text(subtitle ?? "", "Muted");
        _subtitle.FontSize = 14;
        _subtitle.Margin = new Thickness(0, 4, 0, 0);
        _subtitle.Visibility = string.IsNullOrEmpty(subtitle) ? Visibility.Collapsed : Visibility.Visible;
        titles.Children.Add(_title);
        titles.Children.Add(_subtitle);
        header.Children.Add(titles);

        var inner = new StackPanel { Margin = new Thickness(36, 28, 36, 36), MaxWidth = maxWidth };
        inner.Children.Add(header);
        inner.Children.Add(Body);
        Scroller = new ScrollViewer { Content = inner, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Content = Scroller;
    }

    protected void SetTitle(string title, string? subtitle = null)
    {
        _title.Text = title;
        _subtitle.Text = subtitle ?? "";
        _subtitle.Visibility = string.IsNullOrEmpty(subtitle) ? Visibility.Collapsed : Visibility.Visible;
    }

    public virtual void Refresh() { }

    protected static TextBlock SectionTitle(string text)
    {
        var t = Ui.Text(text, "H2");
        t.Margin = new Thickness(0, 26, 0, 12);
        return t;
    }
}

/// <summary>Ouvrir, créer, renommer, déplacer, supprimer, partager les éléments de la bibliothèque.</summary>
public static class ItemActions
{
    public static string KindName(ItemKind k) => k switch
    {
        ItemKind.Document => "Document",
        ItemKind.Slideshow => "Diaporama",
        ItemKind.Deck => "Fiches",
        ItemKind.Quiz => "Quiz",
        _ => ""
    };

    public static string KindIcon(ItemKind k) => k switch
    {
        ItemKind.Document => Icons.Document,
        ItemKind.Slideshow => Icons.Slideshow,
        ItemKind.Deck => Icons.Cards,
        ItemKind.Quiz => Icons.Quiz,
        _ => Icons.Page
    };

    public static void Open(LibraryItem item)
    {
        AppState.Store.Touch(item, opened: true);
        FrameworkElement page = item.Kind switch
        {
            ItemKind.Document => new DocumentEditorView(item),
            ItemKind.Slideshow => new SlideEditorView(item),
            ItemKind.Deck => new DeckEditorView(item),
            ItemKind.Quiz => new QuizEditorView(item),
            _ => throw new InvalidOperationException()
        };
        AppState.Main.Navigate(page);
    }

    /// <summary>Affiche la fenêtre « Nouveau » puis ouvre l'élément créé.</summary>
    public static LibraryItem? CreateWithDialog(ItemKind kind, string? subjectId = null, string? chapterId = null, string? templateId = null)
    {
        if (AppState.Store.Library.Subjects.Count == 0)
        {
            DialogWindow.Info("Aucune matière", "Crée d'abord une matière dans la Bibliothèque.");
            return null;
        }
        var d = new NewItemDialog(kind, subjectId, chapterId, templateId);
        if (d.ShowDialog() != true || d.Subject == null) return null;
        var item = Create(d.Kind, d.ItemTitle, d.Subject.Id, d.ChapterId, d.Kind == ItemKind.Document ? d.SelectedTemplate?.Id : d.UseExposeTemplate ? "expose" : null);
        Open(item);
        return item;
    }

    public static LibraryItem Create(ItemKind kind, string title, string subjectId, string? chapterId, string? templateId = null)
    {
        var store = AppState.Store;
        var item = store.CreateItem(kind, title, subjectId, chapterId);
        if (kind == ItemKind.Document)
        {
            var tpl = Templates.Documents.FirstOrDefault(t => t.Id == templateId);
            var markup = tpl?.Markup ?? "";
            if (markup.Length > 0) markup = markup.Replace(markup.Split('\n')[0], "# " + title);
            File.WriteAllText(DefaultContent.SeedPath(store, item.Id), markup.Length > 0 ? markup : "# " + title + "\n\n");
        }
        else if (kind == ItemKind.Slideshow && templateId == "expose")
        {
            store.SaveSlideshow(SlideshowFactory.CreateExposeTemplate(item.Id, title));
        }
        return item;
    }

    public static ContextMenu Menu(LibraryItem item, Action refresh)
    {
        var m = new ContextMenu();
        void Add(string header, string glyph, Action a)
        {
            var mi = new MenuItem { Header = header, Icon = Ui.Icon(glyph, 14) };
            mi.Click += (_, _) => a();
            m.Items.Add(mi);
        }
        Add("Ouvrir", Icons.Edit, () => Open(item));
        if (item.Kind == ItemKind.Deck) Add("Réviser", Icons.Play, () => ReviewSessionView.Start(item.Id));
        if (item.Kind == ItemKind.Quiz) Add("Faire le quiz", Icons.Play, () => QuizPlayerView.Start(item));
        m.Items.Add(new Separator());
        Add(item.Favorite ? "Retirer des favoris" : "Ajouter aux favoris", item.Favorite ? Icons.StarFill : Icons.Star, () =>
        {
            item.Favorite = !item.Favorite;
            AppState.Store.SaveLibrary();
            refresh();
        });
        Add("Renommer…", Icons.Rename, () =>
        {
            var name = DialogWindow.Ask("Renommer", "Nouveau titre", item.Title, "Renommer");
            if (name == null) return;
            item.Title = name;
            AppState.Store.Touch(item);
            refresh();
        });
        Add("Déplacer…", Icons.MoveTo, () =>
        {
            var d = new MoveDialog(item);
            if (d.ShowDialog() != true || d.Subject == null) return;
            item.SubjectId = d.Subject.Id;
            item.ChapterId = d.ChapterId;
            AppState.Store.Touch(item);
            refresh();
        });
        Add("Dupliquer", Icons.Copy, () => { AppState.Store.Duplicate(item); refresh(); });
        Add("Partager (fichier .studydesk)…", Icons.Share, () => Share(new[] { item }, item.Title));
        m.Items.Add(new Separator());
        Add("Supprimer", Icons.Delete, () =>
        {
            if (!DialogWindow.Confirm("Supprimer", $"Supprimer « {item.Title} » ? Cette action est définitive.")) return;
            AppState.Store.DeleteItem(item);
            refresh();
        });
        return m;
    }

    public static void Share(IEnumerable<LibraryItem> items, string name)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)) + ".studydesk",
            Filter = "Paquet StudyDesk|*.studydesk"
        };
        if (dlg.ShowDialog() != true) return;
        AppState.Store.ExportPackage(items, dlg.FileName);
        DialogWindow.Info("Partage", "Le fichier a été créé. Ton ami peut l'ouvrir avec Bibliothèque › Importer dans StudyDesk.");
    }

    public static void Import(Action refresh)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Paquet StudyDesk|*.studydesk" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var added = AppState.Store.ImportPackage(dlg.FileName);
            DialogWindow.Info("Importation", $"{Ui.Plural(added.Count, "élément importé", "éléments importés")}.");
            refresh();
        }
        catch (Exception ex)
        {
            DialogWindow.Info("Importation impossible", ex.Message);
        }
    }

    /// <summary>Petite ligne de bibliothèque : icône, titre, matière, date.</summary>
    public static Button Row(LibraryItem item, Action refresh, bool showSubject = true)
    {
        var store = AppState.Store;
        var subject = store.GetSubject(item.SubjectId);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(8),
            Background = Ui.BrushFromHex(subject?.ColorHex ?? "#888888"),
            Child = Ui.Icon(KindIcon(item.Kind), 16, System.Windows.Media.Brushes.White)
        };
        grid.Children.Add(icon);
        var texts = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        var title = Ui.Text(item.Title, "H3");
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.TextWrapping = TextWrapping.NoWrap;
        texts.Children.Add(title);
        var chapter = subject?.Chapters.FirstOrDefault(c => c.Id == item.ChapterId)?.Name;
        var meta = KindName(item.Kind) + (showSubject && subject != null ? " · " + subject.Name : "") + (chapter != null ? " · " + chapter : "") + " · modifié " + Ui.RelativeDate(item.ModifiedAt);
        var m = Ui.Text(meta, "Muted");
        m.TextTrimming = TextTrimming.CharacterEllipsis;
        m.TextWrapping = TextWrapping.NoWrap;
        texts.Children.Add(m);
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);
        if (item.Favorite)
        {
            var star = Ui.Icon(Icons.StarFill, 14);
            star.SetResourceReference(TextBlock.ForegroundProperty, "Warn");
            Grid.SetColumn(star, 2);
            grid.Children.Add(star);
        }
        var b = new Button { Style = Ui.Style("CardButton"), Content = grid, Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 6) };
        b.Click += (_, _) => Open(item);
        b.ContextMenu = Menu(item, refresh);
        b.ToolTip = "Clic droit pour plus d'options";
        return b;
    }
}
