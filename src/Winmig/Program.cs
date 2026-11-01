using System.Text.Json;
using System.Text.Json.Serialization;
using Winmig.Core;

return Cli.Executer(args, Console.Out, Console.Error);

public static class Cli
{
    public const string Aide = """
        winmig — inventaire et chiffrage d'une application WinForms ou WPF avant migration vers le web

        Usage :
          winmig analyser <entrée> [-o <dossier>] [options]
          winmig rapport <inventaire.json> [-o <dossier>] [options]

        <entrée> : dossier, solution (.sln, .slnx) ou projet (.csproj, .vbproj).
                   Un projet est analysé avec tous les projets qu'il référence.
        rapport  : refait le rapport depuis un inventaire.json déjà produit, sans le code
                   source (relecture par l'auditeur, avis, périmètre corrigé).

        Options :
          -o, --sortie <dossier>    Dossier de sortie (défaut : ./winmig-rapport)
          --application <nom>      Nom affiché de l'application (défaut : nom de l'entrée)
          --client <nom>           Destinataire du rapport (défaut : aucun)
          --avis <fichier.md>      Avis de l'auditeur, inséré en tête du rapport
          --auteur <nom>           Nom de l'auditeur, imprimé sur le rapport
          --date <AAAA-MM-JJ>      Date du rapport (défaut : aujourd'hui)
          --sans-code              Aucun extrait de code, ni dans le rapport ni dans inventaire.json
          --sans-libelles          Retire aussi les textes des contrôles et les titres des fenêtres
          --inclure <C1,C2>        rapport : ajoute ces écrans (classes) au périmètre
          --exclure <C1,C2>        rapport : retire ces écrans du périmètre
          --json-seul              Écrit seulement inventaire.json, ecrans.csv et constats.csv
          -q, --silencieux         N'affiche que les erreurs
          -h, --aide               Affiche cette aide
          -v, --version            Affiche la version

        Fichiers produits : rapport.html, rapport.md, inventaire.json, ecrans.csv, constats.csv, schemas/*.svg.
        L'analyse lit le code sans le compiler ni l'exécuter, et n'envoie rien sur le réseau.
        """;

