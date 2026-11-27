using System.Text;
using System.Text.Json;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;

namespace StudyDesk.Core.Phone;

/// <summary>Données envoyées à l'app téléphone (JSON camelCase).</summary>
public static class PhonePayload
{
    public static object Build(ProfileStore store, DateTime now)
    {
        var lib = store.Library;
        return new
        {
            profile = store.Profile.Name,
            generatedAt = new DateTimeOffset(now),
            newCardsPerDay = lib.Settings.NewCardsPerDay,
            newStudiedToday = Srs.NewCardsStudiedToday(store.Stats, now),
            subjects = lib.Subjects.OrderBy(s => s.Order).Select(s => new
            {
                id = s.Id, name = s.Name, color = s.ColorHex,
                chapters = s.Chapters.OrderBy(c => c.Order).Select(c => new { id = c.Id, name = c.Name })
            }),
            decks = store.ItemsOf(ItemKind.Deck).OrderBy(i => i.Title).Select(i =>
            {
                var d = store.LoadDeck(i.Id);
                return new
                {
                    id = i.Id, title = i.Title, subjectId = i.SubjectId, chapterId = i.ChapterId,
                    cards = d.Cards.Select(c => new
                    {
                        id = c.Id, front = c.Front, back = c.Back, frontImage = c.FrontImage, backImage = c.BackImage,
                        ease = c.Ease, interval = c.IntervalDays, reps = c.Repetitions, lapses = c.Lapses,
                        due = c.Due is { } du ? new DateTimeOffset(DateTime.SpecifyKind(du, DateTimeKind.Local)) : (DateTimeOffset?)null,
                        lastReview = c.LastReview is { } lr ? new DateTimeOffset(DateTime.SpecifyKind(lr, DateTimeKind.Local)) : (DateTimeOffset?)null,
                        createdAt = new DateTimeOffset(DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Local))
                    })
                };
            }),
            quizzes = store.ItemsOf(ItemKind.Quiz).OrderBy(i => i.Title).Select(i =>
            {
                var q = store.LoadQuiz(i.Id);
                return new
                {
                    id = i.Id, title = i.Title, subjectId = i.SubjectId, chapterId = i.ChapterId, shuffle = q.ShuffleQuestions,
                    questions = q.Questions.Where(x => QuizGrader.Validate(x) == null).Select(x => new
                    {
                        id = x.Id, type = x.Type.ToString(), prompt = x.Prompt, image = x.Image, explanation = x.Explanation,
                        choices = x.Choices.Where(c => c.Text.Trim().Length > 0).Select(c => new { text = c.Text, correct = c.IsCorrect }),
                        correctBool = x.CorrectBool,
                        accepted = x.AcceptedAnswers.Where(a => a.Trim().Length > 0),
                        cloze = QuizGrader.ParseCloze(x.ClozeText).Select(p => new { text = p.Text, answers = p.Answers }),
                        pairs = x.Pairs.Where(p => p.Left.Length > 0 && p.Right.Length > 0).Select(p => new { left = p.Left, right = p.Right })
                    })
                };
            }),
            docs = store.Library.Items.Where(i => i.Kind is ItemKind.Document or ItemKind.Slideshow)
                .Where(i => i.Kind == ItemKind.Slideshow || File.Exists(store.DocumentHtmlPath(i.Id)))
                .OrderBy(i => i.Title)
                .Select(i => new { id = i.Id, title = i.Title, kind = i.Kind == ItemKind.Document ? "document" : "slideshow", subjectId = i.SubjectId, chapterId = i.ChapterId, modifiedAt = new DateTimeOffset(DateTime.SpecifyKind(i.ModifiedAt, DateTimeKind.Local)) })
        };
    }

    public static string BuildJson(ProfileStore store, DateTime now) => JsonSerializer.Serialize(Build(store, now), Json.Web);

    /// <summary>HTML d'un document ou d'un diaporama, pour la lecture sur le téléphone.</summary>
    public static string? ReadableHtml(ProfileStore store, LibraryItem item, Func<string, string> mediaUrl)
    {
        if (item.Kind == ItemKind.Document)
        {
            var path = store.DocumentHtmlPath(item.Id);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        if (item.Kind != ItemKind.Slideshow) return null;
        var show = store.LoadSlideshow(item.Id);
        var theme = SlideThemes.Get(show.ThemeId);
        var sb = new StringBuilder();
        int n = 1;
        foreach (var slide in show.Slides)
        {
            var bg = slide.BackgroundHex ?? theme.Background;
            var bg2 = theme.Background2 ?? bg;
            sb.Append($"<section class=\"slide\" style=\"background:linear-gradient(135deg,{Css(bg)},{Css(bg2)});color:{Css(theme.TextColor)}\">");
            sb.Append($"<div class=\"slide-num\">{n++}</div>");
            foreach (var e in slide.Elements.Where(e => e.Type != SlideElementType.Shape))
            {
                if (e.Type == SlideElementType.Image && e.ImageFile != null)
                {
                    sb.Append($"<img src=\"{Html(mediaUrl(e.ImageFile))}\" alt=\"\">");
                    if (!string.IsNullOrWhiteSpace(e.Caption)) sb.Append($"<p class=\"caption\">{Html(e.Caption)}</p>");
                }
                else if (e.Type == SlideElementType.Text && e.Text.Trim().Length > 0)
                {
                    var color = Css(e.ColorHex ?? (e.IsTitle ? theme.TitleColor : theme.TextColor));
                    if (e.IsTitle) sb.Append($"<h2 style=\"color:{color}\">{Html(e.Text)}</h2>");
                    else if (e.Bullets) sb.Append($"<ul style=\"color:{color}\">" + string.Concat(e.Text.Split('\n').Where(l => l.Trim().Length > 0).Select(l => $"<li>{Html(l.Trim())}</li>")) + "</ul>");
                    else sb.Append($"<p style=\"color:{color}\">{Html(e.Text).Replace("\n", "<br>")}</p>");
                }
            }
            if (!string.IsNullOrWhiteSpace(slide.Notes)) sb.Append($"<details><summary>Notes</summary><p>{Html(slide.Notes)}</p></details>");
            sb.Append("</section>");
        }
        return sb.ToString();
    }

    public static string Html(string s) => System.Net.WebUtility.HtmlEncode(s);

    private static string Css(string hex)
    {
        // #AARRGGBB → rgba()
        if (hex.Length == 9 && hex[0] == '#')
        {
            var a = Convert.ToInt32(hex.Substring(1, 2), 16) / 255.0;
            return $"rgba({Convert.ToInt32(hex.Substring(3, 2), 16)},{Convert.ToInt32(hex.Substring(5, 2), 16)},{Convert.ToInt32(hex.Substring(7, 2), 16)},{a.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)})";
        }
        return hex;
    }
}

