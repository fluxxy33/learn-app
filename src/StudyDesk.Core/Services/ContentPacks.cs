using StudyDesk.Core.Models;

namespace StudyDesk.Core.Services;

/// <summary>Un contenu prêt à l'emploi : un paquet de fiches et un quiz, à ajouter en un clic.</summary>
public sealed class ContentPack
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string SubjectName { get; init; }
    public required string SubjectColor { get; init; }
    public required string SubjectIcon { get; init; }
    public required string Level { get; init; }
    public required string Description { get; init; }
    public required (string Front, string Back)[] Cards { get; init; }
    /// <summary>Questions écrites à la main (ordre, classement, calcul…), ajoutées au quiz.</summary>
    public Func<List<QuizQuestion>>? ExtraQuestions { get; init; }
    /// <summary>Nombre de questions générées automatiquement à partir des fiches (QCM, relier).</summary>
    public int AutoQuestions { get; init; } = 12;
    public bool TwoWayCards { get; init; }
}

/// <summary>
/// Catalogue de contenus libres (faits, définitions, vocabulaire) pour démarrer sans rien taper.
/// </summary>
public static class ContentPacks
{
    private const string Maths = "Mathématiques", Fr = "Français", Hg = "Histoire-Géo", En = "Anglais", Es = "Espagnol",
        Svt = "SVT", Pc = "Physique-Chimie", Philo = "Philosophie";

    private static (string Color, string Icon) Style(string subject) => subject switch
    {
        Maths => ("#2F62D9", ""),
        Fr => ("#C2417A", ""),
        Hg => ("#C2742B", ""),
        En => ("#2B8A5A", ""),
        Es => ("#D64545", ""),
        Svt => ("#5B8C2A", ""),
        Pc => ("#7A4FC4", ""),
        Philo => ("#4F6D7A", ""),
        _ => ("#2F62D9", "")
    };

    private static ContentPack Pack(string id, string title, string subject, string level, string description,
        (string, string)[] cards, Func<List<QuizQuestion>>? extra = null, int auto = 12, bool twoWay = false)
    {
        var (color, icon) = Style(subject);
        return new ContentPack
        {
            Id = id, Title = title, SubjectName = subject, SubjectColor = color, SubjectIcon = icon, Level = level,
            Description = description, Cards = cards, ExtraQuestions = extra, AutoQuestions = auto, TwoWayCards = twoWay
        };
    }

    private static QuizQuestion Ordering(string prompt, params string[] items) =>
        new() { Type = QuestionType.Ordering, Prompt = prompt, Items = items.ToList() };

    private static QuizQuestion Categorize(string prompt, params (string Item, string Category)[] pairs) =>
        new() { Type = QuestionType.Categorize, Prompt = prompt, Pairs = pairs.Select(p => new MatchPair { Left = p.Item, Right = p.Category }).ToList() };

    private static QuizQuestion Numeric(string prompt, double answer, string unit = "", double tolerance = 0, string explanation = "") =>
        new() { Type = QuestionType.Numeric, Prompt = prompt, NumericAnswer = answer, Unit = unit, Tolerance = tolerance, Explanation = explanation };

    private static QuizQuestion WordOrder(string prompt, string sentence, string explanation = "") =>
        new() { Type = QuestionType.WordOrder, Prompt = prompt, Sentence = sentence, Explanation = explanation };

    private static QuizQuestion Cloze(string text, string explanation = "") =>
        new() { Type = QuestionType.FillBlanks, Prompt = "Complète la phrase.", ClozeText = text, Explanation = explanation };

    private static QuizQuestion TrueFalse(string prompt, bool answer, string explanation = "") =>
        new() { Type = QuestionType.TrueFalse, Prompt = prompt, CorrectBool = answer, Explanation = explanation };