    static readonly System.Globalization.CultureInfo Fr = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static int Executer(string[] args, TextWriter sortie, TextWriter erreur)
    {
        if (args.Length == 0 || args[0] is "-h" or "--aide" or "--help" or "aide") { sortie.WriteLine(Aide); return args.Length == 0 ? 1 : 0; }
        if (args[0] is "-v" or "--version") { sortie.WriteLine($"winmig {Winmig.Core.Version.Courante}"); return 0; }
        if (args[0] is not ("analyser" or "rapport")) { erreur.WriteLine($"Commande inconnue : {args[0]}. Voir winmig --aide."); return 2; }

        string? entree = null, dossier = "winmig-rapport", application = null, client = null, avis = null, auteur = null;
        DateOnly date = DateOnly.FromDateTime(DateTime.Now);
        bool sansCode = false, sansLibelles = false, jsonSeul = false, silencieux = false, depuisJson = args[0] == "rapport";
        var inclure = new List<string>(); var exclure = new List<string>();
        static IEnumerable<string> Liste(string v) => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 1; i < args.Length; i++)
        {
            string Valeur() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Valeur manquante après {args[i]}");
            try
            {
                switch (args[i])
                {
                    case "-o" or "--sortie": dossier = Valeur(); break;
                    case "--application": application = Valeur(); break;
                    case "--client": client = Valeur(); break;
                    case "--avis": avis = Valeur(); break;
                    case "--auteur": auteur = Valeur(); break;
                    case "--date": date = DateOnly.ParseExact(Valeur(), "yyyy-MM-dd"); break;
                    case "--sans-code": sansCode = true; break;
                    case "--sans-libelles": sansLibelles = true; break;
                    case "--inclure" when depuisJson: inclure.AddRange(Liste(Valeur())); break;
                    case "--exclure" when depuisJson: exclure.AddRange(Liste(Valeur())); break;
                    case "--json-seul": jsonSeul = true; break;
                    case "-q" or "--silencieux": silencieux = true; break;
                    default:
                        if (args[i].StartsWith('-')) { erreur.WriteLine($"Option inconnue : {args[i]}"); return 2; }
                        if (entree != null) { erreur.WriteLine("Une seule entrée à la fois."); return 2; }
                        entree = args[i];
                        break;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException) { erreur.WriteLine(ex.Message); return 2; }
        }
        if (entree == null) { erreur.WriteLine("Entrée manquante. Voir winmig --aide."); return 2; }
        if (avis != null && !File.Exists(avis)) { erreur.WriteLine($"Fichier d'avis introuvable : {avis}"); return 2; }

        Inventaire inv;
        try
        {
            if (depuisJson)
            {
                if (!File.Exists(entree)) { erreur.WriteLine($"Inventaire introuvable : {entree}"); return 3; }
                inv = Inventaire.Charger(File.ReadAllText(entree), Json);
                var inconnus = inv.Ajuster(inclure, exclure);
                if (inconnus.Count > 0) { erreur.WriteLine($"Écran(s) inconnu(s) : {string.Join(", ", inconnus)}"); return 2; }
            }
            else inv = Analyseur.Analyser(entree, silencieux ? null : m => erreur.WriteLine($"  lecture : {m}"));
        }
        catch (Exception ex) when (ex is FileNotFoundException or ArgumentException or InvalidOperationException or JsonException
            or System.Xml.XmlException or UnauthorizedAccessException or IOException)
        {
            erreur.WriteLine(depuisJson ? $"Inventaire illisible : {ex.Message}" : ex.Message);
            return 3;
        }
        if (sansCode) inv.RetirerExtraits();
        if (sansLibelles) inv.RetirerLibelles();
        var chiffrage = Estimation.Chiffrer(inv);
        Directory.CreateDirectory(dossier);
        File.WriteAllText(Path.Combine(dossier, "inventaire.json"), JsonSerializer.Serialize(new { inventaire = inv, chiffrage = Resume(chiffrage) }, Json));
        File.WriteAllText(Path.Combine(dossier, "ecrans.csv"), Exports.Ecrans(inv));
        File.WriteAllText(Path.Combine(dossier, "constats.csv"), Exports.Constats(inv));
        if (!jsonSeul)
        {
            var options = new OptionsRapport
            {
                Application = application ?? Path.GetFileNameWithoutExtension(Path.TrimEndingDirectorySeparator(depuisJson ? inv.Entree : Path.GetFullPath(entree))),
                Client = client,
                Avis = avis != null ? File.ReadAllText(avis) : null,
                Auteur = auteur,
                Date = date,
                SansCode = sansCode,
            };
            Rapport.Ecrire(inv, chiffrage, options, dossier);
        }
        if (!silencieux)
        {
            sortie.WriteLine($"{inv.Projets.Count} projet(s), {inv.Ecrans.Count} écran(s), {inv.Dependances.Count} dépendance(s), {inv.Constats.Count} point(s) d'attention.");
            sortie.WriteLine($"Chiffrage indicatif : {chiffrage.UnitesTotal.ToString("0.#", Fr)} unités d'écran, {chiffrage.Lots.Count} lot(s), {chiffrage.JoursBas:0}–{chiffrage.JoursHaut:0} jours, {chiffrage.Prix.ToString("N0", Fr)} € HT.");
            if (inv.RapportsCrystal.Count > 0)
            {
                var (n, bas, haut) = Rapport.ForfaitCrystal(inv, chiffrage);
                sortie.WriteLine($"Rapports Crystal Reports : {inv.RapportsCrystal.Count}, dont {n} dans le périmètre, chiffrés à part : {bas.ToString("N0", Fr)} à {haut.ToString("N0", Fr)} € HT.");
            }
            if (inv.PointsNet10.Count > 0)
            {
                int N(PorteeNet10 p) => inv.PointsNet10.Count(x => x.Portee == p);
                sortie.WriteLine($"Passage à .NET 10 : {N(PorteeNet10.AFaire)} point(s) à faire, {N(PorteeNet10.AVerifier)} à vérifier, {N(PorteeNet10.HorsWindows)} pour le web ou Linux, {N(PorteeNet10.Information)} pour information.");
            }
            foreach (var a in inv.Avertissements) sortie.WriteLine($"Avertissement : {a}");
            sortie.WriteLine($"Rapport : {Path.GetFullPath(Path.Combine(dossier, jsonSeul ? "inventaire.json" : "rapport.html"))}");
        }
        return 0;
    }

    static object Resume(Chiffrage c) => new
    {
        parametres = c.Parametres,
        unites = new { ecrans = c.UnitesEcrans, socle = c.UnitesSocle, chantiers = c.UnitesChantiers, total = c.UnitesTotal },
        jours = new { bas = c.JoursBas, haut = c.JoursHaut },
        prixHT = c.Prix,
        repartition = c.Repartition,
        lots = c.Lots.Select(l => new { numero = l.Numero, theme = l.Theme, unites = l.Unites, unitesFacturees = l.UnitesFacturees, prixHT = l.Prix, jours = new { bas = l.JoursBas, haut = l.JoursHaut }, ecrans = l.Ecrans.Select(e => e.Id), chantiers = l.Chantiers.Select(x => x.Constat.Code) }),
    };
}
