using StudyDesk.Core.Models;

namespace StudyDesk.Core.Services;

/// <summary>
/// Répétition espacée (variante de SM-2). Chaque réponse fixe la prochaine date de révision.
/// La même logique est reprise en JavaScript dans l'app téléphone (phone.html).
/// </summary>
public static class Srs
{
    public const double MinEase = 1.3;
    public const double MasteredIntervalDays = 21;

    /// <summary>Calcule l'état suivant d'une carte sans la modifier.</summary>
    public static (double Interval, double Ease, int Reps, int Lapses) Preview(Flashcard c, ReviewGrade g)
    {
        double ease = c.Ease, interval = c.IntervalDays;
        int reps = c.Repetitions, lapses = c.Lapses;

        if (g == ReviewGrade.Again)
        {
            if (reps > 0) lapses++;
            reps = 0;
            ease = Math.Max(MinEase, ease - 0.2);
            interval = 0; // à revoir dans la même séance
            return (interval, ease, reps, lapses);
        }

        if (reps == 0)
        {
            interval = g switch { ReviewGrade.Hard => 1, ReviewGrade.Good => 1, _ => 4 };
            if (c.Lapses > 0 && g == ReviewGrade.Good) interval = 1;
        }
        else if (reps == 1)
        {
            interval = g switch { ReviewGrade.Hard => 2, ReviewGrade.Good => 3, _ => 6 };
        }
        else
        {
            var baseInterval = Math.Max(1, interval);
            interval = g switch
            {
                ReviewGrade.Hard => baseInterval * 1.2,
                ReviewGrade.Good => baseInterval * ease,
                _ => baseInterval * ease * 1.3
            };
        }

        if (g == ReviewGrade.Hard) ease = Math.Max(MinEase, ease - 0.15);
        if (g == ReviewGrade.Easy) ease += 0.15;
        reps++;
        interval = Math.Min(Math.Round(interval, 1), 3650);
        return (interval, ease, reps, lapses);
    }

    public static void Apply(Flashcard c, ReviewGrade g, DateTime now)
    {
        var (interval, ease, reps, lapses) = Preview(c, g);
        c.IntervalDays = interval;
        c.Ease = ease;
        c.Repetitions = reps;
        c.Lapses = lapses;
        c.LastReview = now;
        c.Due = interval <= 0 ? now.AddMinutes(1) : now.Date.AddDays(Math.Round(interval)).AddHours(4);
    }

    public static void Reset(Flashcard c)
    {
        c.Ease = 2.5; c.IntervalDays = 0; c.Repetitions = 0; c.Lapses = 0; c.Due = null; c.LastReview = null;
    }

    public static bool IsDue(Flashcard c, DateTime now) =>
        !c.IsNew && c.Due is { } d && d <= EndOfDay(now);

    public static bool IsMastered(Flashcard c) => c.IntervalDays >= MasteredIntervalDays;

    public static DateTime EndOfDay(DateTime now) => now.Date.AddDays(1).AddTicks(-1);

    /// <summary>Texte court pour afficher un intervalle sur un bouton : « 1 min », « 3 j », « 2 mois ».</summary>
    public static string Describe(double days)
    {
        if (days <= 0) return "< 10 min";
        if (days < 30) return $"{Math.Round(days)} j";
        if (days < 365) return $"{Math.Round(days / 30)} mois";
        return $"{Math.Round(days / 365, 1)} an".Replace(".", ",");
    }

    /// <summary>
    /// Construit une file de révision : cartes dues + nouvelles cartes (limite quotidienne).
    /// </summary>
    public static List<(LibraryItem Item, Deck Deck, Flashcard Card)> BuildQueue(
        IEnumerable<(LibraryItem Item, Deck Deck)> decks, DateTime now, int newLimit, int newAlreadyToday, bool includeAllNew = false)
    {
        var due = new List<(LibraryItem, Deck, Flashcard)>();
        var fresh = new List<(LibraryItem, Deck, Flashcard)>();
        foreach (var (item, deck) in decks)
            foreach (var c in deck.Cards)
            {
                if (IsDue(c, now)) due.Add((item, deck, c));
                else if (c.IsNew) fresh.Add((item, deck, c));
            }

        var remaining = includeAllNew ? int.MaxValue : Math.Max(0, newLimit - newAlreadyToday);
        var rnd = new Random();
        var queue = due.OrderBy(x => x.Item3.Due).ThenBy(_ => rnd.Next()).ToList();
        queue.AddRange(fresh.OrderBy(x => x.Item3.CreatedAt).Take(remaining));
        return queue;
    }

    public static int NewCardsStudiedToday(StatsData stats, DateTime now)
    {
        // Une carte nouvelle étudiée aujourd'hui = première révision de cette carte, aujourd'hui.
        var firsts = stats.Reviews.GroupBy(r => r.CardId).Select(g => g.Min(r => r.Time));
        return firsts.Count(t => t.Date == now.Date);
    }
}
