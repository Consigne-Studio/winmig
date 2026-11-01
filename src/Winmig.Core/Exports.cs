using System.Globalization;
using System.Text;

namespace Winmig.Core;

/// <summary>Exports tableur (séparateur point-virgule, UTF-8 avec BOM : s'ouvre tel quel dans Excel en français).</summary>
public static class Exports
{
    static string C(object? v)
    {
        var s = Convert.ToString(v, CultureInfo.GetCultureInfo("fr-FR")) ?? "";
        return s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    static string Lignes(IEnumerable<IEnumerable<object?>> lignes)
    {
        var sb = new StringBuilder("﻿");
        foreach (var l in lignes) sb.Append(string.Join(';', l.Select(C))).Append("\r\n");
        return sb.ToString();
    }

    public static string Ecrans(Inventaire inv) => Lignes(
        new[] { new object?[] { "Projet", "Classe", "Techno", "Nature", "Titre", "Contrôles", "Événements", "Liaisons", "Lignes de logique", "Branches", "Contrôles tiers", "Composants internes", "Utilisé par", "Points d'attention", "Score", "Classe de complexité", "Unités" } }
        .Concat(inv.Ecrans.Select(e => new object?[] { e.Projet, e.Classe, e.Techno, e.Nature, e.Titre, e.Controles, e.Evenements, e.Liaisons, e.LignesCodeBehind, e.Branches,
            string.Join(", ", e.ControlesTiers), e.ComposantsInternes.Count, e.UtilisePar.Count, string.Join(" ", e.Signaux), e.Score, e.Complexite, e.Unites })));

    public static string Constats(Inventaire inv) => Lignes(
        new[] { new object?[] { "Code", "Titre", "Gravité", "Fichier", "Ligne", "Extrait" } }
        .Concat(inv.Constats.SelectMany(c => c.Occurrences.Select(o => new object?[] { c.Code, c.Titre, c.Gravite, o.Fichier, o.Ligne, o.Extrait }))));
}
