using StudyDesk.Core.Models;

namespace StudyDesk.Core.Services;

public sealed record SubjectMastery(Subject Subject, int TotalCards, int Mastered, int Due, int New)
{
    public double Percent => TotalCards == 0 ? 0 : Mastered * 100.0 / TotalCards;
}

public sealed record DayCount(DateTime Day, int Count);

/// <summary>Calculs des statistiques affichées dans l'accueil et la page Statistiques.</summary>
public sealed class StatsCalculator
{
    private readonly ProfileStore _store;
    private readonly DateTime _now;
    private readonly List<(LibraryItem Item, Deck Deck)> _decks;

    public StatsCalculator(ProfileStore store, DateTime? now = null)
    {
        _store = store;
        _now = now ?? DateTime.Now;
        _decks = store.AllDecks().ToList();
    }

    public int TotalCards => _decks.Sum(d => d.Deck.Cards.Count);
    public int MasteredCards => _decks.Sum(d => d.Deck.Cards.Count(Srs.IsMastered));
    public int DueToday => _decks.Sum(d => d.Deck.Cards.Count(c => Srs.IsDue(c, _now)));

    /// <summary>Nombre de cartes nouvelles proposées aujourd'hui (limite quotidienne).</summary>
    public int NewAvailableToday
    {
        get
        {
            var newCount = _decks.Sum(d => d.Deck.Cards.Count(c => c.IsNew));
            var remaining = Math.Max(0, _store.Library.Settings.NewCardsPerDay - Srs.NewCardsStudiedToday(_store.Stats, _now));
            return Math.Min(newCount, remaining);
        }
    }

    public int ReviewsToday => _store.Stats.Reviews.Count(r => r.Time.Date == _now.Date);

    public IReadOnlyList<DayCount> ReviewsPerDay(int days)
    {
        var start = _now.Date.AddDays(-(days - 1));
        var counts = _store.Stats.Reviews.Where(r => r.Time >= start).GroupBy(r => r.Time.Date).ToDictionary(g => g.Key, g => g.Count());
        return Enumerable.Range(0, days).Select(i => start.AddDays(i)).Select(d => new DayCount(d, counts.GetValueOrDefault(d))).ToList();
    }

    public IReadOnlyList<DayCount> MinutesPerDay(int days)
    {
        var start = _now.Date.AddDays(-(days - 1));
        var counts = _store.Stats.Sessions.Where(s => s.Start >= start).GroupBy(s => s.Start.Date).ToDictionary(g => g.Key, g => g.Sum(s => s.Minutes));
        return Enumerable.Range(0, days).Select(i => start.AddDays(i)).Select(d => new DayCount(d, counts.GetValueOrDefault(d))).ToList();
    }

    /// <summary>Nombre de jours consécutifs (jusqu'à aujourd'hui ou hier) avec au moins une révision ou une session de travail.</summary>
    public int StreakDays
    {
        get
        {
            var days = _store.Stats.Reviews.Select(r => r.Time.Date)
                .Concat(_store.Stats.Sessions.Select(s => s.Start.Date))
                .Concat(_store.Stats.QuizResults.Select(q => q.Time.Date))
                .ToHashSet();
            var d = _now.Date;
            if (!days.Contains(d)) d = d.AddDays(-1);
            int n = 0;
            while (days.Contains(d)) { n++; d = d.AddDays(-1); }
            return n;
        }
    }

    public int MinutesThisWeek
    {
        get
        {
            int diff = ((int)_now.DayOfWeek + 6) % 7; // lundi = 0
            var monday = _now.Date.AddDays(-diff);
            return _store.Stats.Sessions.Where(s => s.Start >= monday).Sum(s => s.Minutes);
        }
    }

    public double? QuizAverage(int last = 20)
    {
        var results = _store.Stats.QuizResults.Where(r => r.Max > 0).OrderByDescending(r => r.Time).Take(last).ToList();
        return results.Count == 0 ? null : results.Average(r => r.Score / r.Max) * 100;
    }

    /// <summary>Cartes à réviser pour chacun des prochains jours (aujourd'hui inclus, retards comptés aujourd'hui).</summary>
    public IReadOnlyList<DayCount> Forecast(int days)
    {
        var result = new int[days];
        foreach (var c in _decks.SelectMany(d => d.Deck.Cards).Where(c => !c.IsNew && c.Due != null))
        {
            var offset = (int)(c.Due!.Value.Date - _now.Date).TotalDays;
            if (offset < 0) offset = 0;
            if (offset < days) result[offset]++;
        }
        return result.Select((n, i) => new DayCount(_now.Date.AddDays(i), n)).ToList();
    }

    public IReadOnlyList<SubjectMastery> MasteryBySubject()
    {
        return _store.Library.Subjects.OrderBy(s => s.Order).Select(s =>
        {
            var cards = _decks.Where(d => d.Item.SubjectId == s.Id).SelectMany(d => d.Deck.Cards).ToList();
            return new SubjectMastery(s, cards.Count, cards.Count(Srs.IsMastered), cards.Count(c => Srs.IsDue(c, _now)), cards.Count(c => c.IsNew));
        }).ToList();
    }

    public IReadOnlyList<(QuizResult Result, string Title)> RecentQuizzes(int n)
    {
        return _store.Stats.QuizResults.OrderByDescending(r => r.Time).Take(n)
            .Select(r => (r, _store.GetItem(r.QuizId)?.Title ?? "Quiz supprimé")).ToList();
    }

    /// <summary>Taux de bonnes réponses (Difficile, Bien ou Facile) sur les 30 derniers jours.</summary>
    public double? Retention30Days
    {
        get
        {
            var recent = _store.Stats.Reviews.Where(r => r.Time >= _now.AddDays(-30)).ToList();
            return recent.Count == 0 ? null : recent.Count(r => r.Grade != ReviewGrade.Again) * 100.0 / recent.Count;
        }
    }
}
