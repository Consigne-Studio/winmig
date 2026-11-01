using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Winmig.Core;

/// <summary>Portée d'un point pour le passage à .NET 10.</summary>
public enum PorteeNet10
{
    /// <summary>À faire : sans correction, le projet ne compile pas ou ne tourne pas sur .NET 10.</summary>
    AFaire,
    /// <summary>À vérifier : fonctionne sur .NET 10 sous Windows, mais change de comportement ou sort du support.</summary>
    AVerifier,
    /// <summary>Fonctionne sur .NET 10 sous Windows ; à remplacer pour le web, Linux ou un conteneur.</summary>
    HorsWindows,
    /// <summary>Pour information, sans travail demandé.</summary>
    Information,
}

/// <summary>Ce qui bloque ou complique le passage d'un projet à .NET 10, et ce qu'en fait GitHub Copilot app modernization.</summary>
public sealed record RegleNet10(string Code, string Titre, PorteeNet10 Portee, string Copilot, bool CopilotLeFait, string Solution);

/// <summary>Un point relevé dans un projet.</summary>
public sealed class PointNet10
{
    public required string Code { get; init; }
    public required string Projet { get; init; }
    public required string Detail { get; init; }
    /// <summary>Portée retenue pour ce projet (celle de la règle, sauf cas particulier : cible .NET 10 déjà présente, client WCF seul).</summary>
    public PorteeNet10 Portee { get; init; }
    public List<string> Fichiers { get; init; } = new();
}

public static class Net10
{
    static readonly DateOnly FinNet8et9 = new(2026, 11, 10), FinNet462 = new(2027, 1, 12), FinNet35 = new(2029, 1, 9);

    /// <summary>Prix publiés du forfait « passage de .NET 8 ou 9 à .NET 10 », par nombre de projets.</summary>
    public static readonly (int Projets, decimal Prix)[] Forfait = { (3, 490m), (10, 990m), (25, 1490m) };

    public static decimal? PrixForfait(int projets) => Forfait.Where(f => projets <= f.Projets).Select(f => (decimal?)f.Prix).FirstOrDefault();

