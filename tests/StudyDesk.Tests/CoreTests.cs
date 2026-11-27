using System.Net.Http.Json;
using System.Text.Json;
using StudyDesk.Core.Models;
using StudyDesk.Core.Phone;
using StudyDesk.Core.Services;

namespace StudyDesk.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sdtest-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
}

public class SrsTests
{
    [Fact]
    public void NewCard_Good_Then_Good_GrowsInterval()
    {
        var c = new Flashcard();
        var now = new DateTime(2026, 1, 10, 18, 0, 0);
        Srs.Apply(c, ReviewGrade.Good, now);
        Assert.Equal(1, c.IntervalDays);
        Assert.Equal(new DateTime(2026, 1, 11, 4, 0, 0), c.Due);
        Srs.Apply(c, ReviewGrade.Good, now.AddDays(1));
        Assert.Equal(3, c.IntervalDays);
        Srs.Apply(c, ReviewGrade.Good, now.AddDays(4));
        Assert.Equal(7.5, c.IntervalDays);
        Assert.Equal(3, c.Repetitions);
    }

    [Fact]
    public void Again_ResetsAndLowersEase()
    {
        var c = new Flashcard();
        var now = DateTime.Now;
        Srs.Apply(c, ReviewGrade.Good, now);
        Srs.Apply(c, ReviewGrade.Good, now);
        Srs.Apply(c, ReviewGrade.Again, now);
        Assert.Equal(0, c.Repetitions);
        Assert.Equal(1, c.Lapses);
        Assert.Equal(2.3, c.Ease, 3);
        Assert.True(c.Due < now.AddMinutes(5));
        Assert.True(Srs.IsDue(c, now));
    }

    [Fact]
    public void Easy_NewCard_FourDays_And_EaseNeverBelowMin()
    {
        var c = new Flashcard();
        Srs.Apply(c, ReviewGrade.Easy, DateTime.Now);
        Assert.Equal(4, c.IntervalDays);
        for (int i = 0; i < 20; i++) Srs.Apply(c, ReviewGrade.Hard, DateTime.Now);
        Assert.Equal(Srs.MinEase, c.Ease, 3);
    }

    [Fact]
    public void Queue_RespectsNewLimit()
    {
        var item = new LibraryItem { Kind = ItemKind.Deck };
        var deck = new Deck();
        for (int i = 0; i < 30; i++) deck.Cards.Add(new Flashcard { Front = "q" + i, Back = "a" });
        var due = new Flashcard { Front = "due", Back = "x" };
        Srs.Apply(due, ReviewGrade.Good, DateTime.Now.AddDays(-5));
        deck.Cards.Add(due);
        var q = Srs.BuildQueue(new[] { (item, deck) }, DateTime.Now, 10, 3);
        Assert.Equal(8, q.Count);
        Assert.Equal("due", q[0].Card.Front);
    }

    [Theory]
    [InlineData(0, "< 10 min")]
    [InlineData(3, "3 j")]
    [InlineData(60, "2 mois")]
    public void Describe(double d, string expected) => Assert.Equal(expected, Srs.Describe(d));
}

public class QuizGraderTests
{
    [Fact]
    public void Normalize_IgnoresAccentsCaseAndPunctuation() =>
        Assert.Equal("ete a l'ecole", QuizGrader.Normalize("  Été à l’École. "));

    [Theory]
    [InlineData("Bonaparte", "bonaparte", true)]
    [InlineData("Bonapart", "Bonaparte", true)]
    [InlineData("Napo", "Napoléon", false)]
    [InlineData("1788", "1789", false)]
    [InlineData("chat", "chut", false)]
    public void TextMatches(string given, string expected, bool ok) => Assert.Equal(ok, QuizGrader.TextMatches(given, expected));

