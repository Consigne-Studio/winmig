using Winmig.Core;
using Xunit;

namespace Winmig.Tests;

static class Ex
{
    public static string Chemin(params string[] parties) => Path.Combine(new[] { AppContext.BaseDirectory, "Exemples" }.Concat(parties).ToArray());
    static readonly Dictionary<string, Inventaire> Cache = new();
    public static Inventaire Inv(params string[] parties)
    {
        var c = Chemin(parties);
        lock (Cache) { if (!Cache.TryGetValue(c, out var i)) Cache[c] = i = Analyseur.Analyser(c); return i; }
    }
    public static Ecran Ecran(this Inventaire inv, string simple) => inv.Ecrans.Single(e => e.Classe.EndsWith("." + simple) || e.Classe == simple);
}

public class WinFormsCSharp
{
    readonly Inventaire inv = Ex.Inv("wf", "Atelier.sln");

    [Fact]
    public void Lit_la_solution_et_les_projets()
    {
        Assert.Equal(new[] { "App", "Lib" }, inv.Projets.Select(p => p.Nom).OrderBy(n => n));
        var app = inv.Projets.Single(p => p.Nom == "App");
        Assert.Equal(Techno.WinForms, app.Techno);
        Assert.Equal("WinExe", app.TypeSortie);
        Assert.Contains("net8.0-windows", app.Cibles);
    }

    [Fact]
    public void Resout_les_versions_depuis_Directory_Build_props()
    {
        Assert.Equal("3.1.1", inv.Dependances.Single(d => d.Nom == "Serilog").Version);
    }

    [Fact]
    public void Reconnait_fenetres_dialogues_et_composants()
    {
        Assert.Equal(NatureEcran.Fenetre, inv.Ecran("FenetrePrincipale").Nature);
        Assert.Equal(NatureEcran.Dialogue, inv.Ecran("ReglageDlg").Nature);
        Assert.Equal(NatureEcran.Composant, inv.Ecran("MesureCtrl").Nature);
        Assert.Equal("Atelier \"pilote\"", inv.Ecran("FenetrePrincipale").Titre);
    }

    [Fact]
    public void Compte_controles_evenements_et_liaisons_du_concepteur()
    {
        var f = inv.Ecran("FenetrePrincipale");
        // menu, 2 éléments de menu, split, arbre, grille, mesure, état, courbe (les colonnes, la minuterie et le conteneur ne comptent pas)
        Assert.Equal(9, f.Controles);
        Assert.Equal(3, f.Evenements);
        Assert.Equal(1, f.Liaisons);
        Assert.Equal(2, f.TypesControles["ToolStripMenuItem"]);
        Assert.Contains("Graphiques : ZedGraphControl", f.ControlesTiers);
    }

    [Fact]
    public void Relie_les_composants_internes_a_leurs_utilisateurs()
    {
        Assert.Equal(new[] { "Atelier.Lib.MesureCtrl" }, inv.Ecran("FenetrePrincipale").ComposantsInternes);
        Assert.Equal(new[] { "App/Atelier.FenetrePrincipale" }, inv.Ecran("MesureCtrl").UtilisePar);
    }

    [Fact]
    public void Reconstitue_la_disposition_avec_les_ancrages()
    {
        var s = inv.Ecran("FenetrePrincipale").Schema!;
        var n = inv.Ecran("FenetrePrincipale").SchemaNoeuds;
        Assert.Equal((800, 500), (s.Al, s.Ah));
        Assert.Equal((0, 0, 800, 24), (n["menu"].Ax, n["menu"].Ay, n["menu"].Al, n["menu"].Ah));
        Assert.Equal((478, 22), (n["etat"].Ay, n["etat"].Ah));
        Assert.Equal((0, 24, 800, 454), (n["split"].Ax, n["split"].Ay, n["split"].Al, n["split"].Ah));
        Assert.Equal((0, 250), (n["arbre"].Ax, n["arbre"].Al));
        // Panneau 2 : la courbe (ajoutée en dernier) prend le bas, la mesure le haut, la grille le reste.
        Assert.Equal((254, 24 + 454 - 150), (n["courbe"].Ax, n["courbe"].Ay));
        Assert.Equal((24, 60), (n["mesure"].Ay, n["mesure"].Ah));
        Assert.Equal((84, 454 - 60 - 150), (n["grille"].Ay, n["grille"].Ah));
    }

