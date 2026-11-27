using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Views;

/// <summary>Page « Réviser » : la révision du jour et la liste des paquets de fiches.</summary>
public sealed class ReviewHubView : PageBase
{
    public override string NavKey => "review";

    public ReviewHubView() : base("Réviser", "Les fiches reviennent juste avant que tu les oublies.")
    {
        Build();
    }

    public override void Refresh() => Build();

    private void Build()
    {
        Body.Children.Clear();
        Actions.Children.Clear();
        Actions.Children.Add(Ui.Button("Nouveau paquet", (_, _) => ItemActions.CreateWithDialog(ItemKind.Deck, LibraryView.SelectedSubjectId), "AccentButton", Icons.Add));

        var store = AppState.Store;
        var stats = new StatsCalculator(store);
        int due = stats.DueToday, fresh = stats.NewAvailableToday;

        var hero = new DockPanel();
        var start = Ui.Button(due + fresh > 0 ? $"Réviser tout ({due + fresh})" : "Rien à réviser aujourd'hui", (_, _) => ReviewSessionView.Start(null), "AccentButton", Icons.Play);
        start.IsEnabled = due + fresh > 0;
        start.Padding = new Thickness(18, 10, 18, 10);
        DockPanel.SetDock(start, Dock.Right);
        hero.Children.Add(start);
        var t = new StackPanel();
        t.Children.Add(Ui.Text("Aujourd'hui", "H2"));
        t.Children.Add(Ui.Text($"{Ui.Plural(due, "fiche à revoir", "fiches à revoir")} et {Ui.Plural(fresh, "nouvelle fiche", "nouvelles fiches")} (limite : {store.Library.Settings.NewCardsPerDay} nouvelles par jour, modifiable dans les Réglages).", "Muted"));
        hero.Children.Add(t);
        Body.Children.Add(Ui.Card(hero));

        var decks = store.AllDecks().ToList();
        if (decks.Count == 0)
        {
            Body.Children.Add(SectionTitle("Mes paquets"));
            Body.Children.Add(Ui.Card(Ui.Text("Aucun paquet de fiches. Crée-en un, ou transforme un document en fiches (onglet Réviser de l'éditeur).", "Muted")));
            return;
        }
        var now = DateTime.Now;
        foreach (var subject in store.Library.Subjects.OrderBy(s => s.Order))
        {
            var mine = decks.Where(d => d.Item.SubjectId == subject.Id).OrderBy(d => d.Item.Title).ToList();
            if (mine.Count == 0) continue;
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 10) };
            head.Children.Add(Ui.Dot(subject.ColorHex, 14));
            var h = Ui.Text(subject.Name, "H2");
            h.Margin = new Thickness(10, 0, 0, 0);
            head.Children.Add(h);
            Body.Children.Add(head);
            foreach (var (item, deck) in mine) Body.Children.Add(DeckRow(item, deck, now));
        }
    }

    private Border DeckRow(LibraryItem item, Deck deck, DateTime now)
    {
        int due = deck.Cards.Count(c => Srs.IsDue(c, now));
        int fresh = deck.Cards.Count(c => c.IsNew);
        int mastered = deck.Cards.Count(Srs.IsMastered);
        var row = new DockPanel();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var review = Ui.Button("Réviser", (_, _) => ReviewSessionView.Start(item.Id), due + fresh > 0 ? "AccentButton" : "SecondaryButton", Icons.Play);
        review.IsEnabled = due + fresh > 0;
        buttons.Children.Add(review);
        var practice = Ui.Button("Entraînement libre", (_, _) => ReviewSessionView.StartPractice(item.Id), "SecondaryButton", null, "Revoir toutes les fiches sans changer les dates de révision");
        practice.Margin = new Thickness(6, 0, 0, 0);
        practice.IsEnabled = deck.Cards.Count > 0;
        buttons.Children.Add(practice);
        var typing = Ui.Button("Écrire", (_, _) => ReviewSessionView.StartTyping(item.Id), "SecondaryButton", Icons.Edit, "S'entraîner en écrivant la réponse au clavier");
        typing.Margin = new Thickness(6, 0, 0, 0);
        typing.IsEnabled = deck.Cards.Count > 0;
        buttons.Children.Add(typing);
        var edit = new Button { Style = Ui.Style("IconButton"), Content = Icons.Edit, ToolTip = "Modifier les fiches", Margin = new Thickness(4, 0, 0, 0) };
        edit.Click += (_, _) => ItemActions.Open(item);
        buttons.Children.Add(edit);
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(Ui.Text(item.Title, "H3"));
        texts.Children.Add(Ui.Text($"{Ui.Plural(deck.Cards.Count, "fiche")} · {due} à revoir · {Ui.Plural(fresh, "nouvelle")} · {Ui.Plural(mastered, "maîtrisée")}", "Muted"));
        var bar = new ProgressBar { Style = Ui.Style("ProgressBarThin"), Value = deck.Cards.Count == 0 ? 0 : mastered * 100.0 / deck.Cards.Count, Width = 240, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        texts.Children.Add(bar);
        row.Children.Add(texts);
        var card = Ui.Card(row, new Thickness(16, 12, 12, 12));
        card.Margin = new Thickness(0, 0, 0, 8);
        card.ContextMenu = ItemActions.Menu(item, Build);
        return card;
    }
}

