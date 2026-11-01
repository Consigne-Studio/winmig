using Winmig.Core;
using Xunit;

namespace Winmig.Tests;

public class Chiffrage
{
    static Ecran Faux(string nom, int controles = 0, int evenements = 0, int lignes = 0, NatureEcran nature = NatureEcran.Dialogue, params string[] signaux)
    {
        var e = new Ecran { Id = "P/" + nom, Classe = "N." + nom, Projet = "P", Techno = Techno.WinForms, Nature = nature, Controles = controles, Evenements = evenements, LignesCodeBehind = lignes };
        e.Signaux.AddRange(signaux);
        e.Fichiers.Add($"P/{nom}.cs");
        return e;
    }

    static Inventaire Inventaire(params Ecran[] ecrans)
    {
        var inv = new Inventaire { Racine = "/", Entree = "P.csproj" };
        inv.Projets.Add(new Projet { Nom = "P", Chemin = "/P/P.csproj", TypeSortie = "WinExe" });
        inv.Ecrans.AddRange(ecrans);
        Estimation.NoterEcrans(inv);
        return inv;
    }

    [Theory]
    [InlineData(10, 0, 0, Complexite.S, 0.5)]
    [InlineData(20, 10, 0, Complexite.M, 1)]
    [InlineData(40, 20, 600, Complexite.L, 2)]
    [InlineData(60, 40, 1500, Complexite.XL, 3)]
    public void Classe_les_ecrans_selon_leur_score(int ctrl, int evt, int lignes, Complexite attendue, double unites)
    {
        var inv = Inventaire(Faux("A", ctrl, evt, lignes));
        Assert.Equal(attendue, inv.Ecrans[0].Complexite);
        Assert.Equal(unites, inv.Ecrans[0].Unites);
    }

    [Fact]
    public void Ajoute_une_demi_unite_par_point_lourd_de_l_ecran()
    {
        var inv = Inventaire(Faux("A", 10, 0, 0, NatureEcran.Dialogue, "W05", "W06", "W07"));
        Assert.Equal(1.5, inv.Ecrans[0].Unites); // 0,5 + W05 + W06 ; W07 est un chantier transverse
    }

    [Fact]
    public void Le_premier_lot_porte_le_socle_et_la_fenetre_principale()
    {
        var ecrans = new List<Ecran> { Faux("MainForm", 20, 10, 0, NatureEcran.Fenetre) };
        for (var i = 0; i < 20; i++) ecrans.Add(Faux($"D{i:00}", 20, 10));
        var c = Estimation.Chiffrer(Inventaire(ecrans.ToArray()));
        Assert.Equal("N.MainForm", c.Lots[0].Ecrans[0].Classe);
        Assert.Equal(4, c.Lots[0].UnitesSocle);
        Assert.All(c.Lots, l => Assert.True(l.Unites <= 10));
        Assert.Equal(25, c.UnitesTotal);
        Assert.Equal(c.Lots.Sum(l => l.UnitesFacturees), c.UnitesTotal);
        Assert.Equal(25 * 2500m, c.Prix);
        Assert.Equal(25 * 2.4, c.JoursBas, 3);
        Assert.Equal(25 * 3.2, c.JoursHaut, 3);
    }

    [Fact]
    public void Un_petit_dernier_lot_rejoint_le_precedent()
    {
        var ecrans = new List<Ecran> { Faux("MainForm", 20, 10, 0, NatureEcran.Fenetre) };
        for (var i = 0; i < 7; i++) ecrans.Add(Faux($"D{i}", 20, 10));
        var c = Estimation.Chiffrer(Inventaire(ecrans.ToArray()));
        Assert.Single(c.Lots);
        Assert.Equal(12, c.Lots[0].UnitesFacturees);
    }

    [Fact]
    public void Un_lot_se_facture_au_moins_cinq_unites()
    {
        var c = Estimation.Chiffrer(new Inventaire { Racine = "/", Entree = "x" });
        Assert.Single(c.Lots);
        Assert.Equal(5, c.Lots[0].UnitesFacturees);
        Assert.Equal(12500m, c.Prix);
    }

