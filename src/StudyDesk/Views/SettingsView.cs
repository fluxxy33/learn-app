using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Helpers;
using StudyDesk.Services;
using StudyDesk.Themes;

namespace StudyDesk.Views;

public sealed class SettingsView : PageBase
{
    public override string NavKey => "settings";

    public SettingsView() : base("Réglages", null, 820)
    {
        Build();
    }

    private void Build()
    {
        Body.Children.Clear();
        var store = AppState.Store;
        var s = store.Library.Settings;

        // ------------------------------------------- Profil
        var profile = Section("Profil");
        var nameRow = Row("Prénom", "Le nom affiché sur l'accueil.");
        var name = new TextBox { Text = store.Profile.Name, Width = 220, Padding = new Thickness(6, 4, 6, 4) };
        name.LostFocus += (_, _) =>
        {
            if (name.Text.Trim().Length == 0 || name.Text.Trim() == store.Profile.Name) return;
            AppState.Profiles.Rename(store.Profile, name.Text.Trim());
        };
        nameRow.Children.Add(name);
        profile.Children.Add(nameRow);
        var colorRow = Row("Couleur", "La couleur de ton avatar.");
        var colors = new WrapPanel { MaxWidth = 300 };
        foreach (var c in SubjectIcons.Colors)
        {
            var b = new Button { Width = 24, Height = 24, Margin = new Thickness(0, 0, 4, 4), Background = Ui.BrushFromHex(c), BorderThickness = new Thickness(c == store.Profile.ColorHex ? 3 : 0), ToolTip = c };
            b.SetResourceReference(BorderBrushProperty, "Fg");
            var hex = c;
            b.Click += (_, _) => { store.Profile.ColorHex = hex; AppState.Profiles.Save(); Build(); };
            colors.Children.Add(b);
        }
        colorRow.Children.Add(colors);
        profile.Children.Add(colorRow);

        // ------------------------------------------- Apparence
        var look = Section("Apparence");
        var themeRow = Row("Thème", "Clair, sombre, ou comme Windows.");
        var themes = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (t, label) in new[] { (AppTheme.System, "Comme Windows"), (AppTheme.Light, "Clair"), (AppTheme.Dark, "Sombre") })
        {
            var rb = new RadioButton { Style = Ui.Style("Chip"), Content = label, IsChecked = s.Theme == t, GroupName = "theme" };
            var theme = t;
            rb.Checked += (_, _) => { s.Theme = theme; store.SaveLibrary(); ThemeService.Apply(theme); };
            themes.Children.Add(rb);
        }
        themeRow.Children.Add(themes);
        look.Children.Add(themeRow);