/// <summary>Séance de révision : recto, réponse, auto-évaluation.</summary>
public sealed class ReviewSessionView : UserControl, IPage
{
    public string NavKey => "review";

    private sealed class Entry
    {
        public required LibraryItem Item;
        public required Deck Deck;
        public required Flashcard Card;
    }

    private readonly List<Entry> _queue;
    private readonly bool _practice;
    private readonly int _total;
    private int _done;
    private readonly int[] _counts = new int[4];
    private bool _revealed;
    private readonly HashSet<Deck> _dirtyDecks = new();
    private readonly DateTime _started = DateTime.Now;

    private readonly Grid _root = new();
    private readonly ProgressBar _progress = new() { Style = Ui.Style("ProgressBarThin") };
    private readonly TextBlock _info = Ui.Text("", "Muted");
    private readonly StackPanel _cardContent = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _card;
    private readonly ContentControl _bottom = new();

    public static void Start(string? deckId)
    {
        var store = AppState.Store;
        var decks = store.AllDecks().Where(d => deckId == null || d.Item.Id == deckId).ToList();
        Launch(decks, false, deckId == null ? "Révision du jour" : decks.FirstOrDefault().Item?.Title ?? "Révision");
    }

    public static void StartSubject(string subjectId)
    {
        var store = AppState.Store;
        var decks = store.AllDecks().Where(d => d.Item.SubjectId == subjectId).ToList();
        Launch(decks, false, store.GetSubject(subjectId)?.Name ?? "Révision");
    }

    public static void StartPractice(string deckId)
    {
        var store = AppState.Store;
        var decks = store.AllDecks().Where(d => d.Item.Id == deckId).ToList();
        Launch(decks, true, (decks.FirstOrDefault().Item?.Title ?? "") + " · entraînement libre");
    }

    /// <summary>Entraînement en écrivant la réponse au clavier (ne change pas les dates de révision).</summary>
    public static void StartTyping(string deckId)
    {
        var store = AppState.Store;
        var decks = store.AllDecks().Where(d => d.Item.Id == deckId).ToList();
        Launch(decks, true, (decks.FirstOrDefault().Item?.Title ?? "") + " · écrire la réponse", typing: true);
    }

    private static void Launch(List<(LibraryItem Item, Deck Deck)> decks, bool practice, string title, bool typing = false)
    {
        var store = AppState.Store;
        List<Entry> queue;
        if (practice)
        {
            var rnd = new Random();
            queue = decks.SelectMany(d => d.Deck.Cards.Select(c => new Entry { Item = d.Item, Deck = d.Deck, Card = c })).OrderBy(_ => rnd.Next()).ToList();
        }
        else
        {
            var q = Srs.BuildQueue(decks, DateTime.Now, store.Library.Settings.NewCardsPerDay, Srs.NewCardsStudiedToday(store.Stats, DateTime.Now));
            queue = q.Select(x => new Entry { Item = x.Item, Deck = x.Deck, Card = x.Card }).ToList();
        }
        if (queue.Count == 0)
        {
            DialogWindow.Info("Rien à réviser", practice ? "Ce paquet ne contient aucune fiche." : "Tout est révisé pour aujourd'hui. Tu peux faire un entraînement libre si tu veux continuer.");
            return;
        }
        AppState.Main.Navigate(new ReviewSessionView(queue, practice, title, typing));
    }

    private readonly bool _typing;
    private int _typedRight, _typedTotal;
    private TextBox? _answerBox;

