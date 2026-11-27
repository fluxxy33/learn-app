using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Views;

/// <summary>Éditeur de paquet de fiches : liste à gauche, recto / verso à droite.</summary>
public sealed class DeckEditorView : EditorShell, IPage, IEditor
{
    public string NavKey => "library";
    private readonly LibraryItem _item;
    private readonly Deck _deck;
    private readonly ListBox _list = CreateList();

    internal static ListBox CreateList()
    {
        var l = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        ScrollViewer.SetHorizontalScrollBarVisibility(l, ScrollBarVisibility.Disabled);
        return l;
    }
    private readonly TextBox _front = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, FontSize = 16, Padding = new Thickness(8), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 220 };
    private readonly TextBox _back = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, FontSize = 16, Padding = new Thickness(8), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 260 };
    private readonly Image _frontImage = new() { MaxHeight = 160, HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform };
    private readonly Image _backImage = new() { MaxHeight = 160, HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform };
    private readonly StackPanel _frontImageBar = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
    private readonly StackPanel _backImageBar = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _cardInfo = Ui.Text("", "Muted");
    private readonly StackPanel _editor = new();
    private readonly TextBlock _empty = Ui.Text("Ce paquet est vide. Clique sur « Nouvelle fiche » ou importe une liste.", "Muted");
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private Flashcard? _current;
    private bool _loading;

    public DeckEditorView(LibraryItem item) : base(item.Title, Icons.Cards)
    {
        _item = item;
        _deck = AppState.Store.LoadDeck(item.Id);
        TitleText.MouseLeftButtonUp += (_, _) =>
        {
            var n = DialogWindow.Ask("Renommer", "Nom du paquet", _item.Title, "Renommer");
            if (n == null) return;
            _item.Title = n; TitleText.Text = n; AppState.Store.Touch(_item);
        };

        var home = RibbonRow(
            Ui.Tool(Icons.Add, "Nouvelle fiche", "Ajouter une fiche (Ctrl + N)", (_, _) => AddCard()),
            Ui.Tool(Icons.Copy, "Dupliquer", "Dupliquer la fiche", (_, _) => DuplicateCard()),
            Ui.Tool(Icons.Sync, "Fiche inverse", "Créer la fiche dans l'autre sens (réponse → question), utile pour le vocabulaire", (_, _) => ReverseCard()),
            Ui.Tool(Icons.Delete, "Supprimer", "Supprimer la fiche (Ctrl + Suppr)", (_, _) => DeleteCard()),
            Ui.Separator(),
            Ui.Tool(Icons.Import, "Importer une liste", "Coller une liste « question ; réponse », une par ligne", (_, _) => ImportText()),
            Ui.Tool(Icons.Refresh, "Remettre à zéro", "Oublier la progression de toutes les fiches de ce paquet", (_, _) => ResetProgress()));
        var review = RibbonRow(
            Ui.Tool(Icons.Play, "Réviser", "Réviser les fiches dues de ce paquet", (_, _) => { Save(); ReviewSessionView.Start(_item.Id); }),
            Ui.Tool(Icons.Cards, "Entraînement libre", "Revoir toutes les fiches sans changer les dates", (_, _) => { Save(); ReviewSessionView.StartPractice(_item.Id); }),
            Ui.Separator(),
            Ui.Tool(Icons.Quiz, "Créer un quiz", "Générer un quiz (QCM, relier) à partir de ces fiches", (_, _) => MakeQuiz()));
        AddRibbonTab("Fiches", home, true);
        AddRibbonTab("Réviser", review);

        // ------------------------------------------- Liste
        var grid = new Grid { Margin = new Thickness(12, 0, 12, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var listCard = new Border { Style = Ui.Style("Card"), Padding = new Thickness(6), Child = _list };
        grid.Children.Add(listCard);
        _list.SelectionChanged += (_, _) => { if (_list.SelectedItem is ListBoxItem { Tag: Flashcard c }) Select(c); };

        // ------------------------------------------- Éditeur
        _editor.Children.Add(FaceHeader("Recto : la question", _frontImageBar, front: true));
        _editor.Children.Add(_front);
        _editor.Children.Add(_frontImage);
        _editor.Children.Add(FaceHeader("Verso : la réponse", _backImageBar, front: false));
        _editor.Children.Add(_back);
        _editor.Children.Add(_backImage);
        _cardInfo.Margin = new Thickness(0, 14, 0, 0);
        _editor.Children.Add(_cardInfo);
        var tip = Ui.Text("Astuce : Ctrl + Entrée dans le verso crée la fiche suivante. Tu peux coller une capture d'écran (Ctrl + V) sur une image.", "Muted");
        tip.Margin = new Thickness(0, 6, 0, 0);
        _editor.Children.Add(tip);
        var editorStack = new StackPanel();
        editorStack.Children.Add(_empty);
        editorStack.Children.Add(_editor);
        var right = new ScrollViewer { Content = new Border { Style = Ui.Style("Card"), Padding = new Thickness(20), Child = editorStack, VerticalAlignment = VerticalAlignment.Top }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        Root.Children.Add(grid);

        _front.TextChanged += (_, _) => Changed();
        _back.TextChanged += (_, _) => Changed();
        _back.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { AddCard(); e.Handled = true; } };
        PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N) { AddCard(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Delete) { DeleteCard(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { Save(); e.Handled = true; }
        };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(); };
        RightActions.Children.Add(Ui.Button("Réviser", (_, _) => { Save(); ReviewSessionView.Start(_item.Id); }, "AccentButton", Icons.Play));

        FillList();
        if (_deck.Cards.Count > 0) _list.SelectedIndex = 0;
        else UpdateEmpty();
        StatusText.Text = Ui.Plural(_deck.Cards.Count, "fiche");
    }

    private StackPanel FaceHeader(string text, StackPanel imageBar, bool front)
    {
        var dock = new StackPanel { Margin = new Thickness(0, front ? 0 : 18, 0, 6) };
        imageBar.Children.Add(Ui.Button("Image", (_, _) => PickImage(front), "SubtleButton", Icons.Picture, "Ajouter une image depuis le PC"));
        imageBar.Children.Add(Ui.Button("En ligne", (_, _) => PickOnline(front), "SubtleButton", Icons.Globe, "Image libre de droits"));
        imageBar.Children.Add(Ui.Button("Coller", (_, _) => PasteImage(front), "SubtleButton", Icons.Paste, "Coller une image (capture d'écran)"));
        imageBar.Children.Add(Ui.Button("Retirer", (_, _) => SetImage(front, null), "SubtleButton", Icons.Delete, "Retirer l'image"));
        imageBar.Margin = new Thickness(-8, 2, 0, 0);
        dock.Children.Add(Ui.Text(text, "H3"));
        dock.Children.Add(imageBar);
        return dock;
    }

    public void OnLeave() => Save();

    private void Save()
    {
        _saveTimer.Stop();
        AppState.Store.SaveDeck(_deck);
        AppState.Store.Touch(_item);
        StatusText.Text = $"{Ui.Plural(_deck.Cards.Count, "fiche")} · enregistré";
    }

    private void Changed()
    {
        if (_loading || _current == null) return;
        _current.Front = _front.Text;
        _current.Back = _back.Text;
        if (_list.SelectedItem is ListBoxItem li) li.Content = RowContent(_current);
        _saveTimer.Stop();
        _saveTimer.Start();
        StatusText.Text = "Modifications non enregistrées";
    }

    private void FillList()
    {
        _list.Items.Clear();
        foreach (var c in _deck.Cards) _list.Items.Add(new ListBoxItem { Content = RowContent(c), Tag = c, Padding = new Thickness(8, 6, 8, 6) });
    }

    private static UIElement RowContent(Flashcard c)
    {
        var sp = new StackPanel();
        var f = new TextBlock { Text = c.Front.Length == 0 ? "(recto vide)" : c.Front.Replace('\n', ' '), FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        f.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        sp.Children.Add(f);
        var b = new TextBlock { Text = c.Back.Replace('\n', ' '), TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12 };
        b.SetResourceReference(TextBlock.ForegroundProperty, "FgMuted");
        sp.Children.Add(b);
        return sp;
    }

    private void Select(Flashcard c)
    {
        _current = c;
        _loading = true;
        _front.Text = c.Front;
        _back.Text = c.Back;
        _loading = false;
        ShowImage(_frontImage, c.FrontImage);
        ShowImage(_backImage, c.BackImage);
        _cardInfo.Text = c.IsNew ? "Nouvelle fiche, pas encore révisée."
            : $"Prochaine révision : {(c.Due?.Date <= DateTime.Today ? "aujourd'hui" : c.Due?.ToString("dddd d MMMM", new System.Globalization.CultureInfo("fr-FR")))} · intervalle {Srs.Describe(c.IntervalDays)} · {Ui.Plural(c.Repetitions, "bonne réponse")} de suite · {Ui.Plural(c.Lapses, "oubli")}";
        UpdateEmpty();
    }

    private static void ShowImage(Image target, string? file)
    {
        target.Source = file == null ? null : ImageHelper.Load(AppState.Store.MediaPath(file), 600);
        target.Visibility = target.Source == null ? Visibility.Collapsed : Visibility.Visible;
        target.Margin = new Thickness(0, 8, 0, 0);
    }

    private void UpdateEmpty()
    {
        _empty.Visibility = _deck.Cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _editor.Visibility = _deck.Cards.Count == 0 || _current == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AddCard()
    {
        var c = new Flashcard();
        var index = _current == null ? _deck.Cards.Count : _deck.Cards.IndexOf(_current) + 1;
        _deck.Cards.Insert(index, c);
        var li = new ListBoxItem { Content = RowContent(c), Tag = c, Padding = new Thickness(8, 6, 8, 6) };
        _list.Items.Insert(index, li);
        _list.SelectedItem = li;
        li.BringIntoView();
        _front.Focus();
        Save();
    }

    private void DuplicateCard()
    {
        if (_current == null) return;
        var c = new Flashcard { Front = _current.Front, Back = _current.Back, FrontImage = _current.FrontImage, BackImage = _current.BackImage };
        var index = _deck.Cards.IndexOf(_current) + 1;
        _deck.Cards.Insert(index, c);
        FillList();
        _list.SelectedIndex = index;
        Save();
    }

    private void ReverseCard()
    {
        if (_current == null) return;
        var c = new Flashcard { Front = _current.Back, Back = _current.Front, FrontImage = _current.BackImage, BackImage = _current.FrontImage };
        var index = _deck.Cards.IndexOf(_current) + 1;
        _deck.Cards.Insert(index, c);
        FillList();
        _list.SelectedIndex = index;
        Save();
    }

    private void DeleteCard()
    {
        if (_current == null) return;
        var index = _deck.Cards.IndexOf(_current);
        _deck.Cards.Remove(_current);
        _current = null;
        FillList();
        if (_deck.Cards.Count > 0) _list.SelectedIndex = Math.Min(index, _deck.Cards.Count - 1);
        UpdateEmpty();
        Save();
    }

    private void ImportText()
    {
        var d = new ImportTextDialog();
        if (d.ShowDialog() != true) return;
        var cards = Converters.CardsFromText(d.Text);
        if (cards.Count == 0) { DialogWindow.Info("Importation", "Aucune ligne reconnue. Sépare la question et la réponse par un point-virgule."); return; }
        _deck.Cards.AddRange(cards);
        FillList();
        _list.SelectedIndex = _deck.Cards.Count - cards.Count;
        Save();
        DialogWindow.Info("Importation", $"{Ui.Plural(cards.Count, "fiche ajoutée", "fiches ajoutées")}.");
    }

    private void ResetProgress()
    {
        if (!DialogWindow.Confirm("Remettre à zéro", "Toutes les fiches de ce paquet redeviendront nouvelles. Continuer ?", "Remettre à zéro")) return;
        foreach (var c in _deck.Cards) Srs.Reset(c);
        Save();
        if (_current != null) Select(_current);
    }

    private void MakeQuiz()
    {
        Save();
        var qs = Converters.QuizFromCards(_deck.Cards);
        if (qs.Count == 0) { DialogWindow.Info("Quiz", "Il faut au moins 3 fiches avec des réponses courtes pour créer un quiz."); return; }
        QuestionsPreviewDialog.CreateQuizFrom(qs, _item);
    }

    // ------------------------------------------- Images

    private void SetImage(bool front, string? file)
    {
        if (_current == null) return;
        if (front) _current.FrontImage = file; else _current.BackImage = file;
        ShowImage(front ? _frontImage : _backImage, file);
        Save();
    }

    private void PickImage(bool front)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = ImageHelper.Filter };
        if (dlg.ShowDialog() != true) return;
        var (bytes, ext) = ImageHelper.Prepare(File.ReadAllBytes(dlg.FileName), Path.GetExtension(dlg.FileName).ToLowerInvariant());
        SetImage(front, AppState.Store.ImportMedia(bytes, ext));
    }

    private void PickOnline(bool front)
    {
        if (!DocumentEditorView.OnlineCheck()) return;
        var d = new ImageSearchDialog(_current?.Front);
        if (d.ShowDialog() != true || d.ResultBytes == null) return;
        SetImage(front, AppState.Store.ImportMedia(d.ResultBytes, ".png"));
    }

    private void PasteImage(bool front)
    {
        if (ImageHelper.FromClipboard() is not { } img) { DialogWindow.Info("Coller", "Le presse-papiers ne contient pas d'image. Fais une capture avec Windows + Maj + S, puis réessaie."); return; }
        SetImage(front, AppState.Store.ImportMedia(img.Bytes, img.Extension));
    }
}

public sealed class ImportTextDialog : DialogWindow
{
    private readonly TextBox _box = new() { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, Height = 260, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(6), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    public string Text => _box.Text;

    public ImportTextDialog() : base("Importer une liste de fiches", 620)
    {
        Body.Children.Add(Ui.Text("Une fiche par ligne. Sépare la question et la réponse par un point-virgule, une tabulation (copier depuis Excel) ou « : ».", "Muted"));
        _box.Margin = new Thickness(0, 10, 0, 0);
        _box.Text = "to take ; prendre\nto write ; écrire\n";
        Body.Children.Add(_box);
        AddOkCancel("Importer");
        Loaded += (_, _) => { _box.Focus(); _box.SelectAll(); };
    }
}