    [Fact]
    public void Cloze_ParsesAlternatives()
    {
        var parts = QuizGrader.ParseCloze("Le roi [Louis XVI|Louis 16] meurt en [1793].");
        Assert.Equal(5, parts.Count);
        Assert.Equal(new[] { "Louis XVI", "Louis 16" }, parts[1].Answers);
        var q = new QuizQuestion { Type = QuestionType.FillBlanks, ClozeText = "Le roi [Louis XVI|Louis 16] meurt en [1793]." };
        Assert.Equal(0.5, QuizGrader.GradeFillBlanks(q, new[] { "louis 16", "1792" }, out var per));
        Assert.True(per[0]);
        Assert.False(per[1]);
    }

    [Fact]
    public void MultipleChoice_PartialCredit()
    {
        var q = new QuizQuestion
        {
            Type = QuestionType.MultipleChoice,
            Choices = { new() { Text = "a", IsCorrect = true }, new() { Text = "b", IsCorrect = true }, new() { Text = "c" } }
        };
        Assert.Equal(1, QuizGrader.GradeMultipleChoice(q, new[] { 0, 1 }));
        Assert.Equal(0.5, QuizGrader.GradeMultipleChoice(q, new[] { 0 }));
        Assert.Equal(0, QuizGrader.GradeMultipleChoice(q, new[] { 0, 2 }));
    }

    [Fact]
    public void Matching_CountsCorrectPairs()
    {
        var q = new QuizQuestion { Type = QuestionType.Matching, Pairs = { new() { Left = "1", Right = "un" }, new() { Left = "2", Right = "deux" } } };
        Assert.Equal(1, QuizGrader.GradeMatching(q, new[] { 0, 1 }));
        Assert.Equal(0, QuizGrader.GradeMatching(q, new[] { 1, 0 }));
    }

    [Fact]
    public void Validate_ReportsProblems()
    {
        Assert.NotNull(QuizGrader.Validate(new QuizQuestion { Type = QuestionType.MultipleChoice, Prompt = "x" }));
        Assert.NotNull(QuizGrader.Validate(new QuizQuestion { Type = QuestionType.FillBlanks, ClozeText = "pas de trou" }));
        Assert.Null(QuizGrader.Validate(new QuizQuestion { Type = QuestionType.TrueFalse, Prompt = "x" }));
    }
}

public class ConverterTests
{
    private static List<OutlineBlock> Pythagore() => new()
    {
        OutlineBlock.Plain("Théorème de Pythagore", 1),
        OutlineBlock.Plain("Énoncé", 2),
        new OutlineBlock { Segments = { ("Dans un triangle rectangle, le carré de l'", false), ("hypoténuse", true), (" est égal à la somme des carrés des deux autres côtés.", false) } },
        OutlineBlock.Plain("Définitions", 2),
        OutlineBlock.Plain("Hypoténuse : le plus long côté d'un triangle rectangle."),
        OutlineBlock.Plain("Triangle rectangle : un triangle qui a un angle droit."),
    };

    [Fact]
    public void Document_To_Cards()
    {
        var cards = Converters.CardsFromOutline(Pythagore());
        Assert.Contains(cards, c => c.Front == "Énoncé" && c.Back.Contains("hypoténuse"));
        Assert.Contains(cards, c => c.Front == "Hypoténuse" && c.Back.StartsWith("le plus long"));
        Assert.Contains(cards, c => c.Front == "Triangle rectangle");
        Assert.DoesNotContain(cards, c => c.Front == "Théorème de Pythagore"); // pas de contenu direct
    }

    [Fact]
    public void Document_To_Quiz_HasCloze()
    {
        var qs = Converters.QuizFromOutline(Pythagore());
        var cloze = Assert.Single(qs, q => q.Type == QuestionType.FillBlanks);
        Assert.Contains("[hypoténuse]", cloze.ClozeText);
        Assert.All(qs, q => Assert.Null(QuizGrader.Validate(q)));
    }

    [Fact]
    public void Cards_To_Quiz_MakesMcqAndMatching()
    {
        var cards = Enumerable.Range(1, 6).Select(i => new Flashcard { Front = "Q" + i, Back = "R" + i }).ToList();
        var qs = Converters.QuizFromCards(cards, new Random(1));
        Assert.Equal(6, qs.Count(q => q.Type == QuestionType.MultipleChoice));
        Assert.Single(qs, q => q.Type == QuestionType.Matching);
        Assert.All(qs.Where(q => q.Type == QuestionType.MultipleChoice), q => Assert.Single(q.Choices, c => c.IsCorrect));
    }

