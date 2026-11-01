using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Winmig.Core;

/// <summary>Lecture des solutions et des projets MSBuild (format SDK et ancien format).</summary>
public static class Projets
{
    static readonly string[] DossiersIgnores = { "bin", "obj", "node_modules", ".git", ".vs", "packages", "TestResults" };

    public static bool Ignore(string chemin) =>
        chemin.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(p => DossiersIgnores.Contains(p, StringComparer.OrdinalIgnoreCase));

    /// <summary>Liste les fichiers projet à analyser à partir d'un dossier, d'une solution ou d'un projet.</summary>
    public static List<string> Decouvrir(string entree, List<string> avertissements)
    {
        var plein = Path.GetFullPath(entree);
        if (Directory.Exists(plein))
            return Directory.EnumerateFiles(plein, "*.*proj", SearchOption.AllDirectories)
                .Where(f => (f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)) && !Ignore(Path.GetRelativePath(plein, f)))
                .OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (!File.Exists(plein))
            throw new FileNotFoundException($"Introuvable : {entree}");

        var ext = Path.GetExtension(plein).ToLowerInvariant();
        var dossier = Path.GetDirectoryName(plein)!;
        List<string> liste;
        if (ext == ".sln")
        {
            var re = new Regex("^Project\\(\"\\{[^}]+\\}\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"([^\"]+\\.(?:cs|vb)proj)\"", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            liste = re.Matches(File.ReadAllText(plein)).Select(m => Normaliser(Path.Combine(dossier, m.Groups[1].Value))).ToList();
        }
        else if (ext == ".slnx")
        {
            XDocument sx;
            try { sx = XDocument.Load(plein); }
            catch (System.Xml.XmlException ex) { throw new InvalidOperationException($"Solution illisible {Path.GetFileName(plein)} : {ex.Message}"); }
            liste = sx.Descendants().Where(e => e.Name.LocalName == "Project")
                .Select(e => (string?)e.Attribute("Path")).Where(p => p != null && Regex.IsMatch(p!, "\\.(cs|vb)proj$", RegexOptions.IgnoreCase))
                .Select(p => Normaliser(Path.Combine(dossier, p!))).ToList();
        }
        else if (ext is ".csproj" or ".vbproj")
        {
            // Le projet d'entrée et tous les projets qu'il référence, de proche en proche.
            liste = new List<string>();
            var aVoir = new Queue<string>();
            aVoir.Enqueue(plein);
            while (aVoir.Count > 0)
            {
                var p = aVoir.Dequeue();
                if (liste.Contains(p, StringComparer.OrdinalIgnoreCase)) continue;
                if (!File.Exists(p)) { avertissements.Add($"Projet référencé introuvable : {Path.GetFileName(p)}"); continue; }
                liste.Add(p);
                try { foreach (var r in ReferencesProjets(p)) aVoir.Enqueue(r); }
                catch (System.Xml.XmlException) { avertissements.Add($"Références illisibles dans {Path.GetFileName(p)}"); }
            }
        }
        else throw new ArgumentException($"Entrée non reconnue : {entree} (attendu : dossier, .sln, .slnx, .csproj ou .vbproj)");

        var manquants = liste.Where(p => !File.Exists(p)).ToList();
        foreach (var m in manquants) avertissements.Add($"Projet listé mais introuvable : {Path.GetFileName(m)}");
        return liste.Except(manquants).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    static string Normaliser(string chemin) => Path.GetFullPath(chemin.Replace('\\', Path.DirectorySeparatorChar));

    static IEnumerable<XElement> Elements(XDocument d, string nom) => d.Descendants().Where(e => e.Name.LocalName == nom);

    static List<string> ReferencesProjets(string projet)
    {
        var d = XDocument.Load(projet);
        var dossier = Path.GetDirectoryName(projet)!;
        return Elements(d, "ProjectReference").Select(e => (string?)e.Attribute("Include")).Where(i => i != null)
            .Select(i => Normaliser(Path.Combine(dossier, i!))).ToList();
    }

    /// <summary>Propriétés MSBuild connues : projet, Directory.Build.props des dossiers parents et leurs imports directs.</summary>
    static Dictionary<string, string> Proprietes(XDocument d, string projet, out Dictionary<string, string> versionsCentrales)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        versionsCentrales = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fichiers = new List<string>();
        var dossier = Path.GetDirectoryName(projet);
        while (dossier != null)
        {
            foreach (var nom in new[] { "Directory.Build.props", "Directory.Packages.props" })
            {
                var f = Path.Combine(dossier, nom);
                if (File.Exists(f) && !fichiers.Contains(f)) fichiers.Add(f);
            }
            dossier = Path.GetDirectoryName(dossier);
        }
        var tous = new List<string>();
        foreach (var f in fichiers)
        {
            tous.Add(f);
            try
            {
                foreach (var imp in Elements(XDocument.Load(f), "Import").Select(e => (string?)e.Attribute("Project")).Where(p => p != null && !p!.Contains("$(")))
                {
                    var c = Normaliser(Path.Combine(Path.GetDirectoryName(f)!, imp!));
                    if (File.Exists(c)) tous.Add(c);
                }
            }
            catch (System.Xml.XmlException) { }
        }
        foreach (var f in tous.AsEnumerable().Reverse())
        {
            try
            {
                var x = XDocument.Load(f);
                foreach (var g in Elements(x, "PropertyGroup").SelectMany(g => g.Elements()))
                    props[g.Name.LocalName] = g.Value.Trim();
                foreach (var v in Elements(x, "PackageVersion"))
                {
                    var inc = (string?)v.Attribute("Include");
                    var ver = (string?)v.Attribute("Version");
                    if (inc != null && ver != null) versionsCentrales[inc] = ver;
                }
            }
            catch (System.Xml.XmlException) { }
        }
        foreach (var g in Elements(d, "PropertyGroup").SelectMany(g => g.Elements()))
            if (!g.HasElements) props[g.Name.LocalName] = g.Value.Trim();
        return props;
    }

    static string? Resoudre(string? valeur, Dictionary<string, string> props)
    {
        if (valeur == null) return null;
        var r = Regex.Replace(valeur, "\\$\\((\\w+)\\)", m => props.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
        return r.Contains("$(") ? null : r;
    }

    public static Projet Lire(string chemin, string? nomProjet = null)
    {
        var d = XDocument.Load(chemin);
        var dossier = Path.GetDirectoryName(chemin)!;
        var racine = d.Root!;
        var sdk = racine.Attribute("Sdk") != null || Elements(d, "Sdk").Any() || Elements(d, "Import").Any(i => i.Attribute("Sdk") != null);
        var props = Proprietes(d, chemin, out var centrales);
        string? P(string nom) => props.TryGetValue(nom, out var v) ? Resoudre(v, props) : null;

        var cibles = new List<string>();
        foreach (var c in new[] { P("TargetFrameworks"), P("TargetFramework") })
            if (!string.IsNullOrWhiteSpace(c)) cibles.AddRange(c.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var ancienne = P("TargetFrameworkVersion");
        if (cibles.Count == 0 && ancienne != null) cibles.Add("net" + ancienne.TrimStart('v').Replace(".", ""));

        var projet = new Projet
        {
            Nom = nomProjet ?? Path.GetFileNameWithoutExtension(chemin),
            Chemin = chemin,
            FormatSdk = sdk,
            Cibles = cibles.Distinct().ToList(),
            TypeSortie = P("OutputType") ?? "Library",
            Plateforme32 = Plateforme32(d, props, P("OutputType")),
        };

        foreach (var pr in Elements(d, "PackageReference"))
        {
            var nom = (string?)pr.Attribute("Include") ?? (string?)pr.Attribute("Update");
            if (nom == null) continue;
            var ver = Resoudre((string?)pr.Attribute("Version") ?? pr.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value, props);
            if (ver == null && centrales.TryGetValue(nom, out var vc)) ver = vc;
            projet.Paquets.Add(new Paquet(nom, ver, "NuGet"));
        }
        var packagesConfig = Path.Combine(dossier, "packages.config");
        if (File.Exists(packagesConfig))
            foreach (var p in Elements(XDocument.Load(packagesConfig), "package"))
            {
                var id = (string?)p.Attribute("id");
                if (id != null && !projet.Paquets.Any(x => x.Nom.Equals(id, StringComparison.OrdinalIgnoreCase)))
                    projet.Paquets.Add(new Paquet(id, (string?)p.Attribute("version"), "NuGet"));
            }
        foreach (var r in Elements(d, "Reference"))
        {
            var inc = (string?)r.Attribute("Include");
            if (inc == null) continue;
            var parts = inc.Split(',', StringSplitOptions.TrimEntries);
            var ver = parts.FirstOrDefault(x => x.StartsWith("Version=", StringComparison.OrdinalIgnoreCase))?.Substring(8);
            projet.ReferencesComposants.Add(parts[0]);
            if (!Catalogue.EstFramework(parts[0]) && !projet.Paquets.Any(x => parts[0].StartsWith(x.Nom, StringComparison.OrdinalIgnoreCase)))
                projet.Paquets.Add(new Paquet(parts[0], ver, "Assemblage"));
        }
        foreach (var c in Elements(d, "COMReference"))
        {
            var inc = (string?)c.Attribute("Include");
            if (inc == null) continue;
            projet.ReferencesCom.Add(inc);
            projet.Paquets.Add(new Paquet(inc, null, "COM"));
        }
        projet.ReferencesProjets.AddRange(Elements(d, "ProjectReference").Select(e => (string?)e.Attribute("Include")).Where(i => i != null)
            .Select(i => Path.GetFileNameWithoutExtension(i!.Replace('\\', '/'))));

        var winforms = string.Equals(P("UseWindowsForms"), "true", StringComparison.OrdinalIgnoreCase)
            || projet.ReferencesComposants.Contains("System.Windows.Forms");
        var wpf = string.Equals(P("UseWPF"), "true", StringComparison.OrdinalIgnoreCase)
            || projet.ReferencesComposants.Contains("PresentationFramework");
        projet.Techno = winforms && wpf ? Techno.Mixte : winforms ? Techno.WinForms : wpf ? Techno.Wpf : Techno.Aucune;
        return projet;
    }

    /// <summary>
    /// Le programme tourne-t-il en 32 bits ? Seuls comptent les groupes sans condition ou de configuration Release
    /// (un x86 limité à Debug|x86 n'est pas la configuration livrée), puis Directory.Build.props.
    /// </summary>
    static string? Plateforme32(XDocument d, Dictionary<string, string> props, string? sortie)
    {
        var groupes = Elements(d, "PropertyGroup").Where(g => g.Attribute("Condition") is not { } c || c.Value.Contains("Release", StringComparison.OrdinalIgnoreCase)).ToList();
        string? Valeur(string nom)
        {
            var e = groupes.SelectMany(g => g.Elements()).LastOrDefault(x => x.Name.LocalName == nom);
            if (e != null) return e.Value.Trim();
            return Elements(d, nom).Any() ? null : props.GetValueOrDefault(nom);
        }
        if (string.Equals(Valeur("PlatformTarget"), "x86", StringComparison.OrdinalIgnoreCase)) return "PlatformTarget x86";
        var rid = $"{Valeur("RuntimeIdentifier")};{Valeur("RuntimeIdentifiers")}";
        if (rid.Contains("win-x86", StringComparison.OrdinalIgnoreCase)) return "RuntimeIdentifier win-x86";
        if (string.Equals(Valeur("Prefer32Bit"), "true", StringComparison.OrdinalIgnoreCase) && sortie is "Exe" or "WinExe"
            && !string.Equals(Valeur("PlatformTarget"), "x64", StringComparison.OrdinalIgnoreCase))
            return "Prefer32Bit";
        return null;
    }

    /// <summary>Fichiers d'un motif sous le dossier d'un projet, sans ceux des sous-projets ni des dossiers ignorés.</summary>
    public static IEnumerable<string> FichiersSousProjet(string dossier, IEnumerable<string> autresDossiersProjets, string motif)
    {
        var sousProjets = autresDossiersProjets.Where(o => !o.Equals(dossier, StringComparison.OrdinalIgnoreCase)
            && o.StartsWith(dossier + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).ToList();
        try
        {
            return Directory.EnumerateFiles(dossier, motif, SearchOption.AllDirectories)
                .Where(f => !Ignore(Path.GetRelativePath(dossier, f)) && !sousProjets.Any(s => f.StartsWith(s + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(f => f, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { return Array.Empty<string>(); }
    }

    /// <summary>Fichiers source du projet : déclarés (ancien format) ou trouvés sous son dossier (format SDK).</summary>
    public static List<string> FichiersSource(Projet projet, IEnumerable<string> autresDossiersProjets, string extension)
    {
        var dossier = Path.GetDirectoryName(projet.Chemin)!;
        if (!projet.FormatSdk)
        {
            var d = XDocument.Load(projet.Chemin);
            var items = extension == ".xaml" ? new[] { "Page", "ApplicationDefinition" } : new[] { "Compile" };
            var declares = d.Descendants().Where(e => items.Contains(e.Name.LocalName))
                .Select(e => (string?)e.Attribute("Include")).Where(i => i != null && i!.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && !i.Contains('*'))
                .Select(i => Normaliser(Path.Combine(dossier, i!))).Where(File.Exists).ToList();
            if (declares.Count > 0 || extension != ".xaml") return declares.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        var sousProjets = autresDossiersProjets.Where(o => !o.Equals(dossier, StringComparison.OrdinalIgnoreCase)
            && o.StartsWith(dossier + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).ToList();
        return Directory.EnumerateFiles(dossier, "*" + extension, SearchOption.AllDirectories)
            .Where(f => !Ignore(Path.GetRelativePath(dossier, f)) && !sousProjets.Any(s => f.StartsWith(s + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    public static int LignesUtiles(string texte, bool vb)
    {
        var n = 0;
        var commentaireBloc = false;
        foreach (var brute in texte.Split('\n'))
        {
            var l = brute.Trim();
            if (l.Length == 0) continue;
            if (vb) { if (!l.StartsWith('\'') && !l.StartsWith("REM ", StringComparison.OrdinalIgnoreCase)) n++; continue; }
            if (commentaireBloc) { if (l.Contains("*/")) commentaireBloc = false; continue; }
            if (l.StartsWith("//")) continue;
            if (l.StartsWith("/*")) { commentaireBloc = !l.Contains("*/"); continue; }
            n++;
        }
        return n;
    }
}
