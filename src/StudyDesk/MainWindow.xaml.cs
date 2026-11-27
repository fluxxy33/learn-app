using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Helpers;
using StudyDesk.Services;
using StudyDesk.Views;

namespace StudyDesk;

/// <summary>Une page affichée dans la zone de contenu.</summary>
public interface IPage
{
    /// <summary>Appelé avant de quitter la page (enregistrement automatique).</summary>
    void OnLeave() { }
    /// <summary>Clé de l'élément de navigation à mettre en surbrillance.</summary>
    string NavKey { get; }
    /// <summary>Recharge l'affichage quand on revient sur la page.</summary>
    void Refresh() { }
}

public partial class MainWindow : Window
{
    private readonly Stack<FrameworkElement> _history = new();
    private bool _suppressNav;
    private bool _closingWithoutPrompt;

    public FrameworkElement? CurrentPage => Host.Content as FrameworkElement;

    public MainWindow()
    {
        InitializeComponent();
        var p = AppState.Store.Profile;
        ProfileName.Text = p.Name;
        AvatarLetter.Text = p.Name.Length > 0 ? p.Name[..1].ToUpperInvariant() : "?";
        Avatar.Background = Ui.BrushFromHex(p.ColorHex);
        Title = "StudyDesk — " + p.Name;

        AppState.Pomodoro.Tick += UpdatePomodoro;
        AppState.Pomodoro.PhaseFinished += phase =>
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        };
        UpdatePomodoro();
        AppState.Store.Changed += (_, _) => RefreshBadges();
        RefreshBadges();

