using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using StudyDesk.Core.Services;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Views;

public sealed class StatsView : PageBase
{
    public override string NavKey => "stats";
    private static readonly CultureInfo Fr = new("fr-FR");

    public StatsView() : base("Statistiques", "Ta régularité et ce que tu maîtrises, matière par matière.")
    {
        Build();
    }

    public override void Refresh() => Build();

    private void Build()
    {
        Body.Children.Clear();
        var store = AppState.Store;
        var s = new StatsCalculator(store);

        // --------------------------------------------- Chiffres clés
        var kpis = new UniformGrid { Columns = 5 };
        kpis.Children.Add(Kpi(s.StreakDays.ToString(), s.StreakDays > 1 ? "jours de suite" : "jour de suite", "Jours consécutifs avec au moins une révision, un quiz ou un Pomodoro."));
        kpis.Children.Add(Kpi(s.ReviewsPerDay(7).Sum(d => d.Count).ToString(), "révisions sur 7 jours", null));
        kpis.Children.Add(Kpi(FormatMinutes(s.MinutesThisWeek), "de Pomodoro cette semaine", null));
        kpis.Children.Add(Kpi(s.Retention30Days is { } r ? $"{r:0} %" : "–", "de bonnes réponses (30 j)", "Part des fiches retrouvées (Difficile, Bien ou Facile) sur les 30 derniers jours."));
        kpis.Children.Add(Kpi(s.QuizAverage() is { } q ? $"{q:0} %" : "–", "moyenne aux quiz", "Moyenne des 20 derniers quiz."));
        Body.Children.Add(kpis);

        // --------------------------------------------- Graphiques
        var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        var reviews = s.ReviewsPerDay(30);
        grid.Children.Add(ChartCard("Révisions par jour", "30 derniers jours", BarChart(reviews, "Accent", d => d.Day.Day == 1 || d.Day.Day % 5 == 0 ? d.Day.ToString("d/MM") : "", d => $"{d.Day.ToString("dddd d MMMM", Fr)} : {Ui.Plural(d.Count, "révision")}")));
        var forecast = s.Forecast(7);
        var fc = ChartCard("À réviser ces prochains jours", "Prévision des fiches à revoir", BarChart(forecast, "Warn", d => d.Day == DateTime.Today ? "auj." : d.Day.ToString("ddd", Fr), d => $"{d.Day.ToString("dddd d MMMM", Fr)} : {Ui.Plural(d.Count, "fiche")}"));
        Grid.SetColumn(fc, 2);
        grid.Children.Add(fc);
        Body.Children.Add(grid);

        var minutes = s.MinutesPerDay(14);
        if (minutes.Any(m => m.Count > 0))
        {
            var mc = ChartCard("Temps de travail (Pomodoro)", "14 derniers jours, en minutes", BarChart(minutes, "Good", d => d.Day.ToString("dd", Fr), d => $"{d.Day.ToString("dddd d MMMM", Fr)} : {FormatMinutes(d.Count)}"));
            mc.Margin = new Thickness(0, 16, 0, 0);
            Body.Children.Add(mc);
        }

        // --------------------------------------------- Maîtrise par matière
        Body.Children.Add(SectionTitle("Maîtrise par matière"));
        var mastery = new StackPanel();
        var any = false;
        foreach (var m in s.MasteryBySubject())
        {
            if (m.TotalCards == 0) continue;
            any = true;
            var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            var name = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(Ui.Dot(m.Subject.ColorHex));
            var nt = Ui.Text(m.Subject.Name, "H3");
            nt.Margin = new Thickness(8, 0, 0, 0);
            name.Children.Add(nt);
            row.Children.Add(name);
            var bar = new ProgressBar { Style = Ui.Style("ProgressBarThin"), Height = 10, Value = m.Percent, Foreground = Ui.BrushFromHex(m.Subject.ColorHex), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(bar, 1);
            row.Children.Add(bar);
            var info = Ui.Text($"{m.Percent:0} % · {m.Mastered}/{m.TotalCards} maîtrisées · {m.Due} à revoir", "Muted");
            info.Margin = new Thickness(14, 0, 0, 0);
            info.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(info, 2);
            row.Children.Add(info);
            mastery.Children.Add(row);
        }
        if (!any) mastery.Children.Add(Ui.Text("Crée des fiches et révise-les pour voir ta progression ici.", "Muted"));
        Body.Children.Add(Ui.Card(mastery));
        var expl = Ui.Text("Une fiche est maîtrisée quand tu la retrouves assez bien pour ne la revoir que dans 3 semaines ou plus.", "Muted");
        expl.Margin = new Thickness(2, 8, 0, 0);
        Body.Children.Add(expl);

        // --------------------------------------------- Derniers quiz
        var quizzes = s.RecentQuizzes(10);
        if (quizzes.Count > 0)
        {
            Body.Children.Add(SectionTitle("Derniers quiz"));
            var list = new StackPanel();
            foreach (var (res, title) in quizzes)
            {
                var row = new DockPanel { Margin = new Thickness(0, 5, 0, 5) };
                var pct = res.Max == 0 ? 0 : res.Score / res.Max * 100;
                var score = new TextBlock { Text = $"{pct:0} %", FontWeight = FontWeights.SemiBold, Width = 60, TextAlignment = TextAlignment.Right };
                score.SetResourceReference(TextBlock.ForegroundProperty, pct >= 80 ? "Good" : pct >= 50 ? "Warn" : "Bad");
                DockPanel.SetDock(score, Dock.Right);
                row.Children.Add(score);
                var when = Ui.Text(Ui.RelativeDate(res.Time) + (res.Source == "phone" ? " · téléphone" : ""), "Muted");
                when.Width = 190;
                DockPanel.SetDock(when, Dock.Right);
                row.Children.Add(when);
                row.Children.Add(Ui.Text(title, "Body"));
                list.Children.Add(row);
            }
            Body.Children.Add(Ui.Card(list));
        }
    }

    private static string FormatMinutes(int m) => m < 60 ? $"{m} min" : $"{m / 60} h {m % 60:00}";

    private static Border Kpi(string value, string label, string? tooltip)
    {
        var sp = new StackPanel();
        var v = new TextBlock { Text = value, FontSize = 28, FontWeight = FontWeights.SemiBold, FontFamily = (FontFamily)Application.Current.FindResource("DisplayFont") };
        v.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        sp.Children.Add(v);
        sp.Children.Add(Ui.Text(label, "Muted"));
        var c = Ui.Card(sp);
        c.Margin = new Thickness(0, 0, 10, 10);
        c.ToolTip = tooltip;
        return c;
    }

    private static Border ChartCard(string title, string subtitle, UIElement chart)
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.Text(title, "H3"));
        var st = Ui.Text(subtitle, "Muted");
        st.Margin = new Thickness(0, 2, 0, 12);
        sp.Children.Add(st);
        sp.Children.Add(chart);
        return Ui.Card(sp);
    }

    /// <summary>Histogramme simple : une barre par jour, valeur maximale indiquée.</summary>
    private static UIElement BarChart(IReadOnlyList<DayCount> data, string colorKey, Func<DayCount, string> label, Func<DayCount, string> tip)
    {
        const double height = 150;
        var max = Math.Max(1, data.Max(d => d.Count));
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Lignes de repère (0, moitié, max)
        var guides = new Grid();
        foreach (var f in new[] { 0.0, 0.5, 1.0 })
        {
            var line = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(28, 0, 0, f * (height - 16)), Opacity = 0.6 };
            line.SetResourceReference(Border.BackgroundProperty, "Line");
            guides.Children.Add(line);
            var lbl = new TextBlock { Text = Math.Round(max * f).ToString(), FontSize = 10, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, f * (height - 16) - 6) };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "FgMuted");
            guides.Children.Add(lbl);
        }
        root.Children.Add(guides);

        var bars = new UniformGrid { Rows = 1, Columns = data.Count, Margin = new Thickness(30, 0, 0, 0) };
        var labels = new UniformGrid { Rows = 1, Columns = data.Count, Margin = new Thickness(30, 4, 0, 0) };
        for (int i = 0; i < data.Count; i++)
        {
            var d = data[i];
            var h = d.Count == 0 ? 2 : Math.Max(4, d.Count / (double)max * (height - 16));
            var bar = new Border { Height = h, VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(3, 3, 0, 0), Margin = new Thickness(data.Count > 15 ? 1.5 : 5, 0, data.Count > 15 ? 1.5 : 5, 0), ToolTip = tip(d) };
            bar.SetResourceReference(Border.BackgroundProperty, d.Count == 0 ? "Line" : colorKey);
            if (i == data.Count - 1 && d.Count > 0) bar.Opacity = 1; else if (d.Count > 0) bar.Opacity = 0.85;
            bars.Children.Add(new Grid { Background = Brushes.Transparent, Children = { bar }, ToolTip = tip(d) });
            var t = new TextBlock { Text = label(d), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
            t.SetResourceReference(TextBlock.ForegroundProperty, "FgMuted");
            labels.Children.Add(t);
        }
        root.Children.Add(bars);
        Grid.SetRow(labels, 1);
        root.Children.Add(labels);
        return root;
    }
}
