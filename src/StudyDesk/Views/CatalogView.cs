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

/// <summary>Catalogue de contenus prêts à l'emploi : un clic ajoute un paquet de fiches et un quiz.</summary>
public sealed class CatalogView : PageBase
{
    public override string NavKey => "catalog";
    private static string? _filter;

    public CatalogView() : base("Catalogue", "Des fiches et des quiz prêts à l'emploi. Ajoute-les en un clic, puis modifie-les comme tu veux.")
    {
        Build();
    }

    public override void Refresh() => Build();

    private void Build()
    {
        Body.Children.Clear();
        var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
        void Chip(string label, string? subject)
        {
            var rb = new RadioButton { Style = Ui.Style("Chip"), Content = label, GroupName = "catalog", IsChecked = _filter == subject, Margin = new Thickness(0, 0, 6, 6) };
            rb.Checked += (_, _) => { _filter = subject; Build(); };
            chips.Children.Add(rb);
        }
        Chip("Tout", null);
        foreach (var s in ContentPacks.All.Select(p => p.SubjectName).Distinct()) Chip(s, s);
        Body.Children.Add(chips);

        var imported = AppState.Store.Library.ImportedPacks;
        var grid = new UniformGrid { Columns = 3 };
        foreach (var pack in ContentPacks.All.Where(p => _filter == null || p.SubjectName == _filter))
        {
            var sp = new DockPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var icon = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(8), Background = Ui.BrushFromHex(pack.SubjectColor), Child = Ui.Icon(pack.SubjectIcon, 16, Brushes.White) };
            DockPanel.SetDock(icon, Dock.Left);
            head.Children.Add(icon);
            var meta = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            meta.Children.Add(Ui.Text(pack.SubjectName, "Label"));
            meta.Children.Add(Ui.Text(pack.Level, "Muted"));
            head.Children.Add(meta);
            DockPanel.SetDock(head, Dock.Top);
            sp.Children.Add(head);

            var already = imported.Contains(pack.Id);
            var add = Ui.Button(already ? "Ajouter à nouveau" : "Ajouter", (_, _) => Add(pack), already ? "SecondaryButton" : "AccentButton", already ? Icons.Accept : Icons.Add);
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Margin = new Thickness(0, 12, 0, 0);
            DockPanel.SetDock(add, Dock.Bottom);
            sp.Children.Add(add);

            var texts = new StackPanel();
            texts.Children.Add(Ui.Text(pack.Title, "H3"));
            var d = Ui.Text(pack.Description, "Muted");
            d.Margin = new Thickness(0, 4, 0, 6);
            texts.Children.Add(d);
            var count = pack.Cards.Length * (pack.TwoWayCards ? 2 : 1);
            texts.Children.Add(Ui.Text($"{Ui.Plural(count, "fiche")} · quiz inclus" + (already ? " · déjà ajouté" : ""), "Muted"));
            sp.Children.Add(texts);

            var card = Ui.Card(sp);
            card.Margin = new Thickness(0, 0, 12, 12);
            card.ToolTip = string.Join("\n", pack.Cards.Take(5).Select(c => $"{c.Front} → {c.Back}")) + (pack.Cards.Length > 5 ? "\n…" : "");
            grid.Children.Add(card);
        }
        Body.Children.Add(grid);

        var note = Ui.Text("Ces contenus sont des connaissances libres (faits, dates, définitions, vocabulaire). Vérifie toujours avec ton cours : ton professeur peut attendre une formulation précise.", "Muted");
        note.Margin = new Thickness(0, 8, 0, 0);
        Body.Children.Add(note);
    }

    private void Add(ContentPack pack)
    {
        var (deck, quiz) = ContentPacks.Import(AppState.Store, pack);
        AppState.Main.RefreshBadges();
        var d = new ChoiceDialog("Contenu ajouté", $"« {pack.Title} » a été ajouté dans la matière {pack.SubjectName} : un paquet de fiches et un quiz.",
            "Réviser les fiches maintenant", "Faire le quiz", "Rester dans le catalogue");
        if (d.ShowDialog() == true)
        {
            if (d.SelectedIndex == 0) ReviewSessionView.Start(deck.Id);
            else if (d.SelectedIndex == 1) QuizPlayerView.Start(quiz);
            else Build();
        }
        else Build();
    }
}
