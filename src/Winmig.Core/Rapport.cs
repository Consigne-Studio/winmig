using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Winmig.Core;

public sealed record OptionsRapport
{
    public required string Application { get; init; }
    public string? Client { get; init; }
    public string? Avis { get; init; }
    public string? Auteur { get; init; }
    public DateOnly Date { get; init; }
    public bool SansCode { get; init; }
}

/// <summary>
/// Rapport d'audit au format A4 (HTML autonome, imprimable en PDF), à la charte « Consigne » :
/// planches grises, cadre à zones, cartouche, une seule couleur de signal (vert marche, gamme outillage).
/// </summary>
public static class Rapport
{
    public const string Repere = "DEV-605";
    const string Signal = "#2BB673";
    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static string NomFichier(Ecran e) => Regex.Replace(e.Classe, @"[^\w\.-]", "_");
    static string E(string? s) => Html(s);
    public static string Html(string? s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
    static string N(double v) => v.ToString(v % 1 == 0 ? "0" : "0.#", Fr);
    static string Eur(decimal v) => v.ToString("#,0", Fr).Replace(' ', ' ') + " €";
    static string Court(Ecran e) => Sources.NomSimple(e.Classe);
    static string Nature(NatureEcran n) => n switch { NatureEcran.Fenetre => "Fenêtre", NatureEcran.Dialogue => "Dialogue", NatureEcran.Page => "Page", _ => "Composant" };
    static string Grav(Gravite g) => g switch { Gravite.Elevee => "Élevée", Gravite.Moyenne => "Moyenne", _ => "Faible" };
    /// <summary>« le point d'entrée (A, B) » ou « les 40 points d'entrée (A, B, C et 37 autres) ».</summary>
    static string Entrees(Inventaire inv)
    {
        // Les écrans ouverts au démarrage sont atteints, pas des points d'entrée : on cite les classes qui portent Main ou App.
        var ecrans = inv.Ecrans.Select(e => e.Classe).ToHashSet(StringComparer.Ordinal);
        var points = inv.PointsEntree.Where(p => !ecrans.Contains(p)).ToList();
        var noms = (points.Count > 0 ? points : inv.PointsEntree).Select(Sources.NomSimple).Distinct().ToList();
        var liste = noms.Count <= 4 ? string.Join(", ", noms) : $"{string.Join(", ", noms.Take(3))} et {noms.Count - 3} autres";
        var projets = inv.Projets.Count(p => p.TypeSortie is "Exe" or "WinExe");
        return projets > 1 ? $"les points d'entrée des {projets} exécutables ({liste})" : $"le point d'entrée ({liste})";
    }

    /// <summary>« à » contracté devant l'article : au point d'entrée, aux points d'entrée.</summary>
    static string Au(string s) => s.StartsWith("le ", StringComparison.Ordinal) ? "au " + s[3..] : s.StartsWith("les ", StringComparison.Ordinal) ? "aux " + s[4..] : "à " + s;

    /// <summary>Première lettre en minuscule, sauf si le premier mot est un sigle ou un nom propre composé (DataSet, WinForms, WCF).</summary>
    static string Min(string s)
    {
        var mot = s.Split(' ')[0];
        return mot.Length > 1 && char.IsUpper(mot[0]) && mot.Skip(1).All(ch => !char.IsUpper(ch)) ? char.ToLowerInvariant(s[0]) + s[1..] : s;
    }

    /// <summary>Découpe une liste en pages équilibrées (pas de dernière page à une ligne) : capacité de la première page, puis des suivantes.</summary>
    static List<List<T>> Repartir<T>(List<T> l, int premiere, int suivantes)
    {
        var n = l.Count <= premiere ? 1 : 1 + (int)Math.Ceiling((l.Count - premiere) / (double)suivantes);
        var res = new List<List<T>>();
        var i = 0;
        for (var p = 0; p < n; p++)
        {
            var reste = n - p;
            var taille = Math.Min(p == 0 ? premiere : suivantes, (int)Math.Ceiling((l.Count - i) / (double)reste));
            res.Add(l.Skip(i).Take(taille).ToList());
            i += taille;
        }
        if (i < l.Count) res[^1].AddRange(l.Skip(i));
        return res;
    }

    static string Pluriel(int n, string un, string plusieurs) => $"{n.ToString(Fr)} {(n > 1 ? plusieurs : un)}";

    public static void Ecrire(Inventaire inv, Chiffrage c, OptionsRapport o, string dossier)
    {
        var schemas = Path.Combine(dossier, "schemas");
        Directory.CreateDirectory(schemas);
        var internes = InternesSimples(inv);
        foreach (var e in inv.Ecrans.Where(e => e.Schema != null))
            File.WriteAllText(Path.Combine(schemas, NomFichier(e) + ".svg"), Dessin(e, inv));
        File.WriteAllText(Path.Combine(dossier, "rapport.html"), Html(inv, c, o));
        File.WriteAllText(Path.Combine(dossier, "rapport.md"), Markdown(inv, c, o));
    }

    /// <summary>Schéma SVG d'un écran, avec ses composants internes dessinés à leur place.</summary>
    public static string Dessin(Ecran e, Inventaire inv, double echelle = 0)
    {
        var parNom = inv.Ecrans.Where(x => x.Schema != null).GroupBy(x => Court(x)).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        Schema.Resolveur r = n => parNom.TryGetValue(n, out var x) ? (x.Schema!, x.SchemaNoeuds) : null;
        return Schema.Svg(e.Schema!, e.Titre, e.SchemaNoeuds, InternesSimples(inv), r, e.Nature is NatureEcran.Fenetre or NatureEcran.Dialogue, echelle);
    }

    static HashSet<string> InternesSimples(Inventaire inv) => inv.Ecrans.Select(e => Court(e)).ToHashSet(StringComparer.Ordinal);

    static string Ressource(string nom)
    {
        using var s = typeof(Rapport).Assembly.GetManifestResourceStream("Winmig.Core.polices." + nom) ?? throw new InvalidOperationException($"Ressource manquante : {nom}");
        using var m = new MemoryStream();
        s.CopyTo(m);
        return Convert.ToBase64String(m.ToArray());
    }

    // ---------------------------------------------------------------- HTML

    sealed class Pages
    {
        public List<(string Section, string Corps, bool Couverture)> Liste { get; } = new();
        public void Ajouter(string section, string corps) => Liste.Add((section, corps, false));
    }

    public static string Html(Inventaire inv, Chiffrage c, OptionsRapport o)
    {
        var internes = InternesSimples(inv);
        var atteints = inv.Ecrans.Where(e => e.Atteint).ToList();
        var lotDe = c.Lots.SelectMany(l => l.Ecrans.Select(e => (e, l.Numero))).ToDictionary(x => x.e, x => x.Numero);
        var pages = new Pages();
        var principale = c.Lots.FirstOrDefault()?.Ecrans.FirstOrDefault(e => e.Nature == NatureEcran.Fenetre && e.Schema != null)
            ?? atteints.Where(e => e.Schema != null).OrderByDescending(e => e.Score).FirstOrDefault();

        // 1. Couverture.
        pages.Liste.Add(("Couverture", Couverture(inv, c, o, principale, internes), true));

        // 2. Synthèse.
        pages.Ajouter("Synthèse", Synthese(inv, c, o));

        // 3. Architecture cible et options.
        pages.Ajouter("Architecture cible", Architecture(inv, c));

        // 4. Plan de lots.
        foreach (var (titre, html) in PlanLots(inv, c)) pages.Ajouter(titre, html);

        // 5. Inventaire des écrans (tableau, découpé).
        var ordre = c.Lots.SelectMany(l => l.Ecrans).ToList();
        const int parPage = 34;
        for (var i = 0; i < Math.Max(ordre.Count, 1); i += parPage)
            pages.Ajouter(i == 0 ? "Inventaire des écrans" : "Inventaire des écrans (suite)", TableEcrans(ordre.Skip(i).Take(parPage).ToList(), lotDe, i == 0, ordre.Count));

        // 6. Planches des écrans (schémas reconstitués).
        const int vignettes = 12;
        var np = 0;
        foreach (var part in Repartir(ordre, vignettes, vignettes))
            pages.Ajouter(np++ == 0 ? "Planche des écrans" : "Planche des écrans (suite)", Planche(part, lotDe, inv, np == 1));

        // 7. Fiches des écrans les plus lourds.
        var fiches = ordre.Where(e => e.Schema is { Enfants.Count: > 0 }).OrderByDescending(e => e.Score).Take(4).ToList();
        if (principale != null) { fiches.Remove(principale); fiches.Insert(0, principale); if (fiches.Count > 4) fiches.RemoveAt(fiches.Count - 1); }
        for (var i = 0; i < fiches.Count; i += 2)
            pages.Ajouter(i == 0 ? "Fiches écran" : "Fiches écran (suite)", string.Join("", fiches.Skip(i).Take(2).Select(e => Fiche(e, lotDe, internes, inv))));

        // 8. Dépendances.
        const int depParPage = 20;
        for (var i = 0; i < Math.Max(inv.Dependances.Count, 1); i += depParPage)
            pages.Ajouter(i == 0 ? "Dépendances" : "Dépendances (suite)", Dependances(inv.Dependances.Skip(i).Take(depParPage).ToList(), i == 0, inv));

        // 8 bis. Rapports Crystal Reports, chiffrés à part.
        if (inv.RapportsCrystal.Count > 0)
        {
            var kc = 0;
            foreach (var part in Repartir(inv.RapportsCrystal, 22, 30))
                pages.Ajouter(kc++ == 0 ? "Rapports Crystal Reports" : "Rapports Crystal Reports (suite)", Crystal(part, kc == 1, inv, c));
        }

        // 8 ter. Passage à .NET 10 : ce que Copilot ne fait pas.
        if (inv.PointsNet10.Count > 0)
        {
            var kn = 0;
            var parts = Repartir(GroupesNet10(inv), 5, 7).ToList();
            foreach (var part in parts)
                pages.Ajouter(kn++ == 0 ? "Passage à .NET 10" : "Passage à .NET 10 (suite)", PointsNet10(part, kn == 1, kn == parts.Count, inv));
        }

        // 9. Points d'attention (2 à 3 par page selon la longueur).
        var blocs = inv.Constats.Select(k => Constat(k, inv, o.SansCode)).ToList();
        var courant = new StringBuilder();
        var hauteur = 0;
        var premier = true;
        foreach (var (html, h) in blocs)
        {
            if (hauteur + h > 900 && courant.Length > 0)
            {
                pages.Ajouter(premier ? "Points d'attention" : "Points d'attention (suite)", (premier ? IntroConstats(inv) : "") + courant);
                courant.Clear(); hauteur = 0; premier = false;
            }
            if (premier && hauteur == 0) hauteur = 90;
            courant.Append(html); hauteur += h;
        }
        if (courant.Length > 0 || premier) pages.Ajouter(premier ? "Points d'attention" : "Points d'attention (suite)", (premier ? IntroConstats(inv) : "") + (inv.Constats.Count == 0 ? "<p class=\"t\">Aucun usage d'API propre au poste Windows n'a été relevé.</p>" : courant.ToString()));

        // 10. Hors périmètre.
        if (c.HorsPerimetre.Count > 0)
        {
            var hp = c.HorsPerimetre.OrderBy(e => e.Projet).ThenBy(e => e.Classe).ToList();
            var k = 0;
            foreach (var part in Repartir(hp, 31, 36))
                pages.Ajouter(k++ == 0 ? "Hors périmètre" : "Hors périmètre (suite)", HorsPerimetre(part, k == 1, c, inv));
        }

        // 11. Méthode, puis annexe projets.
        pages.Ajouter("Méthode et limites", Methode(c));
        const int projetsParPage = 22;
        for (var i = 0; i < Math.Max(inv.Projets.Count, 1); i += projetsParPage)
            pages.Ajouter(i == 0 ? "Annexe : projets analysés" : "Annexe : projets analysés (suite)", Projets(inv, inv.Projets.Skip(i).Take(projetsParPage), i + projetsParPage >= inv.Projets.Count));

        var total = pages.Liste.Count;
        var sb = new StringBuilder();
        sb.Append($"<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append($"<title>Audit de migration — {E(o.Application)}</title><meta name=\"generator\" content=\"winmig {Version.Courante}\">");
        sb.Append("<style>");
        sb.Append($"@font-face{{font-family:Archivo;src:url(data:font/woff2;base64,{Ressource("Archivo-var.woff2")}) format('woff2');font-weight:100 900;font-stretch:62% 125%}}");
        sb.Append($"@font-face{{font-family:'Martian Mono';src:url(data:font/woff2;base64,{Ressource("MartianMono-var.woff2")}) format('woff2');font-weight:100 800;font-stretch:75% 112.5%}}");
        sb.Append($"@font-face{{font-family:Atkinson;src:url(data:font/woff2;base64,{Ressource("AtkinsonHyperlegibleNext-var.woff2")}) format('woff2');font-weight:200 800}}");
        sb.Append(Css);
        sb.Append("</style></head><body>");
        for (var i = 0; i < total; i++)
        {
            var (section, corps, couv) = pages.Liste[i];
            var folio = $"{i + 1:00}/{total:00}";
            sb.Append(couv ? $"<section class=\"page couv\">{Cadre(true)}{corps.Replace("{{FOLIO}}", folio)}</section>"
                : $"<section class=\"page\">{Cadre(false)}<header><span class=\"data\">{E(Repere)} · Audit de migration</span><h2 class=\"titre\">{E(section)}</h2></header><div class=\"corps\">{corps}</div>{PiedDePage(o, folio)}</section>");
        }
        sb.Append("</body></html>");
        return sb.ToString();
    }

    const string Css = """
        *{box-sizing:border-box;margin:0;padding:0}
        @page{size:A4;margin:0}
        html{background:#D4D7DA}
        body{font-family:Atkinson,system-ui,sans-serif;color:#1B1E21;-webkit-font-smoothing:antialiased;font-size:11px;line-height:1.45;
          background:#D4D7DA;background-image:linear-gradient(rgba(27,30,33,.075) 1px,transparent 1px),linear-gradient(90deg,rgba(27,30,33,.075) 1px,transparent 1px);background-size:16px 16px;padding:24px 0}
        .titre{font-family:Archivo,'Arial Narrow',sans-serif;font-weight:800;font-stretch:62%;text-transform:uppercase;line-height:.88;letter-spacing:-.005em}
        .data{font-family:'Martian Mono',ui-monospace,monospace;font-stretch:87.5%;font-weight:500;text-transform:uppercase;letter-spacing:.06em;font-variant-numeric:tabular-nums}
        .mono{font-family:'Martian Mono',ui-monospace,monospace;font-stretch:87.5%;font-size:9px;letter-spacing:.01em}
        .page{position:relative;width:210mm;height:297mm;margin:0 auto 24px;background:#F3F4F5;overflow:hidden;page-break-after:always;break-after:page}
        .page.couv{background:#D4D7DA;background-image:linear-gradient(rgba(27,30,33,.075) 1px,transparent 1px),linear-gradient(90deg,rgba(27,30,33,.075) 1px,transparent 1px);background-size:16px 16px}
        .cadre{position:absolute;inset:0;pointer-events:none}
        header{position:absolute;left:52px;right:52px;top:48px;display:flex;flex-direction:column;gap:8px;border-bottom:1.5px solid #1B1E21;padding-bottom:10px}
        header .data{font-size:8.5px;color:#5C636A}
        header h2{font-size:34px}
        .corps{position:absolute;left:52px;right:52px;top:124px;bottom:150px;overflow:hidden}
        .pied{position:absolute;left:52px;right:52px;bottom:44px;display:flex;justify-content:space-between;align-items:flex-end;gap:16px}
        .pied .gen{font-size:8px;color:#5C636A;max-width:300px;line-height:1.5}
        .cart{width:330px;background:#F3F4F5;border:1.5px solid #1B1E21}
        .cart .r{display:grid;border-top:1px solid #1B1E21}.cart .r:first-child{border-top:0}
        .cart .c{padding:4px 7px 5px;border-left:1px solid #1B1E21;min-width:0}.cart .c:first-child{border-left:0}
        .cart .k{font-size:7px;opacity:.62;letter-spacing:.08em}
        .cart .v{font-size:9.5px;margin-top:2px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
        .cart .v.titre{font-size:17px;margin-top:3px}
        h3{font-family:Archivo,'Arial Narrow',sans-serif;font-weight:800;font-stretch:62%;text-transform:uppercase;font-size:18px;line-height:1;margin:18px 0 8px}
        h3:first-child{margin-top:0}
        p.t{font-size:11.5px;line-height:1.5;margin:0 0 8px;max-width:640px}
        ul.t{margin:0 0 10px 16px;font-size:11.5px;line-height:1.5}ul.t li{margin-bottom:3px}
        table{width:100%;border-collapse:collapse;font-size:9.5px;table-layout:fixed}
        th{font-family:'Martian Mono',monospace;font-stretch:87.5%;font-weight:500;text-transform:uppercase;letter-spacing:.06em;font-size:7.5px;text-align:left;color:#5C636A;border-bottom:1.5px solid #1B1E21;padding:4px 5px}
        td{padding:4px 5px;border-bottom:1px solid #D4D7DA;vertical-align:top;overflow:hidden;text-overflow:ellipsis}
        td.n,th.n{text-align:right;font-variant-numeric:tabular-nums}
        td.nw{white-space:nowrap}
        tr.tot td{border-top:1.5px solid #1B1E21;border-bottom:0;font-weight:600}
        .specs{display:grid;border-top:1.5px solid #1B1E21;border-bottom:1.5px solid #1B1E21}
        .specs>div{padding:10px 0 10px 12px;border-left:1px solid #1B1E21}.specs>div:first-child{padding-left:0;border-left:0}
        .specs .titre{font-size:40px}.specs .data{font-size:8px;margin-top:6px;opacity:.75}
        .tag{display:inline-block;font-family:'Martian Mono',monospace;font-stretch:87.5%;font-size:7.5px;letter-spacing:.06em;text-transform:uppercase;padding:1px 5px;border:1px solid #1B1E21;white-space:nowrap}
        .tag.e{background:#1B1E21;color:#F3F4F5}.tag.f{border-style:dashed;color:#5C636A;border-color:#9AA0A6}
        .cl{display:inline-block;width:22px;text-align:center;font-family:'Martian Mono',monospace;font-size:8px;border:1px solid #1B1E21}
        .cl.XL{background:#1B1E21;color:#F3F4F5}.cl.L{background:#9AA0A6}.cl.S{border-color:#9AA0A6;color:#5C636A}
        .grille{display:grid;grid-template-columns:repeat(3,1fr);gap:14px 14px}
        .vig{border:1px solid #9AA0A6;background:#fff;display:flex;flex-direction:column;min-width:0}
        .vig .img{height:150px;display:flex;align-items:center;justify-content:center;padding:8px;background:#F3F4F5;border-bottom:1px solid #9AA0A6}
        .vig .img svg{max-width:100%;max-height:100%}
        .vig .cap{padding:5px 7px 6px;display:flex;justify-content:space-between;gap:6px;align-items:baseline}
        .vig .cap b{font-size:10px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
        .vide{width:100%;height:100%;background:repeating-linear-gradient(45deg,#D4D7DA 0 1px,transparent 1px 7px);display:flex;align-items:center;justify-content:center;text-align:center;color:#5C636A;font-size:9px;padding:10px}
        .fiche{display:grid;grid-template-columns:minmax(0,1fr) 210px;gap:18px;padding-bottom:18px;margin-bottom:18px;border-bottom:1px solid #9AA0A6}
        .fiche:last-child{margin-bottom:0;padding-bottom:0;border-bottom:0}
        .fiche:last-child{border-bottom:0}
        .fiche .img{border:1px solid #9AA0A6;background:#F3F4F5;padding:10px;height:250px;display:flex;align-items:center;justify-content:center}
        .fiche .img svg{max-width:100%;max-height:100%}
        .kv{display:grid;grid-template-columns:1fr auto;font-size:9.5px;border-top:1.5px solid #1B1E21}
        .kv>span{padding:3px 0;border-bottom:1px solid #D4D7DA}.kv>span:nth-child(even){text-align:right;font-family:'Martian Mono',monospace;font-size:9px}
        .const{border-top:1.5px solid #1B1E21;padding:9px 0 12px}
        .const .tete{display:flex;gap:10px;align-items:baseline;margin-bottom:6px}
        .const .tete .titre{font-size:19px}
        .const .cols{display:grid;grid-template-columns:1fr 1fr;gap:16px;font-size:10.5px;line-height:1.45}
        .const .lab{font-size:7.5px;color:#5C636A;margin-bottom:2px}
        .occ{margin-top:7px;font-family:'Martian Mono',monospace;font-stretch:87.5%;font-size:8px;line-height:1.55;color:#2A2F34;background:#fff;border:1px solid #D4D7DA;padding:5px 7px}
        .occ div{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
        .occ .f{color:#5C636A}
        .barre{height:14px;background:#9AA0A6;position:relative}
        .barre i{position:absolute;top:0;bottom:0;background:#1B1E21}
        .avis{border-left:3px solid #1B1E21;padding:2px 0 2px 14px;margin:0 0 14px}
        .note{font-size:9.5px;color:#5C636A;line-height:1.45}
        @media print{html,body{background:none;padding:0}.page{margin:0}}
        @media screen and (max-width:820px){body{padding:0}.page{transform-origin:top left}}
        """;

    static string Cadre(bool couverture)
    {
        // Cadre de planche : repères de zones A à F en haut, 1 à 8 à gauche.
        const double W = 793.7, H = 1122.5, M = 22;
        var sb = new StringBuilder($"<svg class=\"cadre\" viewBox=\"0 0 {W.ToString(CultureInfo.InvariantCulture)} {H.ToString(CultureInfo.InvariantCulture)}\" preserveAspectRatio=\"none\"><g fill=\"#1B1E21\" stroke=\"#1B1E21\" font-family=\"Martian Mono\" font-size=\"7\" opacity=\".5\">");
        for (var i = 0; i < 6; i++)
        {
            var x = M + (W - 2 * M) / 6 * (i + 0.5);
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{x:0.#}\" y=\"{M - 6}\" text-anchor=\"middle\" stroke=\"none\">{(char)('A' + i)}</text>");
            if (i > 0) sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{M + (W - 2 * M) / 6 * i:0.#}\" y1=\"{M - 9}\" x2=\"{M + (W - 2 * M) / 6 * i:0.#}\" y2=\"{M}\"/>");
        }
        for (var j = 0; j < 8; j++)
        {
            var y = M + (H - 2 * M) / 8 * (j + 0.5);
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{M / 2}\" y=\"{y + 3:0.#}\" text-anchor=\"middle\" stroke=\"none\">{j + 1}</text>");
            if (j > 0) sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{M - 9}\" y1=\"{M + (H - 2 * M) / 8 * j:0.#}\" x2=\"{M}\" y2=\"{M + (H - 2 * M) / 8 * j:0.#}\"/>");
        }
        sb.Append(CultureInfo.InvariantCulture, $"</g><rect x=\"{M}\" y=\"{M}\" width=\"{W - 2 * M:0.#}\" height=\"{H - 2 * M:0.#}\" fill=\"none\" stroke=\"#1B1E21\" stroke-width=\"1.5\"/></svg>");
        return sb.ToString();
    }

    static string Cartouche(OptionsRapport o, string folio, bool grand = false) =>
        $"<div class=\"cart\"{(grand ? " style=\"width:380px\"" : "")}><div class=\"r\" style=\"grid-template-columns:2.4fr 1fr\"><div class=\"c\"><div class=\"k data\">Désignation</div><div class=\"v titre\">{E(o.Application)}</div></div><div class=\"c\"><div class=\"k data\">Repère</div><div class=\"v data\">{Repere}</div></div></div>"
        + $"<div class=\"r\" style=\"grid-template-columns:1.6fr 1fr 1fr\"><div class=\"c\"><div class=\"k data\">{(o.Client != null ? "Destinataire" : "Document")}</div><div class=\"v data\">{E(o.Client ?? "Audit de migration")}</div></div><div class=\"c\"><div class=\"k data\">Date</div><div class=\"v data\">{o.Date:dd.MM.yyyy}</div></div><div class=\"c\"><div class=\"k data\">Folio</div><div class=\"v data\">{folio}</div></div></div></div>";

    static string PiedDePage(OptionsRapport o, string folio) =>
        $"<div class=\"pied\"><div class=\"gen data\">winmig {Version.Courante} · analyse statique du code source, sans compilation ni exécution{(o.Auteur != null ? $" · relu par {E(o.Auteur)}" : "")}</div>{Cartouche(o, folio)}</div>";

    static string Bulle(int d, bool alarme)
    {
        var sw = Math.Max(2, d / 40.0);
        var r = d / 2.0 - sw / 2;
        var fond = alarme ? Signal : "#F3F4F5";
        return string.Create(CultureInfo.InvariantCulture, $"<svg width=\"{d}\" height=\"{d}\" viewBox=\"0 0 {d} {d}\" style=\"display:block\"><circle cx=\"{d / 2.0}\" cy=\"{d / 2.0}\" r=\"{r}\" fill=\"{fond}\" stroke=\"#1B1E21\" stroke-width=\"{sw}\"/><line x1=\"{sw / 2}\" y1=\"{d / 2.0}\" x2=\"{d - sw / 2}\" y2=\"{d / 2.0}\" stroke=\"#1B1E21\" stroke-width=\"{sw}\"/><text x=\"{d / 2.0}\" y=\"{d * 0.4}\" text-anchor=\"middle\" font-family=\"Archivo\" font-stretch=\"62%\" font-weight=\"800\" font-size=\"{d * 0.3}\" fill=\"#1B1E21\">DEV</text><text x=\"{d / 2.0}\" y=\"{d * 0.76}\" text-anchor=\"middle\" font-family=\"Martian Mono\" font-stretch=\"87.5%\" font-weight=\"500\" font-size=\"{d * 0.2}\" fill=\"#1B1E21\">605</text></svg>");
    }

    static string Specs(IEnumerable<(string V, string K)> items, int taille = 40)
    {
        var l = items.ToList();
        return $"<div class=\"specs\" style=\"grid-template-columns:repeat({l.Count},1fr)\">" + string.Join("", l.Select(x => $"<div><div class=\"titre\" style=\"font-size:{taille}px\">{E(x.V)}</div><div class=\"data\">{E(x.K)}</div></div>")) + "</div>";
    }

    static string SvgSchema(Ecran e, Inventaire inv, double echelle = 0) =>
        e.Schema == null ? "<div class=\"vide\">Pas de code de concepteur : écran construit entièrement par le code</div>"
        : e.Schema.Enfants.Count == 0 ? "<div class=\"vide\">Aucun contrôle posé dans le concepteur : contenu créé par le code</div>"
        : Dessin(e, inv, echelle);

    static string Couverture(Inventaire inv, Chiffrage c, OptionsRapport o, Ecran? principale, HashSet<string> internes)
    {
        var atteints = inv.Ecrans.Count(e => e.Atteint);
        var sb = new StringBuilder();
        sb.Append($"<div style=\"position:absolute;left:52px;top:52px\">{Bulle(88, true)}</div>");
        sb.Append($"<div class=\"data\" style=\"position:absolute;left:156px;top:66px;font-size:9.5px;line-height:1.7;color:#5C636A\">Rapport d'audit<br>WinForms · WPF → web</div>");
        sb.Append($"<div class=\"titre\" style=\"position:absolute;left:50px;top:176px;right:52px;font-size:58px\">Audit de migration</div>");
        var nomTaille = o.Application.Length > 22 ? 56 : o.Application.Length > 14 ? 72 : 96;
        sb.Append($"<div class=\"titre\" style=\"position:absolute;left:48px;top:238px;right:52px;font-size:{nomTaille}px;line-height:.9\">{E(o.Application)}</div>");
        sb.Append($"<p style=\"position:absolute;left:52px;top:{238 + nomTaille + 26}px;width:440px;font-size:15px;line-height:1.4;font-weight:500\">Inventaire des écrans, dépendances et usages propres à Windows, architecture cible, plan de lots et prix au forfait.</p>");
        // Le schéma de la fenêtre principale, à plat et coté.
        if (principale?.Schema != null)
        {
            var w = 470.0; var h = 330.0;
            var ratio = (principale.Schema.Ah + 24.0) / (principale.Schema.Al + 2.0);
            var lw = ratio > h / w ? h / ratio : w; var lh = lw * ratio;
            var x = 794 - 70 - lw; var y = 470.0;
            sb.Append(CultureInfo.InvariantCulture, $"<div style=\"position:absolute;left:{x:0}px;top:{y:0}px;width:{lw:0}px;height:{lh:0}px\">{"<svg width=\"100%\" height=\"100%\" " + Dessin(principale, inv)[5..]}</div>");
            sb.Append(CultureInfo.InvariantCulture, $"<svg style=\"position:absolute;left:0;top:0\" width=\"794\" height=\"1123\">{Cote(x, y + lh + 18, x + lw, y + lh + 18, $"{principale.Schema.Al} PX", true)}{Cote(x - 18, y, x - 18, y + lh, $"{principale.Schema.Ah + 22} PX", false)}</svg>");
            sb.Append(CultureInfo.InvariantCulture, $"<div class=\"data\" style=\"position:absolute;left:{x:0}px;top:{y + lh + 36:0}px;font-size:8px;color:#5C636A\">{E(Court(principale))} · schéma reconstitué depuis le code du concepteur</div>");
        }
        sb.Append($"<div style=\"position:absolute;left:52px;top:880px;width:690px\">{Specs(new[] { (atteints.ToString(Fr), "Écrans à migrer"), (N(c.UnitesTotal), "Unités d'écran"), (c.Lots.Count.ToString(Fr), c.Lots.Count > 1 ? "Lots" : "Lot"), ($"{N(Math.Round(c.JoursBas))}–{N(Math.Round(c.JoursHaut))}", "Jours de travail") })}</div>");
        sb.Append($"<div style=\"position:absolute;right:52px;bottom:44px\">{Cartouche(o, "{{FOLIO}}", true)}</div>");
        sb.Append($"<div class=\"data\" style=\"position:absolute;left:52px;bottom:48px;font-size:8px;color:#5C636A;max-width:300px;line-height:1.6\">winmig {Version.Courante}<br>{(o.Auteur != null ? $"Relu par {E(o.Auteur)}" : "Chiffrage indicatif calculé par l'outil")}</div>");
        return sb.ToString();
    }

    static string Cote(double x1, double y1, double x2, double y2, string label, bool horizontal)
    {
        const double a = 6;
        var mx = (x1 + x2) / 2; var my = (y1 + y2) / 2;
        var lw = label.Length * 6.4 + 12;
        var fleches = horizontal
            ? $"M{x1:0.#} {y1:0.#} l{a} -{a / 2.6:0.##} v{a / 1.3:0.##}Z M{x2:0.#} {y2:0.#} l-{a} -{a / 2.6:0.##} v{a / 1.3:0.##}Z"
            : $"M{x1:0.#} {y1:0.#} l-{a / 2.6:0.##} {a} h{a / 1.3:0.##}Z M{x2:0.#} {y2:0.#} l-{a / 2.6:0.##} -{a} h{a / 1.3:0.##}Z";
        var lab = horizontal
            ? $"<rect x=\"{mx - lw / 2:0.#}\" y=\"{my - 7:0.#}\" width=\"{lw:0.#}\" height=\"14\" fill=\"#D4D7DA\" stroke=\"none\"/><text x=\"{mx:0.#}\" y=\"{my + 3.2:0.#}\" text-anchor=\"middle\" stroke=\"none\">{label}</text>"
            : $"<g transform=\"rotate(-90 {mx:0.#} {my:0.#})\"><rect x=\"{mx - lw / 2:0.#}\" y=\"{my - 7:0.#}\" width=\"{lw:0.#}\" height=\"14\" fill=\"#D4D7DA\" stroke=\"none\"/><text x=\"{mx:0.#}\" y=\"{my + 3.2:0.#}\" text-anchor=\"middle\" stroke=\"none\">{label}</text></g>";
        return string.Create(CultureInfo.InvariantCulture, $"<g stroke=\"#1B1E21\" fill=\"#1B1E21\" stroke-width=\"1\" font-family=\"Martian Mono\" font-size=\"9\" letter-spacing=\".5\"><line x1=\"{x1:0.#}\" y1=\"{y1:0.#}\" x2=\"{x2:0.#}\" y2=\"{y2:0.#}\"/><path d=\"{fleches}\"/>{lab}</g>");
    }

    static string Synthese(Inventaire inv, Chiffrage c, OptionsRapport o)
    {
        var at = inv.Ecrans.Where(e => e.Atteint).ToList();
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(o.Avis))
        {
            sb.Append($"<h3>Avis{(o.Auteur != null ? $" de {E(o.Auteur)}" : " de l'auditeur")}</h3><div class=\"avis\">{MiniMarkdown(o.Avis!)}</div>");
        }
        sb.Append("<h3>Ce que dit l'inventaire</h3><ul class=\"t\">");
        foreach (var l in Constatations(inv, c)) sb.Append($"<li>{l}</li>");
        sb.Append("</ul>");
        sb.Append("<h3>Chiffrage</h3>");
        sb.Append(Specs(new[]
        {
            (N(c.UnitesTotal), "Unités d'écran"),
            ($"{N(Math.Round(c.JoursBas))}–{N(Math.Round(c.JoursHaut))}", "Jours de travail"),
            (Eur(c.Prix), "Prix total HT au forfait"),
        }, 34));
        sb.Append(Repartition(c));
        sb.Append($"<p class=\"note\" style=\"margin-top:8px\">{E(c.Lots.Count.ToString(Fr))} lot{(c.Lots.Count > 1 ? "s" : "")}, {E(Eur(c.Parametres.PrixUnite))} HT l'unité d'écran. Chaque lot est commandé, livré et recetté séparément : vous pouvez vous arrêter après n'importe quel lot. Le prix de l'audit ({E(Eur(c.Parametres.PrixAudit))} HT) est déduit du premier lot commandé dans les trois mois.</p>");
        sb.Append("<h3>Prochaines étapes</h3><ul class=\"t\">");
        sb.Append("<li>Valider le périmètre : la liste des écrans à migrer, et le sort des écrans hors périmètre.</li>");
        sb.Append("<li>Confirmer l'architecture cible (page suivante) et l'hébergement : votre réseau, votre cloud, ou un serveur isolé.</li>");
        sb.Append("<li>Commander le lot 01 : le socle et la fenêtre principale tournent sur le web, avec des données de test représentatives, à la fin de ce lot.</li></ul>");
        return sb.ToString();
    }

    static string Repartition(Chiffrage c)
    {
        var parts = new List<(string K, double V, string Fond)>
        {
            ("Socle", c.UnitesSocle, "#1B1E21"),
            ("Chantiers transverses", c.UnitesChantiers, "#5C636A"),
        };
        foreach (var k in Enum.GetValues<Complexite>().Reverse())
        {
            var v = c.Lots.SelectMany(l => l.Ecrans).Where(e => e.Complexite == k).Sum(e => e.Unites);
            if (v > 0) parts.Add(($"Écrans {k}", v, k switch { Complexite.XL => "#2A2F34", Complexite.L => "#9AA0A6", Complexite.M => "#D4D7DA", _ => "#fff" }));
        }
        parts = parts.Where(x => x.V > 0).ToList();
        var total = parts.Sum(x => x.V);
        var sb = new StringBuilder("<div class=\"data\" style=\"font-size:7.5px;color:#5C636A;margin:14px 0 4px\">D'où viennent les unités</div><div style=\"display:flex;height:22px;border:1px solid #1B1E21\">");
        foreach (var (k, v, f) in parts)
            sb.Append(CultureInfo.InvariantCulture, $"<div title=\"{E(k)}\" style=\"width:{v / total * 100:0.##}%;background:{f};border-right:1px solid #1B1E21\"></div>");
        sb.Append("</div><div style=\"display:flex;flex-wrap:wrap;gap:4px 16px;margin-top:6px;font-size:9.5px\">");
        foreach (var (k, v, f) in parts)
            sb.Append($"<span><i style=\"display:inline-block;width:9px;height:9px;background:{f};border:1px solid #1B1E21;margin-right:5px;vertical-align:-1px\"></i>{E(k)} · {E(N(v))} u.</span>");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>Les constats rédigés automatiquement à partir de l'inventaire.</summary>
    public static List<string> Constatations(Inventaire inv, Chiffrage c)
    {
        var at = inv.Ecrans.Where(e => e.Atteint).ToList();
        var l = new List<string>();
        string NomNature(NatureEcran n, int k) => n switch
        {
            NatureEcran.Fenetre => k > 1 ? "fenêtres" : "fenêtre", NatureEcran.Dialogue => k > 1 ? "boîtes de dialogue" : "boîte de dialogue",
            NatureEcran.Page => k > 1 ? "pages" : "page", _ => k > 1 ? "composants" : "composant",
        };
        var natures = at.GroupBy(e => e.Nature).OrderBy(g => g.Key).Select(g => $"{g.Count()} {NomNature(g.Key, g.Count())}");
        var technos = at.Select(e => e.Techno).Distinct().Select(t => t == Techno.Wpf ? "WPF" : "WinForms").ToList();
        var lignes = inv.Projets.Sum(p => p.LignesCode);
        l.Add($"<b>{E(Pluriel(at.Count, "écran", "écrans"))} {E(string.Join(" et ", technos))}</b> à migrer : {E(string.Join(", ", natures))}, sur {E(Pluriel(inv.Projets.Count, "projet", "projets"))} et {E(lignes.ToString("#,0", Fr).Replace(' ', ' '))} lignes de code.");
        if (c.HorsPerimetre.Count > 0)
            l.Add($"<b>{E(Pluriel(c.HorsPerimetre.Count, "autre écran est", "autres écrans sont"))} dans le code mais jamais appelé{(c.HorsPerimetre.Count > 1 ? "s" : "")}</b> depuis {E(Entrees(inv))}. {(c.HorsPerimetre.Count > 1 ? "Ils sont exclus" : "Il est exclu")} du prix et chiffré{(c.HorsPerimetre.Count > 1 ? "s" : "")} à part ({E(N(c.UnitesHorsPerimetre))} unités), à confirmer avec vous.");
        var lourds = at.OrderByDescending(e => e.Score).Take(3).Where(e => e.Complexite >= Complexite.L).ToList();
        l.Add($"Complexité : {E(string.Join(", ", Enum.GetValues<Complexite>().Where(k => c.Repartition.GetValueOrDefault(k) > 0).Select(k => $"{c.Repartition[k]} {k}")))}."
              + (lourds.Count > 0 ? $" Les plus lourds : {E(string.Join(", ", lourds.Select(e => $"{Court(e)} ({e.Complexite}, {N(e.LignesCodeBehind)} lignes de logique)")))}." : " Aucun écran lourd."));
        var elevees = inv.Constats.Where(k => k.Gravite == Gravite.Elevee && k.Occurrences.Any(inv.OccurrenceAtteinte)).ToList();
        if (elevees.Count > 0)
            l.Add($"<b>Ce qui ne passe pas tel quel dans un navigateur</b> : {E(string.Join(" ; ", elevees.Select(k => $"{Min(k.Titre)} ({Pluriel(k.Occurrences.Where(inv.OccurrenceAtteinte).Select(x => x.Fichier).Distinct().Count(), "fichier", "fichiers")})")))}. Chaque point a sa solution et son coût en page « Points d'attention ».");
        if (c.Lots.Count > 0 && c.Lots[0].Chantiers.Count > 0)
            l.Add($"Chantiers transverses chiffrés une fois, au premier lot : {E(string.Join(" ; ", c.Lots[0].Chantiers.Select(x => $"{Min(x.Constat.Titre)} ({N(x.Unites)} u.)")))}.");
        if (inv.RapportsCrystal.Count > 0)
        {
            var (n, bas, haut) = ForfaitCrystal(inv, c);
            l.Add($"<b>{E(Pluriel(inv.RapportsCrystal.Count, "rapport Crystal Reports", "rapports Crystal Reports"))}</b>, dont {n} dans le périmètre : hors unités, chiffrés à part au forfait par rapport ({E(Eur(bas))} à {E(Eur(haut))} HT), page « Rapports Crystal Reports ».");
        }
        var bloquants = GroupesNet10(inv).Where(g => g.Portee == PorteeNet10.AFaire && !Net10.Regle(g.Code).CopilotLeFait).ToList();
        if (bloquants.Count > 0)
            l.Add($"Passage à .NET 10 : {E(string.Join(", ", bloquants.Select(g => Min(Net10.Regle(g.Code).Titre))))} à reprendre, que GitHub Copilot ne traite pas (page « Passage à .NET 10 »).");
        var tiersUi = inv.Dependances.Where(d => d.Interface && d.Risque == Gravite.Elevee).Select(d => d.Famille).Distinct().ToList();
        if (tiersUi.Count > 0)
            l.Add($"Suites de composants tierces à remplacer : {E(string.Join(", ", tiersUi))}. Leurs licences bureau ne couvrent pas le web.");
        else l.Add("Aucune suite de composants commerciale dans l'interface : pas de licence à racheter pour le web.");
        var tempsReel = inv.Constats.FirstOrDefault(k => k.Code == "W07" && k.Occurrences.Any(inv.OccurrenceAtteinte));
        if (tempsReel != null)
            l.Add($"L'application reçoit des données en continu ({E(Pluriel(tempsReel.Occurrences.Where(inv.OccurrenceAtteinte).Count(), "point", "points"))} de synchronisation avec l'interface) : le socle web prévoit un canal SignalR dès le premier lot.");
        var mvvm = at.Count(e => e.Mvvm);
        if (mvvm > 0) l.Add($"{E(Pluriel(mvvm, "écran suit", "écrans suivent"))} le modèle MVVM : leur logique se transpose presque ligne à ligne en services et signaux Angular.");
        var liaisons = at.Sum(e => e.Liaisons);
        var evts = at.Sum(e => e.Evenements);
        l.Add($"{E(Pluriel(evts, "gestionnaire d'événement", "gestionnaires d'événements"))}{(liaisons > 0 ? $" et {E(Pluriel(liaisons, "liaison de données", "liaisons de données"))}" : "")} à reprendre, et {E(at.Sum(e => e.LignesCodeBehind).ToString("#,0", Fr).Replace('\u202F', '\u00A0'))} lignes de logique d'écran : c'est cette logique qui fait l'essentiel du travail, pas le dessin des écrans.");
        return l;
    }

    static string MiniMarkdown(string md)
    {
        var sb = new StringBuilder();
        var liste = false;
        foreach (var brute in md.Replace("\r", "").Split('\n'))
        {
            var ligne = brute.TrimEnd();
            if (ligne.StartsWith("- ") || ligne.StartsWith("* "))
            {
                if (!liste) { sb.Append("<ul class=\"t\">"); liste = true; }
                sb.Append($"<li>{Inline(ligne[2..])}</li>");
                continue;
            }
            if (liste) { sb.Append("</ul>"); liste = false; }
            if (ligne.Length == 0) continue;
            if (ligne.StartsWith('#')) { sb.Append($"<p class=\"t\"><b>{Inline(ligne.TrimStart('#', ' '))}</b></p>"); continue; }
            sb.Append($"<p class=\"t\">{Inline(ligne)}</p>");
        }
        if (liste) sb.Append("</ul>");
        return sb.ToString();
    }

    static string Inline(string s)
    {
        var h = E(s);
        h = Regex.Replace(h, @"\*\*(.+?)\*\*", "<b>$1</b>");
        h = Regex.Replace(h, @"`(.+?)`", "<span class=\"mono\">$1</span>");
        h = Regex.Replace(h, @"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", "<i>$1</i>");
        return h;
    }

    static string Architecture(Inventaire inv, Chiffrage c)
    {
        bool A(string code) => inv.Constats.Any(k => k.Code == code && k.Occurrences.Any(inv.OccurrenceAtteinte));
        var protocoles = inv.Dependances.Where(d => d.Categorie == "Protocole industriel").Select(d => d.Nom).ToList();
        var services = new List<(string Titre, string Detail)>();
        if (A("W18") || protocoles.Count > 0)
            services.Add(("Passerelle industrielle", Protocole(protocoles) + " · connexion, abonnements, écritures contrôlées"));
        if (A("W01")) services.Add(("Service matériel", "Ports série, USB, HID · installé près du matériel"));
        if (A("W05") || A("W14") || inv.Dependances.Any(d => d.Categorie == "Accès aux données"))
            services.Add(("Accès aux données", "Requêtes extraites des écrans · contrôle d'accès"));
        if (A("W03")) services.Add(("Service Windows COM", "Composants COM isolés, exposés par API"));
        if (A("W13")) services.Add(("Contrats WCF", "Mêmes opérations en REST ou gRPC-Web"));
        if (A("W10")) services.Add(("États PDF", "Génération côté serveur"));
        if (A("W09")) services.Add(("Fichiers", "Téléversement, téléchargement, stockage serveur"));
        if (services.Count == 0) services.Add(("Logique métier", "Code .NET existant, réutilisé côté serveur"));
        services = services.Take(5).ToList();

        // Schéma : navigateur → serveur → services.
        const int W = 690, H = 280;
        var sb = new StringBuilder();
        sb.Append($"<svg width=\"{W}\" height=\"{H}\" viewBox=\"0 0 {W} {H}\" style=\"display:block;margin:4px 0 12px\" font-family=\"Atkinson, sans-serif\">");
        string Boite(int x, int y, int w, int h, string titre, string[] lignes, bool sombre = false)
        {
            var t = new StringBuilder($"<rect x=\"{x + 0.75}\" y=\"{y + 0.75}\" width=\"{w - 1.5}\" height=\"{h - 1.5}\" fill=\"{(sombre ? "#1B1E21" : "#fff")}\" stroke=\"#1B1E21\" stroke-width=\"1.5\"/>");
            t.Append($"<text x=\"{x + 10}\" y=\"{y + 22}\" font-family=\"Archivo\" font-stretch=\"62%\" font-weight=\"800\" font-size=\"17\" fill=\"{(sombre ? "#F3F4F5" : "#1B1E21")}\" style=\"text-transform:uppercase\">{E(titre.ToUpperInvariant())}</text>");
            for (var i = 0; i < lignes.Length; i++)
                t.Append($"<text x=\"{x + 10}\" y=\"{y + 40 + i * 14}\" font-size=\"10\" fill=\"{(sombre ? "#D4D7DA" : "#5C636A")}\">{E(lignes[i])}</text>");
            return t.ToString();
        }
        sb.Append(Boite(0, 70, 190, 150, "Navigateur", new[] { "Angular, composants et signaux", "Écrans repris un par un", "Tests Playwright", "Aucune installation sur le poste" }).Replace("height=\"148.5\"", "height=\"148.5\""));
        sb.Append(Boite(250, 50, 190, 190, "Serveur web", new[] { "ASP.NET Core (.NET 8 LTS ou 10)", "API REST, contrôle d'accès", "Hub SignalR : temps réel", "Logique .NET existante", "réutilisée quand c'est possible" }, true));
        var hs = Math.Min(58, (H - 10 * (services.Count - 1)) / services.Count);
        var y0 = (H - (hs * services.Count + 10 * (services.Count - 1))) / 2;
        for (var i = 0; i < services.Count; i++)
        {
            var y = y0 + i * (hs + 10);
            sb.Append($"<rect x=\"500.75\" y=\"{y + 0.75}\" width=\"188.5\" height=\"{hs - 1.5}\" fill=\"#F3F4F5\" stroke=\"#1B1E21\" stroke-width=\"1.5\"/>");
            sb.Append($"<text x=\"510\" y=\"{y + 18}\" font-family=\"Archivo\" font-stretch=\"62%\" font-weight=\"800\" font-size=\"13\" fill=\"#1B1E21\">{E(services[i].Titre.ToUpperInvariant())}</text>");
            var mots = Couper2(services[i].Detail, 40);
            for (var j = 0; j < mots.Count; j++)
                sb.Append($"<text x=\"510\" y=\"{y + 31 + j * 11}\" font-size=\"8.5\" fill=\"#5C636A\">{E(mots[j])}</text>");
            sb.Append($"<line x1=\"440\" y1=\"{145}\" x2=\"500\" y2=\"{y + hs / 2}\" stroke=\"#1B1E21\" stroke-width=\"1\"/>");
        }
        sb.Append("<line x1=\"190\" y1=\"130\" x2=\"250\" y2=\"130\" stroke=\"#1B1E21\" stroke-width=\"1.5\"/><path d=\"M250 130 l-7 -3 v6Z\" fill=\"#1B1E21\"/>");
        sb.Append("<line x1=\"190\" y1=\"165\" x2=\"250\" y2=\"165\" stroke=\"#1B1E21\" stroke-width=\"1.5\" stroke-dasharray=\"4 3\"/><path d=\"M190 165 l7 -3 v6Z\" fill=\"#1B1E21\"/>");
        sb.Append("<text x=\"220\" y=\"122\" text-anchor=\"middle\" font-family=\"Martian Mono\" font-size=\"7.5\" fill=\"#5C636A\">HTTPS</text><text x=\"220\" y=\"182\" text-anchor=\"middle\" font-family=\"Martian Mono\" font-size=\"7.5\" fill=\"#5C636A\">SIGNALR</text>");
        sb.Append("</svg>");

        var html = new StringBuilder();
        html.Append(sb);
        html.Append("<p class=\"t\">L'interface est réécrite en Angular et tourne dans le navigateur. Tout ce qui touche au poste, au réseau industriel ou à la base passe côté serveur, derrière une API qui contrôle les droits. Les données qui arrivent en continu sont poussées par SignalR. Le code .NET qui ne dépend pas de l'interface (calculs, règles, accès aux équipements) est déplacé tel quel côté serveur.</p>");
        html.Append("<h3>Options étudiées</h3>");
        html.Append("<table><colgroup><col style=\"width:23%\"><col style=\"width:27%\"><col style=\"width:27%\"><col style=\"width:23%\"></colgroup><thead><tr><th>Option</th><th>Ce que vous gardez</th><th>Ce que vous acceptez</th><th>Quand la choisir</th></tr></thead><tbody>");
        var wpf = inv.Ecrans.Any(e => e.Atteint && e.Techno == Techno.Wpf);
        var options = new List<string[]>
        {
            new[] { "<b>1. Réécriture web</b> (Angular + ASP.NET Core) · chiffrée ici", "La logique .NET côté serveur ; un client léger sans installation", "Chaque écran est repris : c'est le coût principal", "Accès depuis plusieurs postes ou à distance, interface à moderniser" },
            new[] { "2. Blazor (Server ou WebAssembly)", "Le C# jusque dans l'interface", "Même reprise écran par écran ; écosystème de composants plus petit", "Équipe 100 % .NET qui maintiendra seule" },
            new[] { "3. Transposition automatique (outils commerciaux)", "Le code et l'ergonomie bureau, rendus dans le navigateur", "Une licence d'exécution, aux conditions de l'éditeur ; écrans « bureau » dans un navigateur", "Délai très court et ergonomie actuelle acceptée" },
            new[] { wpf ? "4. Portage Avalonia" : "4. Passage à .NET récent, client lourd conservé", wpf ? "Le XAML et les ViewModels" : "Les écrans tels quels", "Reste une application installée, pas du web", "Besoin multiplateforme sans navigateur" },
            new[] { "5. API d'abord, écrans ensuite", "Le client actuel, le temps de construire l'API", "Deux clients à faire vivre pendant la transition", "Application critique qui ne peut pas s'arrêter" },
        };
        foreach (var r in options) html.Append($"<tr>{string.Join("", r.Select((x, i) => $"<td>{(i == 0 ? x : E(x))}</td>"))}</tr>");
        html.Append("</tbody></table>");
        html.Append("<p class=\"note\" style=\"margin-top:8px\">Les options 2 à 5 ne sont pas chiffrées ici. Les options 1 et 5 se combinent : le socle du premier lot construit l'API dont le client actuel peut déjà se servir.</p>");
        return html.ToString();
    }

    static string Protocole(List<string> paquets)
    {
        var n = string.Join(" ", paquets);
        var l = new List<string>();
        if (Regex.IsMatch(n, "Opc|OPC", RegexOptions.IgnoreCase)) l.Add("OPC UA");
        if (Regex.IsMatch(n, "Modbus", RegexOptions.IgnoreCase)) l.Add("Modbus");
        if (Regex.IsMatch(n, "S7|Sharp7", RegexOptions.IgnoreCase)) l.Add("S7");
        if (Regex.IsMatch(n, "TwinCAT", RegexOptions.IgnoreCase)) l.Add("ADS");
        if (Regex.IsMatch(n, "MQTT", RegexOptions.IgnoreCase)) l.Add("MQTT");
        return l.Count > 0 ? string.Join(", ", l) : "Protocole automate";
    }

    static List<string> Couper2(string s, int n)
    {
        var lignes = new List<string> { "" };
        foreach (var m in s.Split(' '))
        {
            if (lignes[^1].Length + m.Length + 1 > n && lignes[^1].Length > 0) { if (lignes.Count == 2) { lignes[^1] = Tronquer(lignes[^1] + " " + m, n); break; } lignes.Add(""); }
            lignes[^1] = (lignes[^1] + " " + m).Trim();
        }
        return lignes;
    }

    static string Tronquer(string s, int n) => s.Length <= n ? s : s[..(n - 1)].TrimEnd() + "…";

    static IEnumerable<(string Titre, string Html)> PlanLots(Inventaire inv, Chiffrage c)
    {
        var p = c.Parametres;
        var sb = new StringBuilder();
        // Au-delà de 6 lots, le tableau a sa page (ou ses pages) et le calendrier la suivante.
        var seul = c.Lots.Count <= 6;
        string Tete() => "<table><colgroup><col style=\"width:7%\"><col><col style=\"width:9%\"><col style=\"width:10%\"><col style=\"width:13%\"><col style=\"width:15%\"></colgroup><thead><tr><th>Lot</th><th>Contenu</th><th class=\"n\">Écrans</th><th class=\"n\">Unités</th><th class=\"n\">Jours</th><th class=\"n\">Prix HT</th></tr></thead><tbody>";
        sb.Append($"<p class=\"t\">Le travail est découpé en lots d'environ {N(p.UnitesParLot)} unités d'écran. Le premier lot pose le socle et livre la fenêtre principale qui tourne sur le web avec ses données réelles ; les suivants reprennent les écrans dans l'ordre où ils dépendent les uns des autres.</p>");
        sb.Append(Tete());
        var ligne = 0;
        foreach (var l in c.Lots)
        {
            if (!seul && ligne > 0 && (ligne == 22 || (ligne > 22 && (ligne - 22) % 30 == 0)))
            {
                sb.Append("</tbody></table>");
                yield return (ligne == 22 ? "Plan de lots et prix" : "Plan de lots et prix (suite)", sb.ToString());
                sb.Clear(); sb.Append(Tete());
            }
            ligne++;
            sb.Append($"<tr><td class=\"data\">{l.Numero:00}</td><td>{E(l.Theme)}{(l.UnitesSocle > 0 ? $"<div class=\"note\" style=\"font-size:8.5px;margin-top:2px\">dont socle {E(N(l.UnitesSocle))} u., chantiers transverses {E(N(l.Chantiers.Sum(x => x.Unites)))} u. ({E(string.Join(", ", l.Chantiers.Select(x => x.Constat.Code)))}), écrans {E(N(l.Ecrans.Sum(x => x.Unites)))} u.</div>" : "")}</td><td class=\"n\">{l.Ecrans.Count}</td><td class=\"n\">{N(l.UnitesFacturees)}</td><td class=\"n nw\">{N(Math.Round(l.JoursBas))}–{N(Math.Round(l.JoursHaut))}</td><td class=\"n nw\">{E(Eur(l.Prix))}</td></tr>");
        }
        sb.Append($"<tr class=\"tot\"><td></td><td>Total</td><td class=\"n\">{c.Lots.Sum(l => l.Ecrans.Count)}</td><td class=\"n\">{N(c.Lots.Sum(l => l.UnitesFacturees))}</td><td class=\"n nw\">{N(Math.Round(c.JoursBas))}–{N(Math.Round(c.JoursHaut))}</td><td class=\"n nw\">{E(Eur(c.Prix))}</td></tr></tbody></table>");

        if (!seul)
        {
            yield return (ligne <= 22 ? "Plan de lots et prix" : "Plan de lots et prix (suite)", sb.ToString());
            sb.Clear();
        }
        // Calendrier indicatif, un développeur à plein temps.
        sb.Append($"<h3{(seul ? "" : " style=\"margin-top:0\"")}>Calendrier indicatif</h3><p class=\"note\">Une personne à plein temps, lots enchaînés, recette de 10 jours ouvrés en parallèle du lot suivant. Semaines comptées sur la fourchette haute.</p>");
        var totalSem = c.Lots.Sum(l => Math.Ceiling(l.JoursHaut / 5));
        var debut = 0.0;
        sb.Append("<div style=\"margin-top:8px\">");
        foreach (var l in c.Lots)
        {
            var sem = Math.Ceiling(l.JoursHaut / 5);
            sb.Append(CultureInfo.InvariantCulture, $"<div style=\"display:grid;grid-template-columns:52px 1fr 92px;gap:10px;align-items:center;margin-bottom:{(c.Lots.Count > 20 ? 2 : 6)}px\"><span class=\"data\" style=\"font-size:8.5px\">Lot {l.Numero:00}</span><div class=\"barre\" style=\"background:#D4D7DA\"><i style=\"left:{debut / totalSem * 100:0.##}%;width:{sem / totalSem * 100:0.##}%\"></i></div><span class=\"data\" style=\"font-size:8.5px;text-align:right\">{sem:0} sem.</span></div>");
            debut += sem;
        }
        sb.Append($"<div class=\"data\" style=\"display:grid;grid-template-columns:52px 1fr 92px;gap:10px;font-size:8.5px;margin-top:4px\"><span></span><span style=\"color:#5C636A\">Durée totale</span><span style=\"text-align:right\">{totalSem:0} sem.</span></div></div>");

        sb.Append("<h3>Ce que comprend chaque lot</h3><ul class=\"t\">");
        sb.Append("<li>Les écrans du lot réécrits, reliés à l'API, avec les mêmes fonctions qu'aujourd'hui (liste d'écrans et de fonctions validée avant le démarrage).</li>");
        sb.Append("<li>Les API serveur dont ces écrans ont besoin, avec contrôle d'accès.</li>");
        sb.Append("<li>Des tests Playwright de bout en bout sur chaque écran, et des captures de référence pour la non-régression visuelle.</li>");
        sb.Append("<li>La livraison dans votre dépôt Git, une note de version et une démonstration enregistrée. Aucun déplacement : tout se fait à distance.</li>");
        sb.Append("<li>Une recette de 10 jours ouvrés, la correction des écarts au périmètre, puis une garantie de 3 mois sur les anomalies.</li></ul>");
        sb.Append($"<p class=\"note\">Prix ferme par lot, payable 30 % à la commande et 70 % à la recette. Une unité d'écran vaut {E(Eur(p.PrixUnite))} HT ; un lot se facture à ses unités réelles, avec un minimum de {N(p.UnitesMinLot)} unités.</p>");
        yield return (seul ? "Plan de lots et prix" : "Calendrier et contenu des lots", sb.ToString());
    }

    static string TableEcrans(List<Ecran> ecrans, Dictionary<Ecran, int> lotDe, bool premiere, int total)
    {
        var sb = new StringBuilder();
        if (premiere) sb.Append($"<p class=\"t\">{E(Pluriel(total, "écran", "écrans"))} atteints depuis l'application, dans l'ordre de réalisation. Une unité d'écran vaut environ 2,4 à 3,2 jours de travail : S = 0,5 ; M = 1 ; L = 2 ; XL = 3, plus 0,5 par point lourd propre à l'écran (accès direct à la base, dessin personnalisé, animation).</p>");
        sb.Append("<table><colgroup><col style=\"width:6%\"><col><col style=\"width:12%\"><col style=\"width:7%\"><col style=\"width:7%\"><col style=\"width:8%\"><col style=\"width:7%\"><col style=\"width:6%\"><col style=\"width:7%\"><col style=\"width:13%\"></colgroup>");
        sb.Append("<thead><tr><th>Lot</th><th>Écran</th><th>Nature</th><th class=\"n\">Ctrl</th><th class=\"n\">Évt</th><th class=\"n\">Lignes</th><th class=\"n\">Score</th><th>Cl.</th><th class=\"n\">Unités</th><th>Points</th></tr></thead><tbody>");
        foreach (var e in ecrans)
            sb.Append($"<tr><td class=\"data\">{lotDe.GetValueOrDefault(e):00}</td><td class=\"nw\" title=\"{E(e.Classe)}\"><b>{E(Court(e))}</b>{(e.Titre != null ? $" <span style=\"color:#5C636A\">· {E(Tronquer(e.Titre, 26))}</span>" : "")}</td><td>{Nature(e.Nature)}</td><td class=\"n\">{e.Controles}</td><td class=\"n\">{e.Evenements}</td><td class=\"n\">{e.LignesCodeBehind}</td><td class=\"n\">{N(Math.Round(e.Score))}</td><td><span class=\"cl {e.Complexite}\">{e.Complexite}</span></td><td class=\"n\">{N(e.Unites)}</td><td class=\"mono nw\">{E(string.Join(" ", e.Signaux))}</td></tr>");
        sb.Append("</tbody></table>");
        return sb.ToString();
    }

    static string Planche(List<Ecran> ecrans, Dictionary<Ecran, int> lotDe, Inventaire inv, bool premiere)
    {
        var sb = new StringBuilder();
        if (premiere) sb.Append("<p class=\"t\">Chaque écran est redessiné à partir de son code de concepteur (positions, tailles, ancrages), sans lancer l'application. Les composants internes apparaissent en pointillés, avec leur nom : ils ont leur propre vignette.</p>");
        sb.Append("<div class=\"grille\">");
        foreach (var e in ecrans)
            sb.Append($"<div class=\"vig\"><div class=\"img\">{SvgSchema(e, inv, 0.42)}</div><div class=\"cap\"><b title=\"{E(e.Classe)}\">{E(Court(e))}</b><span class=\"data\" style=\"font-size:7.5px;white-space:nowrap\">Lot {lotDe.GetValueOrDefault(e):00} · <span class=\"cl {e.Complexite}\">{e.Complexite}</span></span></div></div>");
        sb.Append("</div>");
        return sb.ToString();
    }

    static string Fiche(Ecran e, Dictionary<Ecran, int> lotDe, HashSet<string> internes, Inventaire inv)
    {
        var types = e.TypesControles.OrderByDescending(t => t.Value).ThenBy(t => t.Key).Take(6).ToList();
        var sb = new StringBuilder("<div class=\"fiche\"><div>");
        sb.Append($"<div style=\"display:flex;justify-content:space-between;align-items:baseline;margin-bottom:8px\"><span class=\"titre\" style=\"font-size:22px\">{E(Court(e))}</span><span class=\"data\" style=\"font-size:8px;color:#5C636A\">{E(Nature(e.Nature))} · {E(e.Techno == Techno.Wpf ? "WPF" : "WinForms")} · lot {lotDe.GetValueOrDefault(e):00}</span></div>");
        sb.Append($"<div class=\"img\">{SvgSchema(e, inv, 0.8)}</div>");
        if (e.Schema != null) sb.Append($"<div class=\"data\" style=\"font-size:7.5px;color:#5C636A;margin-top:5px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis\">Taille de conception {e.LargeurConception} × {e.HauteurConception} px · {E(e.Fichiers.FirstOrDefault() ?? "")}</div>");
        sb.Append("</div><div>");
        sb.Append("<div class=\"kv\">");
        void Kv(string k, string v) => sb.Append($"<span>{E(k)}</span><span>{E(v)}</span>");
        Kv("Classe de complexité", $"{e.Complexite} · score {N(Math.Round(e.Score))}");
        Kv("Unités d'écran", N(e.Unites));
        Kv("Contrôles", e.Controles.ToString(Fr));
        Kv("Gestionnaires d'événements", e.Evenements.ToString(Fr));
        Kv("Liaisons de données", e.Liaisons.ToString(Fr));
        Kv("Lignes de logique", e.LignesCodeBehind.ToString(Fr));
        Kv("Branches (if, case, boucles)", e.Branches.ToString(Fr));
        Kv("Composants internes", e.ComposantsInternes.Count.ToString(Fr));
        Kv("Utilisé par", Pluriel(e.UtilisePar.Count, "écran", "écrans"));
        sb.Append("</div>");
        if (types.Count > 0)
        {
            sb.Append("<div class=\"data\" style=\"font-size:7.5px;color:#5C636A;margin:10px 0 3px\">Contrôles les plus présents</div><div class=\"kv\">");
            foreach (var t in types) Kv(t.Key, t.Value.ToString(Fr));
            sb.Append("</div>");
        }
        if (e.Signaux.Count > 0)
            sb.Append($"<div class=\"data\" style=\"font-size:7.5px;color:#5C636A;margin:10px 0 3px\">Points d'attention</div><div style=\"font-size:9.5px;line-height:1.5\">{string.Join("<br>", e.Signaux.Take(4).Select(s => $"<span class=\"mono\">{s}</span> {E(Catalogue.Regle(s).Titre)}"))}{(e.Signaux.Count > 4 ? $"<br>… et {e.Signaux.Count - 4} autres : {E(string.Join(" ", e.Signaux.Skip(4)))}" : "")}</div>");
        if (e.ControlesTiers.Count > 0)
            sb.Append($"<div class=\"data\" style=\"font-size:7.5px;color:#5C636A;margin:10px 0 3px\">Contrôles tiers</div><div style=\"font-size:9.5px;line-height:1.5\">{string.Join("<br>", e.ControlesTiers.Take(5).Select(E))}</div>");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    static string Dependances(List<Dependance> deps, bool premiere, Inventaire inv)
    {
        var sb = new StringBuilder();
        if (premiere)
        {
            var ui = inv.Dependances.Count(d => d.Interface);
            sb.Append($"<p class=\"t\">{E(Pluriel(inv.Dependances.Count, "dépendance externe", "dépendances externes"))} (paquets NuGet, assemblages référencés, composants COM), {(ui == 0 ? "aucune ne touche l'interface" : $"dont {E(Pluriel(ui, "touche", "touchent"))} l'interface")}. Les bibliothèques qui ne touchent pas l'interface restent côté serveur et ne coûtent rien de plus.</p>");
        }
        if (deps.Count == 0) { sb.Append("<p class=\"t\">Aucune dépendance externe.</p>"); return sb.ToString(); }
        sb.Append("<table><colgroup><col style=\"width:30%\"><col style=\"width:9%\"><col style=\"width:17%\"><col style=\"width:9%\"><col></colgroup><thead><tr><th>Paquet ou assemblage</th><th>Version</th><th>Catégorie</th><th>Risque</th><th>Côté web</th></tr></thead><tbody>");
        foreach (var d in deps)
            sb.Append($"<tr><td title=\"{E(string.Join(", ", d.Projets))}\"><b>{E(d.Nom)}</b><div class=\"note\" style=\"font-size:8px\">{E(d.Origine)} · {E(Pluriel(d.Projets.Count, "projet", "projets"))}</div></td><td class=\"mono\">{E(d.Version ?? "—")}</td><td>{E(d.Categorie)}</td><td><span class=\"tag {(d.Risque == Gravite.Elevee ? "e" : d.Risque == Gravite.Info ? "f" : "")}\">{Grav(d.Risque)}</span></td><td>{E(d.Remplacement)}</td></tr>");
        sb.Append("</tbody></table>");
        return sb.ToString();
    }

    /// <summary>Fourchette du forfait par rapport pour les rapports Crystal du périmètre.</summary>
    public static (int Nombre, decimal Bas, decimal Haut) ForfaitCrystal(Inventaire inv, Chiffrage c)
    {
        var n = inv.RapportsCrystal.Count(r => r.Atteint);
        return (n, n * c.Parametres.PrixRapportSimple, n * c.Parametres.PrixRapportComplexe);
    }

    static string Crystal(List<RapportCrystal> rapports, bool premiere, Inventaire inv, Chiffrage c)
    {
        var sb = new StringBuilder();
        var p = c.Parametres;
        if (premiere)
        {
            var (n, bas, haut) = ForfaitCrystal(inv, c);
            var autres = inv.RapportsCrystal.Count - n;
            sb.Append($"<p class=\"t\">{E(Pluriel(inv.RapportsCrystal.Count, "rapport Crystal Reports (.rpt) trouvé", "rapports Crystal Reports (.rpt) trouvés"))} dans les dossiers des projets, dont {E(Pluriel(n, "ouvert", "ouverts"))} par du code du périmètre{(autres > 0 ? $" ; {E(Pluriel(autres, "autre n'est", "autres ne sont"))} ouvert{(autres > 1 ? "s" : "")} par aucun code atteint (rapport abandonné, ou chargé par un chemin de configuration : à confirmer)" : "")}. Le moteur Crystal Reports ne fonctionne ni sur .NET 5 et suivants, ni dans un navigateur : chaque rapport est refait dans la cible retenue (QuestPDF, rendu HTML vers PDF, RDLC, ou un outil de reporting sous licence), avec une comparaison automatique des PDF avant et après.</p>");
            sb.Append($"<p class=\"t\">Les rapports ne sont pas comptés dans les unités d'écran. Ils sont chiffrés à part, au forfait par rapport : simple {E(Eur(p.PrixRapportSimple))}, moyen {E(Eur(p.PrixRapportMoyen))}, complexe {E(Eur(p.PrixRapportComplexe))} HT. Un pilote de 3 rapports ({E(Eur(p.PrixPiloteCrystal))} HT) met en place le rendu et la comparaison, puis classe tous les autres. Fourchette pour les rapports du périmètre : <b>{E(Eur(bas))} à {E(Eur(haut))} HT</b>.</p>");
        }
        sb.Append("<table><colgroup><col style=\"width:30%\"><col style=\"width:18%\"><col style=\"width:9%\"><col><col style=\"width:11%\"></colgroup><thead><tr><th>Rapport</th><th>Projet</th><th class=\"n\">Taille</th><th>Ouvert par</th><th>Périmètre</th></tr></thead><tbody>");
        foreach (var r in rapports)
        {
            var par = r.OuvertPar.Select(Path.GetFileName).Distinct().ToList();
            var parTexte = par.Count == 0 ? "—" : par.Count <= 2 ? string.Join(", ", par) : $"{string.Join(", ", par.Take(2))} et {par.Count - 2} autre{(par.Count > 3 ? "s" : "")}";
            sb.Append($"<tr><td title=\"{E(r.Fichier)}\"><b>{E(Path.GetFileName(r.Fichier))}</b><div class=\"note\" style=\"font-size:8px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:200px\">{E(Path.GetDirectoryName(r.Fichier)?.Replace('\\', '/') ?? "")}</div></td><td>{E(r.Projet)}</td><td class=\"n nw\">{E(N(Math.Max(1, Math.Round(r.Octets / 1024.0))))} Ko</td><td>{E(parTexte)}</td><td>{(r.Atteint ? "Oui" : "À confirmer")}</td></tr>");
        }
        sb.Append("</tbody></table>");
        return sb.ToString();
    }

    public sealed record GroupeNet10(string Code, PorteeNet10 Portee, List<PointNet10> Points);

    /// <summary>Points .NET 10 regroupés par règle (une ligne par règle), du plus urgent au simple constat.</summary>
    public static List<GroupeNet10> GroupesNet10(Inventaire inv) =>
        inv.PointsNet10.GroupBy(x => x.Code).Select(g => new GroupeNet10(g.Key, g.Min(x => x.Portee), g.ToList()))
            .OrderBy(g => g.Portee).ThenBy(g => g.Code, StringComparer.Ordinal).ToList();

    // Coupe un texte trop long sur une fin de mot, jamais au milieu.
    static string Abreger(string t, int max)
    {
        if (t.Length <= max) return t;
        var fin = t.LastIndexOf(' ', max - 1);
        return t[..(fin > max / 2 ? fin : max - 1)].TrimEnd(' ', ',', ';', ':', '(') + "…";
    }

    static string Liste(IEnumerable<string> l, int max)
    {
        var t = l.Distinct().ToList();
        return t.Count <= max ? string.Join(", ", t) : $"{string.Join(", ", t.Take(max))} et {t.Count - max} autre{(t.Count - max > 1 ? "s" : "")}";
    }

    static string PointsNet10(List<GroupeNet10> groupes, bool premiere, bool derniere, Inventaire inv)
    {
        var sb = new StringBuilder();
        if (premiere)
            sb.Append("<p class=\"t\">Ce qui bloque ou complique le passage des projets à .NET 10 (LTS, supporté jusqu'en novembre 2028). « À faire » : sans correction, le projet ne compile pas ou ne tourne pas sur .NET 10. « À vérifier » : il tourne, mais un comportement change ou sort du support. « Pour le web ou Linux » : il tourne sous Windows, pas ailleurs. La colonne « Copilot » dit ce que GitHub Copilot app modernization en fait. Ces points ne sont pas comptés dans les unités d'écran.</p>");
        sb.Append("<table><colgroup><col style=\"width:21%\"><col style=\"width:27%\"><col style=\"width:17%\"><col></colgroup><thead><tr><th>Point</th><th>Où</th><th>Copilot</th><th>Ce que nous faisons</th></tr></thead><tbody>");
        foreach (var g in groupes)
        {
            var r = Net10.Regle(g.Code);
            var details = g.Points.Select(x => Abreger(x.Detail, 150)).Distinct().ToList();
            var fichiers = g.Points.SelectMany(x => x.Fichiers).Distinct().ToList();
            var tag = g.Portee switch { PorteeNet10.AFaire => "e", PorteeNet10.Information => "f", _ => "" };
            sb.Append($"<tr><td><b>{E(r.Titre)}</b><div style=\"margin-top:4px\"><span class=\"tag {tag}\">{E(Net10.NomPortee(g.Portee))}</span></div></td>");
            sb.Append($"<td>{E(Liste(g.Points.Select(x => x.Projet), 3))}<div class=\"note\">{E(Liste(details, 2))}</div>");
            if (fichiers.Count > 0) sb.Append($"<div class=\"note mono\" style=\"font-size:8px;overflow-wrap:anywhere\">{E(Liste(fichiers, 3))}</div>");
            sb.Append($"</td><td>{(r.CopilotLeFait ? "" : "<b>")}{E(r.Copilot)}{(r.CopilotLeFait ? "" : "</b>")}</td><td>{E(r.Solution)}</td></tr>");
        }
        sb.Append("</tbody></table>");
        if (derniere) sb.Append($"<p class=\"t\" style=\"margin-top:14px\">{VerdictNet10(inv, true)}</p>");
        return sb.ToString();
    }

    /// <summary>Ce que le relevé implique pour le passage à .NET 10, en une ou deux phrases.</summary>
    public static string VerdictNet10(Inventaire inv, bool html)
    {
        string B(string t) => html ? $"<b>{E(t)}</b>" : $"**{t}**";
        string T(string t) => html ? E(t) : t;
        var groupes = GroupesNet10(inv);
        var phrases = new List<string>();
        var bloquants = groupes.Where(g => g.Portee == PorteeNet10.AFaire && !Net10.Regle(g.Code).CopilotLeFait).ToList();
        var framework = inv.PointsNet10.Where(x => x.Code == "N11").Select(x => x.Projet).Distinct().Count();
        var anciens = inv.PointsNet10.Where(x => x.Code == "N01" && x.Portee == PorteeNet10.AFaire).Select(x => x.Projet).Distinct().Count();
        if (bloquants.Count > 0)
            phrases.Add(B($"À reprendre sans Copilot : {string.Join(", ", bloquants.Select(g => Min(Net10.Regle(g.Code).Titre)))}.") + T(" Chaque point se chiffre à part, avant tout travail."));
        if (framework > 0)
            phrases.Add(B($"{Pluriel(framework, "projet est", "projets sont")} encore en .NET Framework") + T(" : c'est un portage, pas un simple changement de cible ; il relève de l'audit ou d'un devis."));
        else if (anciens > 0 && bloquants.Count == 0)
        {
            var prix = Net10.PrixForfait(inv.Projets.Count);
            phrases.Add(B($"{Pluriel(anciens, "projet cible", "projets ciblent")} une version de .NET en fin de support.") + T(prix is { } px
                ? $" Le passage est mécanique : forfait « passage à .NET 10 » de {Eur(px)} HT pour une solution de {Pluriel(inv.Projets.Count, "projet", "projets")}."
                : " Le passage est mécanique ; au-delà de 25 projets, il se chiffre sur devis."));
        }
        if (phrases.Count == 0 && groupes.All(g => g.Portee != PorteeNet10.AFaire))
            phrases.Add(B("Rien n'empêche .NET 10.") + T(groupes.Any(g => g.Portee == PorteeNet10.Information) && groupes.All(g => g.Portee == PorteeNet10.Information) ? " Les projets ciblent déjà .NET 10 ; les anciennes cibles listées sont à retirer quand plus rien ne les utilise." : ""));
        if (groupes.Any(g => g.Portee is PorteeNet10.AVerifier or PorteeNet10.HorsWindows))
            phrases.Add(T("Les points « à vérifier » et « pour le web ou Linux » n'empêchent pas .NET 10 sous Windows."));
        return string.Join(" ", phrases);
    }

    static string IntroConstats(Inventaire inv) =>
        "<p class=\"t\">Usages relevés dans le code qui ne fonctionnent pas dans un navigateur, ou qui changent de forme côté web. Les coûts s'ajoutent une fois au premier lot (chantiers transverses), sauf ceux comptés écran par écran.</p>";

    static (string Html, int Hauteur) Constat(Constat k, Inventaire inv, bool sansCode)
    {
        var occ = k.Occurrences.Where(inv.OccurrenceAtteinte).ToList();
        var hors = k.Occurrences.Count - occ.Count;
        var (b, h) = Estimation.JoursChantier(k, inv);
        var cout = k.JoursHaut == 0 ? $"compté écran par écran (+{N(Catalogue.Regle(k.Code).UnitesParEcran)} unité)" : occ.Count == 0 ? "hors périmètre" : $"{N(b)} à {N(h)} jours";
        var fichiers = occ.Select(o => o.Fichier).Distinct().Count();
        var sb = new StringBuilder("<div class=\"const\">");
        sb.Append($"<div class=\"tete\"><span class=\"mono\" style=\"font-size:10px\">{k.Code}</span><span class=\"titre\">{E(k.Titre)}</span><span class=\"tag {(k.Gravite == Gravite.Elevee ? "e" : k.Gravite == Gravite.Info ? "f" : "")}\">{Grav(k.Gravite)}</span></div>");
        sb.Append($"<div class=\"cols\"><div><div class=\"lab data\">Pourquoi</div>{E(k.Pourquoi)}</div><div><div class=\"lab data\">Solution</div>{E(k.Solution)}</div></div>");
        sb.Append($"<div class=\"data\" style=\"font-size:8px;margin-top:7px\">{E(Pluriel(occ.Count, "occurrence", "occurrences"))} · {E(Pluriel(fichiers, "fichier", "fichiers"))} · {E(Pluriel(k.Ecrans.Count(id => inv.Ecrans.Any(e => e.Id == id && e.Atteint)), "écran", "écrans"))}{(hors > 0 ? $" · {hors} hors périmètre" : "")} · coût : {E(cout)}</div>");
        var montrees = sansCode ? 0 : Math.Min(occ.Count, 5);
        if (montrees > 0)
        {
            sb.Append("<div class=\"occ\">");
            foreach (var o in occ.GroupBy(o => o.Fichier).Select(g => g.First()).Concat(occ).Distinct().Take(montrees))
                sb.Append($"<div><span class=\"f\">{E(Tronquer(o.Fichier, 60))}{(o.Ligne > 0 ? $":{o.Ligne}" : "")}</span>  {E(o.Extrait)}</div>");
            if (occ.Count > montrees) sb.Append($"<div class=\"f\">… et {occ.Count - montrees} autre{(occ.Count - montrees > 1 ? "s" : "")} : liste complète dans constats.csv</div>");
            sb.Append("</div>");
        }
        sb.Append("</div>");
        return (sb.ToString(), 120 + montrees * 13 + (occ.Count > montrees && montrees > 0 ? 13 : 0) + 18);
    }

    static string HorsPerimetre(List<Ecran> ecrans, bool premiere, Chiffrage c, Inventaire inv)
    {
        var sb = new StringBuilder();
        if (premiere)
            sb.Append($"<p class=\"t\">Ces {E(Pluriel(c.HorsPerimetre.Count, "écran est présent", "écrans sont présents"))} dans le code analysé, mais aucun chemin ne {(c.HorsPerimetre.Count > 1 ? "les" : "l'")} relie {E(Au(Entrees(inv)))}. Ce sont souvent des écrans abandonnés, ou une bibliothèque partagée avec d'autres applications. Ils ne sont pas dans le prix. Si certains sont ouverts par un mécanisme que l'analyse ne suit pas (réflexion, configuration), ils s'ajoutent au prix de leurs unités. Total : {E(N(c.UnitesHorsPerimetre))} unités, soit {E(Eur(c.Parametres.PrixUnite * (decimal)c.UnitesHorsPerimetre))} HT.</p>");
        sb.Append("<table><colgroup><col><col style=\"width:22%\"><col style=\"width:13%\"><col style=\"width:8%\"><col style=\"width:8%\"><col style=\"width:9%\"></colgroup><thead><tr><th>Écran</th><th>Projet</th><th>Nature</th><th class=\"n\">Ctrl</th><th>Cl.</th><th class=\"n\">Unités</th></tr></thead><tbody>");
        foreach (var e in ecrans)
            sb.Append($"<tr><td class=\"nw\"><b>{E(Court(e))}</b></td><td class=\"nw\">{E(e.Projet)}</td><td>{Nature(e.Nature)}</td><td class=\"n\">{e.Controles}</td><td><span class=\"cl {e.Complexite}\">{e.Complexite}</span></td><td class=\"n\">{N(e.Unites)}</td></tr>");
        sb.Append("</tbody></table>");
        return sb.ToString();
    }

    static string Methode(Chiffrage c)
    {
        var p = c.Parametres;
        var sb = new StringBuilder();
        sb.Append("<h3>Comment l'inventaire est fait</h3><ul class=\"t\">");
        sb.Append("<li>Les projets (.csproj, .vbproj, ancien et nouveau format) et les solutions sont lus tels quels. Le code C# et VB.NET est analysé par Roslyn, le compilateur .NET, en mode syntaxe : rien n'est compilé ni exécuté, Windows n'est pas nécessaire.</li>");
        sb.Append("<li>Un écran est une classe qui hérite, directement ou non, d'une fenêtre ou d'un contrôle WinForms ou WPF. Le code du concepteur (InitializeComponent, .resx) et le XAML donnent les contrôles, les événements, les liaisons et la disposition.</li>");
        sb.Append("<li>Le périmètre part du projet exécutable : un écran est retenu s'il est référencé, de proche en proche, depuis Main, App ou la fenêtre principale.</li>");
        sb.Append("<li>Les usages propres à Windows sont repérés par 21 règles (ports série, P/Invoke, COM, registre, WCF, impression, fichiers locaux…). Chaque occurrence est listée avec son fichier et sa ligne dans constats.csv.</li></ul>");
        sb.Append("<h3>Comment le chiffrage est calculé</h3>");
        sb.Append("<table><colgroup><col style=\"width:46%\"><col></colgroup><tbody>");
        void L(string k, string v) => sb.Append($"<tr><td>{E(k)}</td><td class=\"mono\">{E(v)}</td></tr>");
        L("Score d'un écran", "contrôles + 2 × événements + liaisons + lignes ÷ 15 + 0,5 × branches + 4 × contrôles tiers");
        L("Classe et unités", "S < 30 → 0,5 · M < 80 → 1 · L < 180 → 2 · XL → 3");
        L("Points lourds propres à l'écran", "+ 0,5 unité chacun (W05, W06, W21)");
        L("Socle du premier lot", $"{N(p.UnitesSocle)} unités : application, authentification, thème, SignalR, CI, tests");
        L("Chantiers transverses", "jours du catalogue × (1 + 0,15 par fichier touché au-delà du premier), plafonné à × 3");
        L("Jours par unité", $"{N(p.JoursUniteBas)} à {N(p.JoursUniteHaut)}");
        L("Prix", $"{Eur(p.PrixUnite)} HT l'unité ; lots d'environ {N(p.UnitesParLot)} unités, {N(p.UnitesMinLot)} au minimum");
        sb.Append("</tbody></table>");
        sb.Append("<h3>Ce que l'analyse ne voit pas</h3><ul class=\"t\">");
        sb.Append("<li>Les écrans ouverts par réflexion ou par configuration, et les contrôles créés dynamiquement à l'exécution.</li>");
        sb.Append("<li>La logique cachée dans la base (procédures stockées, déclencheurs) et le comportement interne des composants tiers.</li>");
        sb.Append("<li>La disposition finale quand le code la modifie à l'exécution : les schémas montrent l'état du concepteur.</li>");
        sb.Append("<li>La qualité fonctionnelle actuelle : un écran qui comporte des anomalies est migré à l'identique, sauf demande contraire.</li></ul>");
        sb.Append("<p class=\"note\">L'audit complète l'outil par une relecture humaine : contrôle des écrans les plus lourds, choix d'architecture, plan de lots validé avec vous et maquette web de deux écrans. Le prix d'un lot n'est ferme qu'après cette relecture.</p>");
        return sb.ToString();
    }

    static string Projets(Inventaire inv, IEnumerable<Projet> projets, bool dernier)
    {
        var sb = new StringBuilder("<table><colgroup><col><col style=\"width:11%\"><col style=\"width:21%\"><col style=\"width:11%\"><col style=\"width:8%\"><col style=\"width:9%\"><col style=\"width:9%\"></colgroup><thead><tr><th>Projet</th><th>Techno</th><th>Cibles</th><th>Sortie</th><th class=\"n\">Fichiers</th><th class=\"n\">Lignes</th><th class=\"n\">Écrans</th></tr></thead><tbody>");
        foreach (var p in projets)
            sb.Append($"<tr><td class=\"nw\"><b>{E(p.Nom)}</b><div class=\"note\" style=\"font-size:8px\">{(p.FormatSdk ? "Format SDK" : "Ancien format")}{(p.Langues.Count > 0 ? " · langues : " + E(string.Join(", ", p.Langues)) : "")}</div></td><td>{p.Techno switch { Techno.Aucune => "—", Techno.Wpf => "WPF", _ => p.Techno.ToString() }}</td><td class=\"mono\">{E(string.Join(" ", p.Cibles))}</td><td>{E(p.TypeSortie)}</td><td class=\"n\">{p.FichiersCs + p.FichiersXaml}</td><td class=\"n\">{p.LignesCode.ToString("#,0", Fr).Replace(' ', ' ')}</td><td class=\"n\">{inv.Ecrans.Count(e => e.Projet == p.Nom)}</td></tr>");
        sb.Append("</tbody></table>");
        if (dernier && inv.Avertissements.Count > 0)
            sb.Append($"<h3>Avertissements</h3><ul class=\"t\">{string.Join("", inv.Avertissements.Take(8).Select(a => $"<li>{E(a)}</li>"))}{(inv.Avertissements.Count > 8 ? $"<li>… et {inv.Avertissements.Count - 8} autres, listés par la commande winmig.</li>" : "")}</ul>");
        return sb.ToString();
    }

    // ---------------------------------------------------------------- Markdown

    public static string Markdown(Inventaire inv, Chiffrage c, OptionsRapport o)
    {
        var sb = new StringBuilder();
        string Brut(string html) => WebUtility.HtmlDecode(Regex.Replace(html, "<b>|</b>", "**").Replace("<i>", "*").Replace("</i>", "*")).Replace("&nbsp;", " ");
        sb.AppendLine($"# Audit de migration : {o.Application}");
        sb.AppendLine();
        sb.AppendLine($"{Repere} · {o.Date:dd/MM/yyyy}{(o.Client != null ? $" · pour {o.Client}" : "")} · winmig {Version.Courante}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(o.Avis)) { sb.AppendLine("## Avis"); sb.AppendLine(); sb.AppendLine(o.Avis!.Trim()); sb.AppendLine(); }
        sb.AppendLine("## Ce que dit l'inventaire"); sb.AppendLine();
        foreach (var l in Constatations(inv, c)) sb.AppendLine($"- {Brut(l)}");
        sb.AppendLine();
        sb.AppendLine("## Plan de lots"); sb.AppendLine();
        sb.AppendLine("| Lot | Contenu | Écrans | Unités | Jours | Prix HT |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|");
        foreach (var l in c.Lots) sb.AppendLine($"| {l.Numero:00} | {l.Theme} | {l.Ecrans.Count} | {N(l.UnitesFacturees)} | {N(Math.Round(l.JoursBas))}–{N(Math.Round(l.JoursHaut))} | {Eur(l.Prix)} |");
        sb.AppendLine($"| | **Total** | {c.Lots.Sum(l => l.Ecrans.Count)} | {N(c.Lots.Sum(l => l.UnitesFacturees))} | {N(Math.Round(c.JoursBas))}–{N(Math.Round(c.JoursHaut))} | **{Eur(c.Prix)}** |");
        sb.AppendLine();
        sb.AppendLine("## Écrans"); sb.AppendLine();
        sb.AppendLine("| Lot | Écran | Nature | Contrôles | Événements | Lignes | Classe | Unités | Points |");
        sb.AppendLine("|---:|---|---|---:|---:|---:|---|---:|---|");
        var lotDe = c.Lots.SelectMany(l => l.Ecrans.Select(e => (e, l.Numero))).ToDictionary(x => x.e, x => x.Numero);
        foreach (var e in c.Lots.SelectMany(l => l.Ecrans))
            sb.AppendLine($"| {lotDe[e]:00} | `{Court(e)}` | {Nature(e.Nature)} | {e.Controles} | {e.Evenements} | {e.LignesCodeBehind} | {e.Complexite} | {N(e.Unites)} | {string.Join(" ", e.Signaux)} |");
        sb.AppendLine();
        sb.AppendLine("## Points d'attention"); sb.AppendLine();
        foreach (var k in inv.Constats)
        {
            var (b, h) = Estimation.JoursChantier(k, inv);
            sb.AppendLine($"### {k.Code} — {k.Titre} ({Grav(k.Gravite).ToLowerInvariant()})");
            sb.AppendLine();
            sb.AppendLine($"{k.Pourquoi} **Solution :** {k.Solution}");
            sb.AppendLine();
            sb.AppendLine($"{Pluriel(k.Occurrences.Count(inv.OccurrenceAtteinte), "occurrence", "occurrences")} dans le périmètre · coût : {(k.JoursHaut == 0 ? "compté par écran" : $"{N(b)} à {N(h)} jours")}.");
            sb.AppendLine();
        }
        if (inv.RapportsCrystal.Count > 0)
        {
            var (n, bas, haut) = ForfaitCrystal(inv, c);
            sb.AppendLine($"## Rapports Crystal Reports ({inv.RapportsCrystal.Count}, dont {n} dans le périmètre)"); sb.AppendLine();
            sb.AppendLine($"Hors unités d'écran, au forfait par rapport : simple {Eur(c.Parametres.PrixRapportSimple)}, moyen {Eur(c.Parametres.PrixRapportMoyen)}, complexe {Eur(c.Parametres.PrixRapportComplexe)} HT, après un pilote de 3 rapports ({Eur(c.Parametres.PrixPiloteCrystal)} HT). Fourchette : {Eur(bas)} à {Eur(haut)} HT.");
            sb.AppendLine();
            sb.AppendLine("| Rapport | Projet | Ouvert par | Périmètre |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var r in inv.RapportsCrystal) sb.AppendLine($"| {r.Fichier} | {r.Projet} | {(r.OuvertPar.Count == 0 ? "—" : string.Join(", ", r.OuvertPar.Select(Path.GetFileName).Distinct()))} | {(r.Atteint ? "oui" : "à confirmer")} |");
            sb.AppendLine();
        }
        if (inv.PointsNet10.Count > 0)
        {
            sb.AppendLine("## Passage à .NET 10"); sb.AppendLine();
            sb.AppendLine(VerdictNet10(inv, false)); sb.AppendLine();
            sb.AppendLine("| Point | Portée | Projet | Détail | Copilot | Ce que nous faisons | Fichiers |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var x in inv.PointsNet10.OrderBy(x => x.Portee).ThenBy(x => x.Code, StringComparer.Ordinal))
            {
                var r = Net10.Regle(x.Code);
                sb.AppendLine($"| {x.Code} {r.Titre} | {Net10.NomPortee(x.Portee)} | {x.Projet} | {x.Detail} | {r.Copilot} | {r.Solution} | {(x.Fichiers.Count == 0 ? "—" : string.Join(", ", x.Fichiers.Take(5)) + (x.Fichiers.Count > 5 ? $" et {x.Fichiers.Count - 5} autres" : ""))} |");
            }
            sb.AppendLine();
        }
        sb.AppendLine("## Dépendances"); sb.AppendLine();
        sb.AppendLine("| Paquet | Version | Catégorie | Risque | Côté web |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var d in inv.Dependances) sb.AppendLine($"| {d.Nom} | {d.Version ?? "—"} | {d.Categorie} | {Grav(d.Risque)} | {d.Remplacement} |");
        if (c.HorsPerimetre.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"## Hors périmètre ({c.HorsPerimetre.Count} écrans, {N(c.UnitesHorsPerimetre)} unités)");
            sb.AppendLine();
            sb.AppendLine(string.Join(", ", c.HorsPerimetre.Select(e => $"`{Court(e)}`")));
        }
        return sb.ToString();
    }
}