    [Fact]
    public void Dessine_un_svg_valide_avec_les_entetes_de_colonnes()
    {
        var e = inv.Ecran("FenetrePrincipale");
        var svg = Rapport.Dessin(e, inv);
        System.Xml.Linq.XDocument.Parse(svg);
        Assert.Contains(">Nom<", svg);
        Assert.Contains(">Valeur<", svg);
        Assert.Contains(">Fichier<", svg);
        Assert.Contains("Atelier &quot;pilote&quot;", svg);
        Assert.Contains("0,0 bar", svg); // le composant interne est dessiné à sa place
    }

    [Fact]
    public void Releve_les_API_propres_a_Windows()
    {
        string[] Codes(string e) => inv.Ecran(e).Signaux.ToArray();
        Assert.Equal(new[] { "W04", "W07", "W08", "W14" }, Codes("FenetrePrincipale"));
        Assert.Equal(new[] { "W05" }, Codes("ReglageDlg"));
        Assert.Equal(new[] { "W01", "W06" }, Codes("MesureCtrl"));
        Assert.Equal(new[] { "W02" }, Codes("AncienEcran"));
    }

    [Fact]
    public void N_attribue_pas_l_appel_d_evenement_a_la_synchronisation_d_interface()
    {
        // « Change?.Invoke(...) » lève un événement : ce n'est pas un passage sur le fil d'interface.
        var w07 = inv.Constats.Single(c => c.Code == "W07");
        Assert.DoesNotContain(w07.Occurrences, o => o.Extrait.Contains("Change?.Invoke"));
        Assert.Single(w07.Occurrences);
    }

    [Fact]
    public void Sort_du_perimetre_un_ecran_jamais_appele()
    {
        Assert.False(inv.Ecran("AncienEcran").Atteint);
        Assert.True(inv.Ecran("ReglageDlg").Atteint);
        Assert.True(inv.Ecran("MesureCtrl").Atteint);
        Assert.Equal(new[] { "Atelier.FenetrePrincipale", "Atelier.Program" }, inv.PointsEntree);
        var c = Estimation.Chiffrer(inv);
        Assert.Equal(new[] { "AncienEcran" }, c.HorsPerimetre.Select(e => Sources.NomSimple(e.Classe)));
        Assert.DoesNotContain(c.Chantiers, x => x.Constat.Code == "W02");
    }
}

public class VisualBasic
{
    readonly Inventaire inv = Ex.Inv("vb");

    [Fact]
    public void Analyse_un_formulaire_VB()
    {
        var f = inv.Ecran("FormBanc");
        Assert.Equal(Techno.WinForms, f.Techno);
        Assert.Equal(3, f.Controles);
        Assert.Equal(2, f.Evenements); // Handles Demarrer.Click + AddHandler Stop1.Click
        Assert.Equal("Banc d'essai", f.Titre);
        Assert.Equal(new[] { "W01", "W07" }, f.Signaux);
        Assert.True(f.Atteint);
        Assert.Equal("Démarrer", f.SchemaNoeuds["Demarrer"].Texte);
        Assert.Equal((110, 12), (f.SchemaNoeuds["Stop1"].Ax, f.SchemaNoeuds["Stop1"].Ay));
        Assert.True(f.Branches >= 3);
    }
}

public class Wpf
{
    readonly Inventaire inv = Ex.Inv("wpf");

    [Fact]
    public void Lit_le_xaml_et_le_viewmodel()
    {
        var f = inv.Ecran("FenetreVue");
        Assert.Equal(Techno.Wpf, f.Techno);
        Assert.Equal(NatureEcran.Fenetre, f.Nature);
        Assert.Equal("Supervision", f.Titre);
        Assert.True(f.Mvvm);
        Assert.Contains("App/Vues/FenetreViewModel.cs", f.Fichiers);
        Assert.Equal(1, f.Evenements);
        Assert.Equal(3, f.Liaisons);
        Assert.Contains("Telerik : RadGridView", f.ControlesTiers);
        Assert.Equal(new[] { "Supervision.Vues.ArbreVue" }, f.ComposantsInternes);
        Assert.False(f.TypesControles.ContainsKey("Setter"));
    }

    [Fact]
    public void Releve_animations_dialogues_et_dispatcher()
    {
        Assert.Equal(new[] { "W07", "W09", "W21" }, inv.Ecran("FenetreVue").Signaux);
    }

    [Fact]
    public void Part_de_la_fenetre_de_demarrage_d_App_xaml()
    {
        Assert.Contains("Supervision.Vues.FenetreVue", inv.PointsEntree);
        Assert.All(inv.Ecrans, e => Assert.True(e.Atteint));
    }

