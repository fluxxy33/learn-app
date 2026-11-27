using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Views;

public sealed class HomeView : PageBase
{
    public override string NavKey => "home";
    private static readonly CultureInfo Fr = new("fr-FR");

    public HomeView() : base("", null)
    {
        Build();
    }

    public override void Refresh() => Build();

    private void Build()
    {
        var store = AppState.Store;
        var stats = new StatsCalculator(store);
        var hour = DateTime.Now.Hour;
        SetTitle((hour < 18 ? "Bonjour " : "Bonsoir ") + store.Profile.Name, Capitalize(DateTime.Now.ToString("dddd d MMMM", Fr)));
        Body.Children.Clear();
        Actions.Children.Clear();
        var newBtn = Ui.Button("Nouveau", (s, _) => NewMenu((FrameworkElement)s).IsOpen = true, "AccentButton", Icons.Add);
        Actions.Children.Add(newBtn);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 });
        Body.Children.Add(grid);

        var left = new StackPanel();
        var right = new StackPanel();
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);

        // ---------------------------------------------------- Révision du jour
        int due = stats.DueToday, fresh = stats.NewAvailableToday;
        var hero = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(22, 20, 22, 20) };
        hero.SetResourceReference(Border.BackgroundProperty, "Accent");
        var heroGrid = new DockPanel();
        var start = new Button { Content = due + fresh > 0 ? "Réviser maintenant" : "Voir mes fiches", Padding = new Thickness(18, 10, 18, 10), Style = Ui.Style("AccentButton"), VerticalAlignment = VerticalAlignment.Center };
        start.SetResourceReference(BackgroundProperty, "AccentFg");
        start.SetResourceReference(ForegroundProperty, "Accent");
        start.Click += (_, _) => { if (due + fresh > 0) ReviewSessionView.Start(null); else AppState.Main.NavigateTo("review"); };
        DockPanel.SetDock(start, Dock.Right);
        heroGrid.Children.Add(start);
        var heroText = new StackPanel();
        var l1 = new TextBlock { Text = "À réviser aujourd'hui", FontSize = 14, Opacity = 0.9 };
        var l2 = new TextBlock { Text = (due + fresh).ToString(), FontSize = 44, FontWeight = FontWeights.Bold, FontFamily = (FontFamily)FindResource("DisplayFont") };
        var l3 = new TextBlock
        {
            Text = due + fresh == 0 ? "Tout est révisé. Reviens demain, ou crée de nouvelles fiches."
                : $"{Ui.Plural(due, "fiche à revoir", "fiches à revoir")} · {Ui.Plural(fresh, "nouvelle", "nouvelles")} · environ {Math.Max(1, (int)Math.Round((due + fresh) * 0.25))} min",
            FontSize = 14, Opacity = 0.9, TextWrapping = TextWrapping.Wrap
        };
        foreach (var t in new[] { l1, l2, l3 }) { t.SetResourceReference(TextBlock.ForegroundProperty, "AccentFg"); heroText.Children.Add(t); }
        heroGrid.Children.Add(heroText);
        hero.Child = heroGrid;
        left.Children.Add(hero);

        // ---------------------------------------------------- Matières
        left.Children.Add(SectionTitle("Mes matières"));
        var subjects = new UniformGrid { Columns = 3 };
        foreach (var m in stats.MasteryBySubject())
        {
            var s = m.Subject;
            var count = store.Library.Items.Count(i => i.SubjectId == s.Id);
            var sp = new StackPanel();
            var head = new DockPanel();
            var ic = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(7), Background = Ui.BrushFromHex(s.ColorHex), Child = Ui.Icon(s.Icon, 15, Brushes.White) };
            head.Children.Add(ic);
            if (m.Due > 0)
            {
                var pill = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(7, 1, 7, 1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                pill.SetResourceReference(Border.BackgroundProperty, "AccentSoft");
                var pt = new TextBlock { Text = m.Due + " à revoir", FontSize = 11, FontWeight = FontWeights.SemiBold };
                pt.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
                pill.Child = pt;
                head.Children.Add(pill);
            }
            sp.Children.Add(head);
            var name = Ui.Text(s.Name, "H3");
            name.Margin = new Thickness(0, 10, 0, 2);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.TextWrapping = TextWrapping.NoWrap;
            sp.Children.Add(name);
            sp.Children.Add(Ui.Text(Ui.Plural(count, "élément") + (m.TotalCards > 0 ? " · " + Ui.Plural(m.TotalCards, "fiche") : ""), "Muted"));
            var bar = new ProgressBar { Style = Ui.Style("ProgressBarThin"), Value = m.Percent, Margin = new Thickness(0, 10, 0, 4), Foreground = Ui.BrushFromHex(s.ColorHex) };
            sp.Children.Add(bar);
            sp.Children.Add(Ui.Text(m.TotalCards > 0 ? $"{m.Percent:0} % maîtrisé" : "Pas encore de fiches", "Muted"));
            var b = new Button { Style = Ui.Style("CardButton"), Content = sp, Margin = new Thickness(0, 0, 10, 10) };
            var subjectId = s.Id;
            b.Click += (_, _) => { LibraryView.SelectedSubjectId = subjectId; AppState.Main.NavigateTo("library"); };
            subjects.Children.Add(b);
        }
        if (subjects.Children.Count == 0)
            left.Children.Add(Ui.Card(Ui.Text("Aucune matière. Ouvre la Bibliothèque pour en créer une.", "Muted")));
        else left.Children.Add(subjects);

        // ---------------------------------------------------- Récents
        var recent = store.Library.Items.OrderByDescending(i => i.OpenedAt ?? i.ModifiedAt).Take(5).ToList();
        if (recent.Count > 0)
        {
            left.Children.Add(SectionTitle("Récemment ouverts"));
            foreach (var item in recent) left.Children.Add(ItemActions.Row(item, Build));
        }

        // ---------------------------------------------------- Colonne de droite : examens
        var examsHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var addExam = new Button { Style = Ui.Style("IconButton"), Content = Icons.Add, ToolTip = "Ajouter un examen" };
        addExam.Click += (_, _) => AddExam();
        DockPanel.SetDock(addExam, Dock.Right);
        examsHeader.Children.Add(addExam);
        examsHeader.Children.Add(Ui.Text("Prochains examens", "H3"));
        var examsPanel = new StackPanel();
        examsPanel.Children.Add(examsHeader);
        var exams = store.Library.Exams.Where(e => e.Date.Date >= DateTime.Today).OrderBy(e => e.Date).ToList();
        if (exams.Count == 0) examsPanel.Children.Add(Ui.Text("Ajoute la date de ton brevet, de ton bac ou de tes partiels pour voir le compte à rebours.", "Muted"));
        foreach (var ex in exams)
        {
            var days = (ex.Date.Date - DateTime.Today).Days;
            var row = new DockPanel { Margin = new Thickness(0, 6, 0, 6) };
            var del = new Button { Style = Ui.Style("IconButton"), Content = Icons.Cancel, FontSize = 11, Width = 26, Height = 26, ToolTip = "Supprimer" };
            var exam = ex;
            del.Click += (_, _) => { store.Library.Exams.Remove(exam); store.SaveLibrary(); Build(); };
            DockPanel.SetDock(del, Dock.Right);
            row.Children.Add(del);
            var big = new TextBlock { Text = days == 0 ? "Auj." : days.ToString(), FontSize = 26, FontWeight = FontWeights.Bold, Width = 64, FontFamily = (FontFamily)FindResource("DisplayFont"), VerticalAlignment = VerticalAlignment.Center };
            big.SetResourceReference(TextBlock.ForegroundProperty, days <= 7 ? "Bad" : days <= 30 ? "Warn" : "Accent");
            DockPanel.SetDock(big, Dock.Left);
            row.Children.Add(big);
            var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(Ui.Text(ex.Name, "H3"));
            t.Children.Add(Ui.Text((days == 0 ? "aujourd'hui" : days == 1 ? "demain" : $"dans {days} jours") + " · " + ex.Date.ToString("d MMMM", Fr), "Muted"));
            row.Children.Add(t);
            examsPanel.Children.Add(row);
        }
        right.Children.Add(Ui.Card(examsPanel));

        // ---------------------------------------------------- Chiffres
        var nums = new UniformGrid { Columns = 2, Margin = new Thickness(0, 14, 0, 0) };
        nums.Children.Add(Stat(stats.StreakDays.ToString(), stats.StreakDays > 1 ? "jours de suite" : "jour de travail"));
        nums.Children.Add(Stat(FormatMinutes(stats.MinutesThisWeek), "Pomodoro cette semaine"));
        nums.Children.Add(Stat(stats.ReviewsToday.ToString(), "révisions aujourd'hui"));
        nums.Children.Add(Stat(stats.TotalCards > 0 ? $"{stats.MasteredCards * 100 / stats.TotalCards} %" : "–", "fiches maîtrisées"));
        right.Children.Add(nums);

        // ---------------------------------------------------- Conseil
        var notDone = Tutorials.All.Where(t => !store.Library.CompletedTutorials.Contains(t.Id)).ToList();
        var tip = notDone.Count > 0 ? notDone[DateTime.Today.DayOfYear % notDone.Count] : Tutorials.All[DateTime.Today.DayOfYear % Tutorials.All.Count];
        var tipPanel = new StackPanel();
        var tipHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        var bulb = Ui.Icon(Icons.Lightbulb, 15);
        bulb.SetResourceReference(TextBlock.ForegroundProperty, "Warn");
        tipHead.Children.Add(bulb);
        var tipLbl = Ui.Text("Conseil du jour", "Label");
        tipLbl.Margin = new Thickness(8, 0, 0, 0);
        tipLbl.VerticalAlignment = VerticalAlignment.Center;
        tipHead.Children.Add(tipLbl);
        tipPanel.Children.Add(tipHead);
        tipPanel.Children.Add(Ui.Text(tip.Title, "H3"));
        var sum = Ui.Text(tip.Summary, "Muted");
        sum.Margin = new Thickness(0, 4, 0, 10);
        tipPanel.Children.Add(sum);
        var read = Ui.Button($"Lire ({tip.Minutes} min)", (_, _) => AppState.Main.Navigate(new TutorialsView(tip.Id)), "SecondaryButton");
        read.HorizontalAlignment = HorizontalAlignment.Left;
        tipPanel.Children.Add(read);
        var tipCard = Ui.Card(tipPanel);
        tipCard.Margin = new Thickness(0, 14, 0, 0);
        right.Children.Add(tipCard);
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    private static string FormatMinutes(int m) => m < 60 ? $"{m} min" : $"{m / 60} h {m % 60:00}";

    private static Border Stat(string value, string label)
    {
        var sp = new StackPanel();
        var v = new TextBlock { Text = value, FontSize = 22, FontWeight = FontWeights.SemiBold };
        v.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        sp.Children.Add(v);
        sp.Children.Add(Ui.Text(label, "Muted"));
        var c = Ui.Card(sp, new Thickness(14, 12, 14, 12));
        c.Margin = new Thickness(0, 0, 8, 8);
        return c;
    }

    private void AddExam()
    {
        var d = new ExamDialog();
        if (d.ShowDialog() != true) return;
        AppState.Store.Library.Exams.Add(new Exam { Name = d.ExamName, Date = d.Date });
        AppState.Store.SaveLibrary();
        Build();
    }

    public static ContextMenu NewMenu(FrameworkElement target, string? subjectId = null, string? chapterId = null)
    {
        var m = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        foreach (var (kind, label) in new[] { (ItemKind.Document, "Document"), (ItemKind.Slideshow, "Diaporama"), (ItemKind.Deck, "Paquet de fiches"), (ItemKind.Quiz, "Quiz") })
        {
            var mi = new MenuItem { Header = label, Icon = Ui.Icon(ItemActions.KindIcon(kind), 14) };
            var k = kind;
            mi.Click += (_, _) => ItemActions.CreateWithDialog(k, subjectId, chapterId);
            m.Items.Add(mi);
        }
        return m;
    }
}

public sealed class ExamDialog : DialogWindow
{
    private readonly TextBox _name = new() { Padding = new Thickness(6, 5, 6, 5) };
    private readonly DatePicker _date = new() { SelectedDate = DateTime.Today.AddDays(30) };
    public string ExamName => _name.Text.Trim();
    public DateTime Date => _date.SelectedDate ?? DateTime.Today;

    public ExamDialog() : base("Nouvel examen")
    {
        Body.Children.Add(FieldLabel("Nom"));
        _name.Text = "Contrôle de ";
        Body.Children.Add(_name);
        Body.Children.Add(FieldLabel("Date"));
        Body.Children.Add(_date);
        AddOkCancel("Ajouter");
        Loaded += (_, _) => { _name.Focus(); _name.CaretIndex = _name.Text.Length; };
    }

    protected override string? Validate() => ExamName.Length == 0 ? "Donne un nom à l'examen." : _date.SelectedDate == null ? "Choisis une date." : null;
}
