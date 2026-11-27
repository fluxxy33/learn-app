namespace StudyDesk.Core.Services;

public enum TutorialAction
{
    None,
    NewDocumentFromTemplate,
    NewExposeSlideshow,
    StartReview,
    StartPomodoro,
    OpenPhone,
    NewDeck,
    NewQuiz,
    OpenCatalog
}

public sealed record Tutorial(
    string Id, string Title, string Category, int Minutes, string Summary, string Markup,
    TutorialAction Action = TutorialAction.None, string? ActionArgument = null, string? ActionLabel = null);

/// <summary>Leçons intégrées : méthodes de travail et prise en main de l'application. Texte au format de <see cref="Templates"/>.</summary>
public static class Tutorials
{
    public static readonly IReadOnlyList<Tutorial> All = new Tutorial[]
    {
        new("bien-demarrer", "Bien démarrer avec StudyDesk", "Prise en main", 3,
            "Le tour de l'application en 3 minutes : matières, documents, fiches, quiz et téléphone.",
            """
# Bien démarrer avec StudyDesk

StudyDesk réunit tout ce qu'il faut pour apprendre : écrire ses cours, préparer des exposés, et surtout **réviser au bon moment**.

## 1. La bibliothèque

Tout est rangé par **matière**, puis par **chapitre**. Dans la Bibliothèque, crée tes matières, choisis une couleur, puis ajoute des chapitres.

## 2. Quatre types de contenus

- **Document** : pour écrire un cours, une dissertation, un compte rendu. Images, tableaux, dessins.
- **Diaporama** : pour un exposé. Thèmes, images, formes, mode présentation plein écran.
- **Fiches** : une question au recto, la réponse au verso.
- **Quiz** : QCM, vrai/faux, texte à trous, réponse courte, relier.

## 3. Réviser

Chaque jour, l'Accueil t'indique combien de fiches réviser. L'application choisit pour toi les fiches qui sont sur le point d'être oubliées.

## 4. Transformer ses cours

Dans un document ou un diaporama, l'onglet **Réviser** crée automatiquement des fiches et des quiz à partir de ce que tu as écrit. Les titres deviennent des questions, les mots en gras deviennent des textes à trous.

> Tout reste sur ton ordinateur. Internet sert seulement à chercher des images et des articles Wikipédia, et tu peux le désactiver dans les Réglages.

## 5. Sur ton téléphone

Ouvre la page Téléphone, scanne le QR code, et révise tes fiches et tes quiz dans le bus. Ta progression revient sur le PC.
"""),

        new("repetition-espacee", "La répétition espacée", "Mémoriser", 4,
            "Pourquoi réviser un peu chaque jour est plus efficace que tout réviser la veille.",
            """
# La répétition espacée

On oublie vite ce qu'on vient d'apprendre : après une journée, on a souvent perdu plus de la moitié d'une leçon. C'est la **courbe de l'oubli**.

## Le principe

Chaque fois que l'on se rappelle une information **juste avant de l'oublier**, on la retient plus longtemps. On peut donc espacer de plus en plus les révisions : 1 jour, 3 jours, 1 semaine, 3 semaines, 2 mois…

## Comment StudyDesk l'utilise

Après avoir vu la réponse d'une fiche, tu indiques si c'était facile :

- **À revoir** : tu ne savais pas. La fiche revient dans quelques minutes.
- **Difficile** : tu as trouvé, mais avec peine. Le délai augmente peu.
- **Bien** : tu as trouvé. Le délai est multiplié.
- **Facile** : c'était évident. Le délai augmente beaucoup.

Sous chaque bouton, tu vois quand la fiche reviendra.

## Les bonnes habitudes

- Réviser **un peu chaque jour** : 10 à 15 minutes suffisent.
- Être **honnête** : si tu as hésité, clique sur Difficile.
- Dire la réponse **à voix haute ou dans ta tête** avant de retourner la fiche. C'est l'effort de se rappeler qui fait mémoriser.

> Une fiche est considérée comme maîtrisée quand son délai dépasse 3 semaines.
""", TutorialAction.StartReview, null, "Réviser maintenant"),

        new("bonnes-fiches", "Écrire de bonnes fiches", "Mémoriser", 4,
            "Une idée par fiche, des questions précises, des images : les règles pour des fiches efficaces.",
            """
# Écrire de bonnes fiches

## Une seule idée par fiche

Une fiche qui demande « Les causes de la Révolution française » est trop large. Découpe-la : une fiche par cause.

## Une vraie question au recto

- Mauvais : « Photosynthèse »
- Bien : « Quels sont les produits de la photosynthèse ? »

## Une réponse courte au verso

Si la réponse fait plus de deux lignes, la question est sans doute trop large.

## Ajoute des images

Une carte, un schéma ou un portrait aide à retenir. Tu peux ajouter une image au recto ou au verso de chaque fiche.

## Dans les deux sens

Pour le vocabulaire, crée la fiche dans les deux sens : « to take → prendre » et « prendre → to take ».

## Importer une liste

Dans l'éditeur de fiches, **Importer du texte** accepte une fiche par ligne, recto et verso séparés par un point-virgule :

to take ; prendre

to write ; écrire
""", TutorialAction.NewDeck, null, "Créer un paquet de fiches"),

        new("prise-de-notes", "Prendre des notes efficacement", "Méthodes", 5,
            "La méthode Cornell, les abréviations et comment relire ses notes.",
            """
# Prendre des notes efficacement

## Ne pas tout écrire

Le but n'est pas de recopier le professeur, mais de garder **les idées importantes** et les exemples.

## Les abréviations utiles

- qqch : quelque chose
- ex. : exemple
- → : entraîne, donne
- ≠ : différent de
- = : égal, signifie
- cf. : voir

## La méthode Cornell

La page est divisée en trois zones :

1. **Notes** : pendant le cours, tu notes l'essentiel.
2. **Mots-clés et questions** : après le cours, tu écris les questions auxquelles tes notes répondent.
3. **Résumé** : en bas, tu résumes la leçon en quelques phrases.

Pour réviser, tu caches les notes et tu essaies de répondre aux questions.

> Le modèle « Prise de notes Cornell » prépare cette structure pour toi.
""", TutorialAction.NewDocumentFromTemplate, "cornell", "Créer une prise de notes Cornell"),

        new("expose", "Réussir un exposé", "Oral", 6,
            "Construire un plan, faire des diapositives lisibles et parler sans lire.",
            """
# Réussir un exposé

## 1. Préparer le fond

- Trouve une **problématique** : une question à laquelle ton exposé répond.
- Fais un **plan** en 2 ou 3 parties.
- Note tes **sources** au fur et à mesure.

## 2. Les diapositives

- **Un titre clair** par diapositive.
- **6 lignes maximum**, avec des mots-clés plutôt que des phrases.
- Des **images** : une bonne image vaut un paragraphe. N'oublie pas d'en citer la source.
- Une police assez grande : 24 points minimum.
- Un même thème du début à la fin.

## 3. À l'oral

- Ne lis pas tes diapositives : elles servent de **support**, c'est toi qui expliques.
- Utilise les **notes de l'orateur** sous chaque diapositive pour tes phrases clés.
- Regarde ton public.
- Entraîne-toi au moins une fois en te chronométrant.

## Raccourcis en mode présentation

- Flèche droite, Espace ou clic : diapositive suivante
- Flèche gauche : diapositive précédente
- N : afficher ou cacher les notes
- B : écran noir
- Échap : quitter

> Le modèle Exposé crée un diaporama de 10 diapositives avec la bonne structure et des conseils dans les notes.
""", TutorialAction.NewExposeSlideshow, null, "Créer un diaporama d'exposé"),

        new("dissertation", "Structurer une dissertation", "Écrit", 6,
            "Introduction en 4 temps, parties équilibrées, transitions et conclusion.",
            """
# Structurer une dissertation

## L'introduction en 4 temps

1. **L'accroche** : une citation, un fait, un exemple qui amène le sujet.
2. **La définition des termes** du sujet.
3. **La problématique** : la question que pose le sujet.
4. **L'annonce du plan**.

## Le développement

- 2 ou 3 parties, chacune avec une **idée directrice**.
- Dans chaque partie, 2 ou 3 sous-parties : **argument, exemple, analyse**.
- Une **transition** entre les parties.

## La conclusion

1. **Le bilan** : réponds clairement à la problématique.
2. **L'ouverture** : une question plus large.

> Astuce : utilise les styles Titre 1 et Titre 2 de l'éditeur. Tu verras d'un coup d'œil si tes parties sont équilibrées.
""", TutorialAction.NewDocumentFromTemplate, "dissertation", "Créer une dissertation"),

        new("pomodoro", "La méthode Pomodoro", "Organisation", 3,
            "25 minutes de travail concentré, 5 minutes de pause : pour éviter de procrastiner.",
            """
# La méthode Pomodoro

## Le principe

1. Choisis **une seule tâche**.
2. Travaille **25 minutes** sans t'interrompre : téléphone loin, notifications coupées.
3. Fais une **pause de 5 minutes**.
4. Après 4 sessions, fais une **grande pause** de 15 minutes.

## Pourquoi ça marche

25 minutes, ce n'est pas long : c'est plus facile de commencer. Les pauses régulières évitent la fatigue.

## Dans StudyDesk

Le minuteur se trouve en bas de la barre de gauche. Tu peux changer les durées dans les Réglages. Ton temps de travail apparaît dans les Statistiques.
""", TutorialAction.StartPomodoro, null, "Lancer un Pomodoro"),

        new("planifier", "Planifier ses révisions", "Organisation", 4,
            "Préparer un examen semaine par semaine, sans tout faire la veille.",
            """
# Planifier ses révisions

## Note la date de l'examen

Sur l'Accueil, ajoute tes examens (brevet, bac, partiels, contrôles). Le compte à rebours t'aide à t'organiser.

## Découpe le travail

- Fais la liste des chapitres à revoir.
- Répartis-les sur les semaines qui restent.
- Garde la dernière semaine pour les **sujets d'entraînement**.

## Varie les méthodes

- **Fiches** : pour les définitions, les dates, le vocabulaire, les formules.
- **Quiz** : pour vérifier que tu as compris.
- **Exercices et sujets** : pour t'entraîner dans les conditions de l'examen.

## Le test plutôt que la relecture

Relire son cours donne l'impression de savoir. **Se tester** (fiches, quiz) montre ce que l'on sait vraiment, et c'est bien plus efficace pour retenir.
"""),

        new("quiz", "Créer des quiz utiles", "Mémoriser", 3,
            "Les 5 types de questions et quand les utiliser.",
            """
# Créer des quiz utiles

## Les 5 types de questions

- **QCM** : une ou plusieurs bonnes réponses. Mets des mauvaises réponses crédibles.
- **Vrai / faux** : rapide pour vérifier une notion.
- **Texte à trous** : écris la phrase et mets les mots à trouver entre crochets : La capitale de l'Espagne est [Madrid].
- **Réponse courte** : l'élève tape la réponse. Les accents et les majuscules ne comptent pas, et une petite faute de frappe est tolérée.
- **Relier** : associer des dates et des événements, des mots et leurs traductions.

## Plusieurs réponses acceptées

Dans un texte à trous, sépare les réponses possibles par une barre : [Louis XVI|Louis 16].

## Ajoute une explication

L'explication s'affiche après la réponse. C'est là que l'on apprend de ses erreurs.

> Tu peux aussi générer un quiz automatiquement à partir d'un document ou d'un paquet de fiches.
""", TutorialAction.NewQuiz, null, "Créer un quiz"),

        new("telephone", "Réviser sur son téléphone", "Prise en main", 2,
            "Envoyer ses fiches, quiz et cours sur Android par QR code, sans compte ni cloud.",
            """
# Réviser sur son téléphone

## Se connecter

1. Le PC et le téléphone doivent être sur le **même réseau Wi-Fi**.
2. Ouvre la page **Téléphone** dans StudyDesk et clique sur **Activer**.
3. Si Windows le demande, autorise StudyDesk sur les **réseaux privés**.
4. Scanne le **QR code** avec l'appareil photo du téléphone.

## Sur le téléphone

- **Réviser** tes fiches avec la répétition espacée
- Faire tes **quiz**
- **Lire** tes cours

## Sans Wi-Fi

Sur le téléphone, appuie sur **Télécharger pour hors ligne**. Tu obtiens un fichier qui s'ouvre sans connexion. Quand tu reviens sur le même Wi-Fi que ton PC, appuie sur **Synchroniser** pour renvoyer ta progression.

> Aucune donnée ne passe par Internet : tout reste entre ton PC et ton téléphone.
""", TutorialAction.OpenPhone, null, "Ouvrir la page Téléphone"),

        new("images", "Bien utiliser les images", "Méthodes", 3,
            "Insérer, dessiner, chercher des images libres de droits et citer ses sources.",
            """
# Bien utiliser les images

## Quatre façons d'ajouter une image

- **Depuis le PC** : bouton Image, ou glisser-déposer un fichier dans la page.
- **Copier-coller** : une capture d'écran (Windows + Maj + S), puis Ctrl + V.
- **Recherche en ligne** : des images libres de droits de Wikimedia Commons.
- **Dessin** : formes, flèches et dessin à main levée pour faire un schéma.

## Citer ses sources

Une image trouvée sur Internet appartient à quelqu'un. Les images de Wikimedia Commons sont libres, mais il faut citer l'auteur et la licence : StudyDesk ajoute la légende automatiquement.

## Des images utiles

Une image doit **expliquer** quelque chose : un schéma, une carte, une photo d'époque. Évite les images décoratives.
"""),

        new("catalogue", "Utiliser le catalogue", "Prise en main", 2,
            "Ajouter en un clic des fiches et des quiz prêts à l'emploi : capitales, dates, formules, verbes…",
            """
# Utiliser le catalogue

Le **Catalogue** contient des contenus déjà prêts : capitales, grandes dates, tables de multiplication, formules, tableau périodique, verbes irréguliers, figures de style, notions de philosophie…

## Ajouter un contenu

1. Ouvre le **Catalogue** dans la barre de gauche.
2. Choisis un contenu et clique sur **Ajouter**.
3. Un paquet de fiches et un quiz sont créés dans la bonne matière.

## Le personnaliser

Les contenus ajoutés sont à toi : tu peux supprimer des fiches, en ajouter, corriger une réponse ou ajouter des images.

> Astuce : commence par un seul paquet et révise-le quelques jours avant d'en ajouter un autre.
""", TutorialAction.OpenCatalog, null, "Ouvrir le catalogue"),

        new("vocabulaire", "Mémoriser du vocabulaire", "Mémoriser", 4,
            "Les méthodes qui marchent pour retenir des mots en langue étrangère.",
            """
# Mémoriser du vocabulaire

## Peu de mots, souvent

Mieux vaut **10 mots par jour** que 70 la veille du contrôle. La répétition espacée s'occupe du reste.

## Dans les deux sens

Crée les fiches dans les deux sens : anglais → français **et** français → anglais. Le bouton **Fiche inverse** le fait pour toi.

## En contexte

Un mot s'apprend mieux dans une phrase. Au verso, ajoute un exemple : « to borrow : emprunter — Can I borrow your pen? »

## Les faux amis

Attention aux mots qui ressemblent au français : *library* (bibliothèque), *actually* (en fait), *sensible* (raisonnable).

## À voix haute

Prononce le mot quand tu révises : tu mémorises aussi le son.

> Le catalogue contient déjà 100 verbes irréguliers et du vocabulaire anglais et espagnol.
""", TutorialAction.NewDeck, null, "Créer un paquet de vocabulaire"),

        new("carte-mentale", "Faire une carte mentale", "Méthodes", 3,
            "Organiser un chapitre en schéma pour le comprendre et le retenir.",
            """
# Faire une carte mentale

## Le principe

Le sujet au **centre**, les grandes idées en **branches**, les détails en **sous-branches**. Le cerveau retient mieux une image organisée qu'un long texte.

## Les règles

- Des **mots-clés**, pas de phrases.
- Une **couleur** par branche.
- Des **dessins** et des symboles : flèches, icônes, petits schémas.

## Dans StudyDesk

- Le modèle **Carte mentale** prépare le plan en texte.
- L'outil **Dessin** (Insertion › Dessin / schéma) permet de tracer la carte avec des formes et des flèches.
""", TutorialAction.NewDocumentFromTemplate, "carte", "Créer une carte mentale"),

        new("stress", "Gérer le stress des examens", "Organisation", 3,
            "Dormir, s'organiser, respirer : arriver serein le jour J.",
            """
# Gérer le stress des examens

## Avant

- **Planifie** tes révisions : le stress vient souvent de l'impression de ne pas avoir le temps.
- **Dors** suffisamment : la mémoire se consolide pendant le sommeil.
- Fais des **pauses** et bouge un peu (méthode Pomodoro).

## La veille

- Relis seulement tes fiches « À retenir », n'apprends rien de nouveau.
- Prépare tes affaires : convocation, stylos, calculatrice.

## Le jour J

- Lis **tout le sujet** avant de commencer.
- Commence par ce que tu sais faire.
- Si tu bloques : respire lentement (4 secondes pour inspirer, 4 pour expirer) et passe à la question suivante.

> Le stress n'est pas ton ennemi : un peu de stress aide à se concentrer.
"""),

        new("controle-maths", "Réussir un contrôle de maths", "Méthodes", 4,
            "Apprendre le cours, s'entraîner et bien rédiger.",
            """
# Réussir un contrôle de maths

## Connaître le cours par cœur

Définitions, propriétés et formules : mets-les en **fiches**. Le catalogue contient déjà les formules de géométrie et les identités remarquables.

## S'entraîner

Refais les exercices du cours **sans regarder la correction**. Comprendre une correction ne suffit pas : il faut savoir refaire.

## Bien rédiger

- Écris les **données** et ce que l'on cherche.
- Cite la **propriété** utilisée (« D'après le théorème de Pythagore… »).
- Fais une **phrase de conclusion** avec l'unité.

## Vérifier

Ton résultat est-il **cohérent** ? Une longueur négative ou une probabilité supérieure à 1, c'est une erreur.
"""),
    };

    public static Tutorial? Get(string id) => All.FirstOrDefault(t => t.Id == id);
}
