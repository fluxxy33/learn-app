using StudyDesk.Core.Models;

namespace StudyDesk.Core.Services;

/// <summary>Thème de diaporama : couleurs et polices appliquées à toutes les diapositives.</summary>
public sealed record SlideTheme(
    string Id, string Name,
    string Background, string? Background2,
    string TitleColor, string TextColor, string Accent,
    string TitleFont, string TextFont);

public static class SlideThemes
{
    public static readonly IReadOnlyList<SlideTheme> All = new[]
    {
        new SlideTheme("blanc", "Blanc épuré", "#FFFFFF", null, "#1B2230", "#3A4252", "#2F62D9", "Segoe UI Semibold", "Segoe UI"),
        new SlideTheme("ocean", "Océan", "#1D3D7A", "#2F62D9", "#FFFFFF", "#E3EBFF", "#8FD3FF", "Segoe UI Semibold", "Segoe UI"),
        new SlideTheme("foret", "Forêt", "#F1F6F1", null, "#1E5631", "#2F3B32", "#3E8E5A", "Georgia", "Segoe UI"),
        new SlideTheme("nuit", "Nuit", "#14161F", "#262A3D", "#F3F4F8", "#C9CDDC", "#F5B544", "Segoe UI Semibold", "Segoe UI"),
        new SlideTheme("papier", "Papier", "#FBF7EE", null, "#5A3A1A", "#3E3428", "#C0692D", "Georgia", "Georgia"),
        new SlideTheme("corail", "Corail", "#FF7A59", "#E0476B", "#FFFFFF", "#FFF2EC", "#FFE08A", "Segoe UI Black", "Segoe UI"),
        new SlideTheme("ardoise", "Ardoise", "#2E3440", null, "#ECEFF4", "#D8DEE9", "#88C0D0", "Consolas", "Segoe UI"),
        new SlideTheme("lavande", "Lavande", "#F4F0FB", "#E7DEF7", "#4B2C83", "#3B3450", "#7A4FC4", "Segoe UI Semibold", "Segoe UI"),
    };

    public static SlideTheme Get(string id) => All.FirstOrDefault(t => t.Id == id) ?? All[1];
}

public enum SlideLayout
{
    TitleSlide,
    TitleAndContent,
    TwoColumns,
    TitleAndImage,
    SectionHeader,
    Blank
}

public static class SlideshowFactory
{
    public const double Width = 1280;
    public const double Height = 720;

    public static Slideshow CreateDefault(string id, string title)
    {
        var s = new Slideshow { Id = id };
        s.Slides.Add(CreateSlide(SlideLayout.TitleSlide, string.IsNullOrWhiteSpace(title) ? "Titre de la présentation" : title, "Ton nom · classe · date"));
        return s;
    }

    public static Slide CreateSlide(SlideLayout layout, string? title = null, string? body = null)
    {
        var slide = new Slide();
        switch (layout)
        {
            case SlideLayout.TitleSlide:
                slide.Elements.Add(Text(true, title ?? "Titre", 100, 230, 1080, 150, 64, TextAlign.Center, bold: true));
                slide.Elements.Add(Text(false, body ?? "Sous-titre", 100, 400, 1080, 80, 30, TextAlign.Center));
                break;
            case SlideLayout.TitleAndContent:
                slide.Elements.Add(Text(true, title ?? "Titre de la diapositive", 80, 50, 1120, 110, 48, TextAlign.Left, bold: true));
                slide.Elements.Add(Text(false, body ?? "Premier point\nDeuxième point\nTroisième point", 80, 180, 1120, 480, 30, TextAlign.Left, bullets: true));
                break;
            case SlideLayout.TwoColumns:
                slide.Elements.Add(Text(true, title ?? "Comparaison", 80, 50, 1120, 110, 48, TextAlign.Left, bold: true));
                slide.Elements.Add(Text(false, body ?? "Point A\nPoint B", 80, 180, 540, 480, 28, TextAlign.Left, bullets: true));
                slide.Elements.Add(Text(false, "Point C\nPoint D", 660, 180, 540, 480, 28, TextAlign.Left, bullets: true));
                break;
            case SlideLayout.TitleAndImage:
                slide.Elements.Add(Text(true, title ?? "Titre", 80, 50, 1120, 110, 48, TextAlign.Left, bold: true));
                slide.Elements.Add(Text(false, body ?? "Explique ce que montre l'image.", 80, 190, 500, 460, 28, TextAlign.Left));
                slide.Elements.Add(new SlideElement
                {
                    Type = SlideElementType.Shape, Shape = ShapeKind.RoundedRectangle, X = 640, Y = 190, Width = 560, Height = 440,
                    FillHex = "#22FFFFFF", StrokeHex = "#88FFFFFF", StrokeThickness = 2
                });
                break;
            case SlideLayout.SectionHeader:
                slide.Elements.Add(Text(true, title ?? "Partie 1", 100, 260, 1080, 130, 60, TextAlign.Left, bold: true));
                slide.Elements.Add(Text(false, body ?? "Ce que l'on va voir", 100, 400, 1080, 70, 28, TextAlign.Left));
                break;
            case SlideLayout.Blank:
                break;
        }
        return slide;
    }

