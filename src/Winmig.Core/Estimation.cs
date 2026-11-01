using System.Text.RegularExpressions;

namespace Winmig.Core;

/// <summary>Paramètres publics du chiffrage. Ils sont imprimés dans le rapport pour que le calcul soit vérifiable.</summary>
public sealed record Parametres
{
    public double JoursUniteBas { get; init; } = 2.4;
    public double JoursUniteHaut { get; init; } = 3.2;
    public double UnitesParLot { get; init; } = 10;
    public decimal PrixUnite { get; init; } = 2500m;
    public double UnitesMinLot { get; init; } = 5;
    public decimal PrixAudit { get; init; } = 4900m;
    public double UnitesSocle { get; init; } = 4;
    public decimal PrixLot => PrixUnite * (decimal)UnitesParLot;
    /// <summary>Rapports Crystal : hors unités, au forfait par rapport après un pilote de 3 rapports.</summary>
    public decimal PrixPiloteCrystal { get; init; } = 1490m;
    public decimal PrixRapportSimple { get; init; } = 250m;
    public decimal PrixRapportMoyen { get; init; } = 550m;
    public decimal PrixRapportComplexe { get; init; } = 1100m;
}

public sealed class Lot
{
    public int Numero { get; init; }
    public List<Ecran> Ecrans { get; } = new();
    public List<(Constat Constat, double Unites)> Chantiers { get; } = new();
    public double UnitesSocle { get; set; }
    public double Unites => UnitesSocle + Ecrans.Sum(e => e.Unites) + Chantiers.Sum(c => c.Unites);
    public double UnitesFacturees { get; set; }
    public decimal Prix { get; set; }
    public double JoursBas { get; set; }
    public double JoursHaut { get; set; }
    public string Theme { get; set; } = "";
}

public sealed class Chiffrage
{
    public required Parametres Parametres { get; init; }
    public double UnitesEcrans { get; init; }
    public double UnitesChantiers { get; init; }
    public double UnitesSocle { get; init; }
    public double UnitesTotal => UnitesEcrans + UnitesChantiers + UnitesSocle;
    public double JoursBas { get; init; }
    public double JoursHaut { get; init; }
    public List<Lot> Lots { get; init; } = new();
    public decimal Prix => Lots.Sum(l => l.Prix);
    public Dictionary<Complexite, int> Repartition { get; init; } = new();
    public List<(Constat Constat, double JoursBas, double JoursHaut)> Chantiers { get; init; } = new();
    /// <summary>Écrans présents dans le code mais non atteints depuis l'application : chiffrés à part, en option.</summary>
    public List<Ecran> HorsPerimetre { get; init; } = new();
    public double UnitesHorsPerimetre => HorsPerimetre.Sum(e => e.Unites);
}

public static class Estimation
{
    static readonly string[] Architecture = { "W01", "W02", "W03", "W13", "W17", "W18", "W07" };

    public static int NombreTiers(Ecran e) => e.ControlesTiers.Sum(t => Regex.Match(t, @"×(\d+)$") is { Success: true } m ? int.Parse(m.Groups[1].Value) : 1);

    /// <summary>Score de travail d'un écran, puis classe S / M / L / XL et unités d'écran.</summary>
    public static void NoterEcrans(Inventaire inv)
    {
        foreach (var e in inv.Ecrans)
        {
            e.Score = Math.Round(e.Controles * 1.0 + e.Evenements * 2.0 + e.Liaisons * 1.0 + e.LignesCodeBehind / 15.0 + e.Branches * 0.5 + NombreTiers(e) * 4.0, 1);
            e.Complexite = e.Score switch { < 30 => Complexite.S, < 80 => Complexite.M, < 180 => Complexite.L, _ => Complexite.XL };
            e.Unites = e.Complexite switch { Complexite.S => 0.5, Complexite.M => 1, Complexite.L => 2, _ => 3 };
            foreach (var code in e.Signaux)
                e.Unites += Catalogue.Regle(code).UnitesParEcran;
        }
    }

    public static (double Bas, double Haut) JoursChantier(Constat c, Inventaire inv)
    {
        var fichiers = c.Occurrences.Where(inv.OccurrenceAtteinte).Select(o => o.Fichier).Distinct().Count();
        if (fichiers == 0) return (0, 0);
        var facteur = Math.Min(3.0, 1 + 0.15 * Math.Max(fichiers - 1, 0));
        return (Math.Round(c.JoursBas * facteur * 2, MidpointRounding.AwayFromZero) / 2, Math.Round(c.JoursHaut * facteur * 2, MidpointRounding.AwayFromZero) / 2);
    }

