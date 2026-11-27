using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using StudyDesk.Core.Models;
using StudyDesk.Core.Phone;
using StudyDesk.Core.Services;

namespace StudyDesk.Services;

/// <summary>État global de l'application : profil ouvert, services partagés.</summary>
public static class AppState
{
    public static ProfileManager Profiles { get; set; } = null!;
    public static ProfileStore Store { get; set; } = null!;
    public static OnlineServices Online { get; } = new();
    public static PomodoroService Pomodoro { get; } = new();
    public static PhoneServer? Phone { get; set; }
    public static MainWindow Main => (MainWindow)Application.Current.MainWindow;
    public static bool SelfTest { get; set; }

    public static ProfileSettings Settings => Store.Library.Settings;

    public static string LogPath => Path.Combine(Profiles.Root, "erreurs.log");

    public static void LogError(Exception ex)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n"); }
        catch { /* rien */ }
    }

    public static void StopPhone()
    {
        Phone?.Dispose();
        Phone = null;
    }
}

public enum PomodoroPhase
{
    Idle,
    Work,
    ShortBreak,
    LongBreak
}

/// <summary>Minuteur Pomodoro : 25 min de travail, 5 min de pause, grande pause toutes les 4 sessions.</summary>
public sealed class PomodoroService
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _phaseStart;

    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Idle;
    public TimeSpan Remaining { get; private set; }
    public TimeSpan Total { get; private set; }
    public bool IsPaused { get; private set; }
    public int CompletedToday { get; private set; }
    private int _cycle;

    public event Action? Tick;
    public event Action<PomodoroPhase>? PhaseFinished;

    public PomodoroService()
    {
        _timer.Tick += (_, _) => OnTick();
    }

    public string PhaseName => Phase switch
    {
        PomodoroPhase.Work => "Concentration",
        PomodoroPhase.ShortBreak => "Petite pause",
        PomodoroPhase.LongBreak => "Grande pause",
        _ => "Pomodoro"
    };

    public void StartWork() => StartPhase(PomodoroPhase.Work);

    public void StartPhase(PomodoroPhase phase)
    {
        var s = AppState.Settings;
        int minutes = phase switch
        {
            PomodoroPhase.Work => s.PomodoroWorkMinutes,
            PomodoroPhase.ShortBreak => s.PomodoroShortBreakMinutes,
            _ => s.PomodoroLongBreakMinutes
        };
        Phase = phase;
        Total = Remaining = TimeSpan.FromMinutes(Math.Max(1, minutes));
        IsPaused = false;
        _phaseStart = DateTime.Now;
        _timer.Start();
        Tick?.Invoke();
    }

    public void TogglePause()
    {
        if (Phase == PomodoroPhase.Idle) { StartWork(); return; }
        IsPaused = !IsPaused;
        Tick?.Invoke();
    }

    public void Stop()
    {
        if (Phase == PomodoroPhase.Work)
        {
            var minutes = (int)Math.Round((Total - Remaining).TotalMinutes);
            if (minutes >= 1) AppState.Store.LogSession(new StudySession { Start = _phaseStart, Minutes = minutes });
        }
        _timer.Stop();
        Phase = PomodoroPhase.Idle;
        Remaining = TimeSpan.Zero;
        IsPaused = false;
        Tick?.Invoke();
    }

    private void OnTick()
    {
        if (IsPaused || Phase == PomodoroPhase.Idle) return;
        Remaining -= TimeSpan.FromSeconds(1);
        if (Remaining <= TimeSpan.Zero)
        {
            var finished = Phase;
            if (finished == PomodoroPhase.Work)
            {
                AppState.Store.LogSession(new StudySession { Start = _phaseStart, Minutes = (int)Math.Round(Total.TotalMinutes) });
                CompletedToday++;
                _cycle++;
            }
            try { SystemSounds.Asterisk.Play(); } catch { /* pas de son */ }
            var next = finished == PomodoroPhase.Work
                ? (_cycle % 4 == 0 ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak)
                : PomodoroPhase.Work;
            PhaseFinished?.Invoke(finished);
            StartPhase(next);
            if (next == PomodoroPhase.Work) IsPaused = true; // on attend l'élève pour reprendre
        }
        Tick?.Invoke();
    }
}
