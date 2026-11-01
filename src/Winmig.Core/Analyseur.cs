using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using CS = Microsoft.CodeAnalysis.CSharp;
using CSS = Microsoft.CodeAnalysis.CSharp.Syntax;
using VB = Microsoft.CodeAnalysis.VisualBasic;
using VBS = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace Winmig.Core;

/// <summary>Point d'entrée : lit les projets, reconnaît les écrans, relève les dépendances et les API propres à Windows.</summary>
public static class Analyseur
{
    static readonly HashSet<string> RacinesWinForms = new(StringComparer.Ordinal)
    {
        "Form", "UserControl", "Control", "ContainerControl", "ScrollableControl", "Panel", "TextBox", "TextBoxBase", "RichTextBox", "Button",
        "Label", "ComboBox", "ListBox", "ListView", "TreeView", "DataGridView", "PictureBox", "TabControl", "GroupBox", "CheckBox",
        "ToolStrip", "MenuStrip", "StatusStrip", "NumericUpDown", "SplitContainer", "FlowLayoutPanel", "TableLayoutPanel", "ProgressBar", "TrackBar",
    };
    static readonly HashSet<string> RacinesWpf = new(StringComparer.Ordinal)
    {
        "Window", "Page", "UserControl", "Control", "ContentControl", "ItemsControl", "Panel", "FrameworkElement", "Decorator", "Selector",
        "TextBox", "Button", "ListBox", "ListView", "TreeView", "DataGrid", "ComboBox", "Canvas", "Grid", "StackPanel", "HeaderedContentControl",
    };
    static readonly HashSet<string> NonVisuels = new(StringComparer.Ordinal)
    {
        "Container", "ToolTip", "ImageList", "Timer", "ErrorProvider", "BindingSource", "HelpProvider", "BackgroundWorker", "ComponentResourceManager",
        "DataSet", "FileSystemWatcher", "EventLog", "PerformanceCounter", "Process", "SerialPort", "ServiceController", "NotifyIcon", "DataGridViewCellStyle",
    };

    public static Inventaire Analyser(string entree, Action<string>? journal = null)
    {
        var avert = new List<string>();
        var chemins = Projets.Decouvrir(entree, avert);
        if (chemins.Count == 0) throw new InvalidOperationException("Aucun projet .csproj ou .vbproj trouvé.");
        var dossiersProjets = chemins.Select(c => Path.GetDirectoryName(c)!).ToList();
        var racine = Commun(dossiersProjets.Append(Directory.Exists(entree) ? Path.GetFullPath(entree) : Path.GetDirectoryName(Path.GetFullPath(entree))!));
        string Rel(string p) => Path.GetRelativePath(racine, p).Replace('\\', '/');

        var inv = new Inventaire { Racine = racine, Entree = Rel(Path.GetFullPath(entree)) };
        inv.Avertissements.AddRange(avert);
        var fichiers = new List<FichierSource>();
        var xamls = new List<(string Chemin, string Projet)>();
        foreach (var c in chemins)
        {
            Projet p;
            // Deux projets du même nom (dossiers différents) : le second prend le nom de son dossier parent.
            var nomProjet = Path.GetFileNameWithoutExtension(c);
            if (inv.Projets.Any(x => string.Equals(x.Nom, nomProjet, StringComparison.OrdinalIgnoreCase)))
            {
                var parent = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(c)) ?? "");
                nomProjet = $"{nomProjet} ({parent})";
                for (var k = 2; inv.Projets.Any(x => string.Equals(x.Nom, nomProjet, StringComparison.OrdinalIgnoreCase)); k++) nomProjet = $"{Path.GetFileNameWithoutExtension(c)} ({k})";
            }
            try { p = Projets.Lire(c, nomProjet); }
            catch (System.Xml.XmlException ex) { inv.Avertissements.Add($"Projet illisible {Rel(c)} : {ex.Message}"); continue; }
            journal?.Invoke($"projet {p.Nom}");
            inv.Projets.Add(p);
            var sources = Projets.FichiersSource(p, dossiersProjets, ".cs").Concat(Projets.FichiersSource(p, dossiersProjets, ".vb"))
                .Where(f => !Regex.IsMatch(Path.GetFileName(f), @"\.(g|g\.i|AssemblyAttributes)\.(cs|vb)$", RegexOptions.IgnoreCase)).ToList();
            foreach (var s in sources)
            {
                try
                {
                    var fs = Sources.Lire(s, p.Nom);
                    fichiers.Add(fs);
                    p.LignesCode += Projets.LignesUtiles(fs.Texte, fs.Vb);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { inv.Avertissements.Add($"Fichier illisible : {Rel(s)}"); }
            }
            p.FichiersCs = sources.Count;
            var x = Projets.FichiersSource(p, dossiersProjets, ".xaml");
            p.FichiersXaml = x.Count;
            xamls.AddRange(x.Select(f => (f, p.Nom)));
            var resx = Directory.EnumerateFiles(Path.GetDirectoryName(c)!, "*.resx", SearchOption.AllDirectories).Where(f => !Projets.Ignore(Path.GetRelativePath(Path.GetDirectoryName(c)!, f))).ToList();
            p.FichiersResx = resx.Count;
            p.Langues.AddRange(resx.Select(f => Regex.Match(Path.GetFileNameWithoutExtension(f), @"\.([a-z]{2}(-[A-Z]{2})?)$")).Where(m => m.Success).Select(m => m.Groups[1].Value).Distinct().OrderBy(l => l));
            foreach (var rpt in Projets.FichiersSousProjet(Path.GetDirectoryName(c)!, dossiersProjets, "*.rpt"))
                if (!inv.RapportsCrystal.Any(r => r.Fichier == Rel(rpt)))
                    inv.RapportsCrystal.Add(new RapportCrystal { Fichier = Rel(rpt), Projet = p.Nom, Octets = new FileInfo(rpt).Length });
            if (p.Techno == Techno.Aucune && x.Count > 0 && sources.Count > 0 && fichiers.Any(f => f.Projet == p.Nom && f.Usings.Any(u => u.StartsWith("System.Windows.Controls", StringComparison.Ordinal)))) p.Techno = Techno.Wpf;
        }

