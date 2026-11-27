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

    /// <summary>Texte de la bonne réponse, pour l'affichage après correction.</summary>
    public static string CorrectAnswerText(QuizQuestion q) => q.Type switch
    {
        QuestionType.MultipleChoice => string.Join(" ; ", q.Choices.Where(c => c.IsCorrect).Select(c => c.Text)),
        QuestionType.TrueFalse => q.CorrectBool ? "Vrai" : "Faux",
        QuestionType.ShortAnswer => q.AcceptedAnswers.FirstOrDefault() ?? "",
        QuestionType.FillBlanks => string.Concat(ParseCloze(q.ClozeText).Select(p => p.Text ?? p.Answers![0])),
        QuestionType.Matching => string.Join(" ; ", q.Pairs.Select(p => $"{p.Left} → {p.Right}")),
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
            _ => null
        };
    }
}

public sealed record ClozePart(string? Text, string[]? Answers);
