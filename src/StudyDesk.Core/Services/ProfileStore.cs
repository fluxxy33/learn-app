using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using StudyDesk.Core.Models;

namespace StudyDesk.Core.Services;

public static class Json
{
    public static readonly JsonSerializerOptions File = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static T? Read<T>(string path) where T : class
    {
        if (!System.IO.File.Exists(path)) return null;
        using var s = System.IO.File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(s, File);
    }

    /// <summary>Écriture atomique : on écrit dans un fichier temporaire puis on remplace.</summary>
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        using (var s = System.IO.File.Create(tmp))
            JsonSerializer.Serialize(s, value, File);
        System.IO.File.Move(tmp, path, true);
    }
}

/// <summary>Gère la liste des profils dans le dossier racine des données.</summary>
public sealed class ProfileManager
{
    public string Root { get; }
    private string IndexPath => Path.Combine(Root, "profiles.json");
    public ProfileIndex Index { get; private set; }

    public ProfileManager(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StudyDesk");
        Directory.CreateDirectory(Root);
        Index = Json.Read<ProfileIndex>(IndexPath) ?? new ProfileIndex();
    }

    public void Save() => Json.Write(IndexPath, Index);

    public string ProfileFolder(Profile p) => Path.Combine(Root, "profiles", p.Id);

    public Profile Create(string name, string colorHex = "#2F62D9")
    {
        var p = new Profile { Name = name.Trim(), ColorHex = colorHex };
        Index.Profiles.Add(p);
        Index.LastProfileId = p.Id;
        Save();
        var store = Open(p);
        DefaultContent.Seed(store);
        return p;
    }

    public ProfileStore Open(Profile p)
    {
        Index.LastProfileId = p.Id;
        Save();
        return new ProfileStore(p, ProfileFolder(p));
    }

    public void Delete(Profile p)
    {
        Index.Profiles.RemoveAll(x => x.Id == p.Id);
        if (Index.LastProfileId == p.Id) Index.LastProfileId = Index.Profiles.FirstOrDefault()?.Id;
        Save();
        var dir = ProfileFolder(p);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    public void Rename(Profile p, string name)
    {
        p.Name = name.Trim();
        Save();
    }
}

/// <summary>Accès aux données d'un profil : bibliothèque, contenus, médias, statistiques.</summary>
public sealed class ProfileStore
{
    public Profile Profile { get; }
    public string Folder { get; }
    public string ItemsFolder => Path.Combine(Folder, "items");
    public string MediaFolder => Path.Combine(Folder, "media");
    public Library Library { get; private set; }
    public StatsData Stats { get; private set; }

    public event EventHandler? Changed;

    public ProfileStore(Profile profile, string folder)
    {
        Profile = profile;
        Folder = folder;
        Directory.CreateDirectory(ItemsFolder);
        Directory.CreateDirectory(MediaFolder);
        Library = Json.Read<Library>(Path.Combine(Folder, "library.json")) ?? new Library();
        Stats = Json.Read<StatsData>(Path.Combine(Folder, "stats.json")) ?? new StatsData();
    }

