using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using CSS = Microsoft.CodeAnalysis.CSharp.Syntax;
using VBS = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace Winmig.Core;

/// <summary>Ce que le code du concepteur WinForms (InitializeComponent) dit d'un écran.</summary>
public sealed class LectureConcepteur
{
    public Dictionary<string, NoeudSchema> Noeuds { get; } = new(StringComparer.Ordinal);
    public List<(string Nom, string Type)> Crees { get; } = new();
    public NoeudSchema Racine { get; } = new() { Nom = "$this", Type = "Form" };
    public int Evenements { get; set; }
    public int Liaisons { get; set; }
    public string? Titre { get; set; }
    public string? StyleBordure { get; set; }
    public List<Occurrence> Minuteries { get; } = new();
}

/// <summary>
/// Lecture du code généré par le concepteur WinForms. Chaque instruction est isolée par Roslyn (C# ou VB.NET),
/// puis reconnue par des motifs communs aux deux langages : création, propriété, ajout à un conteneur, événement.
/// </summary>
public static class Concepteur
{
    const string Moi = @"(?:this\.|Me\.)?";
    static readonly RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    static readonly Regex Creation = new($"^{Moi}(\\w+)\\s*=\\s*new\\s+([\\w\\.]+)\\s*\\(", O);
    static readonly Regex Propriete = new($"^{Moi}(\\w+)\\.([\\w\\.]+)\\s*=\\s*(.+?);?$", O);
    static readonly Regex ProprieteRacine = new(@"^(?:this|Me)\.(\w+)\s*=\s*(.+?);?$", O);
    static readonly Regex Ajout = new($@"^(?:(?:this|Me)\.)?(?:([\w\.]+?)\.)?Controls\.Add\(\s*{Moi}(\w+)", O);
    static readonly Regex AjoutPlage = new(@"^(?:(?:this|Me)\.)?(?:([\w\.]+?)\.)?Controls\.AddRange\(", O);
    static readonly Regex Elements = new(@"^(?:(?:this|Me)\.)?(\w+)\.(Items|DropDownItems|Columns|TabPages|Nodes)\.AddRange\(", O);
    static readonly Regex MembreListe = new(@"(?:this|Me)\.(\w+)", O);
    static readonly Regex Evenement = new(@"(\+=|^AddHandler\s)", O);
    static readonly Regex Point = new(@"Point\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)", O);
    static readonly Regex Taille = new(@"Size\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)", O);
    static readonly Regex Enum = new(@"\.(\w+)\s*\)?$", O);
    static readonly Regex Chaine = new("^@?\"((?:[^\"\\\\]|\\\\.|\"\")*)\"$", O);
    static readonly Regex Ressource = new(@"resources\.ApplyResources\(\s*" + Moi + @"(\w+|this|Me)\s*,\s*""([^""]+)""", O);

    static IEnumerable<SyntaxNode> Instructions(SyntaxNode methode) => methode switch
    {
        CSS.MethodDeclarationSyntax cs => cs.Body!.Statements.Where(s => s is CSS.ExpressionStatementSyntax),
        VBS.MethodBlockSyntax vb => vb.Statements.Where(s => s is VBS.AssignmentStatementSyntax or VBS.ExpressionStatementSyntax or VBS.AddRemoveHandlerStatementSyntax or VBS.CallStatementSyntax),
        _ => Enumerable.Empty<SyntaxNode>(),
    };

    public static string Texte(string valeur)
    {
        var m = Chaine.Match(valeur.Trim());
        if (!m.Success) return "";
        return m.Groups[1].Value.Replace("\\\"", "\"").Replace("\"\"", "\"").Replace("\\\\", "\\").Replace("\\r", "").Replace("\\n", " ").Replace("\\t", " ");
    }