    private ReviewSessionView(List<Entry> queue, bool practice, string title, bool typing = false)
    {
        _queue = queue;
        _practice = practice;
        _typing = typing;
        _total = queue.Count;
        Focusable = true;

        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = new StackPanel { Margin = new Thickness(24, 16, 24, 0) };
        var head = new DockPanel();
        var back = new Button { Style = Ui.Style("IconButton"), Content = Icons.Back, ToolTip = "Terminer la séance" };
        back.Click += (_, _) => AppState.Main.GoBack();
        DockPanel.SetDock(back, Dock.Left);
        head.Children.Add(back);
        var edit = new Button { Style = Ui.Style("IconButton"), Content = Icons.Edit, ToolTip = "Corriger cette fiche" };
        edit.Click += (_, _) => EditCurrent();
        DockPanel.SetDock(edit, Dock.Right);
        head.Children.Add(edit);
        var tt = Ui.Text(title, "H3");
        tt.VerticalAlignment = VerticalAlignment.Center;
        tt.Margin = new Thickness(8, 0, 0, 0);
        head.Children.Add(tt);
        top.Children.Add(head);
        _progress.Margin = new Thickness(0, 12, 0, 6);
        top.Children.Add(_progress);
        top.Children.Add(_info);
        _root.Children.Add(top);

        _card = new Border
        {
            MaxWidth = 760, MinHeight = 320, Margin = new Thickness(24, 20, 24, 12), CornerRadius = new CornerRadius(14), Padding = new Thickness(36, 28, 36, 28),
            BorderThickness = new Thickness(1), Child = new ScrollViewer { Content = _cardContent, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 2, Opacity = 0.12 },
            RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1)
        };
        _card.SetResourceReference(Border.BackgroundProperty, "Surface");
        _card.SetResourceReference(Border.BorderBrushProperty, "Line");
        _card.MouseLeftButtonUp += (_, _) => { if (!_revealed) Reveal(); };
        Grid.SetRow(_card, 1);
        _root.Children.Add(_card);

        _bottom.Margin = new Thickness(24, 0, 24, 24);
        _bottom.MaxWidth = 760;
        Grid.SetRow(_bottom, 2);
        _root.Children.Add(_bottom);
        Content = _root;

