using System.Text;
using System.Text.RegularExpressions;
using StudyDesk.Core.Models;

namespace StudyDesk.Core.Services;

/// <summary>Un paragraphe d'un document, avec son niveau de titre (0 = texte normal) et ses passages en gras.</summary>
public sealed class OutlineBlock
{
    public int Level { get; set; }
    public List<(string Text, bool Bold)> Segments { get; set; } = new();
    public bool IsListItem { get; set; }
    public string Text => string.Concat(Segments.Select(s => s.Text)).Trim();

    public static OutlineBlock Plain(string text, int level = 0) => new() { Level = level, Segments = { (text, false) } };
}

/// <summary>
/// Conversion automatique, entièrement locale :
/// document → fiches, document → quiz, fiches → quiz, diaporama → fiches.
/// </summary>
public static class Converters
{
    private static readonly Regex DefinitionRegex = new(@"^\s*([^:\n]{2,60}?)\s*(?::|=|→|->)\s+(.{3,})$", RegexOptions.Compiled);
    private static readonly Regex SentenceSplit = new(@"(?<=[.!?])\s+(?=[A-ZÀ-Ý0-9«])", RegexOptions.Compiled);

    public static List<Flashcard> CardsFromOutline(IReadOnlyList<OutlineBlock> blocks)
    {
        var cards = new List<Flashcard>();
        var seen = new HashSet<string>();

        void Add(string front, string back)
        {
            front = Clean(front);
            back = Clean(back);
            if (front.Length == 0 || back.Length == 0) return;
            if (back.Length > 700) back = back[..697].TrimEnd() + "…";
            if (seen.Add(QuizGrader.Normalize(front)))
                cards.Add(new Flashcard { Front = front, Back = back });
        }

        // 1) Titre + contenu qui suit
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].Level == 0) continue;
            var sb = new StringBuilder();
            int j = i + 1;
            for (; j < blocks.Count && (blocks[j].Level == 0 || blocks[j].Level > blocks[i].Level); j++)
            {
                if (blocks[j].Level != 0) break; // un sous-titre aura sa propre fiche
                var t = blocks[j].Text;
                if (t.Length == 0) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(blocks[j].IsListItem ? "• " + t : t);
            }
            if (sb.Length > 0) Add(blocks[i].Text, sb.ToString());
        }

        // 2) Définitions « Terme : explication »
        foreach (var b in blocks.Where(b => b.Level == 0))
        {
            var m = DefinitionRegex.Match(b.Text);
            if (!m.Success) continue;
            var term = m.Groups[1].Value.Trim();
            if (term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 7) continue;
            Add(term, m.Groups[2].Value);
        }

        // 3) Si rien n'a été trouvé : une fiche par mot en gras, avec sa phrase
        if (cards.Count == 0)
            foreach (var b in blocks.Where(b => b.Level == 0))
                foreach (var (text, bold) in b.Segments.Where(s => s.Bold && s.Text.Trim().Length > 1))
                {
                    var sentence = SentenceSplit.Split(b.Text).FirstOrDefault(s => s.Contains(text.Trim())) ?? b.Text;
                    Add("Explique : " + text.Trim(), sentence);
                }

        return cards;
    }

    public static List<QuizQuestion> QuizFromOutline(IReadOnlyList<OutlineBlock> blocks)
    {
        var questions = new List<QuizQuestion>();

        // Textes à trous à partir des mots en gras
        foreach (var b in blocks.Where(b => b.Level == 0 && b.Segments.Any(s => s.Bold && s.Text.Trim().Length > 1)))
        {
            var sentences = SplitWithSegments(b);
            foreach (var sentence in sentences)
            {
                var bolds = sentence.Where(s => s.Bold && s.Text.Trim().Length > 1).ToList();
                if (bolds.Count == 0 || bolds.Count > 4) continue;
                var sb = new StringBuilder();
                foreach (var (text, bold) in sentence)
                {
                    if (bold && text.Trim().Length > 1)
                    {
                        var lead = text.Length - text.TrimStart().Length;
                        var trail = text.Length - text.TrimEnd().Length;
                        sb.Append(text[..lead]).Append('[').Append(text.Trim().Replace("[", "").Replace("]", "")).Append(']').Append(text[(text.Length - trail)..]);
                    }
                    else sb.Append(text);
                }
                var cloze = Clean(sb.ToString());
                if (cloze.Length < 15) continue;
                questions.Add(new QuizQuestion { Type = QuestionType.FillBlanks, Prompt = "Complète la phrase.", ClozeText = cloze });
            }
        }

        // QCM / réponses courtes à partir des fiches détectées
        var cards = CardsFromOutline(blocks);
        questions.AddRange(QuizFromCards(cards));
        return questions;
    }

    public static List<QuizQuestion> QuizFromCards(IReadOnlyList<Flashcard> cards, Random? rnd = null)
    {
        rnd ??= new Random();
        var list = new List<QuizQuestion>();
        var usable = cards.Where(c => c.Front.Trim().Length > 0 && c.Back.Trim().Length > 0).ToList();
        foreach (var c in usable)
        {
            var others = usable.Where(o => o != c && QuizGrader.Normalize(o.Back) != QuizGrader.Normalize(c.Back))
                               .Select(o => o.Back.Trim()).Distinct().OrderBy(_ => rnd.Next()).Take(3).ToList();
            if (others.Count >= 2)
            {
                var q = new QuizQuestion { Type = QuestionType.MultipleChoice, Prompt = c.Front.Trim(), Image = c.FrontImage };
                q.Choices.Add(new QuizChoice { Text = Shorten(c.Back), IsCorrect = true });
                q.Choices.AddRange(others.Select(o => new QuizChoice { Text = Shorten(o) }));
                q.Choices = q.Choices.OrderBy(_ => rnd.Next()).ToList();
                q.Explanation = c.Back.Trim().Length > 160 ? c.Back.Trim() : "";
                list.Add(q);
            }
            else if (c.Back.Trim().Length <= 40)
            {
                list.Add(new QuizQuestion
                {
                    Type = QuestionType.ShortAnswer, Prompt = c.Front.Trim(), Image = c.FrontImage,
                    AcceptedAnswers = { c.Back.Trim() }
                });
            }
        }

        // Une question « relier » si les réponses sont courtes
        var shortCards = usable.Where(c => c.Front.Length <= 60 && c.Back.Length <= 60).OrderBy(_ => rnd.Next()).Take(5).ToList();
        if (shortCards.Count >= 3)
        {
            var match = new QuizQuestion { Type = QuestionType.Matching, Prompt = "Relie chaque élément à sa réponse." };
            match.Pairs.AddRange(shortCards.Select(c => new MatchPair { Left = c.Front.Trim(), Right = c.Back.Trim() }));
            list.Add(match);
        }
        return list;
    }

    public static List<Flashcard> CardsFromSlideshow(Slideshow show)
    {
        var cards = new List<Flashcard>();
        foreach (var slide in show.Slides)
        {
            var title = slide.Elements.FirstOrDefault(e => e.Type == SlideElementType.Text && e.IsTitle)?.Text.Trim();
            var body = string.Join("\n", slide.Elements.Where(e => e.Type == SlideElementType.Text && !e.IsTitle)
                                                       .Select(e => e.Bullets ? string.Join("\n", e.Text.Split('\n').Select(l => "• " + l.Trim())) : e.Text.Trim())
                                                       .Where(t => t.Length > 0));
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body)) continue;
            var image = slide.Elements.FirstOrDefault(e => e.Type == SlideElementType.Image)?.ImageFile;
            cards.Add(new Flashcard { Front = title, Back = body, BackImage = image });
            var outline = body.Split('\n').Select(l => OutlineBlock.Plain(l.TrimStart('•', ' '))).ToList();
            cards.AddRange(CardsFromOutline(outline).Where(c => cards.All(x => x.Front != c.Front)));
        }
        return cards;
    }

    /// <summary>Importe des fiches à partir de texte : une fiche par ligne, recto et verso séparés par ; ou tabulation.</summary>
    public static List<Flashcard> CardsFromText(string text)
    {
        var cards = new List<Flashcard>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            string[] parts = line.Contains('\t') ? line.Split('\t', 2)
                : line.Contains(';') ? line.Split(';', 2)
                : line.Contains(" - ") ? line.Split(" - ", 2)
                : line.Split(':', 2);
            if (parts.Length == 2 && parts[0].Trim().Length > 0 && parts[1].Trim().Length > 0)
                cards.Add(new Flashcard { Front = parts[0].Trim(), Back = parts[1].Trim() });
        }
        return cards;
    }

    private static List<List<(string Text, bool Bold)>> SplitWithSegments(OutlineBlock b)
    {
        // Découpe en phrases en conservant l'information « gras »
        var result = new List<List<(string, bool)>>();
        var current = new List<(string, bool)>();
        foreach (var (text, bold) in b.Segments)
        {
            if (bold) { current.Add((text, true)); continue; }
            var pieces = SentenceSplit.Split(text);
            for (int i = 0; i < pieces.Length; i++)
            {
                if (i > 0) { result.Add(current); current = new(); }
                if (pieces[i].Length > 0) current.Add((pieces[i], false));
            }
        }
        if (current.Count > 0) result.Add(current);
        return result;
    }

    private static string Shorten(string s)
    {
        s = Clean(s);
        return s.Length > 160 ? s[..157].TrimEnd() + "…" : s;
    }

    private static string Clean(string s) => Regex.Replace(s ?? "", @"[ \t]+", " ").Trim();
}