    public static readonly RegleNet10[] Regles =
    {
        new("N01", "Cible .NET hors support ou en fin de support", PorteeNet10.AFaire, "Le fait : changement de cible et mise à jour des paquets.", true,
            "Passer à net10.0 (LTS, supporté jusqu'en novembre 2028), mettre à jour les paquets, corriger les changements cassants, relancer tests et pipeline."),
        new("N02", "Exécution en 32 bits", PorteeNet10.AVerifier, "Ne le traite pas.", false,
            ".NET 10 tourne en x86 sous Windows, mais pas sous Linux ni sur ARM ; et Prefer32Bit n'a plus d'effet : le programme passe en 64 bits. Trouver la dépendance 32 bits (pilote, OCX, DLL native), puis la remplacer ou l'isoler."),
        new("N03", "Composants COM et ActiveX", PorteeNet10.HorsWindows, "Ne le traite pas.", false,
            "Fonctionnent sur .NET 10 sous Windows (une référence COM demande MSBuild.exe, pas dotnet build). Pour le web ou Linux : remplacer par une bibliothèque .NET, ou isoler dans un service Windows exposé par API."),
        new("N04", "Automatisation d'Office ou d'Outlook", PorteeNet10.HorsWindows, "Ne le traite pas.", false,
            "Fonctionne sur .NET 10 avec Office installé sur le poste. Côté serveur ou web : fichiers produits sans Office (Open XML SDK, ClosedXML), messagerie par Microsoft Graph."),
        new("N05", "Crystal Reports", PorteeNet10.AFaire, "Ne le traite pas : le moteur Crystal ne tourne que sur .NET Framework.", false,
            "Refaire chaque rapport (QuestPDF, rendu HTML vers PDF ou RDLC) et contrôler chaque rapport refait par comparaison des PDF avant et après."),
        new("N06", "Pilote SQL ancien (Native Client, SQLOLEDB, Jet, ACE)", PorteeNet10.AVerifier, "Ne le traite pas : sa tâche base de données vise Azure SQL.", false,
            "SQL Server Native Client n'est plus livré ni supporté ; SQLOLEDB est déprécié ; Jet n'existe qu'en 32 bits. Passer à Microsoft.Data.SqlClient ou au pilote OLE DB actuel (MSOLEDBSQL), tester sur SQL Server 2022."),
        new("N07", "Envoi de mails par System.Net.Mail", PorteeNet10.AVerifier, "Propose une tâche vers Azure Communication Services, un service payant d'Azure.", false,
            "SmtpClient fonctionne sur .NET 10 mais Microsoft le déconseille, et Microsoft 365 retire l'authentification SMTP de base. Passer à Microsoft Graph ou à SMTP avec OAuth 2.0 (MailKit), sans changer de messagerie."),
        new("N08", "WCF, .NET Remoting ou System.Web", PorteeNet10.AFaire, "Le fait en partie : WCF serveur vers CoreWCF, Web Forms vers Blazor.", true,
            "Serveur WCF, Remoting, Web Forms et System.Web n'existent pas sur .NET 10 : CoreWCF, gRPC ou API, Blazor ou Angular. Un client WCF passe avec les paquets System.ServiceModel.*. Vérifier par des tests de bout en bout."),
        new("N09", "API retirées (BinaryFormatter, domaines d'application)", PorteeNet10.AFaire, "Aucune tâche prévue.", false,
            "BinaryFormatter lève une exception depuis .NET 9, AppDomain.CreateDomain et les formateurs SOAP n'existent plus : changer de sérialisation (System.Text.Json, XML) en gardant la lecture des données déjà enregistrées."),
        new("N10", "System.Data.SqlClient déprécié", PorteeNet10.AVerifier, "Le fait : passage à Microsoft.Data.SqlClient.", true,
            "Passer à Microsoft.Data.SqlClient ; revoir le chiffrement de la connexion, activé par défaut (Encrypt=true)."),
        new("N11", "Projet .NET Framework", PorteeNet10.AFaire, "Le fait en grande partie : format SDK, paquets, API.", true,
            "Portage plutôt que changement de cible : format SDK, paquets, API absentes de .NET 10 ; à chiffrer par l'audit (application de bureau) ou sur devis."),
    };

    public static RegleNet10 Regle(string code) => Regles.First(r => r.Code == code);

    public static string NomPortee(PorteeNet10 p) => p switch
    {
        PorteeNet10.AFaire => "À faire pour .NET 10",
        PorteeNet10.AVerifier => "À vérifier",
        PorteeNet10.HorsWindows => "Pour le web ou Linux",
        _ => "Pour information",
    };

    const RegexOptions Opt = RegexOptions.Compiled | RegexOptions.CultureInvariant;
    static readonly Regex Sql = new(@"\b(SQLNCLI\d*(?:\.\d+)?|SQLOLEDB(?:\.\d+)?|Microsoft\.Jet\.OLEDB\.\d\.\d|Microsoft\.ACE\.OLEDB\.\d+\.\d)\b", Opt | RegexOptions.IgnoreCase);
    static readonly Regex SmtpCode = new(@"\bSystem\.Net\.Mail\b[\s\S]*?\bSmtpClient\b|\bSystem\.Net\.Mail\.SmtpClient\b|\bSystem\.Web\.Mail\b", Opt);
    static readonly Regex SmtpConfig = new(@"<mailSettings\b[\s\S]*?<smtp\b", Opt);
    static readonly Regex ComCs = new(@"\bAxHost\b|\bGetTypeFromProgID\b|\bComImport\b|\bMarshal\.GetActiveObject\b|\bInteraction\.CreateObject\s*\(", Opt);
    static readonly Regex ComVb = new(@"\bAxHost\b|\bGetTypeFromProgID\b|\bComImport\b|\bMarshal\.GetActiveObject\b|(?<![\w.])CreateObject\s*\(|\bInteraction\.CreateObject\s*\(|\bGetObject\s*\(", Opt | RegexOptions.IgnoreCase);
    static readonly Regex Office = new(@"\bMicrosoft\.Office\.Interop\.\w+", Opt | RegexOptions.IgnoreCase);
    static readonly Regex Crystal = new(@"\bCrystalDecisions\b", Opt | RegexOptions.IgnoreCase);
    static readonly Regex WcfServeur = new(@"\bServiceHost\b|\bServiceHostFactory\b|\bServiceBehavior\b", Opt);
    static readonly Regex WcfConfigServeur = new(@"<system\.serviceModel>[\s\S]*?<services>", Opt);
    static readonly Regex Remoting = new(@"\bSystem\.Runtime\.Remoting\b|\bMarshalByRefObject\b[\s\S]*\bRemotingConfiguration\b", Opt);
    static readonly Regex Web = new(@"\bSystem\.Web\.(UI|Services|HttpContext|HttpRuntime|SessionState)\b|\bHttpContext\.Current\b", Opt);
    static readonly Regex Retirees = new(@"\b(BinaryFormatter|SoapFormatter|NetDataContractSerializer|ObjectStateFormatter|LosFormatter)\b|\bAppDomain\.CreateDomain\b", Opt | RegexOptions.IgnoreCase);
    static readonly Regex SqlClientAncien = new(@"\bSystem\.Data\.SqlClient\b", Opt | RegexOptions.IgnoreCase);
    static readonly Regex OfficeRef = new(@"^(Microsoft\.Office|Office$|Interop\.(Excel|Word|Outlook|PowerPoint|Access)|Excel$|Word$|Outlook$|PowerPoint$)", Opt | RegexOptions.IgnoreCase);