    public static readonly IReadOnlyList<ContentPack> All = new[]
    {
        // ------------------------------------------------------------ Mathématiques
        Pack("tables", "Tables de multiplication (6 à 9)", Maths, "Collège",
            "Les tables les plus difficiles, de 6 × 2 à 9 × 10, avec des calculs à taper.",
            (from a in new[] { 6, 7, 8, 9 } from b in Enumerable.Range(2, 9) select ($"{a} × {b}", (a * b).ToString())).ToArray(),
            () => new[] { (6, 7), (7, 8), (8, 9), (9, 6), (7, 7), (8, 6), (9, 8), (6, 9) }
                .Select(p => Numeric($"Combien font {p.Item1} × {p.Item2} ?", p.Item1 * p.Item2)).ToList(), auto: 6),

        Pack("formules-geo", "Aires, périmètres et volumes", Maths, "Collège",
            "Toutes les formules de géométrie du collège, avec des exercices de calcul.",
            new[]
            {
                ("Aire d'un carré de côté c", "c × c = c²"), ("Périmètre d'un carré de côté c", "4 × c"),
                ("Aire d'un rectangle (longueur L, largeur l)", "L × l"), ("Périmètre d'un rectangle", "2 × (L + l)"),
                ("Aire d'un triangle (base b, hauteur h)", "(b × h) ÷ 2"), ("Aire d'un disque de rayon r", "π × r²"),
                ("Périmètre d'un cercle de rayon r", "2 × π × r"), ("Aire d'un trapèze (bases B et b, hauteur h)", "(B + b) × h ÷ 2"),
                ("Aire d'un parallélogramme (base b, hauteur h)", "b × h"), ("Aire d'un losange (diagonales D et d)", "(D × d) ÷ 2"),
                ("Volume d'un cube d'arête c", "c³"), ("Volume d'un pavé droit", "L × l × h"),
                ("Volume d'un cylindre (rayon r, hauteur h)", "π × r² × h"), ("Volume d'un cône (rayon r, hauteur h)", "(π × r² × h) ÷ 3"),
                ("Volume d'une pyramide (aire de base B, hauteur h)", "(B × h) ÷ 3"), ("Volume d'une boule de rayon r", "(4 ÷ 3) × π × r³"),
            },
            () => new List<QuizQuestion>
            {
                Numeric("Aire d'un rectangle de 8 cm sur 5 cm ?", 40, "cm²"),
                Numeric("Aire d'un triangle de base 10 cm et de hauteur 6 cm ?", 30, "cm²"),
                Numeric("Périmètre d'un carré de 7 cm de côté ?", 28, "cm"),
                Numeric("Aire d'un disque de rayon 3 cm (arrondi au dixième) ?", 28.3, "cm²", 0.1, "π × 3² ≈ 28,27 cm²"),
                Numeric("Volume d'un cube de 4 cm d'arête ?", 64, "cm³"),
            }),

        Pack("formules-algebre", "Identités remarquables, puissances et trigonométrie", Maths, "Collège / Lycée",
            "Les formules à savoir par cœur pour le brevet et la seconde.",
            new[]
            {
                ("(a + b)²", "a² + 2ab + b²"), ("(a − b)²", "a² − 2ab + b²"), ("(a + b)(a − b)", "a² − b²"),
                ("aⁿ × aᵐ", "aⁿ⁺ᵐ"), ("aⁿ ÷ aᵐ", "aⁿ⁻ᵐ"), ("(aⁿ)ᵐ", "aⁿˣᵐ"), ("a⁰ (a ≠ 0)", "1"), ("a⁻ⁿ", "1 ÷ aⁿ"),
                ("Théorème de Pythagore (triangle ABC rectangle en A)", "BC² = AB² + AC²"),
                ("Cosinus d'un angle aigu", "côté adjacent ÷ hypoténuse"), ("Sinus d'un angle aigu", "côté opposé ÷ hypoténuse"),
                ("Tangente d'un angle aigu", "côté opposé ÷ côté adjacent"),
                ("Théorème de Thalès (M sur [AB], N sur [AC], (MN) // (BC))", "AM/AB = AN/AC = MN/BC"),
            },
            () => new List<QuizQuestion>
            {
                Numeric("Calcule 2⁵.", 32), Numeric("Calcule 10⁻³ (en écriture décimale).", 0.001),
                Numeric("Un triangle rectangle a des côtés de l'angle droit de 6 cm et 8 cm. Longueur de l'hypoténuse ?", 10, "cm"),
                Cloze("(x + 3)² = x² + [6x] + 9"),
            }),

        // ------------------------------------------------------------ Français
        Pack("figures-style", "Figures de style", Fr, "Collège / Lycée",
            "Les figures de style les plus fréquentes, avec une définition et un exemple.",
            new[]
            {
                ("Métaphore", "Rapprochement de deux éléments sans outil de comparaison. Ex. : « Cet homme est un lion. »"),
                ("Comparaison", "Rapprochement avec un outil (comme, tel, pareil à…). Ex. : « Il est fort comme un lion. »"),
                ("Personnification", "Attribuer des caractéristiques humaines à une chose ou un animal. Ex. : « Le vent hurle. »"),
                ("Hyperbole", "Exagération. Ex. : « Je meurs de faim. »"),
                ("Litote", "Dire moins pour suggérer plus. Ex. : « Va, je ne te hais point. »"),
                ("Euphémisme", "Atténuer une réalité dure. Ex. : « Il nous a quittés » pour « il est mort »."),
                ("Anaphore", "Répétition d'un mot en début de phrase ou de vers. Ex. : « Paris outragé ! Paris brisé ! Paris martyrisé ! »"),
                ("Antithèse", "Opposition de deux idées dans une même phrase. Ex. : « Je vis, je meurs. »"),
                ("Oxymore", "Deux mots de sens contraire côte à côte. Ex. : « Cette obscure clarté »"),
                ("Gradation", "Énumération de termes d'intensité croissante. Ex. : « Va, cours, vole ! »"),
                ("Périphrase", "Désigner une chose par une expression. Ex. : « la Ville Lumière » pour Paris."),
                ("Métonymie", "Désigner une chose par un élément qui lui est lié. Ex. : « boire un verre »"),
                ("Allitération", "Répétition d'un son consonne. Ex. : « Pour qui sont ces serpents qui sifflent sur vos têtes ? »"),
                ("Assonance", "Répétition d'un son voyelle. Ex. : « Tout m'afflige et me nuit et conspire à me nuire »"),
                ("Chiasme", "Structure en miroir AB/BA. Ex. : « Il faut manger pour vivre et non vivre pour manger. »"),
                ("Antiphrase", "Dire le contraire de ce que l'on pense (ironie). Ex. : « Quel temps magnifique ! » sous la pluie."),
            },
            () => new List<QuizQuestion>
            {
                Categorize("Classe chaque exemple selon sa figure de style.",
                    ("Il est rusé comme un renard.", "Comparaison"), ("Ma sœur est une fée.", "Métaphore"),
                    ("Je te l'ai dit mille fois !", "Hyperbole"), ("Le soleil nous sourit.", "Personnification")),
                TrueFalse("« Cette obscure clarté » est un oxymore.", true),
            }),

        Pack("grammaire", "Classes grammaticales et fonctions", Fr, "Collège",
            "Reconnaître la nature et la fonction des mots dans la phrase.",
            new[]
            {
                ("Nom", "Désigne un être, une chose ou une idée (chat, Paris, liberté)."),
                ("Verbe", "Exprime une action ou un état, se conjugue (courir, être)."),
                ("Adjectif qualificatif", "Précise une qualité du nom (grand, rouge)."),
                ("Adverbe", "Mot invariable qui modifie un verbe, un adjectif ou un autre adverbe (vite, très)."),
                ("Déterminant", "Placé avant le nom, il l'introduit (le, une, ce, mon)."),
                ("Pronom", "Remplace un nom (il, elle, lui, celui-ci)."),
                ("Préposition", "Mot invariable qui introduit un complément (à, de, pour, avec)."),
                ("Conjonctions de coordination", "mais, ou, et, donc, or, ni, car"),
                ("Conjonction de subordination", "Introduit une proposition subordonnée (que, quand, parce que, si)."),
                ("Sujet", "Fait l'action ou est dans l'état exprimé par le verbe."),
                ("COD (complément d'objet direct)", "Complète le verbe sans préposition. Question : « quoi ? » ou « qui ? » après le verbe."),
                ("COI (complément d'objet indirect)", "Complète le verbe avec une préposition (à, de). Ex. : Il parle à sa sœur."),
                ("Attribut du sujet", "Donne une caractéristique du sujet après un verbe d'état (être, paraître, sembler…)."),
                ("Complément circonstanciel", "Précise les circonstances : lieu, temps, manière, cause… Il peut être déplacé ou supprimé."),
                ("Épithète", "Adjectif placé directement à côté du nom qu'il qualifie."),
                ("Complément du nom", "Complète un nom, souvent introduit par une préposition (la maison de mon père)."),
            },
            () => new List<QuizQuestion>
            {
                Categorize("Classe chaque mot selon sa nature.",
                    ("rapidement", "Adverbe"), ("courir", "Verbe"), ("bleu", "Adjectif"), ("maison", "Nom"), ("lentement", "Adverbe"), ("joli", "Adjectif")),
                Cloze("Dans « Le chat mange la souris », « la souris » est [COD|complément d'objet direct]."),
            }),

        Pack("connecteurs", "Connecteurs logiques", Fr, "Collège / Lycée",
            "Les mots de liaison pour bien organiser une rédaction ou une dissertation.",
            new[]
            {
                ("de plus", "Addition"), ("en outre", "Addition"), ("mais", "Opposition"), ("cependant", "Opposition"),
                ("pourtant", "Opposition"), ("car", "Cause"), ("parce que", "Cause"), ("puisque", "Cause"),
                ("donc", "Conséquence"), ("c'est pourquoi", "Conséquence"), ("ainsi", "Conséquence"),
                ("par exemple", "Illustration"), ("notamment", "Illustration"), ("en conclusion", "Conclusion"), ("finalement", "Conclusion"),
            },
            () => new List<QuizQuestion>
            {
                Categorize("Classe chaque connecteur selon ce qu'il exprime.",
                    ("toutefois", "Opposition"), ("en effet", "Cause"), ("par conséquent", "Conséquence"), ("également", "Addition"), ("néanmoins", "Opposition")),
            }, auto: 6),

        // ------------------------------------------------------------ Histoire-géographie
        Pack("dates-france", "Grandes dates de l'histoire de France", Hg, "Collège / Lycée",
            "Les dates incontournables, de l'Antiquité à nos jours, avec des frises à remettre dans l'ordre.",
            new[]
            {
                ("Bataille d'Alésia (victoire de César sur Vercingétorix)", "52 av. J.-C."),
                ("Couronnement de Charlemagne empereur", "800"),
                ("Bataille de Marignan (François Ier)", "1515"),
                ("Édit de Nantes (Henri IV)", "1598"),
                ("Prise de la Bastille", "14 juillet 1789"),
                ("Déclaration des droits de l'homme et du citoyen", "26 août 1789"),
                ("Proclamation de la Ire République", "Septembre 1792"),
                ("Sacre de Napoléon Ier", "2 décembre 1804"),
                ("Bataille de Waterloo", "1815"),
                ("Abolition définitive de l'esclavage en France", "1848"),
                ("Début de la IIIe République", "1870"),
                ("Lois Jules Ferry : école gratuite, laïque et obligatoire", "1881-1882"),
                ("Loi de séparation des Églises et de l'État", "1905"),
                ("Première Guerre mondiale", "1914-1918"),
                ("Armistice de la Première Guerre mondiale", "11 novembre 1918"),
                ("Front populaire : congés payés", "1936"),
                ("Seconde Guerre mondiale", "1939-1945"),
                ("Appel du général de Gaulle", "18 juin 1940"),
                ("Débarquement en Normandie", "6 juin 1944"),
                ("Droit de vote des femmes en France", "1944"),
                ("Début de la Ve République", "1958"),
                ("Fin de la guerre d'Algérie (accords d'Évian)", "1962"),
                ("Abolition de la peine de mort", "1981"),
                ("Traité de Maastricht (Union européenne)", "1992"),
                ("Mise en circulation des pièces et billets en euros", "2002"),
            },
            () => new List<QuizQuestion>
            {
                Ordering("Remets ces événements dans l'ordre chronologique.",
                    "Couronnement de Charlemagne", "Bataille de Marignan", "Prise de la Bastille", "Sacre de Napoléon Ier", "Lois Jules Ferry"),
                Ordering("Remets ces événements du XXe siècle dans l'ordre.",
                    "Loi de séparation des Églises et de l'État", "Armistice de 1918", "Front populaire", "Débarquement en Normandie", "Début de la Ve République"),
                Numeric("En quelle année a eu lieu la prise de la Bastille ?", 1789),
            }),

        Pack("capitales-europe", "Capitales d'Europe", Hg, "Tous niveaux",
            "39 pays européens et leur capitale.",
            new[]
            {
                ("Allemagne", "Berlin"), ("Autriche", "Vienne"), ("Belgique", "Bruxelles"), ("Bulgarie", "Sofia"), ("Croatie", "Zagreb"),
                ("Danemark", "Copenhague"), ("Espagne", "Madrid"), ("Estonie", "Tallinn"), ("Finlande", "Helsinki"), ("France", "Paris"),
                ("Grèce", "Athènes"), ("Hongrie", "Budapest"), ("Irlande", "Dublin"), ("Islande", "Reykjavik"), ("Italie", "Rome"),
                ("Lettonie", "Riga"), ("Lituanie", "Vilnius"), ("Luxembourg", "Luxembourg"), ("Malte", "La Valette"), ("Norvège", "Oslo"),
                ("Pays-Bas", "Amsterdam"), ("Pologne", "Varsovie"), ("Portugal", "Lisbonne"), ("République tchèque", "Prague"),
                ("Roumanie", "Bucarest"), ("Royaume-Uni", "Londres"), ("Slovaquie", "Bratislava"), ("Slovénie", "Ljubljana"),
                ("Suède", "Stockholm"), ("Suisse", "Berne"), ("Ukraine", "Kiev (Kyiv)"), ("Serbie", "Belgrade"), ("Albanie", "Tirana"),
                ("Bosnie-Herzégovine", "Sarajevo"), ("Monténégro", "Podgorica"), ("Macédoine du Nord", "Skopje"), ("Moldavie", "Chisinau"),
                ("Biélorussie", "Minsk"), ("Chypre", "Nicosie"),
            }, auto: 15),

        Pack("capitales-monde", "Capitales du monde", Hg, "Tous niveaux",
            "Les capitales des grands pays des autres continents.",
            new[]
            {
                ("États-Unis", "Washington"), ("Canada", "Ottawa"), ("Mexique", "Mexico"), ("Brésil", "Brasilia"), ("Argentine", "Buenos Aires"),
                ("Chili", "Santiago"), ("Pérou", "Lima"), ("Colombie", "Bogota"), ("Venezuela", "Caracas"), ("Cuba", "La Havane"),
                ("Chine", "Pékin"), ("Japon", "Tokyo"), ("Inde", "New Delhi"), ("Russie", "Moscou"), ("Turquie", "Ankara"), ("Iran", "Téhéran"),
                ("Arabie saoudite", "Riyad"), ("Égypte", "Le Caire"), ("Maroc", "Rabat"), ("Algérie", "Alger"), ("Tunisie", "Tunis"),
                ("Sénégal", "Dakar"), ("Mali", "Bamako"), ("Côte d'Ivoire", "Yamoussoukro"), ("Nigeria", "Abuja"), ("Kenya", "Nairobi"),
                ("Éthiopie", "Addis-Abeba"), ("Afrique du Sud", "Pretoria (capitale administrative)"), ("Madagascar", "Antananarivo"),
                ("Cameroun", "Yaoundé"), ("République démocratique du Congo", "Kinshasa"), ("Australie", "Canberra"),
                ("Nouvelle-Zélande", "Wellington"), ("Indonésie", "Jakarta"), ("Thaïlande", "Bangkok"), ("Viêt Nam", "Hanoï"),
                ("Corée du Sud", "Séoul"), ("Pakistan", "Islamabad"),
            },
            () => new List<QuizQuestion>
            {
                TrueFalse("La capitale de l'Australie est Sydney.", false, "C'est Canberra. Sydney est la plus grande ville."),
                TrueFalse("La capitale du Canada est Ottawa.", true),
                Categorize("Classe ces capitales par continent.",
                    ("Nairobi", "Afrique"), ("Lima", "Amérique"), ("Séoul", "Asie"), ("Dakar", "Afrique"), ("Ottawa", "Amérique"), ("Hanoï", "Asie")),
            }, auto: 15),

        Pack("regions-france", "Régions françaises et leurs chefs-lieux", Hg, "Collège",
            "Les 13 régions de France métropolitaine.",
            new[]
            {
                ("Auvergne-Rhône-Alpes", "Lyon"), ("Bourgogne-Franche-Comté", "Dijon"), ("Bretagne", "Rennes"), ("Centre-Val de Loire", "Orléans"),
                ("Corse", "Ajaccio"), ("Grand Est", "Strasbourg"), ("Hauts-de-France", "Lille"), ("Île-de-France", "Paris"),
                ("Normandie", "Rouen"), ("Nouvelle-Aquitaine", "Bordeaux"), ("Occitanie", "Toulouse"), ("Pays de la Loire", "Nantes"),
                ("Provence-Alpes-Côte d'Azur", "Marseille"),
            },
            () => new List<QuizQuestion> { Numeric("Combien la France métropolitaine compte-t-elle de régions ?", 13) }, auto: 10),

        // ------------------------------------------------------------ Langues
        Pack("verbes-irreguliers", "Verbes irréguliers anglais (100 verbes)", En, "Collège / Lycée",
            "Base verbale, prétérit, participe passé et traduction.",
            IrregularVerbs(),
            () => new List<QuizQuestion>
            {
                Cloze("Yesterday, I [went] to the cinema. (go)"), Cloze("She has [written] three letters. (write)"),
                Cloze("We [bought] a new car last week. (buy)"), Cloze("Have you ever [seen] a whale? (see)"),
                WordOrder("Remets les mots dans l'ordre.", "I have never been to London."),
            }, auto: 15),

        Pack("anglais-vocab", "Vocabulaire anglais essentiel", En, "Collège",
            "Les mots du quotidien : maison, école, famille, temps, sentiments.",
            new[]
            {
                ("house", "maison"), ("school", "école"), ("teacher", "professeur"), ("homework", "devoirs"), ("family", "famille"),
                ("brother", "frère"), ("sister", "sœur"), ("parents", "parents"), ("friend", "ami(e)"), ("breakfast", "petit-déjeuner"),
                ("lunch", "déjeuner"), ("dinner", "dîner"), ("weather", "météo, temps qu'il fait"), ("rain", "pluie"), ("sun", "soleil"),
                ("tired", "fatigué"), ("happy", "heureux"), ("sad", "triste"), ("angry", "en colère"), ("hungry", "affamé, avoir faim"),
                ("to borrow", "emprunter"), ("to lend", "prêter"), ("to win", "gagner"), ("to earn", "gagner (de l'argent)"), ("to wait", "attendre"),
                ("library", "bibliothèque (attention : pas « librairie »)"), ("bookshop", "librairie"), ("actually", "en fait (attention : pas « actuellement »)"),
                ("eventually", "finalement (attention : pas « éventuellement »)"), ("sensible", "raisonnable (attention : pas « sensible »)"),
            },
            () => new List<QuizQuestion>
            {
                WordOrder("Remets les mots dans l'ordre.", "She is going to visit her grandmother tomorrow."),
                WordOrder("Remets les mots dans l'ordre.", "What time do you usually get up?"),
                TrueFalse("« Library » veut dire « librairie ».", false, "Library = bibliothèque ; bookshop = librairie."),
            }, auto: 12, twoWay: true),

        Pack("espagnol-vocab", "Espagnol : vocabulaire de base", Es, "Collège",
            "Jours, mois, couleurs, nombres et verbes essentiels.",
            new[]
            {
                ("lunes", "lundi"), ("martes", "mardi"), ("miércoles", "mercredi"), ("jueves", "jeudi"), ("viernes", "vendredi"),
                ("sábado", "samedi"), ("domingo", "dimanche"), ("enero", "janvier"), ("febrero", "février"), ("marzo", "mars"),
                ("abril", "avril"), ("mayo", "mai"), ("junio", "juin"), ("julio", "juillet"), ("agosto", "août"),
                ("septiembre", "septembre"), ("octubre", "octobre"), ("noviembre", "novembre"), ("diciembre", "décembre"),
                ("rojo", "rouge"), ("azul", "bleu"), ("verde", "vert"), ("amarillo", "jaune"), ("negro", "noir"), ("blanco", "blanc"),
                ("uno, dos, tres", "un, deux, trois"), ("diez", "dix"), ("veinte", "vingt"), ("cien", "cent"),
                ("ser", "être (caractéristique permanente)"), ("estar", "être (état, lieu)"), ("tener", "avoir"), ("ir", "aller"),
                ("hacer", "faire"), ("hablar", "parler"), ("comer", "manger"), ("vivir", "vivre, habiter"),
            },
            () => new List<QuizQuestion>
            {
                WordOrder("Remets les mots dans l'ordre.", "Me llamo Ana y tengo quince años."),
                Ordering("Remets les jours de la semaine dans l'ordre.", "lunes", "martes", "miércoles", "jueves", "viernes"),
                Cloze("Yo [soy] francés. (ser)"), Cloze("Nosotros [estamos] en clase. (estar)"),
            }, auto: 12, twoWay: true),

        // ------------------------------------------------------------ Sciences
        Pack("tableau-periodique", "Tableau périodique : 30 éléments", Pc, "Collège / Lycée",
            "Symbole, nom et numéro atomique des éléments les plus courants.",
            new[]
            {
                ("H", "Hydrogène (Z = 1)"), ("He", "Hélium (Z = 2)"), ("Li", "Lithium (Z = 3)"), ("Be", "Béryllium (Z = 4)"), ("B", "Bore (Z = 5)"),
                ("C", "Carbone (Z = 6)"), ("N", "Azote (Z = 7)"), ("O", "Oxygène (Z = 8)"), ("F", "Fluor (Z = 9)"), ("Ne", "Néon (Z = 10)"),
                ("Na", "Sodium (Z = 11)"), ("Mg", "Magnésium (Z = 12)"), ("Al", "Aluminium (Z = 13)"), ("Si", "Silicium (Z = 14)"),
                ("P", "Phosphore (Z = 15)"), ("S", "Soufre (Z = 16)"), ("Cl", "Chlore (Z = 17)"), ("Ar", "Argon (Z = 18)"),
                ("K", "Potassium (Z = 19)"), ("Ca", "Calcium (Z = 20)"), ("Fe", "Fer (Z = 26)"), ("Cu", "Cuivre (Z = 29)"), ("Zn", "Zinc (Z = 30)"),
                ("Ag", "Argent (Z = 47)"), ("Sn", "Étain (Z = 50)"), ("I", "Iode (Z = 53)"), ("Au", "Or (Z = 79)"), ("Hg", "Mercure (Z = 80)"),
                ("Pb", "Plomb (Z = 82)"), ("U", "Uranium (Z = 92)"),
            },
            () => new List<QuizQuestion>
            {
                Numeric("Quel est le numéro atomique du carbone ?", 6),
                Numeric("Combien d'atomes contient une molécule d'eau H₂O ?", 3),
                TrueFalse("Le symbole du sodium est So.", false, "C'est Na, du latin natrium."),
            }, auto: 15),

        Pack("physique-unites", "Grandeurs, unités et formules de physique", Pc, "Collège / Lycée",
            "Les unités du Système international et les relations à connaître.",
            new[]
            {
                ("Unité de longueur", "le mètre (m)"), ("Unité de masse", "le kilogramme (kg)"), ("Unité de temps", "la seconde (s)"),
                ("Unité d'intensité électrique", "l'ampère (A)"), ("Unité de tension électrique", "le volt (V)"), ("Unité de résistance", "l'ohm (Ω)"),
                ("Unité de puissance", "le watt (W)"), ("Unité d'énergie", "le joule (J)"), ("Unité de force", "le newton (N)"),
                ("Unité de pression", "le pascal (Pa)"), ("Unité de fréquence", "le hertz (Hz)"), ("Unité de quantité de matière", "la mole (mol)"),
                ("Loi d'Ohm", "U = R × I"), ("Puissance électrique", "P = U × I"), ("Énergie consommée", "E = P × t"),
                ("Vitesse moyenne", "v = d ÷ t"), ("Poids d'un objet", "P = m × g (g ≈ 9,8 N/kg sur Terre)"), ("Masse volumique", "ρ = m ÷ V"),
            },
            () => new List<QuizQuestion>
            {
                Numeric("Une résistance de 10 Ω est traversée par un courant de 2 A. Quelle est la tension à ses bornes ?", 20, "V", 0, "U = R × I = 10 × 2"),
                Numeric("Une voiture parcourt 150 km en 2 h. Quelle est sa vitesse moyenne ?", 75, "km/h"),
                Numeric("Un appareil de 2 000 W fonctionne 3 h. Énergie consommée en kWh ?", 6, "kWh"),
                Numeric("Poids d'un objet de 10 kg sur Terre (g = 9,8 N/kg) ?", 98, "N"),
            }),

        Pack("cellule", "La cellule et le vivant", Svt, "Collège / Lycée",
            "Les éléments de la cellule et les grands mécanismes du vivant.",
            new[]
            {
                ("Noyau", "Contient l'information génétique (l'ADN) et commande la cellule."),
                ("Membrane plasmique", "Enveloppe qui délimite la cellule et contrôle les échanges."),
                ("Cytoplasme", "Contenu de la cellule, entre la membrane et le noyau."),
                ("Mitochondrie", "Organite qui produit l'énergie de la cellule (respiration cellulaire)."),
                ("Chloroplaste", "Organite des cellules végétales où a lieu la photosynthèse."),
                ("Paroi", "Enveloppe rigide (cellulose) des cellules végétales."),
                ("Vacuole", "Réserve d'eau et de substances, très grande dans les cellules végétales."),
                ("Ribosome", "Fabrique les protéines."),
                ("ADN", "Molécule qui porte l'information génétique."),
                ("Gène", "Portion d'ADN qui code un caractère héréditaire."),
                ("Chromosome", "ADN condensé ; l'être humain en possède 46 (23 paires)."),
                ("Photosynthèse", "Eau + dioxyde de carbone + lumière → glucose + dioxygène."),
                ("Respiration cellulaire", "Glucose + dioxygène → dioxyde de carbone + eau + énergie."),
                ("Mitose", "Division cellulaire qui donne deux cellules identiques."),
                ("Méiose", "Division qui produit les gamètes, avec deux fois moins de chromosomes."),
            },
            () => new List<QuizQuestion>
            {
                Categorize("Ces éléments sont-ils présents dans les cellules animales et végétales, ou seulement végétales ?",
                    ("Noyau", "Animales et végétales"), ("Chloroplaste", "Seulement végétales"), ("Paroi", "Seulement végétales"),
                    ("Mitochondrie", "Animales et végétales"), ("Membrane plasmique", "Animales et végétales")),
                Numeric("Combien de chromosomes possède une cellule humaine (hors gamètes) ?", 46),
            }),

        Pack("systeme-solaire", "Le système solaire", Pc, "Tous niveaux",
            "Les huit planètes, dans l'ordre et avec leurs particularités.",
            new[]
            {
                ("Mercure", "La plus proche du Soleil et la plus petite."), ("Vénus", "La plus chaude, à cause de son effet de serre."),
                ("Terre", "La seule planète connue qui abrite la vie."), ("Mars", "La planète rouge, couverte d'oxyde de fer."),
                ("Jupiter", "La plus grande planète, avec sa Grande Tache rouge."), ("Saturne", "Célèbre pour ses anneaux."),
                ("Uranus", "Elle tourne presque couchée sur le côté."), ("Neptune", "La plus éloignée du Soleil, balayée par des vents violents."),
                ("Soleil", "Étoile au centre du système solaire."), ("Lune", "Satellite naturel de la Terre."),
            },
            () => new List<QuizQuestion>
            {
                Ordering("Remets les planètes dans l'ordre, de la plus proche à la plus éloignée du Soleil.",
                    "Mercure", "Vénus", "Terre", "Mars", "Jupiter", "Saturne", "Uranus", "Neptune"),
                Numeric("Combien y a-t-il de planètes dans le système solaire ?", 8),
                Categorize("Planète rocheuse ou géante gazeuse ?",
                    ("Mars", "Rocheuse"), ("Jupiter", "Géante gazeuse"), ("Vénus", "Rocheuse"), ("Saturne", "Géante gazeuse"), ("Mercure", "Rocheuse"), ("Neptune", "Géante gazeuse")),
            }, auto: 6),

        // ------------------------------------------------------------ Philosophie
        Pack("philo-auteurs", "Philosophes et citations", Philo, "Lycée",
            "Les auteurs du programme et leur idée la plus célèbre.",
            new[]
            {
                ("Socrate", "« Je sais que je ne sais rien. »"), ("Platon", "L'allégorie de la caverne (La République)."),
                ("Aristote", "« L'homme est par nature un animal politique. »"), ("Épicure", "Le bonheur comme absence de troubles (ataraxie)."),
                ("Descartes", "« Je pense, donc je suis. » (Discours de la méthode, 1637)"), ("Pascal", "« L'homme n'est qu'un roseau, […] mais c'est un roseau pensant. »"),
                ("Spinoza", "La liberté comme connaissance de la nécessité (Éthique)."), ("Hobbes", "L'état de nature, « guerre de chacun contre chacun » (Léviathan)."),
                ("Rousseau", "Du contrat social (1762) : la volonté générale."), ("Montesquieu", "La séparation des pouvoirs (De l'esprit des lois)."),
                ("Kant", "L'impératif catégorique : agir selon une règle qui pourrait valoir pour tous."), ("Hume", "Toute connaissance vient de l'expérience (empirisme)."),
                ("Hegel", "L'histoire comme progrès de la liberté."), ("Marx", "La lutte des classes, moteur de l'histoire."),
                ("Nietzsche", "« Dieu est mort. » (Le Gai Savoir)"), ("Freud", "L'inconscient : « le moi n'est pas maître dans sa propre maison »."),
                ("Bergson", "La durée vécue, différente du temps des horloges."), ("Sartre", "« L'existence précède l'essence. »"),
                ("Simone de Beauvoir", "« On ne naît pas femme, on le devient. » (Le Deuxième Sexe)"), ("Hannah Arendt", "La banalité du mal."),
            },
            () => new List<QuizQuestion>
            {
                Ordering("Remets ces philosophes dans l'ordre chronologique.", "Socrate", "Aristote", "Descartes", "Kant", "Nietzsche", "Sartre"),
            }, auto: 15),

        Pack("philo-notions", "Les 17 notions du programme de terminale", Philo, "Lycée",
            "Une définition simple de chaque notion, pour démarrer une dissertation.",
            new[]
            {
                ("L'art", "Activité humaine qui produit des œuvres pour leur beauté ou leur sens, au-delà de l'utile."),
                ("Le bonheur", "État de satisfaction durable et complète."), ("La conscience", "Capacité à savoir ce que l'on pense, ressent et fait."),
                ("Le devoir", "Ce que l'on doit faire, par obligation morale ou légale."), ("L'État", "Institution qui exerce le pouvoir politique sur un territoire et une population."),
                ("L'inconscient", "Ensemble des processus psychiques qui échappent à la conscience."), ("La justice", "Ce qui est conforme au droit (le légal) ou à la morale (le légitime)."),
                ("Le langage", "Capacité à exprimer et communiquer sa pensée au moyen de signes."), ("La liberté", "Pouvoir d'agir selon sa volonté, sans contrainte."),
                ("La nature", "Ce qui existe sans intervention humaine ; aussi : l'essence d'une chose."), ("La raison", "Faculté de penser, de juger et de distinguer le vrai du faux."),
                ("La religion", "Ensemble de croyances et de pratiques liées au sacré."), ("La science", "Connaissance rigoureuse, démontrée ou vérifiée par l'expérience."),
                ("La technique", "Ensemble des moyens et des outils pour transformer la nature."), ("Le temps", "Succession des instants : passé, présent, futur."),
                ("Le travail", "Activité par laquelle l'homme transforme la nature pour satisfaire ses besoins."), ("La vérité", "Accord de la pensée avec la réalité."),
            },
            () => new List<QuizQuestion>
            {
                Numeric("Combien de notions compte le programme de philosophie de terminale générale ?", 17),
                TrueFalse("Ce qui est légal est toujours légitime.", false, "Le légal dépend des lois ; le légitime, de la morale. Une loi peut être injuste."),
            }, auto: 10),
    };