    [Fact]
    public void Les_composants_passent_avant_les_ecrans_qui_les_utilisent()
    {
        var main = Faux("MainForm", 20, 10, 0, NatureEcran.Fenetre);
        var comp = Faux("Jauge", 5, 0, 0, NatureEcran.Composant);
        var dlg = Faux("Zdlg", 5, 0, 0);
        dlg.ComposantsInternes.Add("N.Jauge");
        var c = Estimation.Chiffrer(Inventaire(main, dlg, comp));
        var ordre = c.Lots.SelectMany(l => l.Ecrans).Select(e => Sources.NomSimple(e.Classe)).ToList();
        Assert.True(ordre.IndexOf("Jauge") < ordre.IndexOf("Zdlg"));
    }

    [Fact]
    public void Le_cout_d_un_chantier_croit_avec_les_fichiers_touches()
    {
        var inv = new Inventaire { Racine = "/", Entree = "x" };
        var k = new Constat { Code = "W17", Titre = "t", Gravite = Gravite.Moyenne, Pourquoi = "", Solution = "", JoursBas = 2, JoursHaut = 5 };
        k.Occurrences.Add(new Occurrence("a.cs", 1, ""));
        Assert.Equal((2, 5), Estimation.JoursChantier(k, inv));
        for (var i = 0; i < 30; i++) k.Occurrences.Add(new Occurrence($"f{i}.cs", 1, ""));
        Assert.Equal((6, 15), Estimation.JoursChantier(k, inv));
    }
}

public class LigneDeCommande
{
    static (int Code, string Sortie, string Erreur) Lancer(params string[] args)
    {
        var o = new StringWriter(); var e = new StringWriter();
        var code = Cli.Executer(args, o, e);
        return (code, o.ToString(), e.ToString());
    }

    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "winmig-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact]
    public void Affiche_l_aide_et_la_version()
    {
        Assert.Equal(0, Lancer("--aide").Code);
        Assert.Contains("winmig analyser", Lancer("--aide").Sortie);
        Assert.StartsWith("winmig 1.", Lancer("--version").Sortie);
        Assert.Equal(1, Lancer().Code);
    }

    [Fact]
    public void Refuse_les_erreurs_d_usage()
    {
        Assert.Equal(2, Lancer("compiler").Code);
        Assert.Equal(2, Lancer("analyser").Code);
        Assert.Equal(2, Lancer("analyser", Ex.Chemin("wf"), "--inconnue").Code);
        Assert.Equal(2, Lancer("analyser", Ex.Chemin("wf"), "--date", "23/10/2026").Code);
        Assert.Equal(3, Lancer("analyser", Ex.Chemin("absent.csproj")).Code);
        var r = Lancer("analyser", Ex.Chemin("wf", "App", "Program.cs"));
        Assert.Equal(3, r.Code);
        Assert.Contains("Entrée non reconnue", r.Erreur);
    }