        // Index des classes (parties fusionnées par nom complet).
        var classes = fichiers.SelectMany(f => f.Classes).GroupBy(c => c.NomComplet).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var parNomSimple = classes.Keys.GroupBy(k => Sources.NomSimple(k)).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var projetParNom = inv.Projets.ToDictionary(p => p.Nom, StringComparer.OrdinalIgnoreCase);

        // Fichiers XAML : x:Class → lecture.
        var espacesLocaux = classes.Values.Select(v => v[0].EspaceNoms).Concat(inv.Projets.Select(p => p.Nom)).Distinct().ToList();
        var lecturesXaml = new Dictionary<string, LectureXaml>(StringComparer.Ordinal);
        foreach (var (cx, proj) in xamls)
        {
            var lx = Xaml.Lire(cx, espacesLocaux);
            if (lx.Classe != null && !lx.Dictionnaire) lecturesXaml[lx.Classe] = lx;
        }

        (Techno Techno, string Racine)? Ui(string nomComplet, int profondeur)
        {
            if (profondeur > 12 || !classes.TryGetValue(nomComplet, out var parties)) return null;
            if (lecturesXaml.TryGetValue(nomComplet, out var lx)) return (Techno.Wpf, RacinesWpf.Contains(lx.Racine) ? lx.Racine : Ui(Resoudre(lx.Racine, parties[0]) ?? "", profondeur + 1)?.Racine ?? "UserControl");
            foreach (var p in parties)
                foreach (var b in p.Bases)
                {
                    var simple = Sources.NomSimple(b);
                    var interne = Resoudre(b, p);
                    if (interne != null && interne != nomComplet)
                    {
                        var r = Ui(interne, profondeur + 1);
                        if (r != null) return r;
                        continue;
                    }
                    var usings = parties.SelectMany(x => x.Fichier.Usings).ToList();
                    bool wf = b.Contains("Windows.Forms", StringComparison.Ordinal) || (!b.Contains('.') && usings.Contains("System.Windows.Forms"));
                    bool wpf = b.Contains("System.Windows.Controls", StringComparison.Ordinal) || b is "System.Windows.Window" || (!b.Contains('.') && usings.Any(u => u is "System.Windows.Controls" or "System.Windows"));
                    if (simple == "Form" && !wpf) return (Techno.WinForms, "Form");
                    if (simple is "Window" or "Page" && wpf) return (Techno.Wpf, simple);
                    if (wf && RacinesWinForms.Contains(simple)) return (Techno.WinForms, simple);
                    if (wpf && RacinesWpf.Contains(simple)) return (Techno.Wpf, simple);
                    var tp = projetParNom.GetValueOrDefault(p.Fichier.Projet)?.Techno;
                    if (!b.Contains('.') && tp == Techno.WinForms && RacinesWinForms.Contains(simple)) return (Techno.WinForms, simple);
                    if (!b.Contains('.') && tp == Techno.Wpf && RacinesWpf.Contains(simple)) return (Techno.Wpf, simple);
                }
            return null;
        }