    public static Chiffrage Chiffrer(Inventaire inv, Parametres? p = null)
    {
        p ??= new Parametres();
        var moyenUnite = (p.JoursUniteBas + p.JoursUniteHaut) / 2;
        var chantiers = inv.Constats.Where(c => c.JoursHaut > 0).Select(c => { var (b, h) = JoursChantier(c, inv); return (c, b, h); }).Where(x => x.h > 0).ToList();
        double UnitesDe(double b, double h) => Math.Ceiling((b + h) / 2 / moyenUnite * 2) / 2;

        // Ordre de réalisation : la fenêtre principale et ses composants d'abord, puis par projet et dossier,
        // les composants avant les écrans qui les utilisent.
        var ecrans = inv.Ecrans.Where(e => e.Atteint).ToList();
        var parId = ecrans.ToDictionary(e => e.Classe, StringComparer.Ordinal);
        var principale = ecrans.Where(e => e.Nature == NatureEcran.Fenetre)
            .OrderByDescending(e => inv.Projets.FirstOrDefault(pr => pr.Nom == e.Projet)?.TypeSortie is "WinExe" or "Exe" ? 1 : 0)
            .ThenByDescending(e => Regex.IsMatch(Sources.NomSimple(e.Classe), "^(Main|Principal|Shell)", RegexOptions.IgnoreCase) ? 1 : 0)
            .ThenByDescending(e => e.ComposantsInternes.Count).FirstOrDefault();
        var ordre = new List<Ecran>();
        var places = new HashSet<Ecran>();
        void Placer(Ecran e, int profondeur)
        {
            if (places.Contains(e) || profondeur > 20) return;
            places.Add(e);
            foreach (var c in e.ComposantsInternes) if (parId.TryGetValue(c, out var ce)) Placer(ce, profondeur + 1);
            ordre.Add(e);
        }
        if (principale != null) Placer(principale, 0);
        var pilote = ordre.Count;
        string Dossier(Ecran e) => e.Fichiers.Count > 0 ? Path.GetDirectoryName(e.Fichiers[0])?.Replace('\\', '/') ?? "" : "";
        foreach (var e in ecrans.OrderBy(e => e.Projet, StringComparer.Ordinal).ThenBy(Dossier, StringComparer.Ordinal)
                     .ThenBy(e => e.Nature == NatureEcran.Composant ? 0 : 1).ThenByDescending(e => e.UtilisePar.Count).ThenBy(e => e.Classe, StringComparer.Ordinal))
            Placer(e, 0);

        var lots = new List<Lot>();
        var lot = new Lot { Numero = 1, UnitesSocle = p.UnitesSocle };
        foreach (var (c, b, h) in chantiers) lot.Chantiers.Add((c, UnitesDe(b, h)));
        lots.Add(lot);
        for (var i = 0; i < ordre.Count; i++)
        {
            var e = ordre[i];
            if (i >= pilote && lot.Unites + e.Unites > p.UnitesParLot + 1e-9 && lot.Unites > 0)
            {
                lot = new Lot { Numero = lots.Count + 1 };
                lots.Add(lot);
            }
            lot.Ecrans.Add(e);
        }
        // Un dernier lot trop petit rejoint le précédent.
        if (lots.Count > 1 && lots[^1].Unites < p.UnitesMinLot)
        {
            lots[^2].Ecrans.AddRange(lots[^1].Ecrans);
            lots.RemoveAt(lots.Count - 1);
        }
        for (var i = 0; i < lots.Count; i++)
        {
            var l = lots[i];
            l.UnitesFacturees = Math.Max(Math.Ceiling(l.Unites * 2) / 2, p.UnitesMinLot);
            l.Prix = p.PrixUnite * (decimal)l.UnitesFacturees;
            // Une seule règle publique : unités facturées × 2,4 à 3,2 jours (socle et chantiers sont déjà convertis en unités).
            l.JoursBas = Math.Round(l.UnitesFacturees * p.JoursUniteBas, 1);
            l.JoursHaut = Math.Round(l.UnitesFacturees * p.JoursUniteHaut, 1);
            l.Theme = Theme(l, principale);
        }

        return new Chiffrage
        {
            Parametres = p,
            UnitesEcrans = ecrans.Sum(e => e.Unites),
            UnitesChantiers = lots[0].Chantiers.Sum(c => c.Unites),
            UnitesSocle = p.UnitesSocle,
            JoursBas = Math.Round(lots.Sum(l => l.JoursBas), 1),
            JoursHaut = Math.Round(lots.Sum(l => l.JoursHaut), 1),
            Lots = lots,
            Repartition = Enum.GetValues<Complexite>().ToDictionary(c => c, c => ecrans.Count(e => e.Complexite == c)),
            Chantiers = chantiers,
            HorsPerimetre = inv.Ecrans.Where(e => !e.Atteint).ToList(),
        };
    }

    static string Theme(Lot l, Ecran? principale)
    {
        var parties = new List<string>();
        if (l.UnitesSocle > 0) parties.Add("Socle web, temps réel et chantiers transverses");
        if (principale != null && l.Ecrans.Contains(principale)) parties.Add($"fenêtre principale {Sources.NomSimple(principale.Classe)}");
        var groupes = l.Ecrans.Where(e => e != principale).GroupBy(e => Regex.Replace(Sources.NomSimple(e.Classe), "(Ctrl|Control|Dlg|Dialog|Form|Window|View|Page)$", ""))
            .Select(g => g.Key).ToList();
        if (parties.Count == 0 || l.Ecrans.Count > 1)
        {
            var dossiers = l.Ecrans.Select(e => e.Projet).GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => g.Key).Take(2).ToList();
            parties.Add($"{l.Ecrans.Count} écran{(l.Ecrans.Count > 1 ? "s" : "")} de {string.Join(" et ", dossiers)}");
        }
        return string.Join(" ; ", parties);
    }
}