    [Fact]
    public void Text_Import()
    {
        var cards = Converters.CardsFromText("to take ; prendre\nto go\tgo aller\n\nrien");
        Assert.Equal(2, cards.Count);
        Assert.Equal("prendre", cards[0].Back);
    }

    [Fact]
    public void Slideshow_To_Cards()
    {
        var show = SlideshowFactory.CreateExposeTemplate("x", "Sujet");
        var cards = Converters.CardsFromSlideshow(show);
        Assert.Contains(cards, c => c.Front == "Conclusion");
    }
}

public class StoreTests
{
    [Fact]
    public void Create_Profile_Seeds_Content_And_Reloads()
    {
        using var tmp = new TempDir();
        var pm = new ProfileManager(tmp.Path);
        var p = pm.Create("Léa");
        var store = pm.Open(p);
        Assert.Equal(8, store.Library.Subjects.Count);
        Assert.Equal(4, store.Library.Items.Count);
        var deck = store.AllDecks().Single();
        Assert.Equal(10, deck.Deck.Cards.Count);
        Assert.True(File.Exists(DefaultContent.SeedPath(store, store.ItemsOf(ItemKind.Document).Single().Id)));

        var pm2 = new ProfileManager(tmp.Path);
        Assert.Single(pm2.Index.Profiles);
        var store2 = pm2.Open(pm2.Index.Profiles[0]);
        Assert.Equal(4, store2.Library.Items.Count);
    }

    [Fact]
    public void Package_RoundTrip_Into_Other_Profile()
    {
        using var tmp = new TempDir();
        var pm = new ProfileManager(tmp.Path);
        var a = pm.Open(pm.Create("A"));
        var deckItem = a.ItemsOf(ItemKind.Deck).Single();
        var deck = a.LoadDeck(deckItem.Id);
        deck.Cards[0].FrontImage = a.ImportMedia(new byte[] { 1, 2, 3 }, ".png");
        a.SaveDeck(deck);
        var pkg = Path.Combine(tmp.Path, "x.studydesk");
        a.ExportPackage(a.Library.Items, pkg);

        var b = pm.Open(pm.Create("B"));
        b.DeleteSubject(b.Library.Subjects.First(s => s.Name == "Anglais"));
        var added = b.ImportPackage(pkg);
        Assert.Equal(4, added.Count);
        var imported = b.LoadDeck(added.Single(i => i.Kind == ItemKind.Deck).Id);
        Assert.Equal(10, imported.Cards.Count);
        Assert.True(File.Exists(b.MediaPath(imported.Cards[0].FrontImage!)));
        Assert.Contains(b.Library.Subjects, s => s.Name == "Anglais");
    }

    [Fact]
    public void Duplicate_Deck_Resets_Progress()
    {
        using var tmp = new TempDir();
        var pm = new ProfileManager(tmp.Path);
        var s = pm.Open(pm.Create("A"));
        var item = s.ItemsOf(ItemKind.Deck).Single();
        var d = s.LoadDeck(item.Id);
        Srs.Apply(d.Cards[0], ReviewGrade.Good, DateTime.Now);
        s.SaveDeck(d);
        var copy = s.Duplicate(item);
        Assert.True(s.LoadDeck(copy.Id).Cards.All(c => c.IsNew));
    }

    [Fact]
    public void Stats_Streak_And_Forecast()
    {
        using var tmp = new TempDir();
        var pm = new ProfileManager(tmp.Path);
        var s = pm.Open(pm.Create("A"));
        var now = DateTime.Now;
        foreach (var d in new[] { 0, 1, 2, 4 })
            s.Stats.Reviews.Add(new ReviewLogEntry { CardId = "x", Time = now.AddDays(-d) });
        var calc = new StatsCalculator(s, now);
        Assert.Equal(3, calc.StreakDays);
        Assert.Equal(30, calc.ReviewsPerDay(30).Count);
        Assert.Equal(1, calc.ReviewsToday);
        Assert.Equal(7, calc.Forecast(7).Count);
        Assert.Equal(10, calc.NewAvailableToday);
    }
}

