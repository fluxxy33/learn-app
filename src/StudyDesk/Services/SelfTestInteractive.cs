using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Dialogs;
using StudyDesk.Helpers;
using StudyDesk.Views;

namespace StudyDesk.Services;

/// <summary>
/// Tests de l'autotest qui utilisent la vraie souris et le vrai clavier de Windows (clic, glisser-déposer, frappe),
/// ainsi que les fonctions en ligne et les actions des éditeurs.
/// </summary>
public static class SelfTestInteractive
{
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

    private const uint LeftDown = 0x02, LeftUp = 0x04, KeyUp = 0x02;

    private static T Get<T>(object o, string field) =>
        (T)o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;

    private static object? Call(object o, string method, params object?[] args) =>
        o.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!.Invoke(o, args);

    private static bool MoveTo(Point screen)
    {
        SetCursorPos((int)screen.X, (int)screen.Y);
        return GetCursorPos(out var p) && Math.Abs(p.X - (int)screen.X) <= 1 && Math.Abs(p.Y - (int)screen.Y) <= 1;
    }

    private static async Task Pause(int ms = 150)
    {
        await Task.Delay(ms);
        await Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private static async Task Key(byte vk)
    {
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, KeyUp, UIntPtr.Zero);
        await Pause(80);
    }

    private static async Task Type(string text)
    {
        foreach (var c in text.ToUpperInvariant())
            await Key((byte)c);
    }