    public static LectureConcepteur Lire(SyntaxNode initializeComponent, ISet<string> champs, string? resx)
    {
        var l = new LectureConcepteur();
        l.Noeuds["$this"] = l.Racine;
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var ressources = new List<(string Cible, string Cle)>();

        NoeudSchema Noeud(string nom)
        {
            if (nom is "this" or "Me" or "$this") return l.Racine;
            if (!l.Noeuds.TryGetValue(nom, out var n))
            {
                // « split.Panel1 » : panneau d'un SplitContainer, créé par son parent.
                var point = nom.LastIndexOf('.');
                n = new NoeudSchema { Nom = nom, Type = point > 0 ? "SplitterPanel" : "Inconnu" };
                l.Noeuds[nom] = n;
                if (point > 0)
                {
                    var p = Noeud(nom[..point]);
                    if (!p.Enfants.Contains(n)) p.Enfants.Add(n);
                }
            }
            return n;
        }

        void Proprietes(NoeudSchema n, string prop, string valeur, int ligne)
        {
            switch (prop)
            {
                case "Location":
                    var pt = Point.Match(valeur);
                    if (pt.Success) { n.X = int.Parse(pt.Groups[1].Value); n.Y = int.Parse(pt.Groups[2].Value); }
                    break;
                case "Size" or "ClientSize":
                    var t = Taille.Match(valeur);
                    if (t.Success && (prop == "ClientSize" || n != l.Racine || n.L == 0)) { n.L = int.Parse(t.Groups[1].Value); n.H = int.Parse(t.Groups[2].Value); }
                    break;
                case "Dock":
                    var d = Enum.Match(valeur);
                    if (d.Success) n.Ancrage = d.Groups[1].Value;
                    break;
                case "Visible":
                    if (valeur.Trim().Equals("false", StringComparison.OrdinalIgnoreCase)) n.Visible = false;
                    break;
                case "Text" or "HeaderText":
                    var s = Texte(valeur);
                    if (s.Length > 0) n.Texte = Mnemonique(s);
                    break;
                case "Orientation":
                    if (valeur.Contains("Horizontal", StringComparison.OrdinalIgnoreCase)) n.Elements.Add("@horizontal");
                    break;
                case "SplitterDistance":
                    if (int.TryParse(valeur.Trim().TrimEnd(';'), out var sd)) n.Elements.Add("@split=" + sd);
                    break;
                case "DataSource" or "DataMember":
                    if (!valeur.Trim().Equals("null", StringComparison.OrdinalIgnoreCase) && !valeur.Trim().Equals("Nothing", StringComparison.OrdinalIgnoreCase)) l.Liaisons++;
                    break;
                case "FormBorderStyle":
                    if (n == l.Racine) l.StyleBordure = Enum.Match(valeur).Groups[1].Value;
                    break;
            }
        }

        foreach (var instr in Instructions(initializeComponent))
        {
            var brut = Regex.Replace(instr.ToString(), @"\s+", " ").Trim();
            var ligne = instr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            if (Evenement.IsMatch(brut)) { l.Evenements++; continue; }
            if (brut.Contains(".DataBindings.Add(", StringComparison.OrdinalIgnoreCase)) { l.Liaisons++; continue; }

            var res = Ressource.Match(brut);
            if (res.Success) { ressources.Add((res.Groups[1].Value, res.Groups[2].Value)); continue; }

            var c = Creation.Match(brut);
            if (c.Success && champs.Contains(c.Groups[1].Value))
            {
                var n = Noeud(c.Groups[1].Value);
                n.Type = c.Groups[2].Value;
                l.Crees.Add((n.Nom, n.Type));
                if (Sources.NomSimple(n.Type) == "Timer" && (n.Type.Contains("Forms", StringComparison.Ordinal) || !n.Type.Contains('.')))
                    l.Minuteries.Add(new Occurrence("", ligne, brut.Length > 140 ? brut[..140] : brut));
                continue;
            }

            var a = Ajout.Match(brut);
            if (a.Success && !brut.Contains(".AddRange(", StringComparison.Ordinal))
            {
                var parent = a.Groups[1].Success ? a.Groups[1].Value : "$this";
                var enfant = Noeud(a.Groups[2].Value);
                var p = Noeud(parent);
                if (!p.Enfants.Contains(enfant)) p.Enfants.Add(enfant);
                parents[enfant.Nom] = p.Nom;
                continue;
            }
            var ap = AjoutPlage.Match(brut);
            if (ap.Success)
            {
                var p = Noeud(ap.Groups[1].Success ? ap.Groups[1].Value : "$this");
                var liste = brut[brut.IndexOf('{')..];
                foreach (Match m in MembreListe.Matches(liste))
                {
                    var enfant = Noeud(m.Groups[1].Value);
                    if (!p.Enfants.Contains(enfant)) p.Enfants.Add(enfant);
                    parents[enfant.Nom] = p.Nom;
                }
                continue;
            }
            var el = Elements.Match(brut);
            if (el.Success && brut.Contains('{'))
            {
                var p = Noeud(el.Groups[1].Value);
                var liste = brut[brut.IndexOf('{')..];
                foreach (Match m in MembreListe.Matches(liste)) p.Elements.Add(m.Groups[1].Value);
                continue;
            }

            var pr = Propriete.Match(brut);
            if (pr.Success && (champs.Contains(pr.Groups[1].Value) || l.Noeuds.ContainsKey(pr.Groups[1].Value)))
            {
                Proprietes(Noeud(pr.Groups[1].Value), pr.Groups[2].Value, pr.Groups[3].Value, ligne);
                continue;
            }
            var rr = ProprieteRacine.Match(brut);
            if (rr.Success && !champs.Contains(rr.Groups[1].Value))
            {
                Proprietes(l.Racine, rr.Groups[1].Value, rr.Groups[2].Value, ligne);
                if (rr.Groups[1].Value == "Text") l.Titre = l.Racine.Texte;
            }
        }

        // Écran « localisable » : positions, tailles et textes rangés dans le .resx neutre.
        if (ressources.Count > 0 && resx != null && File.Exists(resx))
        {
            var valeurs = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                foreach (var d in XDocument.Load(resx).Root!.Elements("data"))
                {
                    var nom = (string?)d.Attribute("name");
                    var v = d.Element("value")?.Value;
                    if (nom != null && v != null) valeurs[nom] = v;
                }
            }
            catch (System.Xml.XmlException) { }
            foreach (var (cible, cle) in ressources)
            {
                var n = Noeud(cible);
                string? V(string p) => valeurs.TryGetValue($"{cle}.{p}", out var v) ? v : null;
                if (V("Location") is { } loc && TryPaire(loc, out var x, out var y)) { n.X = x; n.Y = y; }
                if ((n == l.Racine ? V("ClientSize") : V("Size")) is { } sz && TryPaire(sz, out var w, out var h)) { n.L = w; n.H = h; }
                if (V("Dock") is { } dk) n.Ancrage = dk;
                if (V("Text") is { Length: > 0 } tx) { n.Texte = Mnemonique(tx); if (n == l.Racine) l.Titre = n.Texte; }
                if (V("Visible") is "False") n.Visible = false;
            }
        }

        // Valeurs par défaut des barres : MenuStrip et ToolStrip en haut, StatusStrip en bas.
        foreach (var n in l.Noeuds.Values)
        {
            var simple = Sources.NomSimple(n.Type);
            if (n.Ancrage == "None" && simple is "MenuStrip" or "ToolStrip" or "MainMenu") n.Ancrage = "Top";
            if (n.Ancrage == "None" && simple is "StatusStrip" or "StatusBar") n.Ancrage = "Bottom";
            if (n.Type == "SplitterPanel") n.Ancrage = "Panel";
            if (simple == "TabPage") n.Ancrage = "Page";
        }
        return l;
    }

    /// <summary>Retire le préfixe de raccourci clavier (« &amp;Fichier ») ; « &amp;&amp; » est une esperluette.</summary>
    public static string Mnemonique(string s) => s.Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&");

    static bool TryPaire(string v, out int a, out int b)
    {
        a = b = 0;
        var p = v.Split(',', StringSplitOptions.TrimEntries);
        return p.Length == 2 && int.TryParse(p[0], out a) && int.TryParse(p[1], out b);
    }
}