public sealed class SyncRequest
{
    public List<SyncReview> Reviews { get; set; } = new();
    public List<SyncQuizResult> QuizResults { get; set; } = new();
}

public sealed class SyncReview
{
    public string DeckId { get; set; } = "";
    public string CardId { get; set; } = "";
    public DateTimeOffset Time { get; set; }
    public string Grade { get; set; } = "good";
}

public sealed class SyncQuizResult
{
    public string QuizId { get; set; } = "";
    public DateTimeOffset Time { get; set; }
    public double Score { get; set; }
    public int Max { get; set; }
}

public static class PhoneSync
{
    /// <summary>
    /// Applique les révisions faites sur le téléphone. Idempotent : une révision déjà reçue est ignorée.
    /// Retourne le nombre de révisions et de résultats de quiz ajoutés.
    /// </summary>
    public static (int Reviews, int Quizzes) Apply(ProfileStore store, SyncRequest req)
    {
        int reviews = 0, quizzes = 0;
        var decks = new Dictionary<string, Deck>();
        foreach (var r in req.Reviews.OrderBy(r => r.Time))
        {
            var time = r.Time.ToLocalTime().DateTime;
            if (store.Stats.Reviews.Any(x => x.CardId == r.CardId && Math.Abs((x.Time - time).TotalSeconds) < 1)) continue;
            if (!decks.TryGetValue(r.DeckId, out var deck))
            {
                if (store.GetItem(r.DeckId) is not { Kind: ItemKind.Deck }) continue;
                deck = decks[r.DeckId] = store.LoadDeck(r.DeckId);
            }
            var card = deck.Cards.FirstOrDefault(c => c.Id == r.CardId);
            if (card == null) continue;
            var grade = ParseGrade(r.Grade);
            // On n'écrase pas une révision plus récente faite sur le PC
            if (card.LastReview == null || card.LastReview < time) Srs.Apply(card, grade, time);
            store.Stats.Reviews.Add(new ReviewLogEntry
            {
                DeckId = r.DeckId, CardId = r.CardId, Time = time, Grade = grade, IntervalAfter = card.IntervalDays, Source = "phone"
            });
            reviews++;
        }
        foreach (var d in decks.Values) store.SaveDeck(d);

        foreach (var q in req.QuizResults)
        {
            var time = q.Time.ToLocalTime().DateTime;
            if (store.Stats.QuizResults.Any(x => x.QuizId == q.QuizId && Math.Abs((x.Time - time).TotalSeconds) < 1)) continue;
            store.Stats.QuizResults.Add(new QuizResult { QuizId = q.QuizId, Time = time, Score = q.Score, Max = q.Max, Source = "phone" });
            quizzes++;
        }
        if (reviews + quizzes > 0) store.SaveStats();
        return (reviews, quizzes);
    }

    public static ReviewGrade ParseGrade(string g) => g.ToLowerInvariant() switch
    {
        "again" or "0" => ReviewGrade.Again,
        "hard" or "1" => ReviewGrade.Hard,
        "easy" or "3" => ReviewGrade.Easy,
        _ => ReviewGrade.Good
    };
}
