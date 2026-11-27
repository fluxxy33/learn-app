using System.IO;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QRCoder;
using StudyDesk.Core.Models;
using StudyDesk.Core.Phone;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Helpers;
using StudyDesk.Services;

namespace StudyDesk.Views;

/// <summary>Révision sur téléphone : serveur Wi-Fi local, QR code, fichier hors ligne.</summary>
public sealed class PhoneView : PageBase
{
    public override string NavKey => "phone";

    public PhoneView() : base("Téléphone", "Révise tes fiches, fais tes quiz et lis tes cours sur ton téléphone Android. Sans compte et sans cloud.")
    {
        Build();
    }

    public override void Refresh() => Build();

    private void Build()
    {
        Body.Children.Clear();
        Actions.Children.Clear();
        var server = AppState.Phone;
        var running = server?.IsRunning == true;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Body.Children.Add(grid);

        // ------------------------------------------- QR code
        var left = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
        if (running && server!.Url != null)
        {
            var qr = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(14), HorizontalAlignment = HorizontalAlignment.Center };
            qr.Child = new Image { Source = QrImage(server.Url), Width = 260, Height = 260, SnapsToDevicePixels = true };
            RenderOptions.SetBitmapScalingMode(qr.Child, BitmapScalingMode.NearestNeighbor);
            left.Children.Add(qr);
            var scan = Ui.Text("Scanne avec l'appareil photo du téléphone", "Muted");
            scan.HorizontalAlignment = HorizontalAlignment.Center;
            scan.Margin = new Thickness(0, 10, 0, 6);
            left.Children.Add(scan);
            var url = new TextBox { Text = server.Url, IsReadOnly = true, Padding = new Thickness(6, 4, 6, 4), FontSize = 12 };
            left.Children.Add(url);
            var copy = Ui.Button("Copier l'adresse", (_, _) => { try { Clipboard.SetText(server.Url); } catch { } }, "SubtleButton", Icons.Copy);
            copy.HorizontalAlignment = HorizontalAlignment.Left;
            left.Children.Add(copy);
            var addresses = NetworkHelper.GetLocalAddresses();
            if (addresses.Count > 1)
            {
                var lbl = Ui.Text("Ça ne marche pas ? Essaie une autre adresse du PC :", "Muted");
                lbl.Margin = new Thickness(0, 10, 0, 4);
                left.Children.Add(lbl);
                var combo = new ComboBox { ItemsSource = addresses, SelectedItem = server.Address };
                combo.SelectionChanged += (_, _) =>
                {
                    if (combo.SelectedItem is string ip && ip != server.Address) { Start(ip); }
                };
                left.Children.Add(combo);
            }
            var stop = Ui.Button("Désactiver", (_, _) => { AppState.StopPhone(); AppState.Main.RefreshBadges(); Build(); }, "SecondaryButton", Icons.Stop);
            stop.Margin = new Thickness(0, 14, 0, 0);
            stop.HorizontalAlignment = HorizontalAlignment.Left;
            left.Children.Add(stop);
        }
        else
        {
            var icon = Ui.Icon(Icons.Phone, 64);
            icon.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
            icon.Margin = new Thickness(0, 20, 0, 16);
            left.Children.Add(icon);
            var t = Ui.Text("La connexion avec le téléphone est désactivée.", "H3");
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.TextAlignment = TextAlignment.Center;
            left.Children.Add(t);
            var go = Ui.Button("Activer", (_, _) => Start(null), "AccentButton", Icons.Wifi);
            go.Margin = new Thickness(0, 16, 0, 0);
            go.HorizontalAlignment = HorizontalAlignment.Center;
            go.Padding = new Thickness(24, 10, 24, 10);
            left.Children.Add(go);
        }
        grid.Children.Add(Ui.Card(left, new Thickness(20)));