        var fontRow = Row("Police des nouveaux documents", "La police et la taille du texte normal.");
        var fonts = new ComboBox { Width = 160, ItemsSource = new[] { "Segoe UI", "Calibri", "Arial", "Verdana", "Georgia", "Times New Roman", "Cambria", "Comic Sans MS" }, SelectedItem = s.DefaultFontFamily };
        fonts.SelectionChanged += (_, _) => { if (fonts.SelectedItem is string f) { s.DefaultFontFamily = f; store.SaveLibrary(); } };
        var sizes = new ComboBox { Width = 70, Margin = new Thickness(8, 0, 0, 0), ItemsSource = new[] { 12.0, 13, 14, 15, 16, 18 }, SelectedItem = s.DefaultFontSize };
        sizes.SelectionChanged += (_, _) => { if (sizes.SelectedItem is double d) { s.DefaultFontSize = d; store.SaveLibrary(); } };
        fontRow.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { fonts, sizes } });
        look.Children.Add(fontRow);

        // ------------------------------------------- Révision
        var review = Section("Révision");
        var newRow = Row("Nouvelles fiches par jour", "Au-delà, les nouvelles fiches attendent le lendemain. Moins = plus facile à suivre.");
        newRow.Children.Add(NumberBox(s.NewCardsPerDay, 0, 500, v => { s.NewCardsPerDay = v; store.SaveLibrary(); AppState.Main.RefreshBadges(); }));
        review.Children.Add(newRow);
        var workRow = Row("Pomodoro : travail", "Durée d'une session de concentration, en minutes.");
        workRow.Children.Add(NumberBox(s.PomodoroWorkMinutes, 5, 120, v => { s.PomodoroWorkMinutes = v; store.SaveLibrary(); }));
        review.Children.Add(workRow);
        var shortRow = Row("Pomodoro : petite pause", "En minutes.");
        shortRow.Children.Add(NumberBox(s.PomodoroShortBreakMinutes, 1, 30, v => { s.PomodoroShortBreakMinutes = v; store.SaveLibrary(); }));
        review.Children.Add(shortRow);
        var longRow = Row("Pomodoro : grande pause", "Toutes les 4 sessions, en minutes.");
        longRow.Children.Add(NumberBox(s.PomodoroLongBreakMinutes, 5, 60, v => { s.PomodoroLongBreakMinutes = v; store.SaveLibrary(); }));
        review.Children.Add(longRow);

        // ------------------------------------------- En ligne
        var online = Section("En ligne et téléphone");
        var onlineRow = Row("Fonctions en ligne", "Recherche d'images libres (Wikimedia Commons) et de résumés Wikipédia. Seul le mot recherché est envoyé. Tout le reste de l'application fonctionne sans Internet.");
        var toggle = new CheckBox { IsChecked = s.OnlineEnabled, Content = "Activées" };
        toggle.Click += (_, _) => { s.OnlineEnabled = toggle.IsChecked == true; store.SaveLibrary(); };
        onlineRow.Children.Add(toggle);
        online.Children.Add(onlineRow);
        var portRow = Row("Port réseau du téléphone", "À changer seulement si la connexion avec le téléphone ne fonctionne pas.");
        portRow.Children.Add(NumberBox(s.PhonePort, 1024, 65000, v => { s.PhonePort = v; store.SaveLibrary(); }));
        online.Children.Add(portRow);
        var tokenRow = Row("Code de connexion du téléphone", "Crée un nouveau code : les anciens QR codes et fichiers hors ligne ne pourront plus se synchroniser.");
        tokenRow.Children.Add(Ui.Button("Nouveau code", (_, _) =>
        {
            if (!DialogWindow.Confirm("Nouveau code", "Les téléphones déjà connectés devront scanner le nouveau QR code. Continuer ?", "Continuer")) return;
            s.PhoneToken = null;
            store.SaveLibrary();
            AppState.StopPhone();
            AppState.Main.RefreshBadges();
        }, "SecondaryButton", Icons.Refresh));
        online.Children.Add(tokenRow);

        // ------------------------------------------- Données
        var data = Section("Mes données");
        var folderRow = Row("Dossier des données", store.Folder);
        folderRow.Children.Add(Ui.Button("Ouvrir", (_, _) => { try { Process.Start(new ProcessStartInfo("explorer.exe", store.Folder) { UseShellExecute = true }); } catch { } }, "SecondaryButton", Icons.Folder));
        data.Children.Add(folderRow);
        var backupRow = Row("Sauvegarde", "Enregistre tout ton profil (cours, fiches, quiz, images, statistiques) dans un seul fichier .zip, par exemple sur une clé USB.");
        backupRow.Children.Add(Ui.Button("Sauvegarder…", (_, _) => Backup(), "SecondaryButton", Icons.Save));
        data.Children.Add(backupRow);
        var importRow = Row("Importer", "Ajoute le contenu d'un fichier .studydesk partagé par un ami ou un professeur.");
        importRow.Children.Add(Ui.Button("Importer…", (_, _) => ItemActions.Import(() => { }), "SecondaryButton", Icons.Import));
        data.Children.Add(importRow);
        if (AppState.Profiles.Index.Profiles.Count > 1)
        {
            var delRow = Row("Supprimer ce profil", "Efface définitivement ce profil et toutes ses données.");
            delRow.Children.Add(Ui.Button("Supprimer…", (_, _) => DeleteProfile(), "SecondaryButton", Icons.Delete));
            data.Children.Add(delRow);
        }

        var about = Ui.Text($"StudyDesk {typeof(SettingsView).Assembly.GetName().Version?.ToString(3)} · tes données restent sur cet ordinateur.", "Muted");
        about.Margin = new Thickness(0, 24, 0, 0);
        Body.Children.Add(about);
    }

    private StackPanel Section(string title)
    {
        var t = SectionTitle(title);
        if (Body.Children.Count == 0) t.Margin = new Thickness(0, 0, 0, 12);
        Body.Children.Add(t);
        var sp = new StackPanel();
        Body.Children.Add(Ui.Card(sp, new Thickness(18, 6, 18, 6)));
        return sp;
    }

    private static SettingRow Row(string title, string description) => new(title, description);

    /// <summary>Ligne de réglage : texte à gauche, contrôle ajouté ensuite à droite.</summary>
    private sealed class SettingRow : Grid
    {
        public SettingRow(string title, string description)
        {
            Margin = new Thickness(0, 10, 0, 10);
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var texts = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
            texts.Children.Add(Ui.Text(title, "H3"));
            texts.Children.Add(Ui.Text(description, "Muted"));
            Children.Add(texts);
        }

        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
            if (visualAdded is FrameworkElement fe && Children.IndexOf(fe) > 0)
            {
                SetColumn(fe, 1);
                fe.VerticalAlignment = VerticalAlignment.Center;
            }
        }
    }

    private static TextBox NumberBox(int value, int min, int max, Action<int> set)
    {
        var tb = new TextBox { Text = value.ToString(), Width = 80, Padding = new Thickness(6, 4, 6, 4), TextAlignment = TextAlignment.Right };
        tb.LostFocus += (_, _) =>
        {
            if (int.TryParse(tb.Text, out var v)) { v = Math.Clamp(v, min, max); tb.Text = v.ToString(); set(v); }
            else tb.Text = value.ToString();
        };
        return tb;
    }

    private static void Backup()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"StudyDesk-{AppState.Store.Profile.Name}-{DateTime.Now:yyyy-MM-dd}.zip", Filter = "Archive zip|*.zip" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            AppState.Store.Backup(dlg.FileName);
            DialogWindow.Info("Sauvegarde", "Ta sauvegarde est prête. Pour la restaurer, décompresse-la dans le dossier des données à la place du profil.");
        }
        catch (Exception ex) { DialogWindow.Info("Sauvegarde impossible", ex.Message); }
    }

    private static void DeleteProfile()
    {
        var p = AppState.Store.Profile;
        if (!DialogWindow.Confirm("Supprimer le profil", $"Supprimer définitivement le profil « {p.Name} » et toutes ses données ?")) return;
        var other = AppState.Profiles.Index.Profiles.First(x => x.Id != p.Id);
        AppState.StopPhone();
        AppState.Main.SwitchProfile(other);
        AppState.Profiles.Delete(p);
    }
}