        PreviewKeyDown += OnKey;
        Loaded += (_, _) => { Focus(); ShowCurrent(); };
    }

    public void OnLeave() => Flush();

    private void Flush()
    {
        foreach (var d in _dirtyDecks) AppState.Store.SaveDeck(d);
        _dirtyDecks.Clear();
        AppState.Store.SaveStats();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;
        if (!_revealed && (e.Key == Key.Space || e.Key == Key.Enter)) { Reveal(); e.Handled = true; return; }
        if (_revealed && _queue.Count > 0)
        {
            int g = e.Key switch { Key.D1 or Key.NumPad1 => 0, Key.D2 or Key.NumPad2 => 1, Key.D3 or Key.NumPad3 or Key.Space or Key.Enter => 2, Key.D4 or Key.NumPad4 => 3, _ => -1 };
            if (g >= 0) { Grade((ReviewGrade)g); e.Handled = true; }
        }
    }

    private void ShowCurrent()
    {
        _revealed = false;
        _cardContent.Children.Clear();
        _progress.Value = _total == 0 ? 100 : _done * 100.0 / _total;
        if (_queue.Count == 0) { ShowSummary(); return; }
        var e = _queue[0];
        _info.Text = $"{e.Item.Title} · {Ui.Plural(_queue.Count, "fiche restante", "fiches restantes")}" + (e.Card.IsNew ? " · nouvelle fiche" : "");
        AddFace("QUESTION", e.Card.Front, e.Card.FrontImage);
        if (_typing)
        {
            _answerBox = new TextBox { FontSize = 18, Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 16, 0, 0), MaxWidth = 520 };
            _answerBox.KeyDown += (_, k) => { if (k.Key == Key.Enter) { CheckTyped(); k.Handled = true; } };
            _cardContent.Children.Add(_answerBox);
            var check = new Button { Style = Ui.Style("AccentButton"), Content = "Vérifier  (Entrée)", Padding = new Thickness(20, 12, 20, 12), HorizontalAlignment = HorizontalAlignment.Stretch };
            check.Click += (_, _) => CheckTyped();
            _bottom.Content = check;
            Dispatcher.BeginInvoke(() => _answerBox?.Focus(), System.Windows.Threading.DispatcherPriority.Input);
            Animate(false);
            return;
        }
        var reveal = new Button { Style = Ui.Style("AccentButton"), Content = "Afficher la réponse  (Espace)", Padding = new Thickness(20, 12, 20, 12), HorizontalAlignment = HorizontalAlignment.Stretch };
        reveal.Click += (_, _) => Reveal();
        _bottom.Content = reveal;
        Animate(false);
    }

    /// <summary>Compare la réponse tapée au verso de la fiche (accents, majuscules et petites fautes tolérés).</summary>
    public static bool TypedAnswerMatches(string typed, string back)
    {
        static string Clean(string s) => System.Text.RegularExpressions.Regex.Replace(QuizGrader.Normalize(s), @"[^\p{L}\p{N}']+", " ").Trim();
        var g = Clean(typed);
        var b = Clean(back);
        if (g.Length == 0) return false;
        if (QuizGrader.TextMatches(g, b)) return true;
        // Une des parties de la réponse (séparées par — ; / , ou parenthèses)
        foreach (var part in System.Text.RegularExpressions.Regex.Split(back, @"[—;/,()\n]|\bou\b"))
        {
            var p = Clean(part);
            if (p.Length > 1 && QuizGrader.TextMatches(g, p)) return true;
        }
        return b.Contains(g) && g.Length >= b.Length * 0.6;
    }

    private void CheckTyped()
    {
        if (_revealed || _queue.Count == 0 || _answerBox == null) return;
        var e = _queue[0];
        var ok = TypedAnswerMatches(_answerBox.Text, e.Card.Back);
        _typedTotal++;
        if (ok) _typedRight++;
        _answerBox.IsReadOnly = true;
        _answerBox.BorderThickness = new Thickness(2);
        _answerBox.SetResourceReference(Control.BorderBrushProperty, ok ? "Good" : "Bad");
        var verdict = Ui.Text(ok ? "✓ Juste !" : "✗ Pas tout à fait", "H3");
        verdict.SetResourceReference(TextBlock.ForegroundProperty, ok ? "Good" : "Bad");
        verdict.HorizontalAlignment = HorizontalAlignment.Center;
        verdict.Margin = new Thickness(0, 8, 0, 0);
        _cardContent.Children.Add(verdict);
        Reveal();
        Focus();
    }

    private void AddFace(string label, string text, string? image)
    {
        var l = Ui.Text(label, "Label");
        l.HorizontalAlignment = HorizontalAlignment.Center;
        _cardContent.Children.Add(l);
        if (image != null && ImageHelper.Load(AppState.Store.MediaPath(image), 900) is { } bmp)
            _cardContent.Children.Add(new Image { Source = bmp, MaxHeight = 220, Margin = new Thickness(0, 6, 0, 10), Stretch = Stretch.Uniform });
        if (!string.IsNullOrWhiteSpace(text))
        {
            var t = new TextBlock { Text = text, FontSize = text.Length > 160 ? 18 : 24, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
            _cardContent.Children.Add(t);
        }
    }

    private void Reveal()
    {
        if (_queue.Count == 0 || _revealed) return;
        _revealed = true;
        var e = _queue[0];
        var sep = new Border { Height = 1, Margin = new Thickness(0, 18, 0, 18) };
        sep.SetResourceReference(Border.BackgroundProperty, "Line");
        _cardContent.Children.Add(sep);
        AddFace("RÉPONSE", e.Card.Back, e.Card.BackImage);

        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4 };
        var labels = new[] { ("À revoir", "Bad", "1"), ("Difficile", "Warn", "2"), ("Bien", "Good", "3"), ("Facile", "Accent", "4") };
        for (int i = 0; i < 4; i++)
        {
            var g = (ReviewGrade)i;
            var sp = new StackPanel();
            var title = new TextBlock { Text = labels[i].Item1, FontWeight = FontWeights.SemiBold, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center };
            title.SetResourceReference(TextBlock.ForegroundProperty, labels[i].Item2);
            sp.Children.Add(title);
            var hint = _practice ? "touche " + labels[i].Item3 : Srs.Describe(Srs.Preview(e.Card, g).Interval) + " · touche " + labels[i].Item3;
            var h = Ui.Text(hint, "Muted");
            h.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(h);
            var b = new Button { Style = Ui.Style("SecondaryButton"), Content = sp, Margin = new Thickness(i == 0 ? 0 : 4, 0, i == 3 ? 0 : 4, 0), Padding = new Thickness(8, 10, 8, 10) };
            b.Click += (_, _) => Grade(g);
            grid.Children.Add(b);
        }
        _bottom.Content = grid;
    }

    private void Grade(ReviewGrade g)
    {
        if (_queue.Count == 0) return;
        var e = _queue[0];
        _queue.RemoveAt(0);
        _counts[(int)g]++;
        if (!_practice)
        {
            var now = DateTime.Now;
            Srs.Apply(e.Card, g, now);
            _dirtyDecks.Add(e.Deck);
            AppState.Store.Stats.Reviews.Add(new ReviewLogEntry { DeckId = e.Item.Id, CardId = e.Card.Id, Time = now, Grade = g, IntervalAfter = e.Card.IntervalDays });
            if (_counts.Sum() % 5 == 0) Flush();
        }
        if (g == ReviewGrade.Again) _queue.Insert(Math.Min(3, _queue.Count), e);
        else _done++;
        ShowCurrent();
    }

    private void Animate(bool _)
    {
        if (SystemParameters.ClientAreaAnimation == false) return;
        var st = (ScaleTransform)_card.RenderTransform;
        var anim = new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = new QuadraticEase() };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    private void EditCurrent()
    {
        if (_queue.Count == 0) return;
        var e = _queue[0];
        var d = new CardEditDialog(e.Card);
        if (d.ShowDialog() != true) return;
        e.Card.Front = d.Front;
        e.Card.Back = d.Back;
        AppState.Store.SaveDeck(e.Deck);
        var revealed = _revealed;
        ShowCurrent();
        if (revealed) Reveal();
    }

    private void ShowSummary()
    {
        Flush();
        _info.Text = "Séance terminée";
        _progress.Value = 100;
        var minutes = Math.Max(1, (int)Math.Round((DateTime.Now - _started).TotalMinutes));
        var big = Ui.Icon(Icons.Accept, 48);
        big.SetResourceReference(TextBlock.ForegroundProperty, "Good");
        _cardContent.Children.Add(big);
        var t = Ui.Text("Bravo, séance terminée !", "H1");
        t.HorizontalAlignment = HorizontalAlignment.Center;
        t.Margin = new Thickness(0, 12, 0, 8);
        _cardContent.Children.Add(t);
        if (_typing && _typedTotal > 0)
        {
            var typed = Ui.Text($"Réponses écrites : {_typedRight} justes sur {_typedTotal}.", "H3");
            typed.HorizontalAlignment = HorizontalAlignment.Center;
            typed.Margin = new Thickness(0, 0, 0, 6);
            _cardContent.Children.Add(typed);
        }
        var s = Ui.Text($"{Ui.Plural(_counts.Sum(), "réponse")} en {minutes} min : {_counts[3]} faciles, {_counts[2]} bien, {_counts[1]} difficiles, {_counts[0]} à revoir.", "Muted");
        s.HorizontalAlignment = HorizontalAlignment.Center;
        s.TextAlignment = TextAlignment.Center;
        _cardContent.Children.Add(s);
        if (!_practice)
        {
            var tomorrow = new StatsCalculator(AppState.Store).Forecast(2)[1].Count;
            var n = Ui.Text($"Demain : {Ui.Plural(tomorrow, "fiche")} à revoir.", "Muted");
            n.HorizontalAlignment = HorizontalAlignment.Center;
            n.Margin = new Thickness(0, 6, 0, 0);
            _cardContent.Children.Add(n);
        }
        var done = Ui.Button("Terminer", (_, _) => AppState.Main.GoBack(), "AccentButton");
        done.Padding = new Thickness(20, 12, 20, 12);
        done.HorizontalAlignment = HorizontalAlignment.Stretch;
        _bottom.Content = done;
        AppState.Main.RefreshBadges();
    }
}

public sealed class CardEditDialog : DialogWindow
{
    private readonly TextBox _front = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60, Padding = new Thickness(6) };
    private readonly TextBox _back = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, Padding = new Thickness(6) };
    public string Front => _front.Text.Trim();
    public string Back => _back.Text.Trim();

    public CardEditDialog(Flashcard c) : base("Corriger la fiche", 560)
    {
        Body.Children.Add(FieldLabel("Recto (question)"));
        _front.Text = c.Front;
        Body.Children.Add(_front);
        Body.Children.Add(FieldLabel("Verso (réponse)"));
        _back.Text = c.Back;
        Body.Children.Add(_back);
        AddOkCancel("Enregistrer");
    }

    protected override string? Validate() => Front.Length == 0 && Back.Length == 0 ? "La fiche est vide." : null;
}