public class PhoneTests
{
    [Fact]
    public void Sync_Is_Idempotent_And_Updates_Cards()
    {
        using var tmp = new TempDir();
        var pm = new ProfileManager(tmp.Path);
        var s = pm.Open(pm.Create("A"));
        var item = s.ItemsOf(ItemKind.Deck).Single();
        var card = s.LoadDeck(item.Id).Cards[0];
        var req = new SyncRequest
        {
            Reviews = { new SyncReview { DeckId = item.Id, CardId = card.Id, Time = DateTimeOffset.Now, Grade = "easy" } },
            QuizResults = { new SyncQuizResult { QuizId = "q", Time = DateTimeOffset.Now, Score = 3, Max = 5 } }
        };
        Assert.Equal((1, 1), PhoneSync.Apply(s, req));
        Assert.Equal((0, 0), PhoneSync.Apply(s, req));
        Assert.Equal(4, s.LoadDeck(item.Id).Cards[0].IntervalDays);
        Assert.Equal("phone", s.Stats.Reviews.Single().Source);
    }

    [Fact]
    public async Task Server_Serves_App_Data_And_Sync()
    {
        using var tmp = new TempDir();
        var pm = new ProfileManager(tmp.Path);
        var s = pm.Open(pm.Create("A"));
        using var server = new PhoneServer(s, token: "abc");
        server.Start(0, "127.0.0.1");
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.Port}") };

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await http.GetAsync("/api/data?t=wrong")).StatusCode);
        var page = await http.GetStringAsync("/?t=abc");
        Assert.Contains("window.SD_BOOT", page);

        var data = await http.GetFromJsonAsync<JsonElement>("/api/data?t=abc");
        Assert.Equal("A", data.GetProperty("profile").GetString());
        var deck = data.GetProperty("decks")[0];
        var cardId = deck.GetProperty("cards")[0].GetProperty("id").GetString();
        Assert.Equal(5, data.GetProperty("quizzes")[0].GetProperty("questions").GetArrayLength());

        var res = await http.PostAsJsonAsync("/api/sync?t=abc", new
        {
            reviews = new[] { new { deckId = deck.GetProperty("id").GetString(), cardId, time = DateTimeOffset.Now, grade = "good" } },
            quizResults = Array.Empty<object>()
        });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("reviews").GetInt32());

        var offline = await http.GetStringAsync("/offline.html?t=abc");
        Assert.Contains("\"mode\":\"offline\"", offline);
        // la diapositive d'exemple est lisible
        var showId = s.ItemsOf(ItemKind.Slideshow).Single().Id;
        Assert.Contains("Introduction", await http.GetStringAsync($"/api/doc/{showId}?t=abc"));
    }
}

