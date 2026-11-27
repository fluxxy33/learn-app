using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;
using StudyDesk.Helpers;
using StudyDesk.Themes;
using StudyDesk.Views;

namespace StudyDesk.Services;

/// <summary>
/// Autotest lancé avec « StudyDesk.exe --selftest » (utilisé par la compilation automatique) :
/// ouvre chaque écran avec des données d'exemple, fait des captures et note les erreurs.
/// </summary>
public static class SelfTest
{
    public static readonly List<string> Errors = new();
    private static readonly StringBuilder Report = new();
    private static string _out = "";

    public static async Task RunAsync(MainWindow main)
    {
        _out = Environment.GetEnvironmentVariable("STUDYDESK_SELFTEST_OUT") ?? Path.Combine(Environment.CurrentDirectory, "selftest");
        Directory.CreateDirectory(_out);
        await Wait(1500);
        var store = AppState.Store;
        var doc = store.ItemsOf(ItemKind.Document).First();
        var deck = store.ItemsOf(ItemKind.Deck).First();
        var quiz = store.ItemsOf(ItemKind.Quiz).First();
        var show = store.ItemsOf(ItemKind.Slideshow).First();

        // Quelques révisions pour avoir des statistiques
        var d = store.LoadDeck(deck.Id);
        var rnd = new Random(3);
        for (int day = 12; day >= 1; day--)
            foreach (var c in d.Cards.Take(rnd.Next(2, 8)))
            {
                var t = DateTime.Now.AddDays(-day);
                Srs.Apply(c, (ReviewGrade)rnd.Next(0, 4), t);
                store.Stats.Reviews.Add(new ReviewLogEntry { DeckId = deck.Id, CardId = c.Id, Time = t, Grade = ReviewGrade.Good });
            }
        store.SaveDeck(d);
        store.Stats.QuizResults.Add(new QuizResult { QuizId = quiz.Id, Time = DateTime.Now.AddDays(-2), Score = 4, Max = 5 });
        store.Stats.Sessions.Add(new StudySession { Start = DateTime.Now.AddDays(-1), Minutes = 50 });
        store.SaveStats();

        await Step("01-accueil", () => main.NavigateTo("home"), main);
        await Step("02-bibliotheque", () => main.NavigateTo("library"), main);
        await Step("03-reviser", () => main.NavigateTo("review"), main);
        await Step("04-quiz", () => main.NavigateTo("quiz"), main);
        await Step("05-statistiques", () => main.NavigateTo("stats"), main);
        await Step("06-tutoriels", () => main.NavigateTo("tutorials"), main);
        await Step("07-tutoriel-expose", () => main.Navigate(new TutorialsView("expose")), main);
        await Step("08-reglages", () => main.NavigateTo("settings"), main);

        await Step("10-document", () => ItemActions.Open(doc), main);
        await Check("10b-document-enregistrement", () =>
        {
            if (main.CurrentPage is not DocumentEditorView) throw new Exception("L'éditeur de document n'est pas ouvert");
            ((IPage)main.CurrentPage).OnLeave();
            var loaded = DocumentIO.Load(store, doc);
            var outline = DocumentIO.Outline(loaded);
            var cards = Converters.CardsFromOutline(outline);
            var questions = Converters.QuizFromOutline(outline);
            if (!File.Exists(store.ContentPath(doc))) throw new Exception("Fichier .sdoc absent");
            if (!File.ReadAllText(store.DocumentHtmlPath(doc.Id)).Contains("<h1>")) throw new Exception("HTML sans titre");
            if (cards.Count < 3) throw new Exception($"Seulement {cards.Count} fiches générées");
            if (!questions.Any(q => q.Type == QuestionType.FillBlanks)) throw new Exception("Aucun texte à trous généré");
            Log($"   document : {outline.Count} blocs, {cards.Count} fiches, {questions.Count} questions");
            // Image + tableau : aller-retour d'enregistrement
            var bmp = new RenderTargetBitmap(64, 48, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var ctx = dv.RenderOpen()) ctx.DrawRectangle(Brushes.OrangeRed, null, new Rect(0, 0, 64, 48));
            bmp.Render(dv);
            loaded.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Image { Source = bmp, Width = 64 }));
            loaded.Blocks.Add(DocStyles.CreateBox("Test"));
            DocumentIO.Save(store, doc, loaded);
            var again = DocumentIO.Load(store, doc);
            var hasImage = again.Blocks.OfType<BlockUIContainer>().Any(b => b.Child is System.Windows.Controls.Image);
            if (!hasImage) throw new Exception("L'image n'a pas été conservée à l'enregistrement");
            if (!File.ReadAllText(store.DocumentHtmlPath(doc.Id)).Contains("data:image")) throw new Exception("Image absente du HTML");
        });
        await Step("11-document-rouvert", () => { main.GoBack(); ItemActions.Open(doc); }, main);
        await Step("12-diaporama", () => { main.GoBack(); ItemActions.Open(show); }, main);
        await Check("12b-presentation", async () =>
        {
            var s = store.LoadSlideshow(show.Id);
            var w = new PresenterWindow(s, 1) { WindowState = WindowState.Normal, Width = 1280, Height = 720, Topmost = false };
            w.Show();
            await Wait(900);
            Capture(w, "12b-presentation");
            w.Close();
        });
        await Step("13-fiches", () => { main.GoBack(); ItemActions.Open(deck); }, main);
        await Step("14-seance-revision", () => { main.GoBack(); ReviewSessionView.StartPractice(deck.Id); }, main);
        await Step("15-quiz-editeur", () => { main.GoBack(); ItemActions.Open(quiz); }, main);
        await Step("16-quiz-joueur", () => { main.GoBack(); QuizPlayerView.Start(quiz); }, main);
        await Step("17-telephone", () => { main.GoBack(); main.NavigateTo("phone"); }, main);
        await Step("17c-telephone-active", () => (main.CurrentPage as PhoneView)?.Start(null), main);
        AppState.StopPhone();
        await Check("17b-telephone-serveur", async () =>
        {
            var server = new Core.Phone.PhoneServer(store, f => Application.Current.Dispatcher.InvokeAsync(f).Task, "selftest");
            server.Start(0, "127.0.0.1");
            using var http = new System.Net.Http.HttpClient();
            var json = await http.GetStringAsync($"http://127.0.0.1:{server.Port}/api/data?t=selftest");
            var html = await http.GetStringAsync($"http://127.0.0.1:{server.Port}/api/doc/{doc.Id}?t=selftest");
            server.Dispose();
            if (!json.Contains("\"decks\"") || !html.Contains("Pythagore")) throw new Exception("Réponse du serveur incomplète");
        });

        // Nouveautés 1.1 : catalogue, nouveaux types de quiz, thèmes décorés, écrire la réponse
        await Step("40-catalogue", () => { main.GoBack(); main.NavigateTo("catalog"); }, main);
        LibraryItem? solarQuiz = null, solarDeck = null;
        await Check("40b-catalogue-import", () =>
        {
            foreach (var pack in ContentPacks.All)
            {
                var (dk, qz) = ContentPacks.Import(store, pack);
                if (pack.Id == "systeme-solaire") { solarQuiz = qz; solarDeck = dk; }
            }
            Log($"   catalogue : {ContentPacks.All.Count} contenus importés");
        });
        await Step("41-quiz-nouveaux-types-editeur", () =>
        {
            var q = store.LoadQuiz(solarQuiz!.Id);
            q.ShuffleQuestions = false;
            q.Questions = q.Questions.OrderByDescending(x => x.Type == QuestionType.Ordering).ToList();
            store.SaveQuiz(q);
            ItemActions.Open(solarQuiz!);
        }, main);
        await Step("42-quiz-remettre-dans-ordre", () => { main.GoBack(); QuizPlayerView.Start(solarQuiz!); }, main);
        await Step("43-quiz-classer", () =>
        {
            main.GoBack();
            var q = store.LoadQuiz(solarQuiz!.Id);
            q.Questions = q.Questions.OrderByDescending(x => x.Type == QuestionType.Categorize).ToList();
            q.SecondsPerQuestion = 30;
            store.SaveQuiz(q);
            QuizPlayerView.Start(solarQuiz!);
        }, main);
        await Step("44-quiz-phrase", () =>
        {
            main.GoBack();
            var vocab = store.ItemsOf(ItemKind.Quiz).First(i => i.Title.StartsWith("Vocabulaire anglais"));
            var q = store.LoadQuiz(vocab.Id);
            q.ShuffleQuestions = false;
            q.ExamMode = true;
            q.Questions = q.Questions.OrderByDescending(x => x.Type == QuestionType.WordOrder).ToList();
            store.SaveQuiz(q);
            QuizPlayerView.Start(vocab);
        }, main);
        await Step("45-ecrire-la-reponse", () => { main.GoBack(); ReviewSessionView.StartTyping(solarDeck!.Id); }, main);
        foreach (var themeId in new[] { "cahier", "tableau", "minuit", "histoire" })
        {
            await Step("46-theme-" + themeId, () =>
            {
                main.GoBack();
                var s = store.LoadSlideshow(show.Id);
                s.ThemeId = themeId;
                store.SaveSlideshow(s);
                ItemActions.Open(show);
            }, main);
        }
        main.GoBack();
        await Check("47-themes-rendu", () =>
        {
            var s = store.LoadSlideshow(show.Id);
            foreach (var t in SlideThemes.All)
            {
                var canvas = SlideRenderer.Render(s.Slides[0], t);
                canvas.Measure(new Size(1280, 720));
                canvas.Arrange(new Rect(0, 0, 1280, 720));
                var rtb = new RenderTargetBitmap(320, 180, 24, 24, PixelFormats.Pbgra32);
                rtb.Render(canvas);
            }
            Log($"   thèmes : {SlideThemes.All.Count} rendus sans erreur");
        });

        // Vraie souris, vrai clavier, fonctions en ligne
        SelfTestInteractive.SelfTestLog = Log;
        await Interactive("30-diaporama-souris-clavier", () => SelfTestInteractive.SlideDragWithRealMouse(main), main);
        await Interactive("31-document-clavier-insertion", () => SelfTestInteractive.TypeInDocument(main), main);
        await Interactive("32-revision-clavier", () => SelfTestInteractive.ReviewWithKeyboard(main), main);
        await Check("33-dessin-export", SelfTestInteractive.DrawingDialogExport);
        await Check("34-en-ligne-wikimedia-wikipedia", SelfTestInteractive.Online);
        main.NavigateTo("home");

        // Thème sombre
        await Step("20-sombre-accueil", () => { ThemeService.Apply(AppTheme.Dark); main.NavigateTo("home"); }, main);
        await Step("21-sombre-document", () => ItemActions.Open(doc), main);
        await Step("22-sombre-diaporama", () => { main.GoBack(); ItemActions.Open(show); }, main);
        await Step("23-sombre-statistiques", () => { main.GoBack(); main.NavigateTo("stats"); }, main);
        ThemeService.Apply(AppTheme.Light);

        Log(Errors.Count == 0 ? "RÉSULTAT : OK" : $"RÉSULTAT : {Errors.Count} ERREUR(S)");
        foreach (var e in Errors) Log("ERREUR : " + e);
        File.WriteAllText(Path.Combine(_out, "report.txt"), Report.ToString());
        Application.Current.Shutdown(Errors.Count == 0 ? 0 : 1);
    }

    private static void Log(string line) => Report.AppendLine(line);

    private static async Task Wait(int ms)
    {
        await Task.Delay(ms);
        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static async Task Step(string name, Action action, Window window)
    {
        var before = Errors.Count;
        try
        {
            action();
            await Wait(700);
            Capture(window, name);
            Log((Errors.Count == before ? "OK      " : "ERREUR  ") + name);
        }
        catch (Exception ex)
        {
            Errors.Add(name + " : " + ex);
            Log("ERREUR  " + name + " : " + ex.Message);
        }
    }

    private static async Task Check(string name, Func<Task> action)
    {
        try
        {
            await action();
            Log("OK      " + name);
        }
        catch (Exception ex)
        {
            Errors.Add(name + " : " + ex);
            Log("ERREUR  " + name + " : " + ex.Message);
        }
    }

    private static async Task Interactive(string name, Func<Task<string?>> action, Window window)
    {
        try
        {
            var result = await action();
            await Wait(400);
            Capture(window, name);
            Log((result == null ? "OK      " : "INFO    ") + name + (result == null ? "" : " : " + result));
        }
        catch (Exception ex)
        {
            Errors.Add(name + " : " + ex);
            Log("ERREUR  " + name + " : " + ex.Message);
        }
    }

    private static Task Check(string name, Action action) => Check(name, () => { action(); return Task.CompletedTask; });

    private static void Capture(Window w, string name)
    {
        if (w.Content is not FrameworkElement root || root.ActualWidth < 1) return;
        var rtb = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var ctx = dv.RenderOpen())
        {
            ctx.DrawRectangle(w.Background ?? Brushes.White, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
            ctx.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        }
        rtb.Render(dv);
        var enc = new JpegBitmapEncoder { QualityLevel = 88 };
        var scale = Math.Min(1.0, 1100 / root.ActualWidth);
        enc.Frames.Add(BitmapFrame.Create(new TransformedBitmap(rtb, new ScaleTransform(scale, scale))));
        using var fs = File.Create(Path.Combine(_out, name + ".jpg"));
        enc.Save(fs);
    }
}