        InputBindings.Add(new KeyBinding(new RelayCommand(GoBack), Key.Left, ModifierKeys.Alt));
        Loaded += (_, _) => NavHome.IsChecked = true;
        Closing += OnClosing;
    }

    // ------------------------------------------------------------------ Navigation

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressNav || sender is not RadioButton { Tag: string key }) return;
        _history.Clear();
        ShowPage(CreatePage(key));
    }

    public FrameworkElement CreatePage(string key) => key switch
    {
        "home" => new HomeView(),
        "library" => new LibraryView(),
        "review" => new ReviewHubView(),
        "quiz" => new QuizHubView(),
        "stats" => new StatsView(),
        "tutorials" => new TutorialsView(),
        "phone" => new PhoneView(),
        "settings" => new SettingsView(),
        _ => new HomeView()
    };

    /// <summary>Ouvre une page « par-dessus » la page actuelle (retour possible).</summary>
    public void Navigate(FrameworkElement page)
    {
        if (CurrentPage != null) _history.Push(CurrentPage);
        ShowPage(page);
    }

    /// <summary>Ouvre une section principale (Accueil, Bibliothèque...).</summary>
    public void NavigateTo(string key)
    {
        var rb = key switch
        {
            "home" => NavHome, "library" => NavLibrary, "review" => NavReview, "quiz" => NavQuiz, "stats" => NavStats,
            "tutorials" => NavTutorials, "phone" => NavPhone, "settings" => NavSettings, _ => NavHome
        };
        if (rb.IsChecked == true) { _history.Clear(); ShowPage(CreatePage(key)); }
        else rb.IsChecked = true;
    }

    public void GoBack()
    {
        if (_history.Count == 0) { NavigateTo("home"); return; }
        var page = _history.Pop();
        ShowPage(page);
        // Les pages de liste se mettent à jour (un élément a pu être renommé ou créé)
        if (page is IPage p) p.Refresh();
    }

    private void ShowPage(FrameworkElement page)
    {
        if (CurrentPage is IPage old)
        {
            try { old.OnLeave(); }
            catch (Exception ex) { AppState.LogError(ex); DialogWindow.Info("Enregistrement", "Impossible d'enregistrer : " + ex.Message); }
        }
        Host.Content = page;
        if (page is IPage p) HighlightNav(p.NavKey);
        RefreshBadges();
    }

    private void HighlightNav(string key)
    {
        _suppressNav = true;
        foreach (var rb in new[] { NavHome, NavLibrary, NavReview, NavQuiz, NavStats, NavTutorials, NavPhone, NavSettings })
            rb.IsChecked = (string)rb.Tag == key;
        _suppressNav = false;
    }

    public void RefreshBadges()
    {
        try
        {
            var due = new StatsCalculator(AppState.Store).DueToday;
            DueBadge.Visibility = due > 0 ? Visibility.Visible : Visibility.Collapsed;
            DueBadgeText.Text = due > 99 ? "99+" : due.ToString();
            PhoneDot.Visibility = AppState.Phone?.IsRunning == true ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex) { AppState.LogError(ex); }
    }

    // ------------------------------------------------------------------ Pomodoro

    private void UpdatePomodoro()
    {
        var p = AppState.Pomodoro;
        if (p.Phase == PomodoroPhase.Idle)
        {
            PomodoroPhaseText.Text = "Pomodoro";
            PomodoroTime.Text = $"{AppState.Settings.PomodoroWorkMinutes:00}:00";
            PomodoroProgress.Value = 0;
            PomodoroPlay.Content = Icons.Play;
            PomodoroStop.IsEnabled = false;
        }
        else
        {
            PomodoroPhaseText.Text = p.IsPaused ? p.PhaseName + " · en pause" : p.PhaseName;
            PomodoroTime.Text = $"{(int)p.Remaining.TotalMinutes:00}:{p.Remaining.Seconds:00}";
            PomodoroProgress.Value = p.Total.TotalSeconds > 0 ? 100 * (1 - p.Remaining.TotalSeconds / p.Total.TotalSeconds) : 0;
            PomodoroPlay.Content = p.IsPaused ? Icons.Play : Icons.Pause;
            PomodoroStop.IsEnabled = true;
        }
        PomodoroProgress.SetResourceReference(ForegroundProperty, p.Phase is PomodoroPhase.ShortBreak or PomodoroPhase.LongBreak ? "Good" : "Accent");
        PomodoroCount.Text = p.CompletedToday > 0 ? $"{p.CompletedToday} aujourd'hui" : "";
    }

    private void PomodoroPlay_Click(object sender, RoutedEventArgs e) => AppState.Pomodoro.TogglePause();
    private void PomodoroStop_Click(object sender, RoutedEventArgs e) => AppState.Pomodoro.Stop();

    // ------------------------------------------------------------------ Profil

    private void ProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var p in AppState.Profiles.Index.Profiles.Where(p => p.Id != AppState.Store.Profile.Id))
        {
            var item = new MenuItem { Header = "Passer à " + p.Name };
            var profile = p;
            item.Click += (_, _) => SwitchProfile(profile);
            menu.Items.Add(item);
        }
        var add = new MenuItem { Header = "Ajouter un profil…" };
        add.Click += (_, _) =>
        {
            var name = DialogWindow.Ask("Nouveau profil", "Prénom", "", "Créer");
            if (name != null) SwitchProfile(AppState.Profiles.Create(name, Core.Services.SubjectIcons.Colors[AppState.Profiles.Index.Profiles.Count % 10]));
        };
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        menu.Items.Add(add);
        menu.PlacementTarget = ProfileButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        menu.IsOpen = true;
    }

    public void SwitchProfile(Profile p)
    {
        if (CurrentPage is IPage page) page.OnLeave();
        AppState.Pomodoro.Stop();
        App.OpenProfile(p);
    }

    public void CloseWithoutPrompt()
    {
        _closingWithoutPrompt = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closingWithoutPrompt) return;
        try
        {
            if (CurrentPage is IPage page) page.OnLeave();
            if (AppState.Pomodoro.Phase == PomodoroPhase.Work) AppState.Pomodoro.Stop();
        }
        catch (Exception ex) { AppState.LogError(ex); }
    }
}

/// <summary>Une page d'édition (document, diaporama...) : on ne la recrée pas au retour.</summary>
public interface IEditor { }

public sealed class RelayCommand : ICommand
{
    private readonly Action _action;
    public RelayCommand(Action action) => _action = action;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _action();
}
