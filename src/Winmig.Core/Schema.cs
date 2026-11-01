using System.Globalization;
using System.Net;
using System.Text;

namespace Winmig.Core;

/// <summary>
/// Reconstitue la disposition d'un écran WinForms (positions et ancrages du concepteur)
/// et la dessine en SVG, sans exécuter l'application ni avoir besoin de Windows.
/// </summary>
public static class Schema
{
    const int Onglet = 20;

    /// <summary>Calcule la position absolue de chaque nœud, selon les règles d'ancrage de WinForms.</summary>
    public static void Disposer(NoeudSchema racine)
    {
        if (racine.L <= 0 || racine.H <= 0) { racine.L = racine.L > 0 ? racine.L : 640; racine.H = racine.H > 0 ? racine.H : 480; }
        racine.Ax = 0; racine.Ay = 0; racine.Al = racine.L; racine.Ah = racine.H;
        Placer(racine, 0);
    }

    static void Placer(NoeudSchema parent, int profondeur)
    {
        if (profondeur > 40) return;
        var simple = Sources.NomSimple(parent.Type);
        int x = parent.Ax, y = parent.Ay, l = parent.Al, h = parent.Ah;
        if (simple is "GroupBox") { y += 16; h -= 19; x += 3; l -= 6; }
        if (simple is "TabControl") { y += Onglet; h -= Onglet + 2; x += 2; l -= 4; }

        if (simple == "SplitContainer")
        {
            var horizontal = parent.Elements.Contains("@horizontal");
            var dist = parent.Elements.Where(e => e.StartsWith("@split=")).Select(e => int.Parse(e[7..])).DefaultIfEmpty(horizontal ? parent.Ah / 2 : parent.Al / 2).First();
            var panneaux = parent.Enfants.Where(e => e.Type == "SplitterPanel").OrderBy(e => e.Nom, StringComparer.Ordinal).ToList();
            for (var i = 0; i < panneaux.Count; i++)
            {
                var p = panneaux[i];
                if (horizontal) { p.Ax = x; p.Al = l; p.Ay = i == 0 ? y : y + dist + 4; p.Ah = i == 0 ? dist : h - dist - 4; }
                else { p.Ay = y; p.Ah = h; p.Ax = i == 0 ? x : x + dist + 4; p.Al = i == 0 ? dist : l - dist - 4; }
                Placer(p, profondeur + 1);
            }
            return;
        }

        // Ancrage : WinForms traite les contrôles du dernier ajouté au premier.
        var reste = (X: x, Y: y, L: l, H: h);
        var premierePage = true;
        foreach (var e in Enumerable.Reverse(parent.Enfants))
        {
            if (e.Type == "SplitterPanel") continue;
            switch (e.Ancrage)
            {
                case "Top": e.Ax = reste.X; e.Ay = reste.Y; e.Al = reste.L; e.Ah = Hauteur(e); reste.Y += e.Ah; reste.H -= e.Ah; break;
                case "Bottom": e.Ax = reste.X; e.Ah = Hauteur(e); e.Ay = reste.Y + reste.H - e.Ah; e.Al = reste.L; reste.H -= e.Ah; break;
                case "Left": e.Ax = reste.X; e.Ay = reste.Y; e.Al = Largeur(e); e.Ah = reste.H; reste.X += e.Al; reste.L -= e.Al; break;
                case "Right": e.Al = Largeur(e); e.Ax = reste.X + reste.L - e.Al; e.Ay = reste.Y; e.Ah = reste.H; reste.L -= e.Al; break;
                case "Fill": e.Ax = reste.X; e.Ay = reste.Y; e.Al = reste.L; e.Ah = reste.H; break;
                case "Page": e.Ax = x; e.Ay = y; e.Al = l; e.Ah = h; break;
                default: e.Ax = parent.Ax + e.X; e.Ay = parent.Ay + e.Y; e.Al = Math.Max(e.L, 1); e.Ah = Math.Max(e.H, 1); break;
            }
            e.Al = Math.Max(e.Al, 0); e.Ah = Math.Max(e.Ah, 0);
        }
        // Seul le premier onglet d'un TabControl est dessiné.
        foreach (var e in parent.Enfants)
        {
            if (e.Ancrage == "Page") { if (!premierePage) { e.Visible = false; continue; } premierePage = false; }
            Placer(e, profondeur + 1);
        }
    }

