using StudyDesk.Core.Models;

namespace StudyDesk.Core.Services;

/// <summary>Décoration dessinée derrière le contenu de chaque diapositive.</summary>
public enum SlideDecoration
{
    None,
    /// <summary>Bandeau de couleur en haut.</summary>
    TopBar,
    /// <summary>Bande verticale à gauche.</summary>
    SideBar,
    /// <summary>Grands cercles translucides dans les coins.</summary>
    Circles,
    /// <summary>Vague en bas de la diapositive.</summary>
    Wave,
    /// <summary>Cadre fin autour de la diapositive.</summary>
    Frame,
    /// <summary>Grille de points discrète.</summary>
    Dots,
    /// <summary>Bande diagonale.</summary>
    Diagonal,
    /// <summary>Triangle coloré dans un coin.</summary>
    Corner,
    /// <summary>Lignes horizontales comme un cahier.</summary>
    Notebook,
    /// <summary>Petits carrés comme du papier quadrillé.</summary>
    Grid,
    /// <summary>Rayures obliques fines.</summary>
    Stripes
}

/// <summary>Thème de diaporama : couleurs, polices et décoration appliquées à toutes les diapositives.</summary>
public sealed record SlideTheme(
    string Id, string Name,
    string Background, string? Background2,
    string TitleColor, string TextColor, string Accent,
    string TitleFont, string TextFont,
    SlideDecoration Decoration = SlideDecoration.None,
    string? Accent2 = null,
    string Category = "Classique");