        string? Resoudre(string type, PartieClasse contexte)
        {
            var simple = Sources.NomSimple(type);
            if (type.Contains('.') && classes.ContainsKey(type)) return type;
            if (!parNomSimple.TryGetValue(simple, out var candidats)) return null;
            if (type.Contains('.') && !candidats.Any(c => c.EndsWith(type, StringComparison.Ordinal))) return null;
            return candidats.FirstOrDefault(c => c == $"{contexte.EspaceNoms}.{simple}")
                ?? candidats.FirstOrDefault(c => contexte.Fichier.Usings.Any(u => c == $"{u}.{simple}"))
                ?? (candidats.Count == 1 ? candidats[0] : null);
        }

        // Écrans.
        var fichiersEcran = new Dictionary<string, List<Ecran>>(StringComparer.Ordinal);
        void Rattacher(string fichier, Ecran e)
        {
            if (!fichiersEcran.TryGetValue(fichier, out var l)) fichiersEcran[fichier] = l = new List<Ecran>();
            if (!l.Contains(e)) l.Add(e);
        }
        var minuteries = new List<Occurrence>();
        var activeX = new List<Occurrence>();
        var signauxXaml = new List<(string Code, Occurrence Occ, Ecran E)>();
        var nomsEcrans = new HashSet<string>(StringComparer.Ordinal);
        var ecranParClasse = new Dictionary<string, Ecran>(StringComparer.Ordinal);