    public static ContentPack? Get(string id) => All.FirstOrDefault(p => p.Id == id);

    private static (string, string)[] IrregularVerbs()
    {
        var raw = new[]
        {
            "arise|arose|arisen|survenir", "be|was / were|been|être", "bear|bore|borne|supporter", "beat|beat|beaten|battre",
            "become|became|become|devenir", "begin|began|begun|commencer", "bend|bent|bent|plier", "bet|bet|bet|parier",
            "bite|bit|bitten|mordre", "bleed|bled|bled|saigner", "blow|blew|blown|souffler", "break|broke|broken|casser",
            "bring|brought|brought|apporter", "build|built|built|construire", "burn|burnt|burnt|brûler", "buy|bought|bought|acheter",
            "catch|caught|caught|attraper", "choose|chose|chosen|choisir", "come|came|come|venir", "cost|cost|cost|coûter",
            "cut|cut|cut|couper", "deal|dealt|dealt|traiter, distribuer", "dig|dug|dug|creuser", "do|did|done|faire",
            "draw|drew|drawn|dessiner", "dream|dreamt|dreamt|rêver", "drink|drank|drunk|boire", "drive|drove|driven|conduire",
            "eat|ate|eaten|manger", "fall|fell|fallen|tomber", "feed|fed|fed|nourrir", "feel|felt|felt|ressentir",
            "fight|fought|fought|se battre", "find|found|found|trouver", "fly|flew|flown|voler (dans les airs)", "forbid|forbade|forbidden|interdire",
            "forget|forgot|forgotten|oublier", "forgive|forgave|forgiven|pardonner", "freeze|froze|frozen|geler", "get|got|got (US : gotten)|obtenir",
            "give|gave|given|donner", "go|went|gone|aller", "grow|grew|grown|grandir, pousser", "hang|hung|hung|accrocher",
            "have|had|had|avoir", "hear|heard|heard|entendre", "hide|hid|hidden|cacher", "hit|hit|hit|frapper",
            "hold|held|held|tenir", "hurt|hurt|hurt|blesser", "keep|kept|kept|garder", "know|knew|known|savoir, connaître",
            "lay|laid|laid|poser", "lead|led|led|mener", "learn|learnt|learnt|apprendre", "leave|left|left|partir, laisser",
            "lend|lent|lent|prêter", "let|let|let|laisser, permettre", "lie|lay|lain|être allongé", "lose|lost|lost|perdre",
            "make|made|made|fabriquer", "mean|meant|meant|signifier", "meet|met|met|rencontrer", "pay|paid|paid|payer",
            "put|put|put|mettre", "read|read|read|lire", "ride|rode|ridden|aller à cheval, à vélo", "ring|rang|rung|sonner",
            "rise|rose|risen|s'élever", "run|ran|run|courir", "say|said|said|dire", "see|saw|seen|voir",
            "sell|sold|sold|vendre", "send|sent|sent|envoyer", "set|set|set|fixer", "shake|shook|shaken|secouer",
            "shine|shone|shone|briller", "shoot|shot|shot|tirer", "show|showed|shown|montrer", "shut|shut|shut|fermer",
            "sing|sang|sung|chanter", "sink|sank|sunk|couler", "sit|sat|sat|être assis", "sleep|slept|slept|dormir",
            "speak|spoke|spoken|parler", "spend|spent|spent|dépenser, passer (du temps)", "stand|stood|stood|être debout", "steal|stole|stolen|voler (dérober)",
            "swim|swam|swum|nager", "take|took|taken|prendre", "teach|taught|taught|enseigner", "tear|tore|torn|déchirer",
            "tell|told|told|raconter", "think|thought|thought|penser", "throw|threw|thrown|lancer", "understand|understood|understood|comprendre",
            "wake|woke|woken|réveiller", "wear|wore|worn|porter (un vêtement)", "win|won|won|gagner", "write|wrote|written|écrire",
        };
        return raw.Select(r => r.Split('|')).Select(p => ($"to {p[0]} ({p[3]})", $"{p[1]} — {p[2]}")).ToArray();
    }

