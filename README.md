# StudyDesk

Une application Windows pour **apprendre** : écrire ses cours, préparer ses exposés, créer des fiches et des quiz, et réviser au bon moment, sur PC ou sur téléphone.

Pour les collégiens, les lycéens et les étudiants. Tout fonctionne **hors ligne**. Internet sert seulement, si tu le veux, à chercher des images libres de droits et des résumés Wikipédia.

## Télécharger le .exe

1. Ouvre l'onglet **Actions** du dépôt GitHub, puis la dernière exécution réussie de « Compiler StudyDesk ».
2. En bas de la page, télécharge l'artefact **StudyDesk-windows**.
3. Décompresse-le et lance **StudyDesk.exe**. Il n'y a rien à installer : .NET est inclus.

Pour publier une version téléchargeable dans **Releases**, crée un tag qui commence par `v` (par exemple `v1.0.0`). La compilation joint alors automatiquement `StudyDesk-windows.zip` à la version.

Au premier lancement, Windows SmartScreen peut afficher « Windows a protégé votre ordinateur », car l'application n'est pas signée. Clique sur **Informations complémentaires**, puis sur **Exécuter quand même**.

## Ce qu'on peut faire

| Module | Fonctions |
|---|---|
| **Bibliothèque** | Matières (couleur, icône) et chapitres, recherche dans le texte, favoris, déplacer, dupliquer, partager un fichier `.studydesk` |
| **Documents** | Titres 1 à 3, gras, italique, couleurs, surlignage, listes, alignement, tableaux, encadrés « À retenir », images (fichier, glisser-déposer, copier-coller, Wikimedia), dessin et schémas, résumés Wikipédia avec leur source, correcteur orthographique, enregistrement automatique |
| **Diaporamas** | 8 thèmes, mises en page, zones de texte, images, formes et flèches, dessin à main levée, notes de l'orateur, transitions (fondu, glissement, zoom), présentation plein écran, modèle « Exposé » |
| **Fiches** | Recto / verso avec images, import d'une liste, fiches inversées, révision avec **répétition espacée** |
| **Quiz** | QCM, vrai / faux, texte à trous, réponse courte (accents et petites fautes tolérés), relier, explications, score et erreurs à retravailler |
| **Conversion automatique** | Document → fiches, document → quiz, diaporama → fiches, fiches → quiz |
| **Téléphone (Android)** | Scanne un QR code : réviser, faire les quiz et lire ses cours sur le même Wi-Fi. Fichier hors ligne, et synchronisation de la progression vers le PC |
| **Tutoriels** | Répétition espacée, bonnes fiches, prise de notes Cornell, exposé, dissertation, Pomodoro, planning de révision… |
| **Statistiques** | Révisions par jour, prévision, jours de suite, temps de travail, maîtrise par matière, résultats des quiz |
| **Organisation** | Minuteur Pomodoro, compte à rebours des examens, plusieurs profils, thème clair / sombre / automatique, sauvegarde en un fichier |

## Où sont mes données ?

Dans `%APPDATA%\StudyDesk` : un dossier par profil, avec la bibliothèque (`library.json`), les contenus (`items/`), les images (`media/`) et les statistiques (`stats.json`). **Réglages › Sauvegarder** crée une copie complète en `.zip`.

## Pour les développeurs

- `src/StudyDesk.Core` : données, répétition espacée, correction des quiz, conversions, serveur et app téléphone (`Web/phone.html`). Bibliothèque .NET 10 sans dépendance à Windows.
- `src/StudyDesk` : application WPF (.NET 10) avec le thème Fluent de Windows 11.
- `tests/StudyDesk.Tests` : tests unitaires (`dotnet test`).

Compiler en local sous Windows :

```
dotnet build StudyDesk.slnx
dotnet run --project src/StudyDesk
```

`StudyDesk.exe --selftest` ouvre chaque écran avec un profil temporaire, fait des captures et écrit un rapport. La compilation automatique s'en sert pour vérifier l'application.