    /// <summary>Fin de support d'une cible à une date donnée, ou null si elle est supportée.</summary>
    public static string? FinSupport(string cible, DateOnly le)
    {
        string Date(DateOnly fin) => le < fin ? $"fin de support le {fin:dd/MM/yyyy}" : $"hors support depuis le {fin:dd/MM/yyyy}";
        var c = cible.ToLowerInvariant();
        var m = Regex.Match(c, @"^net(\d+)\.(\d+)");
        if (m.Success)
        {
            var v = int.Parse(m.Groups[1].Value);
            return v switch { 8 or 9 => Date(FinNet8et9), < 8 => "hors support", _ => null };
        }
        if (c.StartsWith("netcoreapp", StringComparison.Ordinal)) return "hors support";
        m = Regex.Match(c, @"^net(\d{2,3})$");
        if (!m.Success) return null;
        var f = m.Groups[1].Value;
        return f switch
        {
            "20" or "30" or "35" => le < FinNet35 ? null : "hors support",
            "40" or "403" or "45" or "451" or "452" or "46" or "461" => "hors support",
            "462" => Date(FinNet462),
            _ => null,
        };
    }

    public static string? FinSupport(string cible) => FinSupport(cible, DateOnly.FromDateTime(DateTime.UtcNow));

    static bool EstFramework(string cible) => Regex.IsMatch(cible, @"^net\d{2,3}$", RegexOptions.IgnoreCase);
    static bool EstNet10OuPlus(string cible)
    {
        var m = Regex.Match(cible, @"^net(\d+)\.\d", RegexOptions.IgnoreCase);
        return m.Success && int.Parse(m.Groups[1].Value) >= 10;
    }

    /// <summary>Le texte du code sans ses commentaires (les chaînes restent : elles portent les chaînes de connexion).</summary>
    static string SansCommentaires(FichierSource f)
    {
        var sb = new System.Text.StringBuilder(f.Texte.Length);
        bool Commentaire(SyntaxTrivia t) => (t.Language == LanguageNames.VisualBasic
            ? ((Microsoft.CodeAnalysis.VisualBasic.SyntaxKind)t.RawKind).ToString()
            : ((Microsoft.CodeAnalysis.CSharp.SyntaxKind)t.RawKind).ToString()).Contains("Comment", StringComparison.Ordinal);
        foreach (var t in f.Arbre.GetRoot().DescendantTokens(descendIntoTrivia: false))
        {
            foreach (var tr in t.LeadingTrivia) sb.Append(Commentaire(tr) ? " " : tr.ToString());
            sb.Append(t.Text);
            foreach (var tr in t.TrailingTrivia) sb.Append(Commentaire(tr) ? " " : tr.ToString());
        }
        return sb.ToString();
    }