    private static void Front(Window w)
    {
        w.Activate();
        SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(w).Handle);
    }

    /// <summary>Retourne null si tout va bien, « IGNORÉ » si Windows refuse la souris (session sans bureau), sinon lève une exception.</summary>
    public static async Task<string?> SlideDragWithRealMouse(MainWindow main)
    {
        var show = AppState.Store.ItemsOf(ItemKind.Slideshow).First();
        ItemActions.Open(show);
        await Pause(800);
        Front(main);
        var editor = (SlideEditorView)main.CurrentPage!;
        var stage = Get<Canvas>(editor, "_stage");
        var host = stage.Children.OfType<ElementHost>().First(h => h.Element.Type == SlideElementType.Text && h.Element.IsTitle);
        var element = host.Element;
        var x0 = element.X;
        System.Windows.Input.MouseButtonEventHandler log = (_, a) =>
        {
            var pos = a.GetPosition(stage);
            ElementHost.Diagnostics.Add($"fenêtre : {a.OriginalSource?.GetType().Name} à ({pos.X:0},{pos.Y:0}) n°{a.ClickCount} capture={Mouse.Captured?.GetType().Name}");
        };
        main.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, log, true);

        // 1) clic : sélection
        var center = host.PointToScreen(new Point(host.ActualWidth / 2, host.ActualHeight / 2));
        if (!MoveTo(center)) return "IGNORÉ (pas de souris disponible)";
        mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero);
        await Pause(100);
        // 2) glisser de 40 pixels écran vers la droite (le titre reste dans la diapositive)
        for (int i = 1; i <= 8; i++)
        {
            MoveTo(new Point(center.X + i * 5, center.Y));
            mouse_event(0x0001, 0, 0, 0, UIntPtr.Zero); // MOUSEEVENTF_MOVE
            await Pause(30);
        }
        mouse_event(LeftUp, 0, 0, 0, UIntPtr.Zero);
        await Pause(400);
        if (element.X <= x0 + 20) throw new Exception($"Le glisser-déposer n'a pas déplacé le titre (X {x0} → {element.X})");

        // 3) double-clic : édition du texte, puis frappe au clavier
        host = stage.Children.OfType<ElementHost>().First(h => h.Element == element);
        center = host.PointToScreen(new Point(host.ActualWidth / 2, host.ActualHeight / 2));
        ElementHost.Diagnostics.Add($"cible : élément X={element.X:0} Y={element.Y:0} L={element.Width:0} H={element.Height:0}, hôte chargé={host.IsLoaded} taille={host.ActualWidth:0}x{host.ActualHeight:0}, écran=({center.X:0},{center.Y:0})");
        MoveTo(center);
        mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero); mouse_event(LeftUp, 0, 0, 0, UIntPtr.Zero);
        await Task.Delay(60);
        mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero); mouse_event(LeftUp, 0, 0, 0, UIntPtr.Zero);
        await Pause(700);
        var focused = Keyboard.FocusedElement?.GetType().Name ?? "rien";
        await Type("TEST");
        await Key(0x1B); // Échap : fin de l'édition
        await Pause(300);
        if (!element.Text.Contains("TEST", StringComparison.OrdinalIgnoreCase)) throw new Exception($"Le texte tapé au clavier n'a pas été enregistré : « {element.Text} » (focus : {focused}, sélection : {Get<SlideElement?>(editor, "_selected")?.Text ?? "aucune"}, journal : {string.Join(" | ", ElementHost.Diagnostics)})");

        // 4) actions du ruban : nouvelle diapositive, zone de texte, forme, annuler
        var before = AppState.Store.LoadSlideshow(show.Id).Slides.Count;
        Call(editor, "AddSlide", SlideLayout.TitleAndContent);
        Call(editor, "AddText", false);
        Call(editor, "AddShape", ShapeKind.Arrow);
        await Pause(300);
        Call(editor, "Undo");
        ((IPage)editor).OnLeave();
        var after = AppState.Store.LoadSlideshow(show.Id);
        if (after.Slides.Count != before + 1) throw new Exception("La nouvelle diapositive n'a pas été enregistrée");
        if (after.Slides.Any(s => s.Elements.Any(e => e.Shape == ShapeKind.Arrow && e.Type == SlideElementType.Shape))) throw new Exception("« Annuler » n'a pas retiré la flèche");
        return null;
    }

    public static async Task<string?> TypeInDocument(MainWindow main)
    {
        var doc = AppState.Store.ItemsOf(ItemKind.Document).First();
        ItemActions.Open(doc);
        await Pause(800);
        Front(main);
        var editor = (DocumentEditorView)main.CurrentPage!;
        var rtb = Get<RichTextBox>(editor, "_rtb");
        // Clic dans la page, à la fin du texte
        var p = rtb.PointToScreen(new Point(rtb.ActualWidth / 2, rtb.ActualHeight - 30));
        if (!MoveTo(p)) return "IGNORÉ (pas de souris disponible)";
        mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero); mouse_event(LeftUp, 0, 0, 0, UIntPtr.Zero);
        await Pause(300);
        rtb.CaretPosition = rtb.Document.ContentEnd;
        await Key(0x0D); // Entrée
        await Type("BONJOUR");
        await Pause(200);
        if (!DocumentIO.PlainText(rtb.Document).Contains("BONJOUR", StringComparison.OrdinalIgnoreCase)) throw new Exception("La frappe au clavier n'apparaît pas dans le document");

        // Image + tableau + encadré par les fonctions du ruban
        var png = ImageHelper.ToPng(new System.Windows.Media.Imaging.RenderTargetBitmap(40, 30, 96, 96, System.Windows.Media.PixelFormats.Pbgra32));
        Call(editor, "InsertImageBytes", png, "Légende de test");
        Call(editor, "InsertTable", 3, 2);
        Call(editor, "InsertBox");
        ((IPage)editor).OnLeave();
        var reloaded = DocumentIO.Load(AppState.Store, doc);
        var text = DocumentIO.PlainText(reloaded);
        if (!text.Contains("BONJOUR", StringComparison.OrdinalIgnoreCase)) throw new Exception("Le texte tapé n'a pas été enregistré");
        if (!reloaded.Blocks.OfType<Table>().Any()) throw new Exception("Le tableau n'a pas été enregistré");
        if (!text.Contains("Légende de test")) throw new Exception("La légende de l'image n'a pas été enregistrée");
        return null;
    }

    public static async Task<string?> ReviewWithKeyboard(MainWindow main)
    {
        var deck = AppState.Store.ItemsOf(ItemKind.Deck).First();
        var d = AppState.Store.LoadDeck(deck.Id);
        foreach (var c in d.Cards.Take(2)) Srs.Reset(c);
        AppState.Store.SaveDeck(d);
        var before = AppState.Store.Stats.Reviews.Count;
        ReviewSessionView.Start(deck.Id);
        await Pause(800);
        Front(main);
        main.CurrentPage!.Focus();
        await Pause(200);
        // Espace = afficher la réponse, 3 = Bien
        for (int i = 0; i < 2; i++)
        {
            await Key(0x20);
            await Pause(200);
            await Key(0x33);
            await Pause(200);
        }
        ((IPage)main.CurrentPage!).OnLeave();
        var added = AppState.Store.Stats.Reviews.Count - before;
        if (added == 0) return "IGNORÉ (le clavier n'a pas atteint la fenêtre)";
        if (added < 2) throw new Exception($"Seulement {added} révision(s) enregistrée(s) au clavier");
        return null;
    }

    public static void DrawingDialogExport()
    {
        var d = new DrawingDialog();
        var ink = Get<InkCanvas>(d, "_ink");
        var pts = new System.Windows.Input.StylusPointCollection(new[] { new System.Windows.Input.StylusPoint(10, 10), new System.Windows.Input.StylusPoint(200, 120), new System.Windows.Input.StylusPoint(300, 40) });
        ink.Strokes.Add(new System.Windows.Ink.Stroke(pts));
        d.Measure(new Size(1000, 800));
        d.Arrange(new Rect(0, 0, 1000, 800));
        var err = (string?)Call(d, "Validate");
        if (err != null) throw new Exception("Dessin : " + err);
        if (d.ResultPng == null || d.ResultPng.Length < 100) throw new Exception("Le dessin n'a pas produit d'image");
        d.Close();
    }

    public static async Task Online()
    {
        var images = await AppState.Online.SearchImagesAsync("prise de la Bastille");
        if (images.Count == 0) throw new Exception("Aucune image trouvée sur Wikimedia Commons");
        var big = await AppState.Online.DownloadAsync(images[0].ThumbUrl.Replace("/320px-", "/1024px-"));
        if (ImageHelper.FromBytes(big) == null) throw new Exception("Image téléchargée illisible");
        var titles = await AppState.Online.SearchWikipediaAsync("Pythagore");
        if (titles.Count == 0) throw new Exception("Aucun article Wikipédia trouvé");
        var summary = await AppState.Online.GetWikipediaSummaryAsync(titles[0]);
        if (summary == null || summary.Extract.Length < 50) throw new Exception("Résumé Wikipédia vide");
        SelfTestLog?.Invoke($"   en ligne : {images.Count} images (« {images[0].CleanTitle} », {images[0].License}), Wikipédia : « {summary.Title} »");
    }

    public static Action<string>? SelfTestLog;
}