    public void SaveLibrary()
    {
        Json.Write(Path.Combine(Folder, "library.json"), Library);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SaveStats() => Json.Write(Path.Combine(Folder, "stats.json"), Stats);

    // ------------------------------------------------------------ Matières

    public Subject? GetSubject(string id) => Library.Subjects.FirstOrDefault(s => s.Id == id);

    public Subject AddSubject(string name, string colorHex, string icon)
    {
        var s = new Subject
        {
            Name = name.Trim(), ColorHex = colorHex, Icon = icon,
            Order = Library.Subjects.Count == 0 ? 0 : Library.Subjects.Max(x => x.Order) + 1
        };
        Library.Subjects.Add(s);
        SaveLibrary();
        return s;
    }

    public void DeleteSubject(Subject s)
    {
        foreach (var item in Library.Items.Where(i => i.SubjectId == s.Id).ToList())
            DeleteItem(item, save: false);
        Library.Subjects.Remove(s);
        SaveLibrary();
    }

    public Chapter AddChapter(Subject s, string name)
    {
        var c = new Chapter { Name = name.Trim(), Order = s.Chapters.Count };
        s.Chapters.Add(c);
        SaveLibrary();
        return c;
    }

    public void DeleteChapter(Subject s, Chapter c)
    {
        foreach (var item in Library.Items.Where(i => i.ChapterId == c.Id))
            item.ChapterId = null;
        s.Chapters.Remove(c);
        SaveLibrary();
    }

    // ------------------------------------------------------------ Éléments

    public IEnumerable<LibraryItem> ItemsOf(ItemKind kind) => Library.Items.Where(i => i.Kind == kind);

    public LibraryItem? GetItem(string id) => Library.Items.FirstOrDefault(i => i.Id == id);

    public LibraryItem CreateItem(ItemKind kind, string title, string subjectId, string? chapterId)
    {
        var item = new LibraryItem { Kind = kind, Title = title.Trim(), SubjectId = subjectId, ChapterId = chapterId };
        Library.Items.Add(item);
        switch (kind)
        {
            case ItemKind.Deck: SaveDeck(new Deck { Id = item.Id }); break;
            case ItemKind.Quiz: SaveQuiz(new Quiz { Id = item.Id }); break;
            case ItemKind.Slideshow: SaveSlideshow(SlideshowFactory.CreateDefault(item.Id, title)); break;
        }
        SaveLibrary();
        return item;
    }

    public string ContentPath(LibraryItem item) => ContentPath(item.Id, item.Kind);

    public string ContentPath(string id, ItemKind kind) => Path.Combine(ItemsFolder, kind switch
    {
        ItemKind.Document => id + ".sdoc",
        ItemKind.Slideshow => id + ".slides.json",
        ItemKind.Deck => id + ".deck.json",
        ItemKind.Quiz => id + ".quiz.json",
        _ => id
    });

    /// <summary>Version HTML d'un document (lecture sur téléphone).</summary>
    public string DocumentHtmlPath(string id) => Path.Combine(ItemsFolder, id + ".html");
    /// <summary>Texte brut d'un document (recherche).</summary>
    public string DocumentTextPath(string id) => Path.Combine(ItemsFolder, id + ".txt");

    public void Touch(LibraryItem item, bool opened = false)
    {
        item.ModifiedAt = DateTime.Now;
        if (opened) item.OpenedAt = DateTime.Now;
        SaveLibrary();
    }

    public void DeleteItem(LibraryItem item, bool save = true)
    {
        Library.Items.Remove(item);
        foreach (var path in new[] { ContentPath(item), DocumentHtmlPath(item.Id), DocumentTextPath(item.Id) })
            if (File.Exists(path)) File.Delete(path);
        if (save) SaveLibrary();
    }

    public LibraryItem Duplicate(LibraryItem item)
    {
        var copy = new LibraryItem
        {
            Kind = item.Kind, Title = item.Title + " (copie)", SubjectId = item.SubjectId, ChapterId = item.ChapterId
        };
        Library.Items.Add(copy);
        if (File.Exists(ContentPath(item)))
        {
            switch (item.Kind)
            {
                case ItemKind.Deck:
                    var d = LoadDeck(item.Id); d.Id = copy.Id;
                    foreach (var c in d.Cards) { c.Id = NewId.Create(); Srs.Reset(c); }
                    SaveDeck(d);
                    break;
                case ItemKind.Quiz:
                    var q = LoadQuiz(item.Id); q.Id = copy.Id; SaveQuiz(q);
                    break;
                case ItemKind.Slideshow:
                    var s = LoadSlideshow(item.Id); s.Id = copy.Id; SaveSlideshow(s);
                    break;
                default:
                    File.Copy(ContentPath(item), ContentPath(copy));
                    break;
            }
        }
        foreach (var (src, dst) in new[] { (DocumentHtmlPath(item.Id), DocumentHtmlPath(copy.Id)), (DocumentTextPath(item.Id), DocumentTextPath(copy.Id)) })
            if (File.Exists(src)) File.Copy(src, dst, true);
        SaveLibrary();
        return copy;
    }

    public Deck LoadDeck(string id) => Json.Read<Deck>(ContentPath(id, ItemKind.Deck)) ?? new Deck { Id = id };
    public void SaveDeck(Deck d) => Json.Write(ContentPath(d.Id, ItemKind.Deck), d);
    public Quiz LoadQuiz(string id) => Json.Read<Quiz>(ContentPath(id, ItemKind.Quiz)) ?? new Quiz { Id = id };
    public void SaveQuiz(Quiz q) => Json.Write(ContentPath(q.Id, ItemKind.Quiz), q);
    public Slideshow LoadSlideshow(string id) => Json.Read<Slideshow>(ContentPath(id, ItemKind.Slideshow)) ?? SlideshowFactory.CreateDefault(id, "");
    public void SaveSlideshow(Slideshow s) => Json.Write(ContentPath(s.Id, ItemKind.Slideshow), s);

    public IEnumerable<(LibraryItem Item, Deck Deck)> AllDecks() =>
        ItemsOf(ItemKind.Deck).Select(i => (i, LoadDeck(i.Id))).ToList();

    // ------------------------------------------------------------ Médias

    public string MediaPath(string fileName) => Path.Combine(MediaFolder, Path.GetFileName(fileName));

    public string ImportMedia(byte[] bytes, string extension)
    {
        if (!extension.StartsWith('.')) extension = "." + extension;
        var name = NewId.Create() + extension.ToLowerInvariant();
        File.WriteAllBytes(MediaPath(name), bytes);
        return name;
    }

    public string ImportMediaFile(string path) => ImportMedia(File.ReadAllBytes(path), Path.GetExtension(path));

    // ------------------------------------------------------------ Partage (format maison)

    /// <summary>Exporte des éléments dans un fichier .studydesk (zip) pour les partager.</summary>
    public void ExportPackage(IEnumerable<LibraryItem> items, string destination)
    {
        if (File.Exists(destination)) File.Delete(destination);
        using var zip = ZipFile.Open(destination, ZipArchiveMode.Create);
        var list = items.ToList();
        var media = new HashSet<string>();
        var manifest = new PackageManifest();
        foreach (var item in list)
        {
            var subject = GetSubject(item.SubjectId);
            manifest.Items.Add(new PackageEntry
            {
                Id = item.Id, Kind = item.Kind, Title = item.Title,
                SubjectName = subject?.Name ?? "", SubjectColor = subject?.ColorHex ?? "#2F62D9", SubjectIcon = subject?.Icon ?? "",
                ChapterName = subject?.Chapters.FirstOrDefault(c => c.Id == item.ChapterId)?.Name
            });
            foreach (var path in new[] { ContentPath(item), DocumentHtmlPath(item.Id), DocumentTextPath(item.Id) })
                if (File.Exists(path)) zip.CreateEntryFromFile(path, "items/" + Path.GetFileName(path));
            foreach (var m in MediaReferences(item)) media.Add(m);
        }
        foreach (var m in media)
            if (File.Exists(MediaPath(m))) zip.CreateEntryFromFile(MediaPath(m), "media/" + m);
        var entry = zip.CreateEntry("manifest.json");
        using var s = entry.Open();
        JsonSerializer.Serialize(s, manifest, Json.File);
    }

    public IEnumerable<string> MediaReferences(LibraryItem item)
    {
        switch (item.Kind)
        {
            case ItemKind.Deck:
                foreach (var c in LoadDeck(item.Id).Cards)
                {
                    if (c.FrontImage != null) yield return c.FrontImage;
                    if (c.BackImage != null) yield return c.BackImage;
                }
                break;
            case ItemKind.Quiz:
                foreach (var q in LoadQuiz(item.Id).Questions)
                    if (q.Image != null) yield return q.Image;
                break;
            case ItemKind.Slideshow:
                foreach (var e in LoadSlideshow(item.Id).Slides.SelectMany(s => s.Elements))
                    if (e.ImageFile != null) yield return e.ImageFile;
                break;
        }
    }

    /// <summary>Importe un fichier .studydesk. Les matières manquantes sont créées. Retourne les éléments ajoutés.</summary>
    public List<LibraryItem> ImportPackage(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var manifestEntry = zip.GetEntry("manifest.json") ?? throw new InvalidDataException("Ce fichier n'est pas un paquet StudyDesk.");
        PackageManifest manifest;
        using (var s = manifestEntry.Open())
            manifest = JsonSerializer.Deserialize<PackageManifest>(s, Json.File) ?? new PackageManifest();

        foreach (var e in zip.Entries.Where(e => e.FullName.StartsWith("media/") && e.Name.Length > 0))
        {
            var dest = MediaPath(e.Name);
            if (!File.Exists(dest)) e.ExtractToFile(dest);
        }

        var added = new List<LibraryItem>();
        foreach (var p in manifest.Items)
        {
            var subject = Library.Subjects.FirstOrDefault(s => string.Equals(s.Name, p.SubjectName, StringComparison.OrdinalIgnoreCase))
                          ?? AddSubject(string.IsNullOrWhiteSpace(p.SubjectName) ? "Importé" : p.SubjectName, p.SubjectColor, p.SubjectIcon);
            string? chapterId = null;
            if (!string.IsNullOrWhiteSpace(p.ChapterName))
                chapterId = (subject.Chapters.FirstOrDefault(c => string.Equals(c.Name, p.ChapterName, StringComparison.OrdinalIgnoreCase))
                             ?? AddChapter(subject, p.ChapterName)).Id;

            var item = new LibraryItem { Kind = p.Kind, Title = p.Title, SubjectId = subject.Id, ChapterId = chapterId };
            foreach (var e in zip.Entries.Where(e => e.FullName.StartsWith("items/" + p.Id + ".")))
            {
                var suffix = e.Name[p.Id.Length..];
                var dest = Path.Combine(ItemsFolder, item.Id + suffix);
                e.ExtractToFile(dest, true);
            }
            // Mettre à jour l'identifiant interne des contenus JSON
            switch (item.Kind)
            {
                case ItemKind.Deck: var d = LoadDeck(item.Id); d.Id = item.Id; SaveDeck(d); break;
                case ItemKind.Quiz: var q = LoadQuiz(item.Id); q.Id = item.Id; SaveQuiz(q); break;
                case ItemKind.Slideshow: var sl = LoadSlideshow(item.Id); sl.Id = item.Id; SaveSlideshow(sl); break;
            }
            Library.Items.Add(item);
            added.Add(item);
        }
        SaveLibrary();
        return added;
    }

    /// <summary>Sauvegarde complète du profil dans un zip.</summary>
    public void Backup(string destination)
    {
        if (File.Exists(destination)) File.Delete(destination);
        ZipFile.CreateFromDirectory(Folder, destination);
    }

    // ------------------------------------------------------------ Statistiques

    public void LogReview(ReviewLogEntry e)
    {
        Stats.Reviews.Add(e);
        SaveStats();
    }

    public void LogQuiz(QuizResult r)
    {
        Stats.QuizResults.Add(r);
        SaveStats();
    }

    public void LogSession(StudySession s)
    {
        Stats.Sessions.Add(s);
        SaveStats();
    }
}

public sealed class PackageManifest
{
    public int Version { get; set; } = 1;
    public List<PackageEntry> Items { get; set; } = new();
}

public sealed class PackageEntry
{
    public string Id { get; set; } = "";
    public ItemKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string SubjectName { get; set; } = "";
    public string SubjectColor { get; set; } = "";
    public string SubjectIcon { get; set; } = "";
    public string? ChapterName { get; set; }
}