    /// <summary>Relève les points de chaque projet, à partir du projet lu, de son code et de ses fichiers .config.</summary>
    public static void Relever(Inventaire inv, IReadOnlyList<FichierSource> fichiers, Func<string, string> rel, IReadOnlyDictionary<string, string> cheminsProjets, IReadOnlyList<string> dossiersProjets)
    {
        var le = DateOnly.FromDateTime(inv.Date.UtcDateTime);
        var nettoyes = fichiers.ToDictionary(f => f, SansCommentaires);
        foreach (var p in inv.Projets)
        {
            void Ajouter(string code, string detail, IEnumerable<string>? f = null, PorteeNet10? portee = null) =>
                inv.PointsNet10.Add(new PointNet10
                {
                    Code = code, Projet = p.Nom, Detail = detail, Portee = portee ?? Regle(code).Portee,
                    Fichiers = (f ?? Enumerable.Empty<string>()).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList(),
                });
            var actuelle = p.Cibles.FirstOrDefault(EstNet10OuPlus);
            var modernes = p.Cibles.Where(c => !EstFramework(c) && !c.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase)).ToList();
            var finies = modernes.Select(c => (Cible: c, Fin: FinSupport(c, le))).Where(x => x.Fin != null).ToList();
            if (finies.Count > 0)
                Ajouter("N01", string.Join(" ; ", finies.Select(x => $"{x.Cible} : {x.Fin}")) + (actuelle != null ? $" ({actuelle} déjà ciblé : ancienne cible à retirer quand plus rien ne l'utilise)" : ""),
                    portee: actuelle != null ? PorteeNet10.Information : null);
            var framework = p.Cibles.Where(EstFramework).ToList();
            if (framework.Count > 0 && actuelle == null)
                Ajouter("N11", string.Join(" ; ", framework.Select(c => FinSupport(c, le) is { } fin ? $"{c} : {fin}" : c)));
            if (p.Plateforme32 != null)
                Ajouter("N02", p.Plateforme32 == "Prefer32Bit" ? "Prefer32Bit (réglage par défaut des anciens exécutables : le programme tourne aujourd'hui en 32 bits)" : p.Plateforme32);

            var code = fichiers.Where(f => f.Projet == p.Nom).ToList();
            var dossier = cheminsProjets.TryGetValue(p.Nom, out var cp) ? Path.GetDirectoryName(cp)! : null;
            var configs = dossier == null ? new List<(string Chemin, string Texte)>() : Projets.FichiersSousProjet(dossier, dossiersProjets, "*.config")
                .Where(f => !Path.GetFileName(f).Equals("packages.config", StringComparison.OrdinalIgnoreCase))
                .Select(f => { try { return (Chemin: f, Texte: File.ReadAllText(f)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return (Chemin: f, Texte: ""); } }).ToList();
            List<string> DansCode(Func<FichierSource, Regex> re) => code.Where(f => re(f).IsMatch(nettoyes[f])).Select(f => rel(f.Chemin)).ToList();
            List<string> DansConfig(Regex re) => configs.Where(c => re.IsMatch(c.Texte)).Select(c => rel(c.Chemin)).ToList();
            var refs = p.ReferencesComposants.Concat(p.Paquets.Select(x => x.Nom)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var comRefs = p.ReferencesCom.Where(r => !OfficeRef.IsMatch(r))
                .Concat(p.ReferencesComposants.Where(r => r.StartsWith("AxInterop.", StringComparison.OrdinalIgnoreCase) || (r.StartsWith("Interop.", StringComparison.OrdinalIgnoreCase) && !OfficeRef.IsMatch(r))))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var com = DansCode(f => f.Vb ? ComVb : ComCs);
            if (comRefs.Count > 0 || com.Count > 0)
                Ajouter("N03", string.Join(" ; ", new[] { comRefs.Count > 0 ? $"références : {string.Join(", ", comRefs.Take(4))}{(comRefs.Count > 4 ? "…" : "")}" : null, com.Count > 0 ? $"{com.Count} fichier{(com.Count > 1 ? "s" : "")} de code" : null }.Where(x => x != null)), com);
            var office = DansCode(_ => Office);
            var refsOffice = refs.Concat(p.ReferencesCom).Where(n => n.StartsWith("Microsoft.Office.Interop", StringComparison.OrdinalIgnoreCase) || OfficeRef.IsMatch(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (office.Count > 0 || refsOffice.Count > 0)
                Ajouter("N04", refsOffice.Count > 0 ? string.Join(", ", refsOffice.Take(4)) : "Microsoft.Office.Interop dans le code", office);
            var crystal = DansCode(_ => Crystal);
            var rpt = inv.RapportsCrystal.Count(r => r.Projet == p.Nom);
            if (crystal.Count > 0 || rpt > 0 || refs.Any(r => r.StartsWith("CrystalDecisions", StringComparison.OrdinalIgnoreCase)))
                Ajouter("N05", rpt > 0 ? $"{rpt} rapport{(rpt > 1 ? "s" : "")} .rpt" : "références CrystalDecisions", crystal);
            var sql = code.Select(f => (Chemin: rel(f.Chemin), Texte: nettoyes[f])).Concat(configs.Select(c => (Chemin: rel(c.Chemin), c.Texte)))
                .SelectMany(x => Sql.Matches(x.Texte).Select(m => (Fichier: x.Chemin, Pilote: m.Value))).ToList();
            if (sql.Count != 0)
                Ajouter("N06", string.Join(", ", sql.Select(x => x.Pilote).Distinct(StringComparer.OrdinalIgnoreCase)), sql.Select(x => x.Fichier));
            var smtp = DansCode(_ => SmtpCode).Concat(DansConfig(SmtpConfig)).ToList();
            if (smtp.Count > 0) Ajouter("N07", $"{smtp.Count} fichier{(smtp.Count > 1 ? "s" : "")}", smtp);

            var serveurWcf = DansCode(_ => WcfServeur).Concat(DansConfig(WcfConfigServeur)).ToList();
            var clientWcf = code.Where(f => f.Usings.Any(u => u.StartsWith("System.ServiceModel", StringComparison.Ordinal))).Select(f => rel(f.Chemin)).Except(serveurWcf).ToList();
            var remoting = DansCode(_ => Remoting);
            var web = code.Where(f => f.Usings.Any(u => u is "System.Web" || u.StartsWith("System.Web.UI", StringComparison.Ordinal) || u.StartsWith("System.Web.Services", StringComparison.Ordinal)) || Web.IsMatch(nettoyes[f])).Select(f => rel(f.Chemin)).ToList();
            var parties = new List<string>();
            if (serveurWcf.Count > 0) parties.Add("serveur WCF");
            if (remoting.Count > 0) parties.Add("Remoting");
            if (web.Count > 0) parties.Add("System.Web");
            if (parties.Count > 0)
                Ajouter("N08", string.Join(", ", parties) + (clientWcf.Count > 0 ? ", client WCF" : ""), serveurWcf.Concat(remoting).Concat(web).Concat(clientWcf));
            else if (clientWcf.Count > 0)
                Ajouter("N08", "client WCF seulement (passe avec les paquets System.ServiceModel.*)", clientWcf, PorteeNet10.AVerifier);
            var retirees = DansCode(f => Retirees);
            if (retirees.Count > 0)
            {
                var noms = code.Where(f => retirees.Contains(rel(f.Chemin))).SelectMany(f => Retirees.Matches(nettoyes[f]).Select(m => m.Value)).Distinct(StringComparer.OrdinalIgnoreCase);
                Ajouter("N09", string.Join(", ", noms), retirees);
            }
            var sqlClient = DansCode(_ => SqlClientAncien);
            if (sqlClient.Count > 0 || refs.Any(r => r.Equals("System.Data.SqlClient", StringComparison.OrdinalIgnoreCase)))
                Ajouter("N10", sqlClient.Count > 0 ? $"{sqlClient.Count} fichier{(sqlClient.Count > 1 ? "s" : "")}" : "paquet System.Data.SqlClient", sqlClient);
        }
        inv.PointsNet10.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code) != 0 ? string.CompareOrdinal(a.Code, b.Code) : string.CompareOrdinal(a.Projet, b.Projet));
    }
}