    [Fact]
    public void Dispose_la_grille_et_le_dockpanel()
    {
        var s = inv.Ecran("FenetreVue").Schema!;
        Assert.Equal((900, 600), (s.Al, s.Ah));
        var tous = Tous(s).ToList();
        var menu = tous.Single(n => n.Type == "MenuStrip");
        Assert.Equal((0, 22), (menu.Ay, menu.Ah));
        Assert.Contains("=Fichier", menu.Elements);
        var arbre = tous.Single(n => n.Type == "ArbreVue");
        Assert.Equal((0, 250), (arbre.Ax, arbre.Al));
        var grille = tous.Single(n => n.Type == "RadGridView");
        Assert.True(grille.Ax >= 250 && grille.Ah > 300);
        System.Xml.Linq.XDocument.Parse(Rapport.Dessin(inv.Ecran("FenetreVue"), inv));
    }

    static IEnumerable<NoeudSchema> Tous(NoeudSchema n) => n.Enfants.SelectMany(Tous).Prepend(n);
}

public class AncienFormat
{
    readonly Inventaire inv = Ex.Inv("legacy", "Ancien.csproj");

    [Fact]
    public void Lit_un_projet_net_framework()
    {
        var p = inv.Projets.Single();
        Assert.False(p.FormatSdk);
        Assert.Equal(new[] { "net472" }, p.Cibles);
        Assert.Equal(Techno.WinForms, p.Techno);
        Assert.Equal(new[] { "de" }, p.Langues);
    }

    [Fact]
    public void Classe_les_dependances_tierces_et_COM()
    {
        var d = inv.Dependances.ToDictionary(x => x.Nom);
        Assert.Equal("Suite de composants d'interface", d["DevExpress.XtraGrid.v19.2"].Categorie);
        Assert.Equal("19.2.3.0", d["DevExpress.XtraGrid.v19.2"].Version);
        Assert.Equal(Gravite.Elevee, d["DevExpress.XtraGrid.v19.2"].Risque);
        Assert.Equal("Composant COM", d["AxMSChart20Lib"].Categorie);
        Assert.Equal("2.0.8", d["log4net"].Version);
        Assert.Equal("Sérialisation", d["Newtonsoft.Json"].Categorie);
        Assert.DoesNotContain("System.Windows.Forms", d.Keys);
    }

    [Fact]
    public void Lit_les_positions_d_un_ecran_localisable_dans_le_resx()
    {
        var e = inv.Ecran("Principal");
        Assert.Equal("Ancien poste", e.Titre);
        Assert.Equal((420, 250), (e.Schema!.Al, e.Schema.Ah));
        Assert.Equal((10, 40, 400, 200), (e.SchemaNoeuds["grille"].Ax, e.SchemaNoeuds["grille"].Ay, e.SchemaNoeuds["grille"].Al, e.SchemaNoeuds["grille"].Ah));
        Assert.Equal("Historique & tendances", e.SchemaNoeuds["titre"].Texte);
        Assert.Contains("DevExpress : GridControl", e.ControlesTiers);
        Assert.Contains("W03", e.Signaux);
    }
}

public class DepotsVolumineux
{
    [Fact]
    public void Distingue_deux_projets_de_meme_nom()
    {
        var d = Path.Combine(Path.GetTempPath(), "winmig-doublon-" + Guid.NewGuid().ToString("N"));
        foreach (var (dossier, classe) in new[] { ("A/Outil", "Un"), ("B/outil", "Deux") })
        {
            Directory.CreateDirectory(Path.Combine(d, dossier));
            File.WriteAllText(Path.Combine(d, dossier, Path.GetFileName(dossier) + ".csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net8.0-windows</TargetFramework><UseWindowsForms>true</UseWindowsForms></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(d, dossier, classe + ".cs"), $"namespace N{classe}; static class Program {{ static void Main() => System.Windows.Forms.Application.Run(new {classe}()); }} class {classe} : System.Windows.Forms.Form {{ }}");
        }
        var inv = Analyseur.Analyser(d);
        Assert.Equal(2, inv.Projets.Select(p => p.Nom.ToLowerInvariant()).Distinct().Count());
        Assert.Equal(2, inv.Ecrans.Count);
        Assert.Equal(2, inv.Ecrans.Select(e => e.Id).Distinct().Count());
    }
}
