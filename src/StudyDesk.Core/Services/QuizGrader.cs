using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using StudyDesk.Core.Models;

namespace StudyDesk.Core.Services;

/// <summary>Correction des questions de quiz. Chaque question rapporte entre 0 et 1 point.</summary>
public static partial class QuizGrader
{
    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex BlankRegex();

    /// <summary>Découpe un texte à trous en morceaux : texte normal et trous (avec leurs réponses possibles).</summary>
    public static List<ClozePart> ParseCloze(string text)
    {
        var parts = new List<ClozePart>();
        int last = 0;
        foreach (Match m in BlankRegex().Matches(text ?? ""))
        {
            if (m.Index > last) parts.Add(new ClozePart(text![last..m.Index], null));
            parts.Add(new ClozePart(null, m.Groups[1].Value.Split('|').Select(a => a.Trim()).Where(a => a.Length > 0).ToArray()));
            last = m.Index + m.Length;
        }
        if (text != null && last < text.Length) parts.Add(new ClozePart(text[last..], null));
        return parts;
    }

    public static int BlankCount(string text) => ParseCloze(text).Count(p => p.Answers != null);

    /// <summary>Met en minuscules, enlève les accents, la ponctuation de bord et les espaces en trop.</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch switch { '’' => '\'', 'œ' => 'o', _ => ch });
        }
        var r = Regex.Replace(sb.ToString(), @"\s+", " ");
        return r.Trim(' ', '.', ',', ';', '!', '?', '"', '«', '»');
    }

    public static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    /// <summary>Compare une réponse tapée à une réponse attendue, en tolérant une petite faute de frappe.</summary>
    public static bool TextMatches(string given, string expected)
    {
        var g = Normalize(given);
        var e = Normalize(expected);
        if (g.Length == 0) return false;
        if (g == e) return true;
        // Les nombres doivent être exacts
        if (e.Any(char.IsDigit)) return false;
        int tolerance = e.Length >= 10 ? 2 : e.Length >= 5 ? 1 : 0;
        return Levenshtein(g, e) <= tolerance;
    }

    public static double GradeMultipleChoice(QuizQuestion q, IReadOnlyCollection<int> selected)
    {
        var correct = q.Choices.Select((c, i) => (c, i)).Where(x => x.c.IsCorrect).Select(x => x.i).ToHashSet();
        if (correct.Count == 0) return 0;
        if (correct.Count == 1) return selected.Count == 1 && correct.Contains(selected.First()) ? 1 : 0;
        int good = selected.Count(correct.Contains);
        int bad = selected.Count(i => !correct.Contains(i));
        return Math.Max(0, (good - bad) / (double)correct.Count);
    }

    public static double GradeTrueFalse(QuizQuestion q, bool answer) => answer == q.CorrectBool ? 1 : 0;

    public static double GradeShortAnswer(QuizQuestion q, string answer) =>
        q.AcceptedAnswers.Any(a => TextMatches(answer, a)) ? 1 : 0;

    public static double GradeFillBlanks(QuizQuestion q, IReadOnlyList<string> answers, out bool[] perBlank)
    {
        var blanks = ParseCloze(q.ClozeText).Where(p => p.Answers != null).ToList();
        perBlank = new bool[blanks.Count];
        if (blanks.Count == 0) return 0;
        for (int i = 0; i < blanks.Count; i++)
        {
            var given = i < answers.Count ? answers[i] : "";
            perBlank[i] = blanks[i].Answers!.Any(a => TextMatches(given, a));
        }
        return perBlank.Count(x => x) / (double)blanks.Count;
    }

    /// <summary>mapping[i] = index de la réponse de droite choisie pour l'élément de gauche i (ou -1).</summary>
    public static double GradeMatching(QuizQuestion q, IReadOnlyList<int> mapping)
    {
        if (q.Pairs.Count == 0) return 0;
        int ok = 0;
        for (int i = 0; i < q.Pairs.Count; i++)
            if (i < mapping.Count && mapping[i] >= 0 && mapping[i] < q.Pairs.Count &&
                Normalize(q.Pairs[mapping[i]].Right) == Normalize(q.Pairs[i].Right))
                ok++;
        return ok / (double)q.Pairs.Count;
    }

    /// <summary>order = éléments dans l'ordre choisi par l'élève. Un point par élément bien placé.</summary>
    public static double GradeOrdering(QuizQuestion q, IReadOnlyList<string> order)
    {
        var items = q.Items.Where(i => i.Trim().Length > 0).ToList();
        if (items.Count == 0) return 0;
        int ok = 0;
        for (int i = 0; i < items.Count; i++)
            if (i < order.Count && Normalize(order[i]) == Normalize(items[i])) ok++;
        return ok / (double)items.Count;
    }

    public static List<string> Categories(QuizQuestion q) =>
        q.Pairs.Select(p => p.Right.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>chosen[i] = catégorie choisie pour l'élément i.</summary>
    public static double GradeCategorize(QuizQuestion q, IReadOnlyList<string?> chosen)
    {
        if (q.Pairs.Count == 0) return 0;
        int ok = 0;
        for (int i = 0; i < q.Pairs.Count; i++)
            if (i < chosen.Count && chosen[i] != null && Normalize(chosen[i]!) == Normalize(q.Pairs[i].Right)) ok++;
        return ok / (double)q.Pairs.Count;
    }

    public static bool TryParseNumber(string s, out double value)
    {
        s = (s ?? "").Trim().Replace(" ", "").Replace("\u00A0", "").Replace(',', '.');
        s = Regex.Replace(s, @"[^\d.\-eE+]+$", ""); // enlève une unité tapée à la fin
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static double GradeNumeric(QuizQuestion q, string input)
    {
        if (!TryParseNumber(input, out var v)) return 0;
        var tol = Math.Max(Math.Abs(q.Tolerance), 1e-9);
        return Math.Abs(v - q.NumericAnswer) <= tol ? 1 : 0;
    }

    public static List<string> Words(string sentence) =>
        Regex.Split((sentence ?? "").Trim(), @"\s+").Where(w => w.Length > 0).ToList();

    /// <summary>Un point si toute la phrase est dans le bon ordre, sinon la part de mots bien placés (divisée par deux).</summary>
    public static double GradeWordOrder(QuizQuestion q, IReadOnlyList<string> words)
    {
        var expected = Words(q.Sentence);
        if (expected.Count == 0) return 0;
        if (words.Count == expected.Count && expected.Select((w, i) => Normalize(w) == Normalize(words[i])).All(x => x)) return 1;
        int ok = expected.Where((w, i) => i < words.Count && Normalize(w) == Normalize(words[i])).Count();
        return ok / (double)expected.Count / 2;
    }

    public static string FormatNumber(double v) => v.ToString("0.######", CultureInfo.GetCultureInfo("fr-FR"));

    /// <summary>Texte de la bonne réponse, pour l'affichage après correction.</summary>
    public static string CorrectAnswerText(QuizQuestion q) => q.Type switch
    {
        QuestionType.MultipleChoice => string.Join(" ; ", q.Choices.Where(c => c.IsCorrect).Select(c => c.Text)),
        QuestionType.TrueFalse => q.CorrectBool ? "Vrai" : "Faux",
        QuestionType.ShortAnswer => q.AcceptedAnswers.FirstOrDefault() ?? "",
        QuestionType.FillBlanks => string.Concat(ParseCloze(q.ClozeText).Select(p => p.Text ?? p.Answers![0])),
        QuestionType.Matching => string.Join(" ; ", q.Pairs.Select(p => $"{p.Left} → {p.Right}")),
        QuestionType.Ordering => string.Join(" → ", q.Items.Where(i => i.Trim().Length > 0)),
        QuestionType.Categorize => string.Join(" ; ", Categories(q).Select(c => c + " : " + string.Join(", ", q.Pairs.Where(p => string.Equals(p.Right.Trim(), c, StringComparison.OrdinalIgnoreCase)).Select(p => p.Left)))),
        QuestionType.Numeric => FormatNumber(q.NumericAnswer) + (q.Unit.Length > 0 ? " " + q.Unit : "") + (q.Tolerance > 0 ? $" (± {FormatNumber(q.Tolerance)})" : ""),
        QuestionType.WordOrder => q.Sentence.Trim(),
        _ => ""
    };

    /// <summary>Vérifie qu'une question est complète. Retourne un message d'erreur ou null.</summary>
    public static string? Validate(QuizQuestion q)
    {
        if (q.Type != QuestionType.FillBlanks && string.IsNullOrWhiteSpace(q.Prompt)) return "La question n'a pas d'énoncé.";
        return q.Type switch
        {
            QuestionType.MultipleChoice when q.Choices.Count(c => !string.IsNullOrWhiteSpace(c.Text)) < 2 => "Il faut au moins deux choix.",
            QuestionType.MultipleChoice when !q.Choices.Any(c => c.IsCorrect) => "Coche au moins une bonne réponse.",
            QuestionType.ShortAnswer when !q.AcceptedAnswers.Any(a => !string.IsNullOrWhiteSpace(a)) => "Ajoute au moins une réponse acceptée.",
            QuestionType.FillBlanks when BlankCount(q.ClozeText) == 0 => "Mets au moins un mot entre crochets, par exemple [Paris].",
            QuestionType.Matching when q.Pairs.Count(p => p.Left.Length > 0 && p.Right.Length > 0) < 2 => "Il faut au moins deux paires complètes.",
            QuestionType.Ordering when q.Items.Count(i => i.Trim().Length > 0) < 2 => "Il faut au moins deux éléments à remettre dans l'ordre.",
            QuestionType.Categorize when q.Pairs.Count(p => p.Left.Trim().Length > 0 && p.Right.Trim().Length > 0) < 2 => "Il faut au moins deux éléments avec leur catégorie.",
            QuestionType.Categorize when Categories(q).Count < 2 => "Il faut au moins deux catégories différentes.",
            QuestionType.WordOrder when Words(q.Sentence).Count < 3 => "Écris une phrase d'au moins trois mots.",
            _ => null
        };
    }
}

public sealed record ClozePart(string? Text, string[]? Answers);