public static class SlideThemes
{
    public static readonly IReadOnlyList<SlideTheme> All = new[]
    {
        // Classiques
        new SlideTheme("blanc", "Blanc épuré", "#FFFFFF", null, "#1B2230", "#3A4252", "#2F62D9", "Segoe UI Semibold", "Segoe UI", SlideDecoration.TopBar, null, "Classique"),
        new SlideTheme("ocean", "Océan", "#1D3D7A", "#2F62D9", "#FFFFFF", "#E3EBFF", "#8FD3FF", "Segoe UI Semibold", "Segoe UI", SlideDecoration.Circles, "#FFFFFF", "Classique"),
        new SlideTheme("foret", "Forêt", "#F1F6F1", null, "#1E5631", "#2F3B32", "#3E8E5A", "Georgia", "Segoe UI", SlideDecoration.SideBar, "#A7D3B0", "Classique"),
        new SlideTheme("nuit", "Nuit", "#14161F", "#262A3D", "#F3F4F8", "#C9CDDC", "#F5B544", "Segoe UI Semibold", "Segoe UI", SlideDecoration.Dots, "#F5B544", "Classique"),
        new SlideTheme("papier", "Papier", "#FBF7EE", null, "#5A3A1A", "#3E3428", "#C0692D", "Georgia", "Georgia", SlideDecoration.Frame, null, "Classique"),
        new SlideTheme("corail", "Corail", "#FF7A59", "#E0476B", "#FFFFFF", "#FFF2EC", "#FFE08A", "Segoe UI Black", "Segoe UI", SlideDecoration.Wave, "#FFFFFF", "Classique"),
        new SlideTheme("ardoise", "Ardoise", "#2E3440", null, "#ECEFF4", "#D8DEE9", "#88C0D0", "Consolas", "Segoe UI", SlideDecoration.Grid, "#88C0D0", "Classique"),
        new SlideTheme("lavande", "Lavande", "#F4F0FB", "#E7DEF7", "#4B2C83", "#3B3450", "#7A4FC4", "Segoe UI Semibold", "Segoe UI", SlideDecoration.Corner, "#C9B6EF", "Classique"),
        // École
        new SlideTheme("cahier", "Cahier d'écolier", "#FFFEF8", null, "#1F3A93", "#26304A", "#D64545", "Segoe Print", "Segoe UI", SlideDecoration.Notebook, "#9EC5F0", "École"),
        new SlideTheme("quadrille", "Papier quadrillé", "#FFFFFF", null, "#1B2230", "#2D3748", "#2F62D9", "Segoe UI Semibold", "Segoe UI", SlideDecoration.Grid, "#C7D7F2", "École"),
        new SlideTheme("tableau", "Tableau noir", "#21362B", "#2C4A3A", "#F6F3E7", "#E5E1D3", "#F5D76E", "Segoe Print", "Segoe Print", SlideDecoration.Frame, "#8B5E3C", "École"),
        new SlideTheme("craie", "Tableau blanc", "#F7F8FA", null, "#1B5E9E", "#2B2B2B", "#E04848", "Ink Free", "Segoe UI", SlideDecoration.Frame, "#B8C2CC", "École"),
        new SlideTheme("postit", "Post-it", "#FFF4A8", "#FFE97A", "#3A2E00", "#4A3F10", "#E07A00", "Segoe Print", "Segoe UI", SlideDecoration.Corner, "#FFD34D", "École"),
        new SlideTheme("labo", "Laboratoire", "#EEF8FB", null, "#0B5563", "#1F3A40", "#14A3B8", "Bahnschrift", "Segoe UI", SlideDecoration.Dots, "#9ADBE6", "École"),
        // Modernes
        new SlideTheme("minuit", "Minuit néon", "#0E0B1F", "#1E1547", "#FFFFFF", "#D5D0F5", "#FF4FD8", "Bahnschrift SemiBold", "Segoe UI", SlideDecoration.Diagonal, "#3DE0FF", "Moderne"),
        new SlideTheme("aurore", "Aurore", "#FF9A8B", "#7F7FD5", "#FFFFFF", "#FFF7F5", "#FFFFFF", "Segoe UI Black", "Segoe UI", SlideDecoration.Circles, "#FFFFFF", "Moderne"),
        new SlideTheme("menthe", "Menthe", "#E9FBF3", "#CFF5E4", "#0F5A44", "#1E3B33", "#18A877", "Segoe UI Semibold", "Segoe UI", SlideDecoration.Wave, "#7FE0B8", "Moderne"),
        new SlideTheme("sable", "Sable", "#F6EDE3", null, "#4D3B2A", "#4A4036", "#D08C4A", "Constantia", "Candara", SlideDecoration.Stripes, "#E7D3BC", "Moderne"),
        new SlideTheme("graphite", "Graphite", "#1F2328", null, "#FFFFFF", "#C9D1D9", "#F0883E", "Bahnschrift SemiBold", "Segoe UI", SlideDecoration.TopBar, "#F0883E", "Moderne"),
        new SlideTheme("cerise", "Cerise", "#FFF1F3", null, "#8E1B3A", "#3B2228", "#E0315B", "Georgia", "Segoe UI", SlideDecoration.SideBar, "#F7B6C5", "Moderne"),
        // Matières
        new SlideTheme("histoire", "Parchemin (histoire)", "#F3E6C8", "#E6D2A6", "#5C3317", "#3D2B1A", "#9C2F1E", "Palatino Linotype", "Palatino Linotype", SlideDecoration.Frame, "#B08850", "Matières"),
        new SlideTheme("geo", "Carte (géographie)", "#E4F1F8", "#CFE6D3", "#1D4E6E", "#22343F", "#3C8D5A", "Segoe UI Semibold", "Segoe UI", SlideDecoration.Grid, "#A9CFE3", "Matières"),
        new SlideTheme("sciences", "Atome (sciences)", "#0F2537", "#123F5C", "#FFFFFF", "#D3E6F5", "#4FD1C5", "Bahnschrift SemiBold", "Segoe UI", SlideDecoration.Circles, "#4FD1C5", "Matières"),
        new SlideTheme("langues", "Voyage (langues)", "#FFFFFF", null, "#B3261E", "#2E2E2E", "#1F4E99", "Segoe UI Black", "Segoe UI", SlideDecoration.Diagonal, "#F2C94C", "Matières"),
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
