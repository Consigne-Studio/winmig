namespace Winmig.Core;

/// <summary>Technologie d'interface d'un projet ou d'un écran.</summary>
public enum Techno { WinForms, Wpf, Mixte, Aucune }

/// <summary>Nature d'un écran : fenêtre autonome ou composant réutilisé dans d'autres écrans.</summary>
public enum NatureEcran { Fenetre, Dialogue, Composant, Page }

public enum Complexite { S, M, L, XL }

public enum Gravite { Info, Moyenne, Elevee }

public sealed record Occurrence(string Fichier, int Ligne, string Extrait);

/// <summary>Un projet .csproj ou .vbproj lu sur disque.</summary>
public sealed class Projet
{
    public required string Nom { get; init; }
    public required string Chemin { get; set; }
    public bool FormatSdk { get; init; }
    public List<string> Cibles { get; init; } = new();
    public string TypeSortie { get; init; } = "Library";
    public Techno Techno { get; set; } = Techno.Aucune;
    public List<Paquet> Paquets { get; init; } = new();
    public List<string> ReferencesProjets { get; init; } = new();
    public List<string> ReferencesComposants { get; init; } = new();
    public List<string> ReferencesCom { get; init; } = new();
    /// <summary>Ce qui fait tourner le programme en 32 bits (PlatformTarget x86, RuntimeIdentifier win-x86, Prefer32Bit), ou null.</summary>
    public string? Plateforme32 { get; init; }
    public int FichiersCs { get; set; }
    public int FichiersXaml { get; set; }
    public int LignesCode { get; set; }
    public int FichiersResx { get; set; }
    public List<string> Langues { get; init; } = new();
}

public sealed record Paquet(string Nom, string? Version, string Origine);

/// <summary>Un nœud du schéma d'écran reconstitué à partir du code du concepteur.</summary>
public sealed class NoeudSchema
{
    public required string Nom { get; init; }
    public required string Type { get; set; }
    public string? Texte { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int L { get; set; }
    public int H { get; set; }
    public string Ancrage { get; set; } = "None";
    public bool Visible { get; set; } = true;
    public List<NoeudSchema> Enfants { get; init; } = new();
    public List<string> Elements { get; init; } = new();
    /// <summary>Position calculée après mise en page (repère de l'écran).</summary>
    public int Ax { get; set; }
    public int Ay { get; set; }
    public int Al { get; set; }
    public int Ah { get; set; }
}

public sealed class Ecran
{
    public required string Id { get; init; }
    public required string Classe { get; init; }
    public required string Projet { get; init; }
    public required Techno Techno { get; init; }
    public NatureEcran Nature { get; set; }
    public string TypeBase { get; set; } = "";
    public string? Titre { get; set; }
    public List<string> Fichiers { get; init; } = new();
    public int Controles { get; set; }
    public Dictionary<string, int> TypesControles { get; init; } = new();
    public int Evenements { get; set; }
    public int Liaisons { get; set; }
    public int LignesCodeBehind { get; set; }
    public int Branches { get; set; }
    public int Methodes { get; set; }
    public List<string> ComposantsInternes { get; init; } = new();
    public List<string> ControlesTiers { get; init; } = new();
    public List<string> Signaux { get; init; } = new();
    public bool Mvvm { get; set; }
    public int LargeurConception { get; set; }
    public int HauteurConception { get; set; }
    public NoeudSchema? Schema { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<string, NoeudSchema> SchemaNoeuds { get; set; } = new Dictionary<string, NoeudSchema>();
    /// <summary>Nœuds du concepteur hors de l'arbre (colonnes, éléments de menu) : gardés dans inventaire.json pour refaire le schéma.</summary>
    public List<NoeudSchema> NoeudsHorsArbre
    {
        get
        {
            var arbre = new HashSet<NoeudSchema>(ReferenceEqualityComparer.Instance);
            void Parcourir(NoeudSchema n) { arbre.Add(n); n.Enfants.ForEach(Parcourir); }
            if (Schema != null) Parcourir(Schema);
            return SchemaNoeuds.Values.Where(n => !arbre.Contains(n)).ToList();
        }
        set => horsArbre = value;
    }
    internal List<NoeudSchema> horsArbre = new();
    public double Score { get; set; }
    public Complexite Complexite { get; set; }
    public double Unites { get; set; }
    public List<string> UtilisePar { get; init; } = new();
    /// <summary>Vrai si l'écran est atteint depuis un point d'entrée de l'application (par le code ou par composition).</summary>
    public bool Atteint { get; set; } = true;
}

/// <summary>Un fichier de rapport Crystal Reports (.rpt) trouvé dans le dossier d'un projet.</summary>
public sealed class RapportCrystal
{
    public required string Fichier { get; init; }
    public required string Projet { get; init; }
    public long Octets { get; init; }
    /// <summary>Fichiers de code qui ouvrent ce rapport (nom du fichier ou classe générée).</summary>
    public List<string> OuvertPar { get; init; } = new();
    public bool Atteint { get; set; }
}

public sealed class Dependance
{
    public required string Nom { get; init; }
    public string? Version { get; set; }
    public required string Origine { get; init; }
    public string Categorie { get; set; } = "Autre";
    public string Famille { get; set; } = "";
    public string Remplacement { get; set; } = "";
    public Gravite Risque { get; set; } = Gravite.Info;
    public bool Interface { get; set; }
    public SortedSet<string> Projets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Usage d'une API propre à Windows ou au poste client, à traiter avant de passer au web.</summary>
public sealed class Constat
{
    public required string Code { get; init; }
    public required string Titre { get; init; }
    public required Gravite Gravite { get; init; }
    public required string Pourquoi { get; init; }
    public required string Solution { get; init; }
    public double JoursBas { get; init; }
    public double JoursHaut { get; init; }
    public List<Occurrence> Occurrences { get; init; } = new();
    public SortedSet<string> Ecrans { get; init; } = new(StringComparer.Ordinal);
}

public sealed class Inventaire
{
    public string Outil { get; init; } = "winmig";
    public string VersionOutil { get; init; } = Version.Courante;
    /// <summary>Nom du dossier racine analysé (jamais son chemin complet : il dirait le poste et le nom de l'utilisateur).</summary>
    public required string Racine { get; set; }
    public required string Entree { get; init; }
    public DateTimeOffset Date { get; init; } = DateTimeOffset.UtcNow;
    public List<Projet> Projets { get; init; } = new();
    public List<Ecran> Ecrans { get; init; } = new();
    public List<Dependance> Dependances { get; init; } = new();
    public List<Constat> Constats { get; init; } = new();
    public List<RapportCrystal> RapportsCrystal { get; init; } = new();
    /// <summary>Ce qui bloque ou complique le passage à .NET 10, projet par projet.</summary>
    public List<PointNet10> PointsNet10 { get; init; } = new();
    public List<string> Avertissements { get; init; } = new();
    /// <summary>Classes de départ du calcul de périmètre (projets exécutables). Vide : tout est dans le périmètre.</summary>
    public List<string> PointsEntree { get; init; } = new();
    /// <summary>Fichiers du code atteint depuis les points d'entrée (chemins relatifs). Vide : tout est dans le périmètre.</summary>
    public SortedSet<string> FichiersAtteints { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Relit un inventaire.json produit par winmig (commande « rapport »).</summary>
    public static Inventaire Charger(string json, System.Text.Json.JsonSerializerOptions options)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var racine = doc.RootElement.TryGetProperty("inventaire", out var i) ? i : doc.RootElement;
        var inv = System.Text.Json.JsonSerializer.Deserialize<Inventaire>(racine, options) ?? throw new InvalidOperationException("inventaire.json vide.");
        if (inv.Outil != "winmig") throw new InvalidOperationException("Ce fichier n'a pas été produit par winmig.");
        foreach (var e in inv.Ecrans)
        {
            var noeuds = new Dictionary<string, NoeudSchema>();
            void Parcourir(NoeudSchema n) { noeuds.TryAdd(n.Nom, n); n.Enfants.ForEach(Parcourir); }
            if (e.Schema != null) { e.Schema.Enfants.ForEach(Parcourir); }
            foreach (var n in e.horsArbre) noeuds.TryAdd(n.Nom, n);
            e.horsArbre = new();
            e.SchemaNoeuds = noeuds;
        }
        return inv;
    }

