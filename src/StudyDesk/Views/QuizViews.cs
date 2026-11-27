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

public static class QuizText
{
    public static string TypeName(QuestionType t) => t switch
    {
        QuestionType.MultipleChoice => "QCM",
        QuestionType.TrueFalse => "Vrai / faux",
        QuestionType.FillBlanks => "Texte à trous",
        QuestionType.ShortAnswer => "Réponse courte",
        QuestionType.Matching => "Relier",
        _ => ""
    };
}

/// <summary>Page « Quiz » : tous les quiz, avec le meilleur score.</summary>
public sealed class QuizHubView : PageBase
{
    public override string NavKey => "quiz";

    public QuizHubView() : base("Quiz", "Teste-toi : c'est la meilleure façon de voir ce que tu sais vraiment.")
    {
        Build();
    }

    public override void Refresh() => Build();

    private void Build()
    {
        Body.Children.Clear();
        Actions.Children.Clear();
        Actions.Children.Add(Ui.Button("Nouveau quiz", (_, _) => ItemActions.CreateWithDialog(ItemKind.Quiz, LibraryView.SelectedSubjectId), "AccentButton", Icons.Add));
        var store = AppState.Store;
        var quizzes = store.ItemsOf(ItemKind.Quiz).ToList();
        if (quizzes.Count == 0)
        {
            Body.Children.Add(Ui.Card(Ui.Text("Aucun quiz pour l'instant. Crée-en un, ou génère-le à partir d'un document ou d'un paquet de fiches.", "Muted")));
            return;
        }
        foreach (var subject in store.Library.Subjects.OrderBy(s => s.Order))
        {
            var mine = quizzes.Where(q => q.SubjectId == subject.Id).OrderBy(q => q.Title).ToList();
            if (mine.Count == 0) continue;
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 10) };
            head.Children.Add(Ui.Dot(subject.ColorHex, 14));
            var h = Ui.Text(subject.Name, "H2");
            h.Margin = new Thickness(10, 0, 0, 0);
            head.Children.Add(h);
            Body.Children.Add(head);
            foreach (var item in mine) Body.Children.Add(Row(item));
        }
    }

    private Border Row(LibraryItem item)
    {
        var store = AppState.Store;
        var quiz = store.LoadQuiz(item.Id);
        var results = store.Stats.QuizResults.Where(r => r.QuizId == item.Id && r.Max > 0).ToList();
        var best = results.Count == 0 ? (double?)null : results.Max(r => r.Score / r.Max * 100);
        var last = results.OrderByDescending(r => r.Time).FirstOrDefault();
        var row = new DockPanel();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var play = Ui.Button("Faire le quiz", (_, _) => QuizPlayerView.Start(item), "AccentButton", Icons.Play);
        play.IsEnabled = quiz.Questions.Count > 0;
        buttons.Children.Add(play);
        var edit = new Button { Style = Ui.Style("IconButton"), Content = Icons.Edit, ToolTip = "Modifier", Margin = new Thickness(4, 0, 0, 0) };
        edit.Click += (_, _) => ItemActions.Open(item);
        buttons.Children.Add(edit);
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);
        if (best != null)
        {
            var score = new TextBlock { Text = $"{best:0} %", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Meilleur score" };
            score.SetResourceReference(TextBlock.ForegroundProperty, best >= 80 ? "Good" : best >= 50 ? "Warn" : "Bad");
            DockPanel.SetDock(score, Dock.Right);
            row.Children.Add(score);
        }
        var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        t.Children.Add(Ui.Text(item.Title, "H3"));
        t.Children.Add(Ui.Text(Ui.Plural(quiz.Questions.Count, "question") + (last != null ? $" · dernier essai {Ui.RelativeDate(last.Time)} : {last.Score / last.Max * 100:0} %" : " · jamais fait"), "Muted"));
        row.Children.Add(t);
        var card = Ui.Card(row, new Thickness(16, 12, 12, 12));
        card.Margin = new Thickness(0, 0, 0, 8);
        card.ContextMenu = ItemActions.Menu(item, Build);
        return card;
    }
}

/// <summary>Éditeur de quiz.</summary>
public sealed class QuizEditorView : EditorShell, IPage, IEditor
{
    public string NavKey => "library";
    private readonly LibraryItem _item;
    private readonly Quiz _quiz;
    private readonly ListBox _list = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel _editor = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private QuizQuestion? _current;