    [Fact]
    public void Produit_le_rapport_complet()
    {
        var d = Temp();
        var r = Lancer("analyser", Ex.Chemin("wf", "Atelier.sln"), "-o", d, "-q", "--application", "Atelier <pilote>", "--client", "Client & Cie", "--date", "2026-10-23", "--auteur", "Consigne");
        Assert.Equal(0, r.Code);
        foreach (var f in new[] { "rapport.html", "rapport.md", "inventaire.json", "ecrans.csv", "constats.csv" })
            Assert.True(File.Exists(Path.Combine(d, f)), f);
        Assert.True(File.Exists(Path.Combine(d, "schemas", "Atelier.FenetrePrincipale.svg")));
        var html = File.ReadAllText(Path.Combine(d, "rapport.html"));
        Assert.Contains("Atelier &lt;pilote&gt;", html);
        Assert.DoesNotContain("Atelier <pilote>", html);
        Assert.Contains("Client &amp; Cie", html);
        Assert.Contains("23.10.2026", html);
        Assert.DoesNotContain("{{", html);
        var pages = System.Text.RegularExpressions.Regex.Matches(html, "<section class=\"page").Count;
        Assert.Contains($"{pages:00}/{pages:00}", html);
        Assert.Contains("data:font/woff2;base64,", html);
        Assert.Contains("Hors périmètre", html);
        var csv = File.ReadAllText(Path.Combine(d, "ecrans.csv"));
        Assert.StartsWith("﻿Projet;Classe;", csv);
        Assert.Contains("Atelier.ReglageDlg", csv);
        var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(d, "inventaire.json")));
        Assert.Equal(4, json.RootElement.GetProperty("inventaire").GetProperty("ecrans").GetArrayLength());
        Assert.Equal("Dialogue", json.RootElement.GetProperty("inventaire").GetProperty("ecrans").EnumerateArray().First(e => e.GetProperty("classe").GetString() == "Atelier.ReglageDlg").GetProperty("nature").GetString());
    }

    [Fact]
    public void Masque_le_code_sur_demande()
    {
        var d = Temp();
        Assert.Equal(0, Lancer("analyser", Ex.Chemin("wf", "Atelier.sln"), "-o", d, "-q", "--sans-code").Code);
        var html = File.ReadAllText(Path.Combine(d, "rapport.html"));
        Assert.DoesNotContain("OpenSubKey", html);
        var d2 = Temp();
        Assert.Equal(0, Lancer("analyser", Ex.Chemin("wf", "Atelier.sln"), "-o", d2, "-q").Code);
        Assert.Contains("OpenSubKey", File.ReadAllText(Path.Combine(d2, "rapport.html")));
    }

    [Fact]
    public void Inventorie_les_rapports_Crystal_et_les_chiffre_a_part()
    {
        var d = Temp();
        var r = Lancer("analyser", Ex.Chemin("cr", "Etats.csproj"), "-o", d, "--sans-code");
        Assert.Equal(0, r.Code);
        Assert.Contains("Rapports Crystal Reports : 3, dont 2 dans le périmètre, chiffrés à part : 500 à 2 200 € HT.", r.Sortie.Replace('\u202F', ' ').Replace('\u00A0', ' '));
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(d, "inventaire.json")));
        var rapports = doc.RootElement.GetProperty("inventaire").GetProperty("rapportsCrystal").EnumerateArray().ToList();
        Assert.Equal(new[] { "Rapports/Etiquettes.rpt", "Rapports/Factures.rpt", "Rapports/Ancien.rpt" }, rapports.Select(x => x.GetProperty("fichier").GetString()));
        Assert.Equal(new[] { true, true, false }, rapports.Select(x => x.GetProperty("atteint").GetBoolean()));
        Assert.Equal("Principal.cs", rapports[0].GetProperty("ouvertPar")[0].GetString());
        var html = File.ReadAllText(Path.Combine(d, "rapport.html"));
        Assert.Contains("Rapports Crystal Reports", html);
        Assert.Contains("À confirmer", html);
        // Refait depuis l'inventaire : les rapports Crystal suivent.
        var d2 = Temp();
        Assert.Equal(0, Lancer("rapport", Path.Combine(d, "inventaire.json"), "-o", d2, "-q").Code);
        Assert.Contains("Factures.rpt", File.ReadAllText(Path.Combine(d2, "rapport.html")));
    }

    [Fact]
    public void Releve_ce_qui_bloque_le_passage_a_dotnet_10()
    {
        var d = Temp();
        var r = Lancer("analyser", Ex.Chemin("n10", "Expedition.csproj"), "-o", d, "--sans-code");
        Assert.Equal(0, r.Code);
        Assert.Contains("Passage à .NET 10 : 1 point(s) à faire, 3 à vérifier, 1 pour le web ou Linux, 0 pour information.", r.Sortie);
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(d, "inventaire.json")));
        var points = doc.RootElement.GetProperty("inventaire").GetProperty("pointsNet10").EnumerateArray().ToList();
        Assert.Equal(new[] { "N01", "N02", "N03", "N06", "N07" }, points.Select(x => x.GetProperty("code").GetString()));
        Assert.StartsWith("net8.0-windows : ", points[0].GetProperty("detail").GetString());
        Assert.Contains("10/11/2026", points[0].GetProperty("detail").GetString());
        Assert.Equal("PlatformTarget x86", points[1].GetProperty("detail").GetString());
        Assert.Contains("ZebraPrinterLib", points[2].GetProperty("detail").GetString());
        Assert.Equal("SQLNCLI11", points[3].GetProperty("detail").GetString());
        Assert.Equal(new[] { "App.config", "Avis.cs" }, points[4].GetProperty("fichiers").EnumerateArray().Select(x => x.GetString()));
        var html = File.ReadAllText(Path.Combine(d, "rapport.html"));
        Assert.Contains("Passage à .NET 10", html);
        Assert.Contains("Azure Communication Services", html);
        // Crystal Reports compte aussi parmi les points, et un projet .NET Framework 4.8 est un portage.
        var d2 = Temp();
        Assert.Equal(0, Lancer("analyser", Ex.Chemin("cr", "Etats.csproj"), "-o", d2, "-q", "--json-seul").Code);
        using var doc2 = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(d2, "inventaire.json")));
        Assert.Equal(new[] { "N05", "N11" }, doc2.RootElement.GetProperty("inventaire").GetProperty("pointsNet10").EnumerateArray().Select(x => x.GetProperty("code").GetString()));
    }

    [Fact]
    public void Ne_signale_ni_commentaire_ni_MailKit_ni_x86_de_debug_ni_fichier_d_un_sous_projet()
    {
        var d = Temp();
        Assert.Equal(0, Lancer("analyser", Ex.Chemin("n10b", ""), "-o", d, "-q", "--sans-code").Code);
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(d, "inventaire.json")));
        var points = doc.RootElement.GetProperty("inventaire").GetProperty("pointsNet10").EnumerateArray()
            .Select(x => $"{x.GetProperty("projet").GetString()}:{x.GetProperty("code").GetString()}").OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "Outil:N06", "Outil:N11", "Racine:N01", "Racine:N09", "Vieux:N03", "Vieux:N11" }, points);
        var tous = doc.RootElement.GetProperty("inventaire").GetProperty("pointsNet10").EnumerateArray().ToList();
        var n01 = tous.First(x => x.GetProperty("code").GetString() == "N01");
        Assert.Equal("Information", n01.GetProperty("portee").GetString()); // net10.0 est déjà ciblé
        Assert.Equal("net35", tous.First(x => x.GetProperty("projet").GetString() == "Vieux" && x.GetProperty("code").GetString() == "N11").GetProperty("detail").GetString());
        Assert.Equal(new[] { "Racine/Sub/Outil/App.config" }, tous.First(x => x.GetProperty("code").GetString() == "N06").GetProperty("fichiers").EnumerateArray().Select(x => x.GetString()));
        var html = File.ReadAllText(Path.Combine(d, "rapport.html"));
        Assert.Contains("À reprendre sans Copilot", html);
        Assert.Contains("encore en .NET Framework", html);
    }

    [Fact]
    public void Garde_les_avertissements_sans_chemin_absolu()
    {
        var src = Temp();
        File.WriteAllText(Path.Combine(src, "Seul.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"../Absent/Absent.csproj\" /></ItemGroup></Project>");
        var d = Temp();
        Assert.Equal(0, Lancer("analyser", Path.Combine(src, "Seul.csproj"), "-o", d, "-q", "--json-seul").Code);
        var json = File.ReadAllText(Path.Combine(d, "inventaire.json"));
        Assert.Contains("Projet référencé introuvable : Absent.csproj", json);
        Assert.DoesNotContain(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar).Replace("\\", "\\\\"), json);
    }

    [Theory]
    [InlineData("net8.0", "fin de support le 10/11/2026")]
    [InlineData("net9.0-windows", "fin de support le 10/11/2026")]
    [InlineData("net6.0", "hors support")]
    [InlineData("netcoreapp3.1", "hors support")]
    [InlineData("net462", "fin de support le 12/01/2027")]
    [InlineData("net45", "hors support")]
    [InlineData("net48", null)]
    [InlineData("net10.0", null)]
    [InlineData("netstandard2.0", null)]
    [InlineData("net35", null)]
    public void Connait_les_fins_de_support(string cible, string? attendu) => Assert.Equal(attendu, Winmig.Core.Net10.FinSupport(cible, new DateOnly(2026, 10, 24)));

    [Fact]
    public void Dit_quand_le_support_est_deja_fini() =>
        Assert.Equal("hors support depuis le 10/11/2026", Winmig.Core.Net10.FinSupport("net8.0", new DateOnly(2026, 11, 20)));

    [Fact]
    public void N_envoie_aucun_chemin_absolu_ni_libelle_sur_demande()
    {
        var d = Temp();
        Assert.Equal(0, Lancer("analyser", Ex.Chemin("wf", "Atelier.sln"), "-o", d, "-q", "--sans-code", "--sans-libelles", "--json-seul").Code);
        var json = File.ReadAllText(Path.Combine(d, "inventaire.json"));
        Assert.DoesNotContain(AppContext.BaseDirectory.TrimEnd('/', '\\').Replace("\\", "\\\\"), json);
        Assert.DoesNotContain("\"texte\": \"", json);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("wf", doc.RootElement.GetProperty("inventaire").GetProperty("racine").GetString());
        Assert.All(doc.RootElement.GetProperty("inventaire").GetProperty("ecrans").EnumerateArray(), e => Assert.Equal(System.Text.Json.JsonValueKind.Null, e.GetProperty("titre").ValueKind));
        Assert.All(doc.RootElement.GetProperty("inventaire").GetProperty("projets").EnumerateArray(), p => Assert.False(Path.IsPathRooted(p.GetProperty("chemin").GetString())));
    }

    [Fact]
    public void Insere_l_avis_de_l_auditeur()
    {
        var d = Temp();
        var avis = Path.Combine(d, "avis.md");
        File.WriteAllText(avis, "Migrer **d'abord** la supervision.\n\n- point un\n- point *deux*\n");
        Assert.Equal(0, Lancer("analyser", Ex.Chemin("vb"), "-o", d, "-q", "--avis", avis).Code);
        var html = File.ReadAllText(Path.Combine(d, "rapport.html"));
        Assert.Contains("Migrer <b>d&#39;abord</b> la supervision.", html);
        Assert.Contains("<li>point <i>deux</i></li>", html);
        Assert.Contains("Migrer **d'abord**", File.ReadAllText(Path.Combine(d, "rapport.md")));
        Assert.Equal(2, Lancer("analyser", Ex.Chemin("vb"), "-o", d, "--avis", Path.Combine(d, "absent.md")).Code);
    }

    [Fact]
    public void Json_seul_n_ecrit_pas_de_rapport()
    {
        var d = Temp();
        Assert.Equal(0, Lancer("analyser", Ex.Chemin("legacy", "Ancien.csproj"), "-o", d, "--json-seul", "-q").Code);
        Assert.True(File.Exists(Path.Combine(d, "inventaire.json")));
        Assert.False(File.Exists(Path.Combine(d, "rapport.html")));
    }

    [Fact]
    public void Refait_le_rapport_depuis_l_inventaire_sans_le_code()
    {
        var d = Temp();
        Assert.Equal(0, Lancer("analyser", Ex.Chemin("wf", "Atelier.sln"), "-o", d, "-q", "--sans-code").Code);
        var json = File.ReadAllText(Path.Combine(d, "inventaire.json"));
        Assert.DoesNotContain("OpenSubKey", json);
        var prix = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("chiffrage").GetProperty("prixHT").GetDecimal();

        var d2 = Temp();
        var r = Lancer("rapport", Path.Combine(d, "inventaire.json"), "-o", d2, "-q", "--application", "Atelier");
        Assert.Equal(0, r.Code);
        var html = File.ReadAllText(Path.Combine(d2, "rapport.html"));
        Assert.Contains(">Nom<", File.ReadAllText(Path.Combine(d2, "schemas", "Atelier.FenetrePrincipale.svg")));
        Assert.Contains("0,0 bar", html);
        Assert.Equal(prix, System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(d2, "inventaire.json"))).RootElement.GetProperty("chiffrage").GetProperty("prixHT").GetDecimal());

        var d3 = Temp();
        Assert.Equal(0, Lancer("rapport", Path.Combine(d, "inventaire.json"), "-o", d3, "-q", "--inclure", "AncienEcran").Code);
        var j3 = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(d3, "inventaire.json"))).RootElement;
        Assert.True(j3.GetProperty("inventaire").GetProperty("ecrans").EnumerateArray().Single(e => e.GetProperty("classe").GetString() == "Atelier.AncienEcran").GetProperty("atteint").GetBoolean());
        Assert.Contains(j3.GetProperty("chiffrage").GetProperty("lots").EnumerateArray().SelectMany(l => l.GetProperty("chantiers").EnumerateArray()), c => c.GetString() == "W02");

        Assert.Equal(2, Lancer("rapport", Path.Combine(d, "inventaire.json"), "-o", d3, "--exclure", "Inexistant").Code);
        Assert.Equal(3, Lancer("rapport", Path.Combine(d, "absent.json")).Code);
        File.WriteAllText(Path.Combine(d, "faux.json"), "{\"a\":1");
        Assert.Equal(3, Lancer("rapport", Path.Combine(d, "faux.json")).Code);
        Assert.Equal(2, Lancer("analyser", Ex.Chemin("wf"), "--inclure", "X").Code);
    }
}
