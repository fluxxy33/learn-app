namespace StudyDesk.Core.Models;

/// <summary>Un utilisateur de l'application (plusieurs profils possibles sur un même PC).</summary>
public sealed class Profile
{
    public string Id { get; set; } = NewId.Create();
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "#2F62D9";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public sealed class ProfileIndex
{
    public List<Profile> Profiles { get; set; } = new();
    public string? LastProfileId { get; set; }
}

public enum ItemKind
{
    Document,
    Slideshow,
    Deck,
    Quiz
}

public sealed class Subject
{
    public string Id { get; set; } = NewId.Create();
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "#2F62D9";
    /// <summary>Glyphe de la police Segoe Fluent Icons.</summary>
    public string Icon { get; set; } = "";
    public int Order { get; set; }
    public List<Chapter> Chapters { get; set; } = new();
}

public sealed class Chapter
{
    public string Id { get; set; } = NewId.Create();
    public string Name { get; set; } = "";
    public int Order { get; set; }
}

/// <summary>Métadonnées d'un élément de la bibliothèque. Le contenu est stocké dans un fichier séparé.</summary>
public sealed class LibraryItem
{
    public string Id { get; set; } = NewId.Create();
    public ItemKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string? ChapterId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    public DateTime? OpenedAt { get; set; }
    public bool Favorite { get; set; }
}

public sealed class Exam
{
    public string Id { get; set; } = NewId.Create();
    public string Name { get; set; } = "";
    public DateTime Date { get; set; }
}

public enum AppTheme
{
    System,
    Light,
    Dark
}

public sealed class ProfileSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool OnlineEnabled { get; set; } = true;
    public int PomodoroWorkMinutes { get; set; } = 25;
    public int PomodoroShortBreakMinutes { get; set; } = 5;
    public int PomodoroLongBreakMinutes { get; set; } = 15;
    public int NewCardsPerDay { get; set; } = 20;
    public int PhonePort { get; set; } = 8765;
    public string DefaultFontFamily { get; set; } = "Segoe UI";
    public double DefaultFontSize { get; set; } = 15;
}

/// <summary>Contenu du fichier library.json d'un profil.</summary>
public sealed class Library
{
    public int Version { get; set; } = 1;
    public List<Subject> Subjects { get; set; } = new();
    public List<LibraryItem> Items { get; set; } = new();
    public List<Exam> Exams { get; set; } = new();
    public ProfileSettings Settings { get; set; } = new();
    public HashSet<string> CompletedTutorials { get; set; } = new();
}

public static class NewId
{
    public static string Create() => Guid.NewGuid().ToString("N")[..12];
}