    /// <summary>Retire les extraits de code (fichier et ligne restent) : pour envoyer l'inventaire sans aucune ligne de code.</summary>
    public void RetirerExtraits()
    {
        foreach (var c in Constats)
            for (var k = 0; k < c.Occurrences.Count; k++) c.Occurrences[k] = c.Occurrences[k] with { Extrait = "" };
    }

    /// <summary>Retire aussi les libellés d'interface (textes des contrôles, titres des fenêtres) : option --sans-libelles.</summary>
    public void RetirerLibelles()
    {
        foreach (var e in Ecrans)
        {
            e.Titre = null;
            void Vider(NoeudSchema n) { n.Texte = null; n.Enfants.ForEach(Vider); }
            if (e.Schema != null) Vider(e.Schema);
            foreach (var n in e.SchemaNoeuds.Values) n.Texte = null;
        }
    }

    /// <summary>Corrige le périmètre à la main : classes (nom complet ou simple) ajoutées ou retirées.</summary>
    public List<string> Ajuster(IEnumerable<string> inclure, IEnumerable<string> exclure)
    {
        var inconnus = new List<string>();
        Ecran? Trouver(string nom)
        {
            var e = Ecrans.FirstOrDefault(x => x.Classe == nom) ?? Ecrans.FirstOrDefault(x => x.Classe.EndsWith("." + nom, StringComparison.Ordinal));
            if (e == null) inconnus.Add(nom);
            return e;
        }
        foreach (var n in inclure)
            if (Trouver(n) is { } e) { e.Atteint = true; if (PointsEntree.Count > 0) foreach (var f in e.Fichiers) FichiersAtteints.Add(f); }
        foreach (var n in exclure)
            if (Trouver(n) is { } e)
            {
                e.Atteint = false;
                foreach (var f in e.Fichiers.Where(f => !Ecrans.Any(x => x != e && x.Atteint && x.Fichiers.Contains(f)))) FichiersAtteints.Remove(f);
            }
        return inconnus;
    }

    public bool OccurrenceAtteinte(Occurrence o) => PointsEntree.Count == 0 || FichiersAtteints.Contains(o.Fichier) || o.Ligne == 0;
}

public static class Version
{
    public const string Courante = "1.0.0";
}