    public static SlideElement Text(bool isTitle, string text, double x, double y, double w, double h, double size,
        TextAlign align, bool bold = false, bool bullets = false) => new()
    {
        Type = SlideElementType.Text, IsTitle = isTitle, Text = text, X = x, Y = y, Width = w, Height = h,
        FontSize = size, Align = align, Bold = bold, Bullets = bullets
    };

    /// <summary>Modèle « Exposé » : la structure attendue par les professeurs.</summary>
    public static Slideshow CreateExposeTemplate(string id, string subjectTitle)
    {
        var s = new Slideshow { Id = id, ThemeId = "ocean", Transition = SlideTransition.Fade };
        s.Slides.Add(CreateSlide(SlideLayout.TitleSlide, subjectTitle, "Prénom NOM · Classe · Date"));
        s.Slides[^1].Notes = "Présente-toi et annonce le sujet en une phrase. Regarde ton public, pas l'écran.";
        s.Slides.Add(CreateSlide(SlideLayout.TitleAndContent, "Introduction", "Accroche : une question, un chiffre ou une image marquante\nProblématique : la question à laquelle l'exposé répond\nAnnonce du plan"));
        s.Slides[^1].Notes = "La problématique doit être une vraie question. Annonce les parties avec « d'abord », « ensuite », « enfin ».";
        s.Slides.Add(CreateSlide(SlideLayout.TitleAndContent, "Plan", "I. Première partie\nII. Deuxième partie\nIII. Troisième partie"));
        s.Slides.Add(CreateSlide(SlideLayout.SectionHeader, "I. Première partie", "Idée principale de la partie"));
        s.Slides.Add(CreateSlide(SlideLayout.TitleAndImage, "Un exemple concret", "Une image vaut mieux qu'un long texte. Remplace le cadre par une image (Insertion › Image)."));
        s.Slides[^1].Notes = "Commente l'image : que voit-on ? Pourquoi est-ce important ?";
        s.Slides.Add(CreateSlide(SlideLayout.SectionHeader, "II. Deuxième partie", "Idée principale de la partie"));
        s.Slides.Add(CreateSlide(SlideLayout.TitleAndContent, "À retenir", "Pas plus de 6 lignes par diapositive\nDes mots-clés, pas des phrases entières\nTu dis le reste à l'oral"));
        s.Slides.Add(CreateSlide(SlideLayout.SectionHeader, "III. Troisième partie", "Idée principale de la partie"));
        s.Slides.Add(CreateSlide(SlideLayout.TitleAndContent, "Conclusion", "Réponse à la problématique\nRésumé des idées importantes\nOuverture : une nouvelle question"));
        s.Slides.Add(CreateSlide(SlideLayout.TitleAndContent, "Sources", "Livre, auteur, année\nSite web (adresse + date de consultation)\nImages : Wikimedia Commons, licence"));
        s.Slides[^1].Notes = "Toujours citer ses sources, y compris pour les images.";
        return s;
    }
}
