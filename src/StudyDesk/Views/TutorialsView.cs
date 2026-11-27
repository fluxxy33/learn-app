using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Views;

/// <summary>Tutoriels intégrés : méthodes de travail et prise en main.</summary>
public sealed class TutorialsView : PageBase
{
    public override string NavKey => "tutorials";

    public TutorialsView(string? openId = null) : base("Tutoriels", "Des méthodes pour mieux apprendre, et le mode d'emploi de StudyDesk.")
    {
        if (openId != null && Tutorials.Get(openId) is { } t) ShowTutorial(t);
        else ShowList();
    }

    public override void Refresh()
    {
        if (_currentId == null) ShowList();
    }

    private string? _currentId;

    private void ShowList()
    {
        _currentId = null;
        SetTitle("Tutoriels", "Des méthodes pour mieux apprendre, et le mode d'emploi de StudyDesk.");
        Actions.Children.Clear();
        Body.Children.Clear();
        var done = AppState.Store.Library.CompletedTutorials;
        foreach (var group in Tutorials.All.GroupBy(t => t.Category))
        {
            var h = Ui.Text(group.Key, "H2");
            h.Margin = new Thickness(0, Body.Children.Count == 0 ? 0 : 18, 0, 10);
            Body.Children.Add(h);
            var grid = new UniformGrid { Columns = 3 };
            foreach (var t in group)
            {
                var sp = new StackPanel();
                var head = new DockPanel();
                var mins = Ui.Text($"{t.Minutes} min", "Muted");
                DockPanel.SetDock(mins, Dock.Right);
                head.Children.Add(mins);
                if (done.Contains(t.Id))
                {
                    var ok = Ui.IconText(Icons.Check, "Lu", 12);
                    ok.SetResourceReference(TextBlock.ForegroundProperty, "Good");
                    head.Children.Add(ok);
                }
                sp.Children.Add(head);
                var title = Ui.Text(t.Title, "H3");
                title.Margin = new Thickness(0, 6, 0, 4);
                sp.Children.Add(title);
                sp.Children.Add(Ui.Text(t.Summary, "Muted"));
                var b = new Button { Style = Ui.Style("CardButton"), Content = sp, Margin = new Thickness(0, 0, 10, 10), VerticalContentAlignment = VerticalAlignment.Top };
                var tut = t;
                b.Click += (_, _) => ShowTutorial(tut);
                grid.Children.Add(b);
            }
            Body.Children.Add(grid);
        }
    }

    private void ShowTutorial(Tutorial t)
    {
        _currentId = t.Id;
        SetTitle(t.Title, $"{t.Category} · {t.Minutes} min de lecture");
        Actions.Children.Clear();
        var back = Ui.Button("Tous les tutoriels", (_, _) => ShowList(), "SecondaryButton", Icons.Back);
        Actions.Children.Add(back);
        Body.Children.Clear();

        var doc = new FlowDocument { FontFamily = (FontFamily)FindResource("UiFont"), FontSize = 16, PagePadding = new Thickness(0), LineHeight = 26 };
        doc.SetResourceReference(FlowDocument.ForegroundProperty, "Fg");
        MarkupRenderer.Render(doc, t.Markup, themed: true, 16);
        // Le titre est déjà affiché en haut de la page
        if (doc.Blocks.FirstBlock is Paragraph p && DocStyles.HeadingLevel(p) == 1) doc.Blocks.Remove(p);
        var viewer = new FlowDocumentScrollViewer
        {
            Document = doc, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, IsToolBarVisible = false, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left,
            Focusable = false
        };
        Body.Children.Add(viewer);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
        if (t.Action != TutorialAction.None && t.ActionLabel != null)
        {
            var go = Ui.Button(t.ActionLabel, (_, _) => { MarkDone(t); RunAction(t); }, "AccentButton", Icons.Play);
            go.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(go);
        }
        actions.Children.Add(Ui.Button("J'ai compris", (_, _) => { MarkDone(t); ShowList(); }, "SecondaryButton", Icons.Check));
        Body.Children.Add(actions);
        Scroller.ScrollToTop();
    }

    private static void MarkDone(Tutorial t)
    {
        if (AppState.Store.Library.CompletedTutorials.Add(t.Id)) AppState.Store.SaveLibrary();
    }

    private static void RunAction(Tutorial t)
    {
        switch (t.Action)
        {
            case TutorialAction.NewDocumentFromTemplate:
                ItemActions.CreateWithDialog(ItemKind.Document, templateId: t.ActionArgument);
                break;
            case TutorialAction.NewExposeSlideshow:
                ItemActions.CreateWithDialog(ItemKind.Slideshow, templateId: "expose");
                break;
            case TutorialAction.NewDeck:
                ItemActions.CreateWithDialog(ItemKind.Deck);
                break;
            case TutorialAction.NewQuiz:
                ItemActions.CreateWithDialog(ItemKind.Quiz);
                break;
            case TutorialAction.StartReview:
                ReviewSessionView.Start(null);
                break;
            case TutorialAction.StartPomodoro:
                AppState.Pomodoro.StartWork();
                break;
            case TutorialAction.OpenCatalog:
                AppState.Main.NavigateTo("catalog");
                break;
            case TutorialAction.OpenPhone:
                AppState.Main.NavigateTo("phone");
                break;
        }
    }
}
