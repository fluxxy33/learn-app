using System.IO;
using System.Windows;
using System.Windows.Threading;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Services;
using StudyDesk.Themes;

namespace StudyDesk;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo("fr-FR");
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = new System.Globalization.CultureInfo("fr-FR");
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");

        AppState.SelfTest = e.Args.Contains("--selftest");
        string? root = null;
        if (AppState.SelfTest)
        {
            root = Path.Combine(Path.GetTempPath(), "StudyDesk-selftest-" + Environment.ProcessId);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        AppState.Profiles = new ProfileManager(root);
        ThemeService.Apply(Core.Models.AppTheme.System);

        var pm = AppState.Profiles;
        Core.Models.Profile? profile;
        if (AppState.SelfTest)
            profile = pm.Create("Test");
        else if (pm.Index.Profiles.Count == 1)
            profile = pm.Index.Profiles[0];
        else
        {
            var w = new ProfileWindow(pm);
            profile = w.ShowDialog() == true ? w.Selected : null;
        }
        if (profile == null) { Shutdown(); return; }

        OpenProfile(profile);
    }

    public static void OpenProfile(Core.Models.Profile profile)
    {
        AppState.StopPhone();
        AppState.Store = AppState.Profiles.Open(profile);
        ThemeService.Apply(AppState.Settings.Theme);
        var old = Current.MainWindow as MainWindow;
        var main = new MainWindow();
        Current.MainWindow = main;
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
        old?.CloseWithoutPrompt();
        if (AppState.SelfTest) _ = SelfTest.RunAsync(main);
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (AppState.Profiles != null) AppState.LogError(e.Exception);
        if (AppState.SelfTest)
        {
            Console.Error.WriteLine("ERREUR: " + e.Exception);
            SelfTest.Errors.Add(e.Exception.ToString());
            e.Handled = true;
            return;
        }
        MessageBox.Show("Une erreur inattendue s'est produite :\n\n" + e.Exception.Message +
                        "\n\nTon travail déjà enregistré n'est pas perdu. Le détail est dans le fichier erreurs.log du dossier de données.",
            "StudyDesk", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppState.StopPhone();
        base.OnExit(e);
    }
}