        foreach (var (nom, parties) in classes.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var ui = Ui(nom, 0);
            if (ui == null) continue;
            var (techno, rac) = ui.Value;
            var p0 = parties[0];
            var e = new Ecran
            {
                Id = $"{p0.Fichier.Projet}/{nom}",
                Classe = nom,
                Projet = p0.Fichier.Projet,
                Techno = techno,
                TypeBase = rac,
                Nature = rac switch { "Form" or "Window" => NatureEcran.Fenetre, "Page" => NatureEcran.Page, _ => NatureEcran.Composant },
            };
            foreach (var p in parties.OrderBy(p => p.Fichier.Concepteur ? 1 : 0).ThenBy(p => p.Fichier.Chemin, StringComparer.Ordinal))
            {
                if (!e.Fichiers.Contains(Rel(p.Fichier.Chemin))) e.Fichiers.Add(Rel(p.Fichier.Chemin));
                Rattacher(p.Fichier.Chemin, e);
            }
            foreach (var p in parties.Where(p => !p.Fichier.Concepteur || p.InitializeComponent == null))
            {
                var (lignes, methodes, branches, handles) = Sources.Mesurer(p);
                e.LignesCodeBehind += lignes; e.Methodes += methodes; e.Branches += branches; e.Evenements += handles;
            }

            if (techno == Techno.WinForms)
            {
                var concepteur = parties.FirstOrDefault(p => p.InitializeComponent != null && p.Fichier.Concepteur) ?? parties.FirstOrDefault(p => p.InitializeComponent != null);
                if (concepteur != null)
                {
                    var champs = parties.SelectMany(p => p.Champs).ToHashSet(StringComparer.Ordinal);
                    var baseNom = Regex.Replace(concepteur.Fichier.Chemin, @"\.Designer\.(cs|vb)$|\.(cs|vb)$", "", RegexOptions.IgnoreCase);
                    var lc = Concepteur.Lire(concepteur.InitializeComponent!, champs, baseNom + ".resx");
                    var usings = concepteur.Fichier.Usings.Concat(parties.SelectMany(p => p.Fichier.Usings)).Distinct().ToList();
                    foreach (var (_, type) in lc.Crees)
                    {
                        var simple = Sources.NomSimple(type);
                        if (NonVisuels.Contains(simple) || simple.StartsWith("DataGridView", StringComparison.Ordinal) && simple.EndsWith("Column", StringComparison.Ordinal) || simple == "ColumnHeader") continue;
                        e.Controles++;
                        e.TypesControles[simple] = e.TypesControles.GetValueOrDefault(simple) + 1;
                        var famille = Catalogue.FamilleControle(type) ?? (type.Contains('.') ? null
                            : usings.Select(Catalogue.FamilleControle).FirstOrDefault(f => f != null));
                        if (famille != null && !type.StartsWith("System.", StringComparison.Ordinal) && !RacinesWinForms.Contains(simple))
                            e.ControlesTiers.Add($"{famille.Famille} : {simple}");
                        var interne = Resoudre(type, concepteur);
                        if (interne != null && interne != nom) e.ComposantsInternes.Add(interne);
                        if (simple.StartsWith("Ax", StringComparison.Ordinal) && (type.StartsWith("Ax", StringComparison.Ordinal) || type.Contains(".Ax", StringComparison.Ordinal)) && interne == null)
                            activeX.Add(new Occurrence(Rel(concepteur.Fichier.Chemin), 0, type));
                    }
                    foreach (var m in lc.Minuteries) minuteries.Add(m with { Fichier = Rel(concepteur.Fichier.Chemin) });
                    e.Evenements += lc.Evenements;
                    e.Liaisons += lc.Liaisons;
                    e.Titre = lc.Titre;
                    lc.Racine.Type = rac;
                    if (lc.Racine.L == 0 && lc.Racine.H == 0 && rac != "Form") { lc.Racine.L = 400; lc.Racine.H = 300; }
                    Schema.Disposer(lc.Racine);
                    e.Schema = lc.Racine;
                    e.LargeurConception = lc.Racine.Al;
                    e.HauteurConception = lc.Racine.Ah;
                    e.SchemaNoeuds = lc.Noeuds;
                    if (rac == "Form" && (lc.StyleBordure is "FixedDialog" || Regex.IsMatch(Sources.NomSimple(nom), "(Dlg|Dialog|Box|Prompt)$")))
                        e.Nature = NatureEcran.Dialogue;
                }
            }
            else if (lecturesXaml.TryGetValue(nom, out var lx))
            {
                e.Fichiers.Insert(0, Rel(lx.Chemin));
                e.Controles = lx.Controles;
                foreach (var (t, n) in lx.Types) e.TypesControles[t] = n;
                e.ControlesTiers.AddRange(lx.Tiers);
                e.Evenements += lx.Evenements;
                e.Liaisons += lx.Liaisons;
                e.Titre = lx.Titre;
                foreach (var loc in lx.Locaux.Distinct())
                {
                    var interne = Resoudre(loc, p0);
                    if (interne != null && interne != nom) e.ComposantsInternes.Add(interne);
                }
                foreach (var (code, occ) in lx.Signaux) signauxXaml.Add((code, occ with { Fichier = Rel(occ.Fichier) }, e));
                if (lx.Schema != null)
                {
                    e.Schema = lx.Schema;
                    e.LargeurConception = lx.Largeur;
                    e.HauteurConception = lx.Hauteur;
                    e.SchemaNoeuds = new Dictionary<string, NoeudSchema>();
                }
                if (rac == "Window" && Regex.IsMatch(Sources.NomSimple(nom), "(Dlg|Dialog|Box|Prompt)$")) e.Nature = NatureEcran.Dialogue;
                // ViewModel associé par convention de nom : sa logique fait partie du travail sur l'écran.
                var court = Regex.Replace(Sources.NomSimple(nom), "(View|Window|Page|Control|Vue|Fenetre|Ecran|Controle)$", "");
                var vm = new[] { Sources.NomSimple(nom) + "ViewModel", court + "ViewModel", court + "Vm", court + "VM", court + "VueModele", court + "Modele" }
                    .Where(parNomSimple.ContainsKey).SelectMany(n => parNomSimple[n]).FirstOrDefault();
                if (vm != null)
                {
                    e.Mvvm = true;
                    foreach (var p in classes[vm])
                    {
                        var (lignes, methodes, branches, _) = Sources.Mesurer(p);
                        e.LignesCodeBehind += lignes; e.Methodes += methodes; e.Branches += branches;
                        if (!e.Fichiers.Contains(Rel(p.Fichier.Chemin))) e.Fichiers.Add(Rel(p.Fichier.Chemin));
                        Rattacher(p.Fichier.Chemin, e);
                    }
                }
                if (e.Liaisons > e.Evenements && e.Liaisons >= 3) e.Mvvm = true;
            }
            e.ComposantsInternes.Sort(StringComparer.Ordinal);
            var dedoublonnes = e.ComposantsInternes.Distinct().ToList();
            e.ComposantsInternes.Clear(); e.ComposantsInternes.AddRange(dedoublonnes);
            var tiers = e.ControlesTiers.GroupBy(t => t).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key).OrderBy(t => t).ToList();
            e.ControlesTiers.Clear(); e.ControlesTiers.AddRange(tiers);
            inv.Ecrans.Add(e);
            ecranParClasse[nom] = e;
            nomsEcrans.Add(Sources.NomSimple(nom));
        }
        foreach (var e in inv.Ecrans)
            foreach (var c in e.ComposantsInternes)
                if (ecranParClasse.TryGetValue(c, out var ce) && !ce.UtilisePar.Contains(e.Id)) ce.UtilisePar.Add(e.Id);

        Perimetre(inv, fichiers, classes, parNomSimple, ecranParClasse, xamls, Resoudre, Rel);

        // Constats : API propres au poste Windows.
        var constats = new Dictionary<string, Constat>(StringComparer.Ordinal);
        void Ajouter(string code, Occurrence occ, IEnumerable<Ecran>? ecrans)
        {
            var r = Catalogue.Regle(code);
            if (!constats.TryGetValue(code, out var c))
                constats[code] = c = new Constat { Code = r.Code, Titre = r.Titre, Gravite = r.Gravite, Pourquoi = r.Pourquoi, Solution = r.Solution, JoursBas = r.JoursBas, JoursHaut = r.JoursHaut };
            if (!c.Occurrences.Any(o => o.Fichier == occ.Fichier && o.Ligne == occ.Ligne)) c.Occurrences.Add(occ);
            if (ecrans != null) foreach (var e in ecrans) { c.Ecrans.Add(e.Id); if (!e.Signaux.Contains(code)) e.Signaux.Add(code); }
        }
        foreach (var f in fichiers)
        {
            var ecrans = fichiersEcran.GetValueOrDefault(f.Chemin);
            foreach (var (code, ligne, extrait) in Scanner(f, ecrans != null))
                Ajouter(code, new Occurrence(Rel(f.Chemin), ligne, extrait), ecrans);
        }
        foreach (var m in minuteries) Ajouter("W08", m, inv.Ecrans.Where(e => e.Fichiers.Contains(m.Fichier)));
        foreach (var a in activeX) Ajouter("W03", a, inv.Ecrans.Where(e => e.Fichiers.Contains(a.Fichier)));
        foreach (var (code, occ, e) in signauxXaml) Ajouter(code, occ, new[] { e });
        foreach (var p in inv.Projets)
            foreach (var com in p.ReferencesCom) Ajouter("W03", new Occurrence(Rel(p.Chemin), 0, $"COMReference {com}"), null);
        foreach (var e in inv.Ecrans) e.Signaux.Sort(StringComparer.Ordinal);
        inv.Constats.AddRange(constats.Values.OrderBy(c => c.Gravite == Gravite.Elevee ? 0 : c.Gravite == Gravite.Moyenne ? 1 : 2).ThenBy(c => c.Code, StringComparer.Ordinal));
        foreach (var c in inv.Constats) c.Occurrences.Sort((a, b) => string.CompareOrdinal(a.Fichier, b.Fichier) is var o && o != 0 ? o : a.Ligne.CompareTo(b.Ligne));

        // Dépendances.
        var deps = new Dictionary<string, Dependance>(StringComparer.OrdinalIgnoreCase);
        var nomsProjets = inv.Projets.Select(p => p.Nom).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var p in inv.Projets)
            foreach (var pq in p.Paquets)
            {
                if (nomsProjets.Contains(pq.Nom) || Catalogue.EstFramework(pq.Nom)) continue;
                if (!deps.TryGetValue(pq.Nom, out var d))
                {
                    var f = Catalogue.Famille(pq.Nom);
                    deps[pq.Nom] = d = new Dependance
                    {
                        Nom = pq.Nom, Version = pq.Version, Origine = pq.Origine,
                        Famille = f?.Famille ?? "", Categorie = f?.Categorie ?? (pq.Origine == "COM" ? "Composant COM" : "Non catalogué"),
                        Remplacement = f?.Remplacement ?? (pq.Origine == "COM" ? "Remplacer, ou isoler dans un service Windows exposé par API" : "Rôle à vérifier : si la bibliothèque ne touche pas l'interface, elle reste côté serveur"),
                        Risque = f?.Risque ?? (pq.Origine == "COM" ? Gravite.Elevee : Gravite.Info), Interface = f?.Interface ?? false,
                    };
                }
                d.Projets.Add(p.Nom);
                if (d.Version == null && pq.Version != null) d.Version = pq.Version;
            }
        inv.Dependances.AddRange(deps.Values.OrderBy(d => d.Risque == Gravite.Elevee ? 0 : d.Risque == Gravite.Moyenne ? 1 : 2).ThenBy(d => d.Categorie).ThenBy(d => d.Nom, StringComparer.OrdinalIgnoreCase));

        // Rapports Crystal : ouverts par leur nom de fichier ou par la classe typée générée (new Factures()).
        foreach (var r in inv.RapportsCrystal)
        {
            var nom = Path.GetFileName(r.Fichier);
            var classe = Path.GetFileNameWithoutExtension(r.Fichier);
            var cree = new Regex($@"\bnew\s+(?:[\w.]+\.)?{Regex.Escape(classe)}\s*\(", RegexOptions.IgnoreCase);
            foreach (var f in fichiers)
            {
                var genere = Path.GetFileNameWithoutExtension(f.Chemin).Equals(classe, StringComparison.OrdinalIgnoreCase) && f.Texte.Contains("ReportClass", StringComparison.Ordinal);
                if (!genere && (f.Texte.Contains(nom, StringComparison.OrdinalIgnoreCase) || cree.IsMatch(f.Texte))) r.OuvertPar.Add(Rel(f.Chemin));
            }
            r.Atteint = inv.PointsEntree.Count == 0 ? r.OuvertPar.Count > 0 : r.OuvertPar.Any(inv.FichiersAtteints.Contains);
        }
        inv.RapportsCrystal.Sort((a, b) => b.Atteint.CompareTo(a.Atteint) != 0 ? b.Atteint.CompareTo(a.Atteint) : string.CompareOrdinal(a.Fichier, b.Fichier));
        Net10.Relever(inv, fichiers, Rel, inv.Projets.ToDictionary(p => p.Nom, p => p.Chemin, StringComparer.Ordinal), dossiersProjets);
        Estimation.NoterEcrans(inv);
        // Aucun chemin absolu dans l'inventaire : il part chez l'auditeur.
        inv.Racine = Path.GetFileName(racine);
        foreach (var p in inv.Projets) p.Chemin = Rel(p.Chemin);
        return inv;
    }

    /// <summary>
    /// Périmètre : un écran est atteint s'il est référencé, de proche en proche, depuis une classe d'un projet exécutable
    /// (Main, App, fenêtre principale), par le code ou par composition dans le concepteur ou le XAML.
    /// </summary>
    static void Perimetre(Inventaire inv, List<FichierSource> fichiers, Dictionary<string, List<PartieClasse>> classes, Dictionary<string, List<string>> parNomSimple,
        Dictionary<string, Ecran> ecranParClasse, List<(string Chemin, string Projet)> xamls, Func<string, PartieClasse, string?> resoudre, Func<string, string> rel)
    {
        var executables = inv.Projets.Where(p => p.TypeSortie is "WinExe" or "Exe").Select(p => p.Nom).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Points d'entrée : la méthode Main (classe, module VB ou instructions de premier niveau), la classe Application (WPF),
        // l'objet de démarrage VB, puis la fenêtre déclarée dans App.xaml.
        var racines = new List<string>();
        var main = new Regex(@"\b(static\s+(async\s+)?(void|int|Task|Task<int>)\s+Main\s*\(|Sub\s+Main\s*\()", RegexOptions.IgnoreCase);
        foreach (var f in fichiers.Where(f => executables.Contains(f.Projet)))
        {
            var racine = f.Arbre.GetRoot();
            var premierNiveau = racine.DescendantNodes().Any(n => n is CSS.GlobalStatementSyntax);
            if (!premierNiveau && !main.IsMatch(f.Texte)) continue;
            racines.AddRange(f.Classes.Select(c => c.NomComplet));
            var contexte = f.Classes.FirstOrDefault() ?? classes.Values.SelectMany(v => v).FirstOrDefault(p => p.Fichier.Projet == f.Projet);
            if (contexte != null)
                foreach (var tok in racine.DescendantTokens())
                    if (tok.ValueText.Length >= 3 && parNomSimple.ContainsKey(tok.ValueText) && resoudre(tok.ValueText, contexte) is { } cible) racines.Add(cible);
        }
        racines.AddRange(classes.Where(c => c.Value.Any(p => executables.Contains(p.Fichier.Projet) && p.Bases.Any(b => Sources.NomSimple(b) == "Application"))).Select(c => c.Key));
        foreach (var p in inv.Projets.Where(p => executables.Contains(p.Nom)))
        {
            var dossier = Path.GetDirectoryName(p.Chemin)!;
            var demarrage = new List<string>();
            try
            {
                var x = System.Xml.Linq.XDocument.Load(p.Chemin);
                demarrage.AddRange(x.Descendants().Where(e => e.Name.LocalName == "StartupObject").Select(e => e.Value.Trim()));
            }
            catch (System.Xml.XmlException) { }
            var myapp = Path.Combine(dossier, "My Project", "Application.myapp");
            if (File.Exists(myapp))
                try { demarrage.AddRange(System.Xml.Linq.XDocument.Load(myapp).Descendants().Where(e => e.Name.LocalName == "MainForm").Select(e => e.Value.Trim())); }
                catch (System.Xml.XmlException) { }
            foreach (var d in demarrage.Where(d => d.Length > 0))
                racines.AddRange(parNomSimple.GetValueOrDefault(Sources.NomSimple(d)) ?? new List<string>());
        }
        // Projet exécutable sans point d'entrée reconnu : toutes ses classes servent de départ.
        foreach (var exe in executables.Where(e => !racines.Any(r => classes.TryGetValue(r, out var ps) && ps.Any(p => p.Fichier.Projet.Equals(e, StringComparison.OrdinalIgnoreCase)))))
            racines.AddRange(classes.Where(c => c.Value.Any(p => p.Fichier.Projet.Equals(exe, StringComparison.OrdinalIgnoreCase))).Select(c => c.Key));
        // WPF : la fenêtre de démarrage est déclarée dans App.xaml (StartupUri).
        foreach (var (chemin, projet) in xamls.Where(x => executables.Contains(x.Projet)))
        {
            try
            {
                var r = System.Xml.Linq.XDocument.Load(chemin).Root!;
                if (r.Name.LocalName == "Application" && (string?)r.Attribute("StartupUri") is { } uri)
                {
                    var fichier = Path.GetFileName(uri);
                    racines.AddRange(ecranParClasse.Values.Where(e => e.Fichiers.Any(f => Path.GetFileName(f).Equals(fichier, StringComparison.OrdinalIgnoreCase))).Select(e => e.Classe));
                }
            }
            catch (System.Xml.XmlException) { }
        }
        if (racines.Count == 0) return;
        inv.PointsEntree.AddRange(racines.Where(r => ecranParClasse.ContainsKey(r) || Regex.IsMatch(Sources.NomSimple(r), "^(Program|App|Application|Startup)$"))
            .Distinct().OrderBy(r => r, StringComparer.Ordinal));
        if (inv.PointsEntree.Count == 0) inv.PointsEntree.AddRange(racines.Distinct().OrderBy(r => r, StringComparer.Ordinal).Take(3));

        var aretes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (nom, parties) in classes)
        {
            var cibles = aretes[nom] = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in parties)
                foreach (var tok in p.Noeud.DescendantTokens())
                {
                    if (tok.ValueText.Length < 3 || !parNomSimple.ContainsKey(tok.ValueText)) continue;
                    var cible = resoudre(tok.ValueText, p);
                    if (cible != null && cible != nom) cibles.Add(cible);
                }
            if (ecranParClasse.TryGetValue(nom, out var e)) cibles.UnionWith(e.ComposantsInternes);
        }
        var vus = new HashSet<string>(StringComparer.Ordinal);
        var file = new Queue<string>(racines);
        while (file.Count > 0)
        {
            var c = file.Dequeue();
            if (!vus.Add(c)) continue;
            if (aretes.TryGetValue(c, out var suivants)) foreach (var s in suivants) if (!vus.Contains(s)) file.Enqueue(s);
            // Une classe atteinte entraîne ses classes de base (un écran hérite de leur code).
            if (classes.TryGetValue(c, out var parties))
                foreach (var p in parties) foreach (var b in p.Bases) if (resoudre(b, p) is { } bc && !vus.Contains(bc)) file.Enqueue(bc);
        }
        foreach (var e in inv.Ecrans) e.Atteint = vus.Contains(e.Classe);
        foreach (var c in vus) if (classes.TryGetValue(c, out var ps)) foreach (var p in ps) inv.FichiersAtteints.Add(rel(p.Fichier.Chemin));
        foreach (var e in inv.Ecrans.Where(e => e.Atteint)) foreach (var f in e.Fichiers) inv.FichiersAtteints.Add(f);
    }

    static string Commun(IEnumerable<string> dossiers)
    {
        var liste = dossiers.Select(d => Path.GetFullPath(d).TrimEnd(Path.DirectorySeparatorChar)).ToList();
        var commun = liste[0];
        foreach (var d in liste.Skip(1))
            while (!(d + Path.DirectorySeparatorChar).StartsWith(commun + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                commun = Path.GetDirectoryName(commun) ?? commun;
        return commun;
    }

    /// <summary>Repère les règles du catalogue dans un fichier, jeton par jeton (C# et VB.NET).</summary>
    public static IEnumerable<(string Code, int Ligne, string Extrait)> Scanner(FichierSource f, bool fichierEcran)
    {
        var racine = f.Arbre.GetRoot();
        var lignes = f.Texte.Split('\n');
        var vus = new HashSet<(string, int)>();
        var invokeRequired = f.Texte.Contains("InvokeRequired", StringComparison.Ordinal);
        string Extrait(int l) { var t = l - 1 < lignes.Length ? lignes[l - 1].Trim() : ""; return t.Length > 140 ? t[..140] + "…" : t; }
        IEnumerable<(string, int, string)> Emettre(string code, int ligne)
        {
            if (vus.Add((code, ligne))) yield return (code, ligne, Extrait(ligne));
        }
        bool Comparer(string a, string b) => f.Vb ? a.Equals(b, StringComparison.OrdinalIgnoreCase) : a == b;

        foreach (var regle in Catalogue.Regles)
        {
            if (regle.EcranSeulement && !fichierEcran) continue;
            if (regle.Usings.Length == 0) continue;
            foreach (var n in racine.DescendantNodes().Where(n => n is CSS.UsingDirectiveSyntax or VBS.SimpleImportsClauseSyntax))
            {
                var nom = n is CSS.UsingDirectiveSyntax u ? u.Name?.ToString() ?? "" : ((VBS.SimpleImportsClauseSyntax)n).Name.ToString();
                if (regle.Usings.Any(x => nom == x || nom.StartsWith(x + ".", StringComparison.Ordinal) || (x.Length > 4 && nom.Contains(x, StringComparison.Ordinal))))
                    foreach (var r in Emettre(regle.Code, n.GetLocation().GetLineSpan().StartLinePosition.Line + 1)) yield return r;
            }
        }

        foreach (var tok in racine.DescendantTokens())
        {
            var estId = f.Vb ? tok.RawKind == (int)VB.SyntaxKind.IdentifierToken : tok.RawKind == (int)CS.SyntaxKind.IdentifierToken;
            if (!estId) continue;
            var t = tok.ValueText;
            if (tok.Parent is CSS.UsingDirectiveSyntax || tok.Parent?.Ancestors().Any(a => a is CSS.UsingDirectiveSyntax or VBS.ImportsStatementSyntax) == true) continue;
            var ligne = tok.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            foreach (var regle in Catalogue.Regles)
            {
                if (regle.EcranSeulement && !fichierEcran) continue;
                var ok = false;
                if (regle.Identifiants.Any(i => Comparer(i, t))) ok = true;
                if (!ok && regle.Attributs.Length > 0 && regle.Attributs.Any(a => Comparer(a, t) || Comparer(a + "Attribute", t))
                    && tok.Parent!.AncestorsAndSelf().Any(n => n is CSS.AttributeSyntax or VBS.AttributeSyntax)) ok = true;
                if (!ok && regle.Surcharges.Length > 0 && regle.Surcharges.Any(s => Comparer(s, t)))
                {
                    if (tok.Parent is CSS.MethodDeclarationSyntax m && m.Identifier == tok && m.Modifiers.Any(x => x.ValueText == "override")) ok = true;
                    if (tok.Parent is VBS.MethodStatementSyntax vm && vm.Identifier == tok && vm.Modifiers.Any(x => x.ValueText.Equals("Overrides", StringComparison.OrdinalIgnoreCase))) ok = true;
                }
                if (!ok && regle.Membres.Length > 0)
                {
                    var prec = tok.GetPreviousToken();
                    var prec2 = prec.GetPreviousToken();
                    var suiv = tok.GetNextToken();
                    foreach (var m in regle.Membres)
                    {
                        var point = m.IndexOf('.');
                        if (point > 0)
                        {
                            if (Comparer(m[(point + 1)..], t) && prec.Text == "." && Comparer(m[..point], prec2.ValueText)) { ok = true; break; }
                        }
                        else if (Comparer(m, t) && suiv.Text == "(")
                        {
                            var receveur = prec.Text == "." ? prec2.ValueText : null;
                            if (receveur == null || receveur is "this" or "base" || receveur.Equals("Me", StringComparison.OrdinalIgnoreCase) || receveur.Equals("MyBase", StringComparison.OrdinalIgnoreCase)
                                || receveur.EndsWith("Dispatcher", StringComparison.Ordinal) || (invokeRequired && prec2.Text != "?"))
                            { ok = true; break; }
                        }
                    }
                }
                if (ok) foreach (var r in Emettre(regle.Code, ligne)) yield return r;
            }
        }
    }
}
