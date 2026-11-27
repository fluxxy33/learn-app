namespace StudyDesk.Core.Services;

/// <summary>
/// Modèles de documents écrits en « texte simple » :
/// # Titre 1, ## Titre 2, ### Titre 3, - puce, 1. liste numérotée, > encadré « À retenir »,
/// **gras**, *italique*, --- séparateur. Une ligne vide sépare les paragraphes.
/// </summary>
public static class Templates
{
    public sealed record DocTemplate(string Id, string Name, string Description, string Markup);

    public static readonly IReadOnlyList<DocTemplate> Documents = new[]
    {
        new DocTemplate("vide", "Document vide", "Une page blanche.", ""),
        new DocTemplate("cours", "Fiche de cours", "Titre, notions clés, définitions et résumé. Prête à être transformée en fiches.", FicheDeCours),
        new DocTemplate("cornell", "Prise de notes Cornell", "Notes, mots-clés et résumé : la méthode de prise de notes des universités.", Cornell),
        new DocTemplate("dissertation", "Dissertation", "Introduction, développement en parties, conclusion.", Dissertation),
        new DocTemplate("commentaire", "Commentaire de texte", "Présentation du texte, axes de lecture, conclusion.", Commentaire),
        new DocTemplate("tp", "Compte rendu de TP", "Objectif, matériel, protocole, résultats, conclusion.", CompteRenduTp),
        new DocTemplate("expose", "Préparer un exposé", "Le plan et les notes de ton exposé avant de faire le diaporama.", PreparationExpose),
    };

    public const string FicheDeCours = """
# Titre du chapitre

> Objectif : ce que je dois savoir faire à la fin du chapitre.

## Notion 1

Explique la notion avec tes mots. Mets les **mots importants en gras** : ils deviendront des textes à trous dans les quiz.

## Notion 2

Une explication courte, puis un exemple.

- Exemple 1
- Exemple 2

## Définitions

Mot-clé : sa définition en une phrase.

Autre mot-clé : sa définition.

## Résumé

Trois phrases maximum pour résumer tout le chapitre.
""";

    public const string Cornell = """
# Sujet du cours — date

## Mots-clés et questions

- Question 1 ?
- Question 2 ?
- Mot-clé

## Notes

Prends tes notes ici pendant le cours. Utilise des abréviations, des flèches, des listes.

## Résumé

Après le cours, résume en 3 à 5 phrases ce que tu as compris. C'est ce résumé que tu reliras avant le contrôle.
""";

    public const string Dissertation = """
# Sujet : recopie le sujet exactement

## Introduction

**Accroche** : une citation, un fait ou un exemple qui introduit le sujet.

**Définition des termes** : explique les mots importants du sujet.

**Problématique** : la question à laquelle ta dissertation va répondre.

**Annonce du plan** : « Nous verrons d'abord…, puis…, enfin… »

## I. Première partie

Idée directrice de la partie.

### A. Premier argument

Argument, exemple précis, analyse de l'exemple.

### B. Deuxième argument

Argument, exemple précis, analyse de l'exemple.

> Transition : une phrase qui résume la partie I et annonce la partie II.

## II. Deuxième partie

### A. Premier argument

### B. Deuxième argument

## Conclusion

**Bilan** : réponds clairement à la problématique.

**Ouverture** : élargis vers une autre question proche.
""";

    public const string Commentaire = """
# Commentaire : titre de l'œuvre, auteur

## Introduction

Présente l'auteur, l'œuvre, la date et le genre du texte.

Situe l'extrait : où se trouve-t-il dans l'œuvre ?

**Problématique** : qu'est-ce qui rend ce texte intéressant ?

Annonce des axes de lecture.

## I. Premier axe

Citation entre guillemets, procédé d'écriture, interprétation.

## II. Deuxième axe

Citation, procédé, interprétation.

## Conclusion

Réponds à la problématique, puis ouvre vers un autre texte ou une autre œuvre.
""";

    public const string CompteRenduTp = """
# TP : titre de l'expérience

## Objectif

Ce que l'on cherche à montrer ou à mesurer.

## Hypothèse

Ce que je pense obtenir, et pourquoi.

## Matériel

- Matériel 1
- Matériel 2

## Protocole

1. Première étape
2. Deuxième étape
3. Troisième étape

## Résultats

Tableau de mesures, schéma ou photo (Insertion › Image).

## Interprétation et conclusion

Les résultats confirment-ils l'hypothèse ? Quelles sont les sources d'erreur ?
""";

    public const string PreparationExpose = """
# Exposé : sujet

> Durée prévue : 10 minutes. Une diapositive par minute environ.

## Problématique

La question à laquelle l'exposé répond.

## Plan

1. Introduction
2. Première partie
3. Deuxième partie
4. Conclusion

## Ce que je dis à l'oral

Écris ici les phrases clés de chaque partie. Sur les diapositives, mets seulement des mots-clés et des images.

## Sources

- Livre ou manuel
- Site web (adresse, date de consultation)
""";

    public const string PythagoreExample = """
# Théorème de Pythagore

> Objectif : savoir calculer la longueur d'un côté dans un triangle rectangle.

## Énoncé du théorème

Dans un triangle rectangle, le carré de l'**hypoténuse** est égal à la **somme des carrés** des deux autres côtés.

Si ABC est rectangle en A, alors BC² = AB² + AC².

## L'hypoténuse

C'est le côté opposé à l'**angle droit**. C'est toujours le plus long côté du triangle.

## Réciproque

Si BC² = AB² + AC², alors le triangle ABC est **rectangle en A**.

## Définitions

Hypoténuse : le plus long côté d'un triangle rectangle, en face de l'angle droit.

Triangle rectangle : un triangle qui a un angle droit.

## Exemple

AB = 3 cm et AC = 4 cm. Alors BC² = 9 + 16 = 25, donc BC = **5 cm**.

---

Astuce : clique sur Réviser › Créer des fiches pour transformer ce cours en fiches de révision.
""";
}
