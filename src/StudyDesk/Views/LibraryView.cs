using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Views;

/// <summary>Bibliothèque : matières et chapitres à gauche, contenus à droite.</summary>
public sealed class LibraryView : UserControl, IPage
{
    public string NavKey => "library";

    /// <summary>Matière sélectionnée (gardée entre deux visites). Null = toutes.</summary>
    public static string? SelectedSubjectId { get; set; }
    public static string? SelectedChapterId { get; set; }
    private static ItemKind? _kindFilter;

    private readonly StackPanel _subjects = new();
    private readonly StackPanel _items = new();
    private readonly TextBox _search = new() { Padding = new Thickness(8, 6, 8, 6), MinWidth = 190 };
    private readonly TextBlock _title = Ui.Text("", "H1");
    private readonly TextBlock _subtitle = Ui.Text("", "Muted");
    private readonly WrapPanel _filters = new() { Margin = new Thickness(0, 14, 0, 14) };
    private readonly StackPanel _chapterBar = new() { Orientation = Orientation.Horizontal };

    public LibraryView()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ---------------------------------------------- Colonne des matières
        var leftDock = new DockPanel { Margin = new Thickness(16, 24, 8, 16) };
        var leftHead = new DockPanel { Margin = new Thickness(8, 0, 0, 10) };
        var addSubject = new Button { Style = Ui.Style("IconButton"), Content = Icons.Add, ToolTip = "Nouvelle matière" };
        addSubject.Click += (_, _) => NewSubject();
        DockPanel.SetDock(addSubject, Dock.Right);
        leftHead.Children.Add(addSubject);
        var lt = Ui.Text("Matières", "H3");
        lt.VerticalAlignment = VerticalAlignment.Center;
        leftHead.Children.Add(lt);
        DockPanel.SetDock(leftHead, Dock.Top);
        leftDock.Children.Add(leftHead);
        var catalogBtn = Ui.Button("Contenus prêts à l'emploi", (_, _) => AppState.Main.NavigateTo("catalog"), "SubtleButton", "\uE719");
        catalogBtn.HorizontalContentAlignment = HorizontalAlignment.Left;
        DockPanel.SetDock(catalogBtn, Dock.Bottom);
        leftDock.Children.Add(catalogBtn);
        var importBtn = Ui.Button("Importer un fichier .studydesk", (_, _) => ItemActions.Import(Refresh), "SubtleButton", Icons.Import);
        importBtn.HorizontalContentAlignment = HorizontalAlignment.Left;
        DockPanel.SetDock(importBtn, Dock.Bottom);
        leftDock.Children.Add(importBtn);
        leftDock.Children.Add(new ScrollViewer { Content = _subjects, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        root.Children.Add(leftDock);

        // ---------------------------------------------- Contenus
        var right = new DockPanel { Margin = new Thickness(16, 24, 32, 16) };
        Grid.SetColumn(right, 1);
        var head = new DockPanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        _search.ToolTip = "Rechercher dans les titres et le texte des documents";
        var searchBox = new Grid();
        searchBox.Children.Add(_search);
        var placeholder = new TextBlock { Text = "Rechercher…", IsHitTestVisible = false, Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        placeholder.SetResourceReference(TextBlock.ForegroundProperty, "FgMuted");
        searchBox.Children.Add(placeholder);
        _search.TextChanged += (_, _) => { placeholder.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; FillItems(); };
        actions.Children.Add(searchBox);
        var newBtn = Ui.Button("Nouveau", (s, _) => HomeView.NewMenu((FrameworkElement)s, SelectedSubjectId, SelectedChapterId).IsOpen = true, "AccentButton", Icons.Add);
        newBtn.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(newBtn);
        DockPanel.SetDock(actions, Dock.Right);
        head.Children.Add(actions);
        var titles = new StackPanel();
        _title.TextWrapping = TextWrapping.NoWrap;
        _title.FontSize = 24;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        titles.Children.Add(_title);
        _subtitle.Margin = new Thickness(0, 4, 0, 0);
        titles.Children.Add(_subtitle);
        head.Children.Add(titles);
        DockPanel.SetDock(head, Dock.Top);
        right.Children.Add(head);

        var filterRow = new DockPanel();
        DockPanel.SetDock(_chapterBar, Dock.Right);
        _chapterBar.VerticalAlignment = VerticalAlignment.Center;
        filterRow.Children.Add(_chapterBar);
        filterRow.Children.Add(_filters);
        DockPanel.SetDock(filterRow, Dock.Top);
        right.Children.Add(filterRow);
        right.Children.Add(new ScrollViewer { Content = _items, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 8, 0) });
        root.Children.Add(right);

        Content = root;
        if (SelectedSubjectId != null && AppState.Store.GetSubject(SelectedSubjectId) == null) SelectedSubjectId = null;
        Refresh();
    }

    public void Refresh()
    {
        FillSubjects();
        FillFilters();
        FillItems();
    }

    // ---------------------------------------------- Matières et chapitres

    private void FillSubjects()
    {
        _subjects.Children.Clear();
        var store = AppState.Store;
        _subjects.Children.Add(SubjectButton(null, Icons.Library, "Tout", store.Library.Items.Count, "#5F6878"));
        foreach (var s in store.Library.Subjects.OrderBy(s => s.Order))
        {
            _subjects.Children.Add(SubjectButton(s, s.Icon, s.Name, store.Library.Items.Count(i => i.SubjectId == s.Id), s.ColorHex));
            if (s.Id == SelectedSubjectId)
            {
                foreach (var c in s.Chapters.OrderBy(c => c.Order))
                    _subjects.Children.Add(ChapterButton(s, c));
                var addChapter = Ui.Button("Ajouter un chapitre", (_, _) => NewChapter(s), "SubtleButton", Icons.Add);
                addChapter.HorizontalContentAlignment = HorizontalAlignment.Left;
                addChapter.Margin = new Thickness(28, 0, 0, 6);
                addChapter.SetResourceReference(ForegroundProperty, "FgMuted");
                _subjects.Children.Add(addChapter);
            }
        }
    }

    private RadioButton SubjectButton(Subject? s, string glyph, string name, int count, string color)
    {
        var dock = new DockPanel();
        var countText = Ui.Text(count.ToString(), "Muted");
        countText.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(countText, Dock.Right);
        dock.Children.Add(countText);
        var icon = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(6), Background = Ui.BrushFromHex(color), Child = Ui.Icon(glyph, 12, Brushes.White) };
        dock.Children.Add(icon);
        var t = new TextBlock { Text = name, Margin = new Thickness(10, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        dock.Children.Add(t);
        var rb = new RadioButton
        {
            Style = Ui.Style("NavItem"), GroupName = "subjects", Content = dock,
            IsChecked = (s?.Id ?? null) == SelectedSubjectId && SelectedChapterId == null
        };
        rb.Click += (_, _) =>
        {
            SelectedSubjectId = s?.Id;
            SelectedChapterId = null;
            Refresh();
        };
        if (s != null) rb.ContextMenu = SubjectMenu(s);
        return rb;
    }

    private RadioButton ChapterButton(Subject s, Chapter c)
    {
        var count = AppState.Store.Library.Items.Count(i => i.ChapterId == c.Id);
        var dock = new DockPanel();
        var countText = Ui.Text(count.ToString(), "Muted");
        DockPanel.SetDock(countText, Dock.Right);
        dock.Children.Add(countText);
        dock.Children.Add(new TextBlock { Text = c.Name, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 6, 0) });
        var rb = new RadioButton { Style = Ui.Style("NavItem"), GroupName = "subjects", Content = dock, Margin = new Thickness(28, 0, 0, 0), Height = 32, IsChecked = SelectedChapterId == c.Id };
        rb.Click += (_, _) => { SelectedChapterId = c.Id; Refresh(); };
        var m = new ContextMenu();
        var rename = new MenuItem { Header = "Renommer…", Icon = Ui.Icon(Icons.Rename, 14) };
        rename.Click += (_, _) =>
        {
            var n = DialogWindow.Ask("Renommer le chapitre", "Nom", c.Name, "Renommer");
            if (n == null) return;
            c.Name = n;
            AppState.Store.SaveLibrary();
            Refresh();
        };
        var up = new MenuItem { Header = "Monter", Icon = Ui.Icon(Icons.Up, 14) };
        up.Click += (_, _) => MoveChapter(s, c, -1);
        var down = new MenuItem { Header = "Descendre", Icon = Ui.Icon(Icons.Down, 14) };
        down.Click += (_, _) => MoveChapter(s, c, 1);
        var del = new MenuItem { Header = "Supprimer le chapitre", Icon = Ui.Icon(Icons.Delete, 14) };
        del.Click += (_, _) =>
        {
            if (!DialogWindow.Confirm("Supprimer le chapitre", $"Supprimer le chapitre « {c.Name} » ? Ses contenus restent dans la matière.")) return;
            AppState.Store.DeleteChapter(s, c);
            SelectedChapterId = null;
            Refresh();
        };
        m.Items.Add(rename); m.Items.Add(up); m.Items.Add(down); m.Items.Add(new Separator()); m.Items.Add(del);
        rb.ContextMenu = m;
        return rb;
    }

    private void MoveChapter(Subject s, Chapter c, int delta)
    {
        var list = s.Chapters.OrderBy(x => x.Order).ToList();
        var i = list.IndexOf(c);
        var j = i + delta;
        if (j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        for (int k = 0; k < list.Count; k++) list[k].Order = k;
        AppState.Store.SaveLibrary();
        Refresh();
    }

    private ContextMenu SubjectMenu(Subject s)
    {
        var m = new ContextMenu();
        var edit = new MenuItem { Header = "Modifier (nom, couleur, icône)…", Icon = Ui.Icon(Icons.Edit, 14) };
        edit.Click += (_, _) =>
        {
            var d = new SubjectDialog(s);
            if (d.ShowDialog() != true) return;
            s.Name = d.SubjectName; s.ColorHex = d.ColorHex; s.Icon = d.Glyph;
            AppState.Store.SaveLibrary();
            Refresh();
        };
        var chapter = new MenuItem { Header = "Ajouter un chapitre…", Icon = Ui.Icon(Icons.Add, 14) };
        chapter.Click += (_, _) => NewChapter(s);
        var up = new MenuItem { Header = "Monter", Icon = Ui.Icon(Icons.Up, 14) };
        up.Click += (_, _) => MoveSubject(s, -1);
        var down = new MenuItem { Header = "Descendre", Icon = Ui.Icon(Icons.Down, 14) };
        down.Click += (_, _) => MoveSubject(s, 1);
        var share = new MenuItem { Header = "Partager toute la matière…", Icon = Ui.Icon(Icons.Share, 14) };
        share.Click += (_, _) => ItemActions.Share(AppState.Store.Library.Items.Where(i => i.SubjectId == s.Id).ToList(), s.Name);
        var del = new MenuItem { Header = "Supprimer la matière…", Icon = Ui.Icon(Icons.Delete, 14) };
        del.Click += (_, _) =>
        {
            var n = AppState.Store.Library.Items.Count(i => i.SubjectId == s.Id);
            if (!DialogWindow.Confirm("Supprimer la matière", $"Supprimer « {s.Name} » et ses {Ui.Plural(n, "élément")} ? Cette action est définitive.")) return;
            AppState.Store.DeleteSubject(s);
            SelectedSubjectId = null;
            SelectedChapterId = null;
            Refresh();
        };
        foreach (var mi in new object[] { edit, chapter, up, down, share, new Separator(), del }) m.Items.Add(mi);
        return m;
    }

    private void MoveSubject(Subject s, int delta)
    {
        var list = AppState.Store.Library.Subjects.OrderBy(x => x.Order).ToList();
        var i = list.IndexOf(s);
        var j = i + delta;
        if (j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        for (int k = 0; k < list.Count; k++) list[k].Order = k;
        AppState.Store.SaveLibrary();
        Refresh();
    }

    private void NewSubject()
    {
        var d = new SubjectDialog(null);
        if (d.ShowDialog() != true) return;
        var s = AppState.Store.AddSubject(d.SubjectName, d.ColorHex, d.Glyph);
        SelectedSubjectId = s.Id;
        SelectedChapterId = null;
        Refresh();
    }

    private void NewChapter(Subject s)
    {
        var n = DialogWindow.Ask("Nouveau chapitre", "Nom du chapitre", "", "Créer");
        if (n == null) return;
        var c = AppState.Store.AddChapter(s, n);
        SelectedSubjectId = s.Id;
        SelectedChapterId = c.Id;
        Refresh();
    }

    // ---------------------------------------------- Contenus

    private void FillFilters()
    {
        _filters.Children.Clear();
        void Chip(string label, ItemKind? k)
        {
            var rb = new RadioButton { Style = Ui.Style("Chip"), Content = label, GroupName = "kind", IsChecked = _kindFilter == k };
            rb.Checked += (_, _) => { _kindFilter = k; FillItems(); };
            _filters.Children.Add(rb);
        }
        Chip("Tout", null);
        Chip("Documents", ItemKind.Document);
        Chip("Diaporamas", ItemKind.Slideshow);
        Chip("Fiches", ItemKind.Deck);
        Chip("Quiz", ItemKind.Quiz);

        _chapterBar.Children.Clear();
        var subject = SelectedSubjectId == null ? null : AppState.Store.GetSubject(SelectedSubjectId);
        if (subject != null)
        {
            var review = Ui.Button("Réviser la matière", (_, _) => ReviewSessionView.StartSubject(subject.Id), "SecondaryButton", Icons.Cards);
            _chapterBar.Children.Add(review);
        }
    }

    private void FillItems()
    {
        var store = AppState.Store;
        var subject = SelectedSubjectId == null ? null : store.GetSubject(SelectedSubjectId);
        var chapter = subject?.Chapters.FirstOrDefault(c => c.Id == SelectedChapterId);
        _title.Text = chapter?.Name ?? subject?.Name ?? "Bibliothèque";

        IEnumerable<LibraryItem> items = store.Library.Items;
        if (subject != null) items = items.Where(i => i.SubjectId == subject.Id);
        if (chapter != null) items = items.Where(i => i.ChapterId == chapter.Id);
        if (_kindFilter != null) items = items.Where(i => i.Kind == _kindFilter);
        var q = QuizGrader.Normalize(_search.Text);
        if (q.Length > 0) items = items.Where(i => Matches(i, q));
        var list = items.OrderByDescending(i => i.Favorite).ThenByDescending(i => i.ModifiedAt).ToList();
        _subtitle.Text = (subject != null && chapter != null ? subject.Name + " · " : "") + Ui.Plural(list.Count, "élément");

        _items.Children.Clear();
        if (list.Count == 0)
        {
            var empty = new StackPanel { Margin = new Thickness(0, 30, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            var icon = Ui.Icon(Icons.Library, 40);
            icon.SetResourceReference(TextBlock.ForegroundProperty, "FgMuted");
            empty.Children.Add(icon);
            var t = Ui.Text(q.Length > 0 ? "Aucun résultat pour cette recherche." : "Rien ici pour l'instant.", "H3");
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 12, 0, 4);
            empty.Children.Add(t);
            if (q.Length == 0)
            {
                var hint = Ui.Text("Crée un document, un diaporama, des fiches ou un quiz avec le bouton Nouveau.", "Muted");
                hint.HorizontalAlignment = HorizontalAlignment.Center;
                empty.Children.Add(hint);
            }
            _items.Children.Add(empty);
            return;
        }

        if (chapter == null && subject != null && _kindFilter == null && q.Length == 0)
        {
            // Regroupement par chapitre
            foreach (var group in list.GroupBy(i => i.ChapterId).OrderBy(g => subject.Chapters.FirstOrDefault(c => c.Id == g.Key)?.Order ?? int.MaxValue))
            {
                var name = subject.Chapters.FirstOrDefault(c => c.Id == group.Key)?.Name ?? "Sans chapitre";
                var h = Ui.Text(name, "Label");
                h.Margin = new Thickness(2, 12, 0, 6);
                _items.Children.Add(h);
                foreach (var item in group) _items.Children.Add(ItemActions.Row(item, Refresh, showSubject: false));
            }
        }
        else
        {
            foreach (var item in list) _items.Children.Add(ItemActions.Row(item, Refresh, showSubject: subject == null));
        }
    }

    private static bool Matches(LibraryItem i, string q)
    {
        if (QuizGrader.Normalize(i.Title).Contains(q)) return true;
        try
        {
            var store = AppState.Store;
            switch (i.Kind)
            {
                case ItemKind.Document:
                    var txt = store.DocumentTextPath(i.Id);
                    return System.IO.File.Exists(txt) && QuizGrader.Normalize(System.IO.File.ReadAllText(txt)).Contains(q);
                case ItemKind.Deck:
                    return store.LoadDeck(i.Id).Cards.Any(c => QuizGrader.Normalize(c.Front + " " + c.Back).Contains(q));
                case ItemKind.Quiz:
                    return store.LoadQuiz(i.Id).Questions.Any(x => QuizGrader.Normalize(x.Prompt + " " + x.ClozeText).Contains(q));
                case ItemKind.Slideshow:
                    return store.LoadSlideshow(i.Id).Slides.SelectMany(s => s.Elements).Any(e => QuizGrader.Normalize(e.Text).Contains(q));
            }
        }
        catch { /* fichier illisible */ }
        return false;
    }
}