    static int Hauteur(NoeudSchema n) => n.H > 0 ? n.H : Sources.NomSimple(n.Type) switch { "MenuStrip" => 24, "StatusStrip" => 22, "ToolStrip" => 25, _ => 24 };
    static int Largeur(NoeudSchema n) => n.L > 0 ? n.L : 120;

    static string E(string? s) => Rapport.Html(s);
    static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>Résout un composant interne (nom simple) vers son propre schéma, pour le dessiner à sa place.</summary>
    public delegate (NoeudSchema Racine, IReadOnlyDictionary<string, NoeudSchema> Noeuds)? Resolveur(string nomSimple);

    /// <summary>Dessine le schéma. Couleurs de la charte : graphite, carter, relevé ; aucun signal (le schéma est neutre).</summary>
    public static string Svg(NoeudSchema racine, string? titre, IReadOnlyDictionary<string, NoeudSchema> noeuds, ISet<string> composantsInternes,
        Resolveur? composant = null, bool fenetre = true, double echelle = 0)
    {
        const string G = "#1B1E21", R = "#F3F4F5", Ca = "#9AA0A6";
        var s = new StringBuilder();
        var barre = fenetre ? 22 : 0;
        var taille = echelle > 0 ? string.Create(CultureInfo.InvariantCulture, $" width=\"{(racine.Al + 2) * echelle:0}\" height=\"{(racine.Ah + barre + 2) * echelle:0}\"") : "";
        s.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\"{taille} viewBox=\"-1 -{barre + 1} {racine.Al + 2} {racine.Ah + barre + 2}\" font-family=\"Atkinson Hyperlegible Next, Atkinson, Segoe UI, sans-serif\" font-size=\"11\">");
        if (fenetre)
        {
            s.Append(CultureInfo.InvariantCulture, $"<rect x=\"0\" y=\"-{barre}\" width=\"{racine.Al}\" height=\"{barre}\" fill=\"{G}\"/>");
            s.Append(CultureInfo.InvariantCulture, $"<text x=\"8\" y=\"-7\" fill=\"{R}\" font-size=\"11\">{E(Couper(titre ?? racine.Texte ?? "", racine.Al / 6.2))}</text>");
            s.Append(CultureInfo.InvariantCulture, $"<rect x=\"0\" y=\"0\" width=\"{racine.Al}\" height=\"{racine.Ah}\" fill=\"{R}\" stroke=\"{G}\" stroke-width=\"1.5\"/>");
        }
        else s.Append(CultureInfo.InvariantCulture, $"<rect x=\"0\" y=\"0\" width=\"{racine.Al}\" height=\"{racine.Ah}\" fill=\"{R}\" stroke=\"{Ca}\" stroke-width=\"1\"/>");
        Dessin(s, racine, noeuds, composantsInternes, composant, 0);
        if (!racine.Enfants.Any(e => e.Visible))
            s.Append(CultureInfo.InvariantCulture, $"<text x=\"{racine.Al / 2.0:0.#}\" y=\"{racine.Ah / 2.0 + 4:0.#}\" text-anchor=\"middle\" fill=\"#5C636A\" font-size=\"{Math.Clamp(racine.Al / 22.0, 9, 16):0.#}\">contenu créé par le code</text>");
        s.Append("</svg>");
        return s.ToString();
    }

    static void Dessin(StringBuilder s, NoeudSchema racine, IReadOnlyDictionary<string, NoeudSchema> noeuds, ISet<string> composantsInternes, Resolveur? composant, int profondeurComposant)
    {
        const string G = "#1B1E21", Ca = "#9AA0A6", Gr = "#5C636A", R = "#F3F4F5", P = "#D4D7DA", B = "#FFFFFF";
        void Texte(double x, double y, string? t, double largeur, string couleur = G, string ancre = "start", int taille = 11)
        {
            if (string.IsNullOrWhiteSpace(t) || largeur < 12) return;
            s.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(x)}\" y=\"{F(y)}\" fill=\"{couleur}\" font-size=\"{taille}\" text-anchor=\"{ancre}\">{E(Couper(t, largeur / (taille * 0.56)))}</text>");
        }

        void Dessiner(NoeudSchema n, int profondeur)
        {
            if (!n.Visible || profondeur > 40 || n.Al < 2 || n.Ah < 2) return;
            // Hors de la zone visible de l'écran : ignoré.
            if (n.Ax >= racine.Al || n.Ay >= racine.Ah) return;
            var t = Sources.NomSimple(n.Type);
            int x = n.Ax, y = n.Ay, l = Math.Min(n.Al, racine.Al - n.Ax), h = Math.Min(n.Ah, racine.Ah - n.Ay);
            string Rect(string fond, string trait, double ep = 1, string extra = "") =>
                $"<rect x=\"{x + 0.5}\" y=\"{y + 0.5}\" width=\"{Math.Max(l - 1, 1)}\" height=\"{Math.Max(h - 1, 1)}\" fill=\"{fond}\" stroke=\"{trait}\" stroke-width=\"{F(ep)}\" {extra}/>";
            var interne = composantsInternes.Contains(t);
            if (interne)
            {
                var sous = profondeurComposant < 3 ? composant?.Invoke(t) : null;
                if (sous != null && sous.Value.Racine.Al > 0 && sous.Value.Racine.Ah > 0 && l > 30 && h > 20)
                {
                    // Le composant est redisposé à la taille de sa place, à l'échelle 1, puis remis à sa propre taille.
                    var r = sous.Value.Racine;
                    var (l0, h0) = (r.L, r.H);
                    r.L = l; r.H = h; Disposer(r);
                    s.Append(CultureInfo.InvariantCulture, $"<svg x=\"{x}\" y=\"{y}\" width=\"{l}\" height=\"{h}\" viewBox=\"0 0 {l} {h}\" overflow=\"hidden\">");
                    Dessin(s, r, sous.Value.Noeuds, composantsInternes, composant, profondeurComposant + 1);
                    s.Append("</svg>");
                    r.L = l0; r.H = h0; Disposer(r);
                    s.Append(Rect("none", G, 1, "stroke-dasharray=\"4 3\""));
                }
                else s.Append(Rect(R, G, 1, "stroke-dasharray=\"4 3\""));
                s.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + 4}\" y=\"{y + 4}\" width=\"{Math.Min(l - 8, t.Length * 6.6 + 10):0}\" height=\"16\" fill=\"{G}\"/>");
                Texte(x + 9, y + 15.5, t, l - 14, R, "start", 10);
                return;
            }
            switch (t)
            {
                case "MenuStrip" or "MainMenu":
                    s.Append(Rect(P, P));
                    var mx = x + 6.0;
                    foreach (var it in n.Elements.Where(e => !e.StartsWith('@')))
                    {
                        var tx = it.StartsWith('=') ? it[1..] : noeuds.TryGetValue(it, out var mi) ? mi.Texte : it;
                        if (string.IsNullOrEmpty(tx)) continue;
                        Texte(mx, y + h / 2.0 + 4, tx, l - (mx - x));
                        mx += tx.Length * 6.4 + 16;
                    }
                    return;
                case "ToolStrip" or "BindingNavigator":
                    s.Append(Rect(P, P));
                    var n2 = Math.Min(n.Elements.Count(e => !e.StartsWith('@')), Math.Max(0, (l - 8) / 26));
                    for (var i = 0; i < n2; i++)
                        s.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + 6 + i * 26}\" y=\"{y + 4}\" width=\"18\" height=\"{Math.Max(h - 8, 4)}\" fill=\"{R}\" stroke=\"{Ca}\"/>");
                    return;
                case "StatusStrip" or "StatusBar":
                    s.Append(Rect(P, P));
                    return;
                case "Button":
                    s.Append(Rect(P, G));
                    Texte(x + l / 2.0, y + h / 2.0 + 4, n.Texte, l - 6, G, "middle");
                    return;
                case "TextBox" or "MaskedTextBox" or "RichTextBox" or "NumericUpDown" or "DateTimePicker" or "ComboBox" or "DomainUpDown":
                    s.Append(Rect(B, Ca));
                    if (t is "ComboBox" or "DateTimePicker" or "NumericUpDown" or "DomainUpDown")
                        s.Append(CultureInfo.InvariantCulture, $"<path d=\"M{x + l - 13} {y + h / 2.0 - 2} l4 4 l4 -4\" fill=\"none\" stroke=\"{G}\"/>");
                    if (t == "RichTextBox" || (t == "TextBox" && h > 30))
                        for (var ly = y + 10; ly < y + h - 6 && ly < y + 70; ly += 9)
                            s.Append(CultureInfo.InvariantCulture, $"<line x1=\"{x + 6}\" y1=\"{ly}\" x2=\"{x + Math.Min(l - 6, 6 + (l - 12) * (ly % 3 == 0 ? 0.8 : 0.6))}\" y2=\"{ly}\" stroke=\"{P}\" stroke-width=\"3\"/>");
                    return;
                case "Label" or "LinkLabel":
                    Texte(x, y + Math.Min(h, 16) - 3, n.Texte ?? n.Nom, Math.Max(l, 60), t == "LinkLabel" ? Gr : G);
                    return;
                case "CheckBox" or "RadioButton":
                    if (t == "CheckBox") s.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + 0.5}\" y=\"{y + h / 2.0 - 5.5}\" width=\"11\" height=\"11\" fill=\"{B}\" stroke=\"{G}\"/>");
                    else s.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{x + 6}\" cy=\"{y + h / 2.0}\" r=\"5.5\" fill=\"{B}\" stroke=\"{G}\"/>");
                    Texte(x + 16, y + h / 2.0 + 4, n.Texte, l - 16);
                    return;
                case "DataGridView" or "DataGrid" or "ListView" or "ListBox" or "CheckedListBox" or "PropertyGrid" or "GridControl" or "UltraGrid" or "RadGridView" or "ObjectListView" or "C1FlexGrid":
                    s.Append(Rect(B, Ca));
                    var entete = t is "ListBox" or "CheckedListBox" ? 0 : 20;
                    if (entete > 0)
                    {
                        s.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + 1}\" y=\"{y + 1}\" width=\"{Math.Max(l - 2, 1)}\" height=\"{entete}\" fill=\"{P}\"/>");
                        var cols = n.Elements.Where(e => !e.StartsWith('@')).Select(c => c.StartsWith('=') ? c[1..] : noeuds.TryGetValue(c, out var cn) ? cn.Texte ?? "" : "").ToList();
                        if (cols.Count == 0) cols = new List<string> { "", "", "" };
                        var lc = (l - 2.0) / cols.Count;
                        for (var i = 0; i < cols.Count; i++)
                        {
                            if (i > 0) s.Append(CultureInfo.InvariantCulture, $"<line x1=\"{F(x + 1 + i * lc)}\" y1=\"{y + 1}\" x2=\"{F(x + 1 + i * lc)}\" y2=\"{y + h - 1}\" stroke=\"{P}\"/>");
                            Texte(x + 6 + i * lc, y + 14, cols[i], lc - 8, G, "start", 10);
                        }
                    }
                    for (var ly = y + entete + 18; ly < y + h - 2; ly += 18)
                        s.Append(CultureInfo.InvariantCulture, $"<line x1=\"{x + 1}\" y1=\"{ly}\" x2=\"{x + l - 1}\" y2=\"{ly}\" stroke=\"{P}\"/>");
                    return;
                case "TreeView" or "TreeList" or "RadTreeView" or "UltraTree":
                    s.Append(Rect(B, Ca));
                    var k = 0;
                    for (var ly = y + 14; ly < y + h - 6; ly += 17, k++)
                    {
                        var ind = new[] { 0, 14, 28, 28, 14, 28, 0 }[k % 7];
                        s.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + 8 + ind}\" y=\"{ly - 4}\" width=\"8\" height=\"8\" fill=\"none\" stroke=\"{Ca}\"/>");
                        s.Append(CultureInfo.InvariantCulture, $"<line x1=\"{x + 22 + ind}\" y1=\"{ly}\" x2=\"{x + Math.Min(l - 8, 22 + ind + 60 + (k * 37 % 50))}\" y2=\"{ly}\" stroke=\"{P}\" stroke-width=\"4\"/>");
                    }
                    return;
                case "GroupBox":
                    s.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + 0.5}\" y=\"{y + 7.5}\" width=\"{Math.Max(l - 1, 1)}\" height=\"{Math.Max(h - 8, 1)}\" fill=\"none\" stroke=\"{Ca}\"/>");
                    if (!string.IsNullOrEmpty(n.Texte))
                    {
                        var lt = Math.Min(n.Texte.Length * 6.2 + 8, l - 12);
                        s.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + 6}\" y=\"{y}\" width=\"{F(lt)}\" height=\"14\" fill=\"{R}\"/>");
                        Texte(x + 9, y + 11, n.Texte, lt);
                    }
                    break;
                case "TabControl":
                    var pages = n.Enfants.Where(e => e.Ancrage == "Page").ToList();
                    var ox = x + 2.0;
                    for (var i = 0; i < Math.Max(pages.Count, 1); i++)
                    {
                        var tt = i < pages.Count ? pages[i].Texte ?? pages[i].Nom : "";
                        var lw = Math.Max(tt.Length * 6.2 + 16, 40);
                        if (ox + lw > x + l) break;
                        s.Append(CultureInfo.InvariantCulture, $"<rect x=\"{F(ox)}\" y=\"{y + 0.5}\" width=\"{F(lw)}\" height=\"{Onglet}\" fill=\"{(i == 0 ? R : P)}\" stroke=\"{Ca}\"/>");
                        Texte(ox + 8, y + 14, tt, lw - 10);
                        ox += lw;
                    }
                    s.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + 0.5}\" y=\"{y + Onglet + 0.5}\" width=\"{Math.Max(l - 1, 1)}\" height=\"{Math.Max(h - Onglet - 1, 1)}\" fill=\"{R}\" stroke=\"{Ca}\"/>");
                    break;
                case "PictureBox":
                    s.Append(Rect(P, Ca));
                    s.Append(CultureInfo.InvariantCulture, $"<path d=\"M{x} {y} L{x + l} {y + h} M{x + l} {y} L{x} {y + h}\" stroke=\"{Ca}\"/>");
                    return;
                case "Chart" or "ZedGraphControl" or "PlotView" or "FormsPlot" or "CartesianChart" or "TChart":
                    s.Append(Rect(B, Ca));
                    s.Append(CultureInfo.InvariantCulture, $"<path d=\"M{x + 12} {y + 8} V{y + h - 12} H{x + l - 8}\" fill=\"none\" stroke=\"{G}\"/>");
                    var pts = Enumerable.Range(0, 9).Select(i => $"{F(x + 14 + i * (l - 26) / 8.0)},{F(y + h - 16 - (h - 30) * (0.3 + 0.35 * Math.Sin(i * 0.9) * Math.Sin(i * 0.35 + 1)))}");
                    s.Append(CultureInfo.InvariantCulture, $"<polyline points=\"{string.Join(' ', pts)}\" fill=\"none\" stroke=\"{Gr}\" stroke-width=\"1.5\"/>");
                    return;
                case "ProgressBar" or "TrackBar":
                    s.Append(Rect(B, Ca));
                    s.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + 2}\" y=\"{y + 2}\" width=\"{Math.Max((l - 4) * 0.45, 1):0}\" height=\"{Math.Max(h - 4, 1)}\" fill=\"{Ca}\"/>");
                    return;
                case "Panel" or "FlowLayoutPanel" or "TableLayoutPanel" or "SplitContainer" or "SplitterPanel" or "TabPage" or "UserControl":
                    if (t == "SplitContainer") s.Append(Rect(P, P));
                    else if (t == "SplitterPanel") s.Append(Rect(R, Ca, 0.6));
                    break;
                case "WebBrowser" or "WebView2" or "ChromiumWebBrowser":
                    s.Append(Rect(B, Ca));
                    Texte(x + l / 2.0, y + h / 2.0 + 4, "contenu web", l - 8, Gr, "middle");
                    return;
                case "Splitter" or "ToolTip" or "ImageList" or "Timer" or "ErrorProvider" or "BindingSource" or "ContextMenuStrip" or "HelpProvider":
                    return;
                default:
                    s.Append(Rect(R, Ca));
                    Texte(x + 5, y + Math.Min(h - 4, 14), t, l - 8, Gr, "start", 10);
                    return;
            }
            foreach (var e in n.Enfants) Dessiner(e, profondeur + 1);
        }

        foreach (var e in racine.Enfants) Dessiner(e, 0);
    }

    static string Couper(string t, double max)
    {
        t = t.Replace('\n', ' ').Trim();
        var n = (int)Math.Max(max, 1);
        return t.Length <= n ? t : t[..Math.Max(n - 1, 1)].TrimEnd() + "…";
    }
}