    public QuizEditorView(LibraryItem item) : base(item.Title, Icons.Quiz)
    {
        _item = item;
        _quiz = AppState.Store.LoadQuiz(item.Id);
        TitleText.MouseLeftButtonUp += (_, _) =>
        {
            var n = DialogWindow.Ask("Renommer", "Nom du quiz", _item.Title, "Renommer");
            if (n == null) return;
            _item.Title = n; TitleText.Text = n; AppState.Store.Touch(_item);
        };

        var add = new List<UIElement>();
        foreach (QuestionType t in Enum.GetValues<QuestionType>())
        {
            var type = t;
            add.Add(Ui.Tool(Icons.Add, QuizText.TypeName(t), "Ajouter une question : " + QuizText.TypeName(t), (_, _) => AddQuestion(type)));
        }
        add.Add(Ui.Separator());
        add.Add(Ui.Tool(Icons.Up, null, "Monter la question", (_, _) => MoveQuestion(-1)));
        add.Add(Ui.Tool(Icons.Down, null, "Descendre la question", (_, _) => MoveQuestion(1)));
        add.Add(Ui.Tool(Icons.Copy, null, "Dupliquer la question", (_, _) => DuplicateQuestion()));
        add.Add(Ui.Tool(Icons.Delete, null, "Supprimer la question", (_, _) => DeleteQuestion()));
        add.Add(Ui.Separator());
        var shuffle = new CheckBox { Content = "Mélanger les questions", IsChecked = _quiz.ShuffleQuestions, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        shuffle.Click += (_, _) => { _quiz.ShuffleQuestions = shuffle.IsChecked == true; Save(); };
        add.Add(shuffle);
        AddRibbonTab("Questions", RibbonRow(add.ToArray()), true);
        RightActions.Children.Add(Ui.Button("Faire le quiz", (_, _) => { Save(); QuizPlayerView.Start(_item); }, "AccentButton", Icons.Play));

        var grid = new Grid { Margin = new Thickness(12, 0, 12, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new Border { Style = Ui.Style("Card"), Padding = new Thickness(6), Child = _list });
        _list.SelectionChanged += (_, _) => { if (_list.SelectedItem is ListBoxItem { Tag: QuizQuestion q }) Select(q); };
        var right = new ScrollViewer { Content = new Border { Style = Ui.Style("Card"), Padding = new Thickness(20), Child = _editor, VerticalAlignment = VerticalAlignment.Top }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        Root.Children.Add(grid);
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(); };
        PreviewKeyDown += (_, e) => { if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { Save(); e.Handled = true; } };

        FillList();
        if (_quiz.Questions.Count > 0) _list.SelectedIndex = 0;
        else ShowEmpty();
    }

    public void OnLeave() => Save();

    private void Save()
    {
        _saveTimer.Stop();
        AppState.Store.SaveQuiz(_quiz);
        AppState.Store.Touch(_item);
        StatusText.Text = $"{Ui.Plural(_quiz.Questions.Count, "question")} · enregistré";
    }

    private void Changed()
    {
        if (_list.SelectedItem is ListBoxItem li && _current != null) li.Content = RowContent(_current, _quiz.Questions.IndexOf(_current));
        StatusText.Text = "Modifications non enregistrées";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void ShowEmpty()
    {
        _editor.Children.Clear();
        _editor.Children.Add(Ui.Text("Ce quiz est vide", "H2"));
        var t = Ui.Text("Ajoute une question avec les boutons du ruban : QCM, vrai / faux, texte à trous, réponse courte ou relier.", "Muted");
        t.Margin = new Thickness(0, 6, 0, 0);
        _editor.Children.Add(t);
    }

    private void FillList()
    {
        _list.Items.Clear();
        for (int i = 0; i < _quiz.Questions.Count; i++)
            _list.Items.Add(new ListBoxItem { Content = RowContent(_quiz.Questions[i], i), Tag = _quiz.Questions[i], Padding = new Thickness(8, 6, 8, 6) });
    }

    private static UIElement RowContent(QuizQuestion q, int index)
    {
        var sp = new StackPanel();
        var err = QuizGrader.Validate(q);
        var head = new TextBlock { Text = $"{index + 1}. {QuizText.TypeName(q.Type)}" + (err != null ? "  ⚠" : ""), FontSize = 11, FontWeight = FontWeights.SemiBold };
        head.SetResourceReference(TextBlock.ForegroundProperty, err != null ? "Warn" : "Accent");
        sp.Children.Add(head);
        var text = q.Type == QuestionType.FillBlanks ? q.ClozeText : q.Prompt;
        var t = new TextBlock { Text = string.IsNullOrWhiteSpace(text) ? "(question vide)" : text.Replace('\n', ' '), TextTrimming = TextTrimming.CharacterEllipsis };
        t.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        sp.Children.Add(t);
        return sp;
    }

    private void AddQuestion(QuestionType type)
    {
        var q = new QuizQuestion { Type = type };
        switch (type)
        {
            case QuestionType.MultipleChoice:
                q.Choices.AddRange(new[] { new QuizChoice { IsCorrect = true }, new QuizChoice(), new QuizChoice(), new QuizChoice() });
                break;
            case QuestionType.Matching:
                q.Pairs.AddRange(new[] { new MatchPair(), new MatchPair(), new MatchPair() });
                break;
            case QuestionType.FillBlanks:
                q.Prompt = "Complète la phrase.";
                break;
        }
        var index = _current == null ? _quiz.Questions.Count : _quiz.Questions.IndexOf(_current) + 1;
        _quiz.Questions.Insert(index, q);
        FillList();
        _list.SelectedIndex = index;
        Save();
    }

    private void MoveQuestion(int delta)
    {
        if (_current == null) return;
        var i = _quiz.Questions.IndexOf(_current);
        var j = i + delta;
        if (j < 0 || j >= _quiz.Questions.Count) return;
        (_quiz.Questions[i], _quiz.Questions[j]) = (_quiz.Questions[j], _quiz.Questions[i]);
        FillList();
        _list.SelectedIndex = j;
        Save();
    }

    private void DuplicateQuestion()
    {
        if (_current == null) return;
        var json = System.Text.Json.JsonSerializer.Serialize(_current, Json.File);
        var copy = System.Text.Json.JsonSerializer.Deserialize<QuizQuestion>(json, Json.File)!;
        copy.Id = NewId.Create();
        var i = _quiz.Questions.IndexOf(_current) + 1;
        _quiz.Questions.Insert(i, copy);
        FillList();
        _list.SelectedIndex = i;
        Save();
    }

    private void DeleteQuestion()
    {
        if (_current == null) return;
        var i = _quiz.Questions.IndexOf(_current);
        _quiz.Questions.Remove(_current);
        _current = null;
        FillList();
        if (_quiz.Questions.Count > 0) _list.SelectedIndex = Math.Min(i, _quiz.Questions.Count - 1);
        else ShowEmpty();
        Save();
    }

    // ------------------------------------------------------------ Éditeur d'une question

    private void Select(QuizQuestion q)
    {
        _current = q;
        _editor.Children.Clear();

        var typeRow = new DockPanel();
        var types = new ComboBox { Width = 180 };
        foreach (QuestionType t in Enum.GetValues<QuestionType>()) types.Items.Add(QuizText.TypeName(t));
        types.SelectedIndex = (int)q.Type;
        types.SelectionChanged += (_, _) =>
        {
            q.Type = (QuestionType)types.SelectedIndex;
            if (q.Type == QuestionType.MultipleChoice && q.Choices.Count == 0) q.Choices.AddRange(new[] { new QuizChoice { IsCorrect = true }, new QuizChoice() });
            if (q.Type == QuestionType.Matching && q.Pairs.Count == 0) q.Pairs.AddRange(new[] { new MatchPair(), new MatchPair() });
            Changed();
            Select(q);
        };
        DockPanel.SetDock(types, Dock.Right);
        typeRow.Children.Add(types);
        typeRow.Children.Add(Ui.Text($"Question {_quiz.Questions.IndexOf(q) + 1}", "H2"));
        _editor.Children.Add(typeRow);

        var warning = Ui.Text("", "Muted");
        warning.SetResourceReference(TextBlock.ForegroundProperty, "Warn");
        warning.Margin = new Thickness(0, 6, 0, 0);
        void Validate()
        {
            var err = QuizGrader.Validate(q);
            warning.Text = err == null ? "" : "⚠ " + err;
            warning.Visibility = err == null ? Visibility.Collapsed : Visibility.Visible;
        }

        if (q.Type != QuestionType.FillBlanks)
        {
            _editor.Children.Add(Label("Énoncé"));
            var prompt = Multi(q.Prompt, 60);
            prompt.TextChanged += (_, _) => { q.Prompt = prompt.Text; Validate(); Changed(); };
            _editor.Children.Add(prompt);
        }

        // Image de la question
        var imgRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var img = new Image { MaxHeight = 140, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
        void ShowImg() { img.Source = q.Image == null ? null : ImageHelper.Load(AppState.Store.MediaPath(q.Image), 500); img.Visibility = img.Source == null ? Visibility.Collapsed : Visibility.Visible; }
        imgRow.Children.Add(Ui.Button("Image", (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = ImageHelper.Filter };
            if (dlg.ShowDialog() != true) return;
            var (bytes, ext) = ImageHelper.Prepare(File.ReadAllBytes(dlg.FileName), Path.GetExtension(dlg.FileName).ToLowerInvariant());
            q.Image = AppState.Store.ImportMedia(bytes, ext); ShowImg(); Changed();
        }, "SubtleButton", Icons.Picture));
        imgRow.Children.Add(Ui.Button("En ligne", (_, _) =>
        {
            if (!DocumentEditorView.OnlineCheck()) return;
            var d = new ImageSearchDialog();
            if (d.ShowDialog() != true || d.ResultBytes == null) return;
            q.Image = AppState.Store.ImportMedia(d.ResultBytes, ".png"); ShowImg(); Changed();
        }, "SubtleButton", Icons.Globe));
        imgRow.Children.Add(Ui.Button("Coller", (_, _) =>
        {
            if (ImageHelper.FromClipboard() is not { } c) return;
            q.Image = AppState.Store.ImportMedia(c.Bytes, c.Extension); ShowImg(); Changed();
        }, "SubtleButton", Icons.Paste));
        imgRow.Children.Add(Ui.Button("Retirer", (_, _) => { q.Image = null; ShowImg(); Changed(); }, "SubtleButton", Icons.Delete));
        _editor.Children.Add(imgRow);
        _editor.Children.Add(img);
        ShowImg();

        switch (q.Type)
        {
            case QuestionType.MultipleChoice:
                _editor.Children.Add(Label("Choix (coche les bonnes réponses)"));
                var choices = new StackPanel();
                void FillChoices()
                {
                    choices.Children.Clear();
                    foreach (var c in q.Choices.ToList())
                    {
                        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                        var ok = new CheckBox { IsChecked = c.IsCorrect, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Bonne réponse" };
                        ok.Click += (_, _) => { c.IsCorrect = ok.IsChecked == true; Validate(); Changed(); };
                        DockPanel.SetDock(ok, Dock.Left);
                        row.Children.Add(ok);
                        var del = new Button { Style = Ui.Style("IconButton"), Content = Icons.Cancel, FontSize = 11, ToolTip = "Supprimer ce choix" };
                        var choice = c;
                        del.Click += (_, _) => { q.Choices.Remove(choice); FillChoices(); Validate(); Changed(); };
                        DockPanel.SetDock(del, Dock.Right);
                        row.Children.Add(del);
                        var tb = new TextBox { Text = c.Text, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(6, 0, 4, 0) };
                        tb.TextChanged += (_, _) => { choice.Text = tb.Text; Validate(); Changed(); };
                        row.Children.Add(tb);
                        choices.Children.Add(row);
                    }
                }
                FillChoices();
                _editor.Children.Add(choices);
                var addChoice = Ui.Button("Ajouter un choix", (_, _) => { q.Choices.Add(new QuizChoice()); FillChoices(); Changed(); }, "SubtleButton", Icons.Add);
                addChoice.HorizontalAlignment = HorizontalAlignment.Left;
                _editor.Children.Add(addChoice);
                break;

            case QuestionType.TrueFalse:
                _editor.Children.Add(Label("Bonne réponse"));
                var tf = new StackPanel { Orientation = Orientation.Horizontal };
                var vrai = new RadioButton { Content = "Vrai", IsChecked = q.CorrectBool, GroupName = "tf" + q.Id, Margin = new Thickness(0, 0, 20, 0) };
                var faux = new RadioButton { Content = "Faux", IsChecked = !q.CorrectBool, GroupName = "tf" + q.Id };
                vrai.Checked += (_, _) => { q.CorrectBool = true; Changed(); };
                faux.Checked += (_, _) => { q.CorrectBool = false; Changed(); };
                tf.Children.Add(vrai);
                tf.Children.Add(faux);
                _editor.Children.Add(tf);
                break;

            case QuestionType.ShortAnswer:
                _editor.Children.Add(Label("Réponses acceptées (une par ligne)"));
                var answers = Multi(string.Join("\n", q.AcceptedAnswers), 70);
                answers.TextChanged += (_, _) => { q.AcceptedAnswers = answers.Text.Split('\n').Select(a => a.Trim()).Where(a => a.Length > 0).ToList(); Validate(); Changed(); };
                _editor.Children.Add(answers);
                _editor.Children.Add(Hint("Les majuscules, les accents et une petite faute de frappe ne comptent pas. Les nombres doivent être exacts."));
                break;

            case QuestionType.FillBlanks:
                _editor.Children.Add(Label("Phrase avec les mots à trouver entre crochets"));
                var cloze = Multi(q.ClozeText, 90);
                var preview = Ui.Text("", "Muted");
                void UpdatePreview()
                {
                    var n = QuizGrader.BlankCount(q.ClozeText);
                    preview.Text = n == 0 ? "" : $"{Ui.Plural(n, "trou")} : " + string.Concat(QuizGrader.ParseCloze(q.ClozeText).Select(p => p.Text ?? "_____"));
                }
                cloze.TextChanged += (_, _) => { q.ClozeText = cloze.Text; UpdatePreview(); Validate(); Changed(); };
                _editor.Children.Add(cloze);
                var mkBlank = Ui.Button("Transformer la sélection en trou", (_, _) =>
                {
                    if (cloze.SelectionLength == 0) return;
                    var start = cloze.SelectionStart;
                    var sel = cloze.SelectedText.Trim('[', ']');
                    cloze.SelectedText = "[" + sel + "]";
                    cloze.CaretIndex = start + sel.Length + 2;
                    cloze.Focus();
                }, "SubtleButton", Icons.Edit);
                mkBlank.HorizontalAlignment = HorizontalAlignment.Left;
                mkBlank.Margin = new Thickness(0, 6, 0, 0);
                _editor.Children.Add(mkBlank);
                _editor.Children.Add(Hint("Exemple : La capitale de l'Espagne est [Madrid]. Plusieurs réponses possibles : [Louis XVI|Louis 16]."));
                preview.Margin = new Thickness(0, 6, 0, 0);
                _editor.Children.Add(preview);
                UpdatePreview();
                break;

            case QuestionType.Matching:
                _editor.Children.Add(Label("Paires à relier (gauche → droite)"));
                var pairs = new StackPanel();
                void FillPairs()
                {
                    pairs.Children.Clear();
                    foreach (var p in q.Pairs.ToList())
                    {
                        var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                        row.ColumnDefinitions.Add(new ColumnDefinition());
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                        row.ColumnDefinitions.Add(new ColumnDefinition());
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        var pair = p;
                        var l = new TextBox { Text = p.Left, Padding = new Thickness(6, 4, 6, 4) };
                        l.TextChanged += (_, _) => { pair.Left = l.Text; Validate(); Changed(); };
                        row.Children.Add(l);
                        var arrow = Ui.Icon(Icons.Forward, 12);
                        Grid.SetColumn(arrow, 1);
                        row.Children.Add(arrow);
                        var r = new TextBox { Text = p.Right, Padding = new Thickness(6, 4, 6, 4) };
                        r.TextChanged += (_, _) => { pair.Right = r.Text; Validate(); Changed(); };
                        Grid.SetColumn(r, 2);
                        row.Children.Add(r);
                        var del = new Button { Style = Ui.Style("IconButton"), Content = Icons.Cancel, FontSize = 11, ToolTip = "Supprimer la paire" };
                        del.Click += (_, _) => { q.Pairs.Remove(pair); FillPairs(); Validate(); Changed(); };
                        Grid.SetColumn(del, 3);
                        row.Children.Add(del);
                        pairs.Children.Add(row);
                    }
                }
                FillPairs();
                _editor.Children.Add(pairs);
                var addPair = Ui.Button("Ajouter une paire", (_, _) => { q.Pairs.Add(new MatchPair()); FillPairs(); Changed(); }, "SubtleButton", Icons.Add);
                addPair.HorizontalAlignment = HorizontalAlignment.Left;
                _editor.Children.Add(addPair);
                break;
        }

        _editor.Children.Add(Label("Explication (affichée après la réponse, facultatif)"));
        var expl = Multi(q.Explanation, 50);
        expl.TextChanged += (_, _) => { q.Explanation = expl.Text; Changed(); };
        _editor.Children.Add(expl);
        _editor.Children.Add(warning);
        Validate();
    }

    private static TextBlock Label(string text)
    {
        var t = Ui.Text(text, "Label");
        t.Margin = new Thickness(0, 16, 0, 6);
        return t;
    }

    private static TextBlock Hint(string text)
    {
        var t = Ui.Text(text, "Muted");
        t.Margin = new Thickness(0, 6, 0, 0);
        return t;
    }

    private static TextBox Multi(string text, double minHeight) => new()
    {
        Text = text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = minHeight, Padding = new Thickness(8, 6, 8, 6), FontSize = 15,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 200
    };
}

/// <summary>Faire un quiz : une question à la fois, correction immédiate, score final.</summary>
public sealed class QuizPlayerView : UserControl, IPage
{
    public string NavKey => "quiz";
    private readonly LibraryItem _item;
    private readonly List<QuizQuestion> _questions;
    private int _index;
    private double _score;
    private readonly List<(QuizQuestion Q, double Score)> _answers = new();
    private readonly StackPanel _body = new();
    private readonly ProgressBar _progress = new() { Style = Ui.Style("ProgressBarThin") };
    private readonly TextBlock _info = Ui.Text("", "Muted");
    private readonly ContentControl _bottom = new();
    private Func<double>? _grader;
    private bool _checked;
    private readonly Random _rnd = new();

    public static void Start(LibraryItem item)
    {
        var quiz = AppState.Store.LoadQuiz(item.Id);
        var valid = quiz.Questions.Where(q => QuizGrader.Validate(q) == null).ToList();
        if (valid.Count == 0)
        {
            DialogWindow.Info("Quiz vide", "Ce quiz n'a pas encore de question complète. Ouvre-le pour en ajouter.");
            return;
        }
        if (quiz.ShuffleQuestions) valid = valid.OrderBy(_ => Random.Shared.Next()).ToList();
        AppState.Main.Navigate(new QuizPlayerView(item, valid));
    }

    private QuizPlayerView(LibraryItem item, List<QuizQuestion> questions)
    {
        _item = item;
        _questions = questions;
        var root = new DockPanel();
        var top = new StackPanel { Margin = new Thickness(24, 16, 24, 0) };
        var head = new DockPanel();
        var back = new Button { Style = Ui.Style("IconButton"), Content = Icons.Back, ToolTip = "Quitter le quiz" };
        back.Click += (_, _) => AppState.Main.GoBack();
        DockPanel.SetDock(back, Dock.Left);
        head.Children.Add(back);
        var title = Ui.Text(item.Title, "H3");
        title.VerticalAlignment = VerticalAlignment.Center;
        title.Margin = new Thickness(8, 0, 0, 0);
        head.Children.Add(title);
        top.Children.Add(head);
        _progress.Margin = new Thickness(0, 12, 0, 6);
        top.Children.Add(_progress);
        top.Children.Add(_info);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        _bottom.Margin = new Thickness(24, 8, 24, 24);
        _bottom.MaxWidth = 760;
        DockPanel.SetDock(_bottom, Dock.Bottom);
        root.Children.Add(_bottom);
        var card = new Border { Style = Ui.Style("Card"), Padding = new Thickness(28), MaxWidth = 760, Child = _body, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(24, 16, 24, 8) };
        root.Children.Add(new ScrollViewer { Content = card, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _bottom.Content is Button b && b.IsEnabled)
            {
                b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                e.Handled = true;
            }
        };
        Loaded += (_, _) => ShowQuestion();
    }

    private void ShowQuestion()
    {
        _body.Children.Clear();
        _checked = false;
        _progress.Value = _index * 100.0 / _questions.Count;
        if (_index >= _questions.Count) { ShowResults(); return; }
        var q = _questions[_index];
        _info.Text = $"Question {_index + 1} sur {_questions.Count} · {QuizText.TypeName(q.Type)}";

        var prompt = new TextBlock { Text = q.Type == QuestionType.FillBlanks && string.IsNullOrWhiteSpace(q.Prompt) ? "Complète la phrase." : q.Prompt, FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        prompt.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        _body.Children.Add(prompt);
        if (q.Image != null && ImageHelper.Load(AppState.Store.MediaPath(q.Image), 900) is { } bmp)
            _body.Children.Add(new Image { Source = bmp, MaxHeight = 260, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left });
        var area = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        _body.Children.Add(area);

        switch (q.Type)
        {
            case QuestionType.MultipleChoice:
            {
                var multi = q.Choices.Count(c => c.IsCorrect) > 1;
                if (multi) area.Children.Add(Ui.Text("Plusieurs réponses possibles.", "Muted"));
                var order = q.Choices.Select((c, i) => (c, i)).Where(x => x.c.Text.Trim().Length > 0).OrderBy(_ => _rnd.Next()).ToList();
                var controls = new List<(System.Windows.Controls.Primitives.ToggleButton Box, int Index, QuizChoice Choice)>();
                foreach (var (c, i) in order)
                {
                    System.Windows.Controls.Primitives.ToggleButton box = multi ? new CheckBox() : new RadioButton { GroupName = "q" + _index };
                    box.Content = new TextBlock { Text = c.Text, TextWrapping = TextWrapping.Wrap, FontSize = 16 };
                    box.Margin = new Thickness(0, 8, 0, 0);
                    box.Padding = new Thickness(8, 2, 0, 2);
                    controls.Add((box, i, c));
                    area.Children.Add(box);
                }
                _grader = () =>
                {
                    var selected = controls.Where(x => x.Box.IsChecked == true).Select(x => x.Index).ToList();
                    foreach (var (box, _, choice) in controls)
                    {
                        box.IsEnabled = false;
                        if (choice.IsCorrect) Mark(box, "Good");
                        else if (box.IsChecked == true) Mark(box, "Bad");
                    }
                    return QuizGrader.GradeMultipleChoice(q, selected);
                };
                break;
            }
            case QuestionType.TrueFalse:
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                var t = new RadioButton { Content = new TextBlock { Text = "Vrai", FontSize = 16 }, GroupName = "tf" + _index, Margin = new Thickness(0, 0, 30, 0) };
                var f = new RadioButton { Content = new TextBlock { Text = "Faux", FontSize = 16 }, GroupName = "tf" + _index };
                row.Children.Add(t);
                row.Children.Add(f);
                area.Children.Add(row);
                _grader = () =>
                {
                    t.IsEnabled = f.IsEnabled = false;
                    Mark(q.CorrectBool ? t : f, "Good");
                    if (t.IsChecked != true && f.IsChecked != true) return 0;
                    var ans = t.IsChecked == true;
                    if (ans != q.CorrectBool) Mark(ans ? t : f, "Bad");
                    return QuizGrader.GradeTrueFalse(q, ans);
                };
                break;
            }
            case QuestionType.ShortAnswer:
            {
                var box = new TextBox { FontSize = 18, Padding = new Thickness(10, 8, 10, 8) };
                area.Children.Add(box);
                Dispatcher.BeginInvoke(() => box.Focus(), DispatcherPriority.Input);
                _grader = () =>
                {
                    box.IsReadOnly = true;
                    var s = QuizGrader.GradeShortAnswer(q, box.Text);
                    box.SetResourceReference(Control.BorderBrushProperty, s > 0 ? "Good" : "Bad");
                    box.BorderThickness = new Thickness(2);
                    return s;
                };
                break;
            }
            case QuestionType.FillBlanks:
            {
                var wrap = new WrapPanel();
                var boxes = new List<TextBox>();
                foreach (var part in QuizGrader.ParseCloze(q.ClozeText))
                {
                    if (part.Text != null)
                    {
                        foreach (var word in System.Text.RegularExpressions.Regex.Split(part.Text, @"(?<=\s)"))
                        {
                            if (word.Length == 0) continue;
                            var tb = new TextBlock { Text = word, FontSize = 17, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
                            tb.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
                            wrap.Children.Add(tb);
                        }
                    }
                    else
                    {
                        var longest = part.Answers!.Max(a => a.Length);
                        var box = new TextBox { MinWidth = Math.Max(70, longest * 11), FontSize = 16, Margin = new Thickness(2, 3, 4, 3), Padding = new Thickness(4, 2, 4, 2) };
                        boxes.Add(box);
                        wrap.Children.Add(box);
                    }
                }
                area.Children.Add(wrap);
                Dispatcher.BeginInvoke(() => boxes.FirstOrDefault()?.Focus(), DispatcherPriority.Input);
                _grader = () =>
                {
                    var s = QuizGrader.GradeFillBlanks(q, boxes.Select(b => b.Text).ToList(), out var per);
                    var blanks = QuizGrader.ParseCloze(q.ClozeText).Where(p => p.Answers != null).ToList();
                    for (int i = 0; i < boxes.Count; i++)
                    {
                        boxes[i].IsReadOnly = true;
                        boxes[i].BorderThickness = new Thickness(2);
                        boxes[i].SetResourceReference(Control.BorderBrushProperty, per[i] ? "Good" : "Bad");
                        if (!per[i]) boxes[i].Text = (boxes[i].Text.Length > 0 ? boxes[i].Text + " → " : "") + blanks[i].Answers![0];
                    }
                    return s;
                };
                break;
            }
            case QuestionType.Matching:
            {
                area.Children.Add(Ui.Text("Choisis la bonne réponse pour chaque élément.", "Muted"));
                var rights = q.Pairs.Select((p, i) => (p.Right, i)).OrderBy(_ => _rnd.Next()).ToList();
                var combos = new List<ComboBox>();
                foreach (var p in q.Pairs)
                {
                    var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
                    row.ColumnDefinitions.Add(new ColumnDefinition());
                    row.ColumnDefinitions.Add(new ColumnDefinition());
                    var l = new TextBlock { Text = p.Left, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 0) };
                    l.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
                    row.Children.Add(l);
                    var cb = new ComboBox { FontSize = 15 };
                    foreach (var (text, _) in rights) cb.Items.Add(text);
                    Grid.SetColumn(cb, 1);
                    row.Children.Add(cb);
                    combos.Add(cb);
                    area.Children.Add(row);
                }
                _grader = () =>
                {
                    var mapping = combos.Select(c => c.SelectedIndex < 0 ? -1 : rights[c.SelectedIndex].i).ToList();
                    for (int i = 0; i < combos.Count; i++)
                    {
                        combos[i].IsEnabled = false;
                        var ok = mapping[i] >= 0 && QuizGrader.Normalize(q.Pairs[mapping[i]].Right) == QuizGrader.Normalize(q.Pairs[i].Right);
                        combos[i].BorderThickness = new Thickness(2);
                        combos[i].SetResourceReference(Control.BorderBrushProperty, ok ? "Good" : "Bad");
                        combos[i].ToolTip = ok ? null : "Bonne réponse : " + q.Pairs[i].Right;
                    }
                    return QuizGrader.GradeMatching(q, mapping);
                };
                break;
            }
        }

        var check = Ui.Button("Valider  (Entrée)", (_, _) => Check(), "AccentButton");
        check.Padding = new Thickness(20, 12, 20, 12);
        check.HorizontalAlignment = HorizontalAlignment.Stretch;
        _bottom.Content = check;
    }

    private static void Mark(Control c, string brushKey)
    {
        c.SetResourceReference(ForegroundProperty, brushKey);
        c.FontWeight = FontWeights.SemiBold;
        if (c is ContentControl { Content: TextBlock tb }) tb.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
    }

    private void Check()
    {
        if (_checked || _grader == null) return;
        _checked = true;
        var q = _questions[_index];
        var s = _grader();
        _score += s;
        _answers.Add((q, s));
        var ok = s >= 0.999;
        var fb = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 20, 0, 0) };
        fb.SetResourceReference(Border.BackgroundProperty, ok ? "GoodSoft" : "BadSoft");
        var sp = new StackPanel();
        var head = new TextBlock { Text = ok ? "Bonne réponse !" : s > 0 ? $"Presque : {s * 100:0} % des points" : "Mauvaise réponse", FontWeight = FontWeights.SemiBold, FontSize = 15 };
        head.SetResourceReference(TextBlock.ForegroundProperty, ok ? "Good" : "Bad");
        sp.Children.Add(head);
        if (!ok)
        {
            var ans = Ui.Text("Réponse : " + QuizGrader.CorrectAnswerText(q), "Body");
            ans.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(ans);
        }
        if (!string.IsNullOrWhiteSpace(q.Explanation))
        {
            var ex = Ui.Text(q.Explanation, "Body");
            ex.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(ex);
        }
        fb.Child = sp;
        _body.Children.Add(fb);
        var next = Ui.Button(_index + 1 < _questions.Count ? "Question suivante  (Entrée)" : "Voir le résultat", (_, _) => { _index++; ShowQuestion(); }, "AccentButton");
        next.Padding = new Thickness(20, 12, 20, 12);
        next.HorizontalAlignment = HorizontalAlignment.Stretch;
        _bottom.Content = next;
        next.Focus();
    }

    private void ShowResults()
    {
        var max = _questions.Count;
        var pct = _score * 100 / max;
        AppState.Store.LogQuiz(new QuizResult { QuizId = _item.Id, Time = DateTime.Now, Score = Math.Round(_score, 2), Max = max });
        _info.Text = "Résultat";
        var big = new TextBlock { Text = $"{pct:0} %", FontSize = 56, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        big.SetResourceReference(TextBlock.ForegroundProperty, pct >= 80 ? "Good" : pct >= 50 ? "Warn" : "Bad");
        _body.Children.Add(big);
        var s = Ui.Text($"{_score:0.#} point(s) sur {max}. " + (pct >= 80 ? "Excellent travail !" : pct >= 50 ? "C'est bien, continue comme ça." : "Revois ton cours et tes fiches, puis réessaie."), "Body");
        s.HorizontalAlignment = HorizontalAlignment.Center;
        s.TextAlignment = TextAlignment.Center;
        _body.Children.Add(s);
        var mistakes = _answers.Where(a => a.Score < 0.999).ToList();
        if (mistakes.Count > 0)
        {
            var h = Ui.Text("À retravailler", "H3");
            h.Margin = new Thickness(0, 24, 0, 8);
            _body.Children.Add(h);
            foreach (var (q, _) in mistakes)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                sp.Children.Add(Ui.Text(q.Type == QuestionType.FillBlanks ? "Texte à trous" : q.Prompt, "H3"));
                sp.Children.Add(Ui.Text(QuizGrader.CorrectAnswerText(q), "Muted"));
                _body.Children.Add(sp);
            }
        }
        var row = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        var again = Ui.Button("Recommencer", (_, _) => { AppState.Main.GoBack(); Start(_item); }, "SecondaryButton", Icons.Refresh);
        again.Margin = new Thickness(0, 0, 6, 0);
        again.Padding = new Thickness(16, 12, 16, 12);
        row.Children.Add(again);
        var done = Ui.Button("Terminer", (_, _) => AppState.Main.GoBack(), "AccentButton");
        done.Margin = new Thickness(6, 0, 0, 0);
        row.Children.Add(done);
        _bottom.Content = row;
    }
}
