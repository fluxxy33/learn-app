using StudyDesk.Core.Models;

namespace StudyDesk.Core.Services;

/// <summary>Icônes (police Segoe Fluent Icons / Segoe MDL2 Assets) utilisables pour les matières.</summary>
public static class SubjectIcons
{
    public static readonly IReadOnlyList<(string Glyph, string Name)> All = new[]
    {
        ("", "Calculatrice"), ("", "Lettres"), ("", "Histoire"), ("", "Monde"),
        ("", "Langue"), ("", "Santé"), ("", "Ampoule"), ("", "Discussion"),
        ("", "Musique"), ("", "Sport"), ("", "Couleurs"), ("", "Code"),
        ("", "Diplôme"), ("", "Dictionnaire"), ("", "Économie"), ("", "Géographie"),
        ("", "Nature"), ("", "Ruban"), ("", "Nuage"), ("", "Livres"),
    };

    public static readonly IReadOnlyList<string> Colors = new[]
    {
        "#2F62D9", "#C2742B", "#2B8A5A", "#7A4FC4", "#D64545", "#1B8FA6", "#B8860B", "#C2417A", "#4F6D7A", "#5B8C2A"
    };
}

/// <summary>Contenu créé avec un nouveau profil : matières par défaut et quelques exemples prêts à réviser.</summary>
public static class DefaultContent
{
    public static void Seed(ProfileStore store)
    {
        if (store.Library.Subjects.Count > 0) return;

        var maths = store.AddSubject("Mathématiques", "#2F62D9", "");
        store.AddSubject("Français", "#C2417A", "");
        var histoire = store.AddSubject("Histoire-Géo", "#C2742B", "");
        var anglais = store.AddSubject("Anglais", "#2B8A5A", "");
        store.AddSubject("SVT", "#5B8C2A", "");
        store.AddSubject("Physique-Chimie", "#7A4FC4", "");
        store.AddSubject("Espagnol", "#D64545", "");
        store.AddSubject("Philosophie", "#4F6D7A", "");

        var chGeo = store.AddChapter(maths, "Géométrie");
        store.AddChapter(maths, "Fractions");
        var chRevo = store.AddChapter(histoire, "La Révolution française");
        var chVerbs = store.AddChapter(anglais, "Grammaire");

        // Exemple : fiches de verbes irréguliers
        var deckItem = store.CreateItem(ItemKind.Deck, "Verbes irréguliers (exemple)", anglais.Id, chVerbs.Id);
        var deck = store.LoadDeck(deckItem.Id);
        foreach (var (f, b) in new[]
        {
            ("to be", "was / were — been (être)"), ("to go", "went — gone (aller)"), ("to take", "took — taken (prendre)"),
            ("to write", "wrote — written (écrire)"), ("to see", "saw — seen (voir)"), ("to buy", "bought — bought (acheter)"),
            ("to think", "thought — thought (penser)"), ("to begin", "began — begun (commencer)"), ("to know", "knew — known (savoir, connaître)"),
            ("to speak", "spoke — spoken (parler)"),
        })
            deck.Cards.Add(new Flashcard { Front = f, Back = b });
        store.SaveDeck(deck);

        // Exemple : quiz
        var quizItem = store.CreateItem(ItemKind.Quiz, "Révolution française (exemple)", histoire.Id, chRevo.Id);
        var quiz = store.LoadQuiz(quizItem.Id);
        quiz.Questions.Add(new QuizQuestion
        {
            Type = QuestionType.MultipleChoice, Prompt = "En quelle année a lieu la prise de la Bastille ?",
            Choices = { new() { Text = "1789", IsCorrect = true }, new() { Text = "1792" }, new() { Text = "1799" }, new() { Text = "1804" } },
            Explanation = "La Bastille est prise le 14 juillet 1789."
        });
        quiz.Questions.Add(new QuizQuestion
        {
            Type = QuestionType.TrueFalse, Prompt = "La Déclaration des droits de l'homme et du citoyen date de 1789.", CorrectBool = true,
            Explanation = "Elle est adoptée le 26 août 1789."
        });
        quiz.Questions.Add(new QuizQuestion
        {
            Type = QuestionType.FillBlanks, Prompt = "Complète la phrase.",
            ClozeText = "Le roi [Louis XVI|Louis 16] est exécuté en [1793]."
        });
        quiz.Questions.Add(new QuizQuestion
        {
            Type = QuestionType.ShortAnswer, Prompt = "Quel général prend le pouvoir par un coup d'État en 1799 ?",
            AcceptedAnswers = { "Bonaparte", "Napoléon Bonaparte", "Napoléon" }
        });
        quiz.Questions.Add(new QuizQuestion
        {
            Type = QuestionType.Matching, Prompt = "Relie chaque date à son événement.",
            Pairs =
            {
                new() { Left = "1789", Right = "Prise de la Bastille" },
                new() { Left = "1792", Right = "Proclamation de la République" },
                new() { Left = "1793", Right = "Exécution de Louis XVI" },
            }
        });
        store.SaveQuiz(quiz);

        // Exemple : document de cours (écrit au format texte simple, converti à la première ouverture)
        var docItem = store.CreateItem(ItemKind.Document, "Théorème de Pythagore (exemple)", maths.Id, chGeo.Id);
        File.WriteAllText(SeedPath(store, docItem.Id), Templates.PythagoreExample);

        // Exemple : diaporama
        var showItem = store.CreateItem(ItemKind.Slideshow, "Exposé : la Révolution française (exemple)", histoire.Id, chRevo.Id);
        store.SaveSlideshow(SlideshowFactory.CreateExposeTemplate(showItem.Id, "La Révolution française"));

        store.Library.Exams.Add(new Exam { Name = "Brevet / Bac blanc", Date = DateTime.Today.AddDays(45) });
        store.SaveLibrary();
    }

    /// <summary>Fichier texte simple utilisé pour créer un document à sa première ouverture.</summary>
    public static string SeedPath(ProfileStore store, string id) => Path.Combine(store.ItemsFolder, id + ".seed.txt");
}