public class V11Tests
{
    [Fact]
    public void New_Question_Types_Grade()
    {
        var ord = new QuizQuestion { Type = QuestionType.Ordering, Items = { "a", "b", "c", "d" } };
        Assert.Equal(1, QuizGrader.GradeOrdering(ord, new[] { "a", "b", "c", "d" }));
        Assert.Equal(0.5, QuizGrader.GradeOrdering(ord, new[] { "a", "b", "d", "c" }));

        var cat = new QuizQuestion { Type = QuestionType.Categorize, Pairs = { new() { Left = "chat", Right = "Nom" }, new() { Left = "vite", Right = "Adverbe" } } };
        Assert.Equal(new[] { "Nom", "Adverbe" }, QuizGrader.Categories(cat));
        Assert.Equal(1, QuizGrader.GradeCategorize(cat, new[] { "nom", "Adverbe" }));
        Assert.Equal(0.5, QuizGrader.GradeCategorize(cat, new[] { "Nom", null }));

        var num = new QuizQuestion { Type = QuestionType.Numeric, NumericAnswer = 28.3, Tolerance = 0.1, Unit = "cm²" };
        Assert.Equal(1, QuizGrader.GradeNumeric(num, "28,27"));
        Assert.Equal(1, QuizGrader.GradeNumeric(num, "28.3 cm²"));
        Assert.Equal(0, QuizGrader.GradeNumeric(num, "29"));
        Assert.Equal(0, QuizGrader.GradeNumeric(num, "abc"));
        Assert.Equal(1, QuizGrader.GradeNumeric(new QuizQuestion { NumericAnswer = 1500 }, "1 500"));

        var wo = new QuizQuestion { Type = QuestionType.WordOrder, Sentence = "I have never been to London." };
        Assert.Equal(1, QuizGrader.GradeWordOrder(wo, QuizGrader.Words("I have never been to London.")));
        Assert.True(QuizGrader.GradeWordOrder(wo, new[] { "I", "never", "have", "been", "to", "London." }) < 1);
        Assert.Contains("28,3 cm²", QuizGrader.CorrectAnswerText(num));
    }

    [Fact]
    public void All_Packs_Import_With_Valid_Questions()
    {
        using var tmp = new TempDir();
        var pm = new ProfileManager(tmp.Path);
        var s = pm.Open(pm.Create("A"));
        Assert.True(ContentPacks.All.Count >= 18);
        Assert.Equal(ContentPacks.All.Count, ContentPacks.All.Select(p => p.Id).Distinct().Count());
        foreach (var pack in ContentPacks.All)
        {
            var (deckItem, quizItem) = ContentPacks.Import(s, pack, new Random(1));
            var deck = s.LoadDeck(deckItem.Id);
            var quiz = s.LoadQuiz(quizItem.Id);
            Assert.True(deck.Cards.Count >= 8, pack.Id);
            Assert.True(quiz.Questions.Count >= 4, pack.Id);
            foreach (var q in quiz.Questions) Assert.True(QuizGrader.Validate(q) == null, pack.Id + " : " + q.Prompt + " → " + QuizGrader.Validate(q));
            // Chaque question « à la main » doit être juste avec sa propre réponse
            foreach (var q in quiz.Questions)
            {
                switch (q.Type)
                {
                    case QuestionType.Ordering: Assert.Equal(1, QuizGrader.GradeOrdering(q, q.Items)); break;
                    case QuestionType.Categorize: Assert.Equal(1, QuizGrader.GradeCategorize(q, q.Pairs.Select(p => p.Right).ToList())); break;
                    case QuestionType.Numeric: Assert.Equal(1, QuizGrader.GradeNumeric(q, q.NumericAnswer.ToString(System.Globalization.CultureInfo.InvariantCulture))); break;
                    case QuestionType.WordOrder: Assert.Equal(1, QuizGrader.GradeWordOrder(q, QuizGrader.Words(q.Sentence))); break;
                }
            }
        }
        Assert.Equal(ContentPacks.All.Count, s.Library.ImportedPacks.Count);
        Assert.Equal(100, ContentPacks.Get("verbes-irreguliers")!.Cards.Length);
    }

    [Fact]
    public void Themes_Are_Unique_And_Numerous()
    {
        Assert.True(SlideThemes.All.Count >= 24);
        Assert.Equal(SlideThemes.All.Count, SlideThemes.All.Select(t => t.Id).Distinct().Count());
    }

    [Fact]
    public void Phone_Payload_Includes_New_Fields()
    {
        using var tmp = new TempDir();
        var pm = new ProfileManager(tmp.Path);
        var s = pm.Open(pm.Create("A"));
        ContentPacks.Import(s, ContentPacks.Get("systeme-solaire")!);
        var json = StudyDesk.Core.Phone.PhonePayload.BuildJson(s, DateTime.Now);
        Assert.Contains("\"items\":[\"Mercure\"", json);
        Assert.Contains("\"examMode\"", json);
    }
}
