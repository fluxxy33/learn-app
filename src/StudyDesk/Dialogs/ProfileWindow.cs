using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Dialogs;

/// <summary>Écran de démarrage : créer son profil ou choisir qui travaille.</summary>
public sealed class ProfileWindow : Window
{
    private readonly ProfileManager _pm;
    private readonly StackPanel _list = new();
    public Profile? Selected { get; private set; }

    public ProfileWindow(ProfileManager pm)
    {
        _pm = pm;
        Title = "StudyDesk";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Content");
        SetResourceReference(ForegroundProperty, "Fg");
        FontFamily = (FontFamily)FindResource("UiFont");
        FontSize = 14;
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
        Build();
    }

    private void Build()
    {
        var root = new StackPanel { Margin = new Thickness(32, 28, 32, 28) };
        var logo = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/logo.png")), Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(logo);
        var title = Ui.Text(_pm.Index.Profiles.Count == 0 ? "Bienvenue dans StudyDesk" : "Qui travaille aujourd'hui ?", "H1");
        title.Margin = new Thickness(0, 14, 0, 6);
        root.Children.Add(title);

        if (_pm.Index.Profiles.Count == 0)
        {
            root.Children.Add(Ui.Text("Écris tes cours, prépare tes exposés et révise avec des fiches et des quiz. Tout reste sur ton ordinateur.", "Muted"));
            var lbl = Ui.Text("Ton prénom", "Label");
            lbl.Margin = new Thickness(0, 20, 0, 6);
            root.Children.Add(lbl);
            var name = new TextBox { Padding = new Thickness(8, 6, 8, 6), FontSize = 15 };
            root.Children.Add(name);
            var hint = Ui.Text("Des matières et quelques exemples seront créés pour te montrer comment tout fonctionne. Tu pourras les supprimer.", "Muted");
            hint.Margin = new Thickness(0, 10, 0, 0);
            root.Children.Add(hint);
            var go = new Button { Content = "Commencer", Style = Ui.Style("AccentButton"), IsDefault = true, Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(16, 10, 16, 10) };
            go.Click += (_, _) =>
            {
                if (name.Text.Trim().Length == 0) { name.Focus(); return; }
                Selected = _pm.Create(name.Text.Trim());
                DialogResult = true;
            };
            root.Children.Add(go);
            Loaded += (_, _) => name.Focus();
        }
        else
        {
            root.Children.Add(Ui.Text("Chaque profil a sa propre bibliothèque et ses propres statistiques.", "Muted"));
            _list.Margin = new Thickness(0, 18, 0, 0);
            root.Children.Add(_list);
            FillList();
            var add = Ui.Button("Ajouter un profil", (_, _) =>
            {
                var n = DialogWindow.Ask("Nouveau profil", "Prénom", "", "Créer");
                if (n == null) return;
                Selected = _pm.Create(n);
                DialogResult = true;
            }, "SubtleButton", Icons.Add);
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Margin = new Thickness(0, 10, 0, 0);
            root.Children.Add(add);
        }
        Content = root;
    }

    private void FillList()
    {
        _list.Children.Clear();
        foreach (var p in _pm.Index.Profiles.OrderByDescending(p => p.Id == _pm.Index.LastProfileId))
        {
            var row = new DockPanel();
            var avatar = new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Background = Ui.BrushFromHex(p.ColorHex),
                Child = new TextBlock { Text = p.Name.Length > 0 ? p.Name[..1].ToUpperInvariant() : "?", Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            DockPanel.SetDock(avatar, Dock.Left);
            row.Children.Add(avatar);
            var name = Ui.Text(p.Name, "H3");
            name.VerticalAlignment = VerticalAlignment.Center;
            name.Margin = new Thickness(12, 0, 0, 0);
            row.Children.Add(name);
            var btn = new Button { Style = Ui.Style("CardButton"), Content = row, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12) };
            var profile = p;
            btn.Click += (_, _) => { Selected = profile; DialogResult = true; };
            _list.Children.Add(btn);
        }
    }
}
