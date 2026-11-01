using Microsoft.CodeAnalysis;
using CS = Microsoft.CodeAnalysis.CSharp;
using CSS = Microsoft.CodeAnalysis.CSharp.Syntax;
using VB = Microsoft.CodeAnalysis.VisualBasic;
using VBS = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace Winmig.Core;

/// <summary>Un fichier C# ou VB.NET analysé par Roslyn (syntaxe seule : pas besoin de compiler ni de Windows).</summary>
public sealed class FichierSource
{
    public required string Chemin { get; init; }
    public required string Projet { get; init; }
    public required bool Vb { get; init; }
    public required SyntaxTree Arbre { get; init; }
    public required string Texte { get; init; }
    public List<string> Usings { get; init; } = new();
    public List<PartieClasse> Classes { get; init; } = new();
    public bool Concepteur => Chemin.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) || Chemin.EndsWith(".Designer.vb", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Une déclaration de classe (une partie, si la classe est partielle).</summary>
public sealed class PartieClasse
{
    public required string Nom { get; init; }
    public required string EspaceNoms { get; init; }
    public required FichierSource Fichier { get; init; }
    public required SyntaxNode Noeud { get; init; }
    public List<string> Bases { get; init; } = new();
    public List<string> Champs { get; init; } = new();
    public SyntaxNode? InitializeComponent { get; set; }
    public bool Abstraite { get; init; }
    public string NomComplet => string.IsNullOrEmpty(EspaceNoms) ? Nom : EspaceNoms + "." + Nom;
}

public static class Sources
{
    public static FichierSource Lire(string chemin, string projet)
    {
        var texte = File.ReadAllText(chemin);
        var vb = chemin.EndsWith(".vb", StringComparison.OrdinalIgnoreCase);
        var arbre = vb ? VB.VisualBasicSyntaxTree.ParseText(texte, path: chemin) : CS.CSharpSyntaxTree.ParseText(texte, path: chemin);
        var f = new FichierSource { Chemin = chemin, Projet = projet, Vb = vb, Arbre = arbre, Texte = texte };
        var racine = arbre.GetRoot();
        if (vb)
        {
            f.Usings.AddRange(racine.DescendantNodes().OfType<VBS.SimpleImportsClauseSyntax>().Select(i => i.Name.ToString()));
            foreach (var c in racine.DescendantNodes().OfType<VBS.ClassBlockSyntax>())
            {
                var p = new PartieClasse
                {
                    Nom = c.ClassStatement.Identifier.ValueText,
                    EspaceNoms = string.Join(".", c.Ancestors().OfType<VBS.NamespaceBlockSyntax>().Reverse().Select(n => n.NamespaceStatement.Name.ToString())),
                    Fichier = f,
                    Noeud = c,
                    Abstraite = c.ClassStatement.Modifiers.Any(m => m.ValueText.Equals("MustInherit", StringComparison.OrdinalIgnoreCase)),
                };
                p.Bases.AddRange(c.Inherits.SelectMany(i => i.Types).Select(t => t.ToString()));
                foreach (var champ in c.Members.OfType<VBS.FieldDeclarationSyntax>())
                    p.Champs.AddRange(champ.Declarators.SelectMany(d => d.Names).Select(n => n.Identifier.ValueText));
                p.InitializeComponent = c.Members.OfType<VBS.MethodBlockSyntax>()
                    .FirstOrDefault(m => m.SubOrFunctionStatement.Identifier.ValueText.Equals("InitializeComponent", StringComparison.OrdinalIgnoreCase));
                f.Classes.Add(p);
            }
        }
        else
        {
            f.Usings.AddRange(racine.DescendantNodes().OfType<CSS.UsingDirectiveSyntax>().Where(u => u.Name != null).Select(u => u.Name!.ToString()));
            foreach (var c in racine.DescendantNodes().OfType<CSS.ClassDeclarationSyntax>())
            {
                var ns = string.Join(".", c.Ancestors().OfType<CSS.BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
                var p = new PartieClasse
                {
                    Nom = c.Identifier.ValueText,
                    EspaceNoms = ns,
                    Fichier = f,
                    Noeud = c,
                    Abstraite = c.Modifiers.Any(m => m.ValueText == "abstract"),
                };
                if (c.BaseList != null) p.Bases.AddRange(c.BaseList.Types.Select(t => t.Type.ToString()));
                foreach (var champ in c.Members.OfType<CSS.FieldDeclarationSyntax>())
                    p.Champs.AddRange(champ.Declaration.Variables.Select(v => v.Identifier.ValueText));
                p.InitializeComponent = c.Members.OfType<CSS.MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == "InitializeComponent" && m.Body != null);
                f.Classes.Add(p);
            }
        }
        return f;
    }

    /// <summary>Nom simple d'un type (sans espace de noms ni arguments génériques).</summary>
    public static string NomSimple(string type)
    {
        var t = type;
        var chevron = t.IndexOfAny(new[] { '<', '(' });
        if (chevron >= 0) t = t[..chevron];
        var point = t.LastIndexOf('.');
        return (point >= 0 ? t[(point + 1)..] : t).Trim();
    }

    /// <summary>Mesures de la logique d'une partie de classe : lignes utiles, méthodes, branches, gestionnaires VB « Handles ».</summary>
    public static (int Lignes, int Methodes, int Branches, int Handles) Mesurer(PartieClasse p)
    {
        var lignes = Projets.LignesUtiles(p.Noeud.ToString(), p.Fichier.Vb);
        int methodes = 0, branches = 0, handles = 0;
        foreach (var n in p.Noeud.DescendantNodes())
        {
            if (p.Fichier.Vb)
            {
                switch ((VB.SyntaxKind)n.RawKind)
                {
                    case VB.SyntaxKind.SubBlock or VB.SyntaxKind.FunctionBlock or VB.SyntaxKind.ConstructorBlock: methodes++; break;
                    case VB.SyntaxKind.SingleLineIfStatement or VB.SyntaxKind.MultiLineIfBlock or VB.SyntaxKind.ElseIfBlock or VB.SyntaxKind.CaseBlock
                        or VB.SyntaxKind.ForBlock or VB.SyntaxKind.ForEachBlock or VB.SyntaxKind.WhileBlock or VB.SyntaxKind.SimpleDoLoopBlock
                        or VB.SyntaxKind.DoWhileLoopBlock or VB.SyntaxKind.DoUntilLoopBlock or VB.SyntaxKind.DoLoopWhileBlock or VB.SyntaxKind.DoLoopUntilBlock
                        or VB.SyntaxKind.CatchBlock or VB.SyntaxKind.TernaryConditionalExpression or VB.SyntaxKind.BinaryConditionalExpression
                        or VB.SyntaxKind.AndAlsoExpression or VB.SyntaxKind.OrElseExpression: branches++; break;
                    case VB.SyntaxKind.HandlesClause: handles += ((VBS.HandlesClauseSyntax)n).Events.Count; break;
                }
            }
            else
            {
                switch ((CS.SyntaxKind)n.RawKind)
                {
                    case CS.SyntaxKind.MethodDeclaration or CS.SyntaxKind.ConstructorDeclaration or CS.SyntaxKind.LocalFunctionStatement:
                        methodes++; break;
                    case CS.SyntaxKind.IfStatement or CS.SyntaxKind.CaseSwitchLabel or CS.SyntaxKind.CasePatternSwitchLabel or CS.SyntaxKind.SwitchExpressionArm
                        or CS.SyntaxKind.ForStatement or CS.SyntaxKind.ForEachStatement or CS.SyntaxKind.WhileStatement or CS.SyntaxKind.DoStatement
                        or CS.SyntaxKind.CatchClause or CS.SyntaxKind.ConditionalExpression or CS.SyntaxKind.CoalesceExpression
                        or CS.SyntaxKind.LogicalAndExpression or CS.SyntaxKind.LogicalOrExpression: branches++; break;
                }
            }
        }
        return (lignes, methodes, branches, handles);
    }
}