    /// <summary>Ajoute le contenu dans la bibliothèque : matière (créée si besoin), paquet de fiches et quiz.</summary>
    public static (LibraryItem Deck, LibraryItem Quiz) Import(ProfileStore store, ContentPack pack, Random? rnd = null)
    {
        rnd ??= new Random();
        var subject = store.Library.Subjects.FirstOrDefault(s => string.Equals(s.Name, pack.SubjectName, StringComparison.OrdinalIgnoreCase))
                      ?? store.AddSubject(pack.SubjectName, pack.SubjectColor, pack.SubjectIcon);

        var deckItem = store.CreateItem(ItemKind.Deck, pack.Title, subject.Id, null);
        var deck = store.LoadDeck(deckItem.Id);
        var cards = pack.Cards.Select(c => new Flashcard { Front = c.Front, Back = c.Back }).ToList();
        deck.Cards.AddRange(cards);
        if (pack.TwoWayCards)
            deck.Cards.AddRange(pack.Cards.Select(c => new Flashcard { Front = c.Back, Back = c.Front }));
        store.SaveDeck(deck);

        var quizItem = store.CreateItem(ItemKind.Quiz, pack.Title + " — quiz", subject.Id, null);
        var quiz = store.LoadQuiz(quizItem.Id);
        var auto = Converters.QuizFromCards(cards, rnd);
        var mcq = auto.Where(q => q.Type == QuestionType.MultipleChoice).OrderBy(_ => rnd.Next()).Take(pack.AutoQuestions).ToList();
        quiz.Questions.AddRange(mcq);
        quiz.Questions.AddRange(auto.Where(q => q.Type == QuestionType.Matching).Take(1));
        if (pack.ExtraQuestions != null) quiz.Questions.AddRange(pack.ExtraQuestions());
        store.SaveQuiz(quiz);

        store.Library.ImportedPacks.Add(pack.Id);
        store.SaveLibrary();
        return (deckItem, quizItem);
    }
}