        // ------------------------------------------- Explications
        var right = new StackPanel();
        Grid.SetColumn(right, 2);
        right.Children.Add(Ui.Text("Comment faire", "H2"));
        var steps = new[]
        {
            "Connecte le téléphone au même réseau Wi-Fi que ce PC.",
            "Clique sur Activer. Si Windows le demande, autorise StudyDesk sur les réseaux privés.",
            "Scanne le QR code avec l'appareil photo, puis ouvre le lien.",
            "Sur le téléphone, ajoute la page à l'écran d'accueil (menu ⋮ de Chrome) pour la retrouver facilement."
        };
        for (int i = 0; i < steps.Length; i++)
        {
            var row = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            var n = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), VerticalAlignment = VerticalAlignment.Top };
            n.SetResourceReference(Border.BackgroundProperty, "AccentSoft");
            var nt = new TextBlock { Text = (i + 1).ToString(), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            nt.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
            n.Child = nt;
            DockPanel.SetDock(n, Dock.Left);
            row.Children.Add(n);
            var st = Ui.Text(steps[i], "Body");
            st.Margin = new Thickness(12, 3, 0, 0);
            row.Children.Add(st);
            right.Children.Add(row);
        }

        var what = Ui.Text("Sur le téléphone, tu peux réviser tes fiches (répétition espacée), faire tes quiz et lire tes cours. Tes réponses reviennent sur le PC automatiquement.", "Muted");
        what.Margin = new Thickness(0, 16, 0, 0);
        right.Children.Add(what);

        if (server?.LastSync is { } last)
        {
            var sync = Ui.Text($"Dernière synchronisation : {Ui.RelativeDate(last)}.", "Body");
            sync.Margin = new Thickness(0, 12, 0, 0);
            sync.SetResourceReference(TextBlock.ForegroundProperty, "Good");
            right.Children.Add(sync);
        }

        right.Children.Add(SectionTitle("Sans Wi-Fi"));
        right.Children.Add(Ui.Text("Crée un fichier qui contient tes fiches, tes quiz et tes cours. Envoie-le sur le téléphone (câble USB, e-mail, Drive…) et ouvre-le avec Chrome : il marche sans connexion. Quand tu reviens sur le même Wi-Fi que le PC, avec la connexion activée ici, appuie sur Synchroniser dans le fichier.", "Muted"));
        var export = Ui.Button("Créer le fichier hors ligne…", (_, _) => ExportOffline(), "SecondaryButton", Icons.Download);
        export.Margin = new Thickness(0, 10, 0, 0);
        export.HorizontalAlignment = HorizontalAlignment.Left;
        right.Children.Add(export);

        var priv = Ui.Text("Confidentialité : rien ne passe par Internet. Le téléphone parle directement au PC sur ton réseau local, avec un code secret contenu dans le QR code.", "Muted");
        priv.Margin = new Thickness(0, 20, 0, 0);
        right.Children.Add(priv);
        grid.Children.Add(right);
    }

    private static string Token()
    {
        var s = AppState.Settings;
        if (string.IsNullOrEmpty(s.PhoneToken))
        {
            s.PhoneToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
            AppState.Store.SaveLibrary();
        }
        return s.PhoneToken;
    }

    /// <summary>Les documents jamais ouverts n'ont pas encore de version lisible : on la crée.</summary>
    private static void PrepareDocuments()
    {
        var store = AppState.Store;
        foreach (var item in store.ItemsOf(ItemKind.Document).ToList())
        {
            if (File.Exists(store.DocumentHtmlPath(item.Id))) continue;
            try
            {
                var doc = DocumentIO.Load(store, item);
                File.WriteAllText(store.DocumentHtmlPath(item.Id), DocumentIO.ToHtml(doc));
                File.WriteAllText(store.DocumentTextPath(item.Id), DocumentIO.PlainText(doc));
            }
            catch (Exception ex) { AppState.LogError(ex); }
        }
    }

    internal void Start(string? address)
    {
        PrepareDocuments();
        AppState.StopPhone();
        var dispatcher = Application.Current.Dispatcher;
        var server = new PhoneServer(AppState.Store, f => dispatcher.InvokeAsync(f).Task, Token());
        server.Synced += (r, q) =>
        {
            AppState.Main.RefreshBadges();
            if (AppState.Main.CurrentPage is PhoneView pv) pv.Build();
        };
        server.Log += msg => AppState.LogError(new Exception("Téléphone : " + msg));
        var port = AppState.Settings.PhonePort;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                server.Start(port + attempt, address);
                AppState.Phone = server;
                break;
            }
            catch (SocketException) { /* port occupé : on essaie le suivant */ }
        }
        if (AppState.Phone == null)
        {
            DialogWindow.Info("Téléphone", "Impossible d'ouvrir la connexion : les ports réseau sont occupés. Change le port dans les Réglages.");
            return;
        }
        if (NetworkHelper.GetLocalAddresses().Count == 0)
            DialogWindow.Info("Pas de réseau", "Ce PC ne semble connecté à aucun réseau Wi-Fi ou Ethernet. Connecte-le au même Wi-Fi que ton téléphone.");
        AppState.Main.RefreshBadges();
        Build();
    }

    private static ImageSource QrImage(string text)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(10);
        return ImageHelper.FromBytes(png)!;
    }

    private static void ExportOffline()
    {
        PrepareDocuments();
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"StudyDesk-{PhoneServer.Sanitize(AppState.Store.Profile.Name)}.html",
            Filter = "Page web|*.html"
        };
        if (dlg.ShowDialog() != true) return;
        var server = AppState.Phone;
        var address = server?.Address ?? NetworkHelper.GetLocalAddresses().FirstOrDefault();
        var port = server?.Port ?? AppState.Settings.PhonePort;
        var url = address == null ? null : $"http://{address}:{port}";
        File.WriteAllText(dlg.FileName, WebApp.BuildOffline(AppState.Store, url, Token()));
        DialogWindow.Info("Fichier créé", "Envoie ce fichier sur ton téléphone et ouvre-le avec Chrome. Pour renvoyer ta progression, active la connexion ici puis appuie sur Synchroniser dans le fichier.");
    }
}
