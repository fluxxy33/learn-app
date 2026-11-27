namespace StudyDesk.Core.Models;

// ---------------------------------------------------------------- Fiches

public sealed class Deck
{
    public string Id { get; set; } = "";
    public List<Flashcard> Cards { get; set; } = new();
}

public sealed class Flashcard
{
    public string Id { get; set; } = NewId.Create();
    public string Front { get; set; } = "";
    public string Back { get; set; } = "";
    /// <summary>Nom de fichier dans le dossier media du profil.</summary>
    public string? FrontImage { get; set; }
    public string? BackImage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Répétition espacée
    public double Ease { get; set; } = 2.5;
    public double IntervalDays { get; set; }
    public int Repetitions { get; set; }
    public int Lapses { get; set; }
    public DateTime? Due { get; set; }
    public DateTime? LastReview { get; set; }

    public bool IsNew => LastReview is null;
}

// ---------------------------------------------------------------- Quiz

public enum QuestionType
{
    MultipleChoice,
    TrueFalse,
    FillBlanks,
    ShortAnswer,
    Matching
}

public sealed class Quiz
{
    public string Id { get; set; } = "";
    public List<QuizQuestion> Questions { get; set; } = new();
    public bool ShuffleQuestions { get; set; } = true;
}

public sealed class QuizChoice
{
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
}

public sealed class MatchPair
{
    public string Left { get; set; } = "";
    public string Right { get; set; } = "";
}

public sealed class QuizQuestion
{
    public string Id { get; set; } = NewId.Create();
    public QuestionType Type { get; set; }
    public string Prompt { get; set; } = "";
    public string? Image { get; set; }
    public string Explanation { get; set; } = "";

    /// <summary>QCM : une ou plusieurs bonnes réponses.</summary>
    public List<QuizChoice> Choices { get; set; } = new();
    /// <summary>Vrai / faux.</summary>
    public bool CorrectBool { get; set; } = true;
    /// <summary>Réponse courte : réponses acceptées.</summary>
    public List<string> AcceptedAnswers { get; set; } = new();
    /// <summary>Texte à trous : les mots entre crochets sont à trouver. Ex : « La capitale est [Paris]. »</summary>
    public string ClozeText { get; set; } = "";
    /// <summary>Relier : paires gauche / droite.</summary>
    public List<MatchPair> Pairs { get; set; } = new();
}

// ---------------------------------------------------------------- Diaporamas

public enum SlideElementType
{
    Text,
    Image,
    Shape
}

public enum ShapeKind
{
    Rectangle,
    RoundedRectangle,
    Ellipse,
    Line,
    Arrow,
    Triangle
}

public enum TextAlign
{
    Left,
    Center,
    Right
}

public enum SlideTransition
{
    None,
    Fade,
    Slide,
    Zoom
}

public sealed class Slideshow
{
    public string Id { get; set; } = "";
    public string ThemeId { get; set; } = "ocean";
    public SlideTransition Transition { get; set; } = SlideTransition.Fade;
    public List<Slide> Slides { get; set; } = new();
}

public sealed class Slide
{
    public string Id { get; set; } = NewId.Create();
    public List<SlideElement> Elements { get; set; } = new();
    public string Notes { get; set; } = "";
    /// <summary>Traits dessinés à main levée, au format ISF encodé en base64.</summary>
    public string? Ink { get; set; }
    public string? BackgroundHex { get; set; }
}

public sealed class SlideElement
{
    public string Id { get; set; } = NewId.Create();
    public SlideElementType Type { get; set; }
    /// <summary>Rôle de texte : titre ou corps (utilisé par les thèmes et la conversion en fiches).</summary>
    public bool IsTitle { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 400;
    public double Height { get; set; } = 120;

    public string Text { get; set; } = "";
    public double FontSize { get; set; } = 28;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Bullets { get; set; }
    /// <summary>Null = couleur du thème.</summary>
    public string? ColorHex { get; set; }
    public TextAlign Align { get; set; } = TextAlign.Left;

    public string? ImageFile { get; set; }
    public string? Caption { get; set; }

    public ShapeKind Shape { get; set; }
    public string? FillHex { get; set; }
    public string? StrokeHex { get; set; }
    public double StrokeThickness { get; set; } = 3;
}

// ---------------------------------------------------------------- Statistiques

public enum ReviewGrade
{
    Again = 0,
    Hard = 1,
    Good = 2,
    Easy = 3
}

public sealed class ReviewLogEntry
{
    public string DeckId { get; set; } = "";
    public string CardId { get; set; } = "";
    public DateTime Time { get; set; }
    public ReviewGrade Grade { get; set; }
    public double IntervalAfter { get; set; }
    public string Source { get; set; } = "pc";
}

public sealed class QuizResult
{
    public string QuizId { get; set; } = "";
    public DateTime Time { get; set; }
    public double Score { get; set; }
    public int Max { get; set; }
    public string Source { get; set; } = "pc";
}

public sealed class StudySession
{
    public DateTime Start { get; set; }
    public int Minutes { get; set; }
}

public sealed class StatsData
{
    public List<ReviewLogEntry> Reviews { get; set; } = new();
    public List<QuizResult> QuizResults { get; set; } = new();
    public List<StudySession> Sessions { get; set; } = new();
}
