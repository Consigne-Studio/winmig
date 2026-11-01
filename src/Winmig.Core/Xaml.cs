using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Winmig.Core;

/// <summary>Ce qu'un fichier XAML dit d'un écran WPF.</summary>
public sealed class LectureXaml
{
    public required string Chemin { get; init; }
    public string? Classe { get; set; }
    public string Racine { get; set; } = "";
    public string? Titre { get; set; }
    public int Controles { get; set; }
    public int Evenements { get; set; }
    public int Liaisons { get; set; }
    public Dictionary<string, int> Types { get; } = new();
    public List<string> Tiers { get; } = new();
    public List<string> Locaux { get; } = new();
    public List<(string Code, Occurrence Occ)> Signaux { get; } = new();
    public NoeudSchema? Schema { get; set; }
    public int Largeur { get; set; }
    public int Hauteur { get; set; }
    public bool Dictionnaire => Racine is "ResourceDictionary" or "Application";
}

public static class Xaml
{
    public const string NsX = "http://schemas.microsoft.com/winfx/2006/xaml";
    const string NsPresentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    static readonly HashSet<string> NonVisuels = new(StringComparer.Ordinal)
    {
        "RowDefinition", "ColumnDefinition", "Setter", "Style", "Trigger", "DataTrigger", "MultiTrigger", "MultiDataTrigger", "EventTrigger",
        "Condition", "ResourceDictionary", "Storyboard", "BeginStoryboard", "DoubleAnimation", "ColorAnimation", "ThicknessAnimation",
        "ObjectAnimationUsingKeyFrames", "DoubleAnimationUsingKeyFrames", "DiscreteObjectKeyFrame", "EasingDoubleKeyFrame", "SolidColorBrush",
        "LinearGradientBrush", "RadialGradientBrush", "GradientStop", "Thickness", "Binding", "MultiBinding", "DataTemplate", "ControlTemplate",
        "ItemsPanelTemplate", "HierarchicalDataTemplate", "InputBinding", "KeyBinding", "MouseBinding", "CommandBinding", "VisualState",
        "VisualStateGroup", "VisualTransition", "String", "Double", "Int32", "Boolean", "Color", "CornerRadius", "BooleanToVisibilityConverter",
        "DropShadowEffect", "BlurEffect", "ScaleTransform", "RotateTransform", "TranslateTransform", "TransformGroup", "SkewTransform",
        "CollectionViewSource", "ObjectDataProvider", "XmlDataProvider", "FontFamily", "Pen", "GeometryDrawing", "DrawingImage", "DrawingGroup",
        "EventSetter", "SortDescription", "PropertyGroupDescription", "ContentPresenter", "ItemsPresenter", "ScrollContentPresenter",
    };

    static readonly Regex Evenement = new(@"^(Preview\w+|\w*(Click|Changed|Loaded|Unloaded|Closing|Closed|Opened|Activated|Deactivated|KeyDown|KeyUp|MouseDown|MouseUp|MouseMove|MouseEnter|MouseLeave|MouseWheel|Checked|Unchecked|DoubleClick|Drop|DragEnter|DragOver|DragLeave|Executed|CanExecute|Tick|Expanded|Collapsed|Scroll|GotFocus|LostFocus|Selected|Unselected|Initialized|ContentRendered|SizeChanged|TextInput|Sorting|AutoGeneratingColumn|CellEditEnding|RowEditEnding|BeginningEdit))$", RegexOptions.CultureInvariant);
    static readonly Regex Identifiant = new(@"^[A-Za-z_]\w*$", RegexOptions.CultureInvariant);

    public static LectureXaml Lire(string chemin, IEnumerable<string> espacesLocaux)
    {
        var l = new LectureXaml { Chemin = chemin };
        XDocument d;
        try { d = XDocument.Load(chemin, LoadOptions.SetLineInfo); }
        catch (XmlException) { return l; }
        var r = d.Root!;
        l.Racine = r.Name.LocalName;
        l.Classe = (string?)r.Attribute(XName.Get("Class", NsX));
        l.Titre = (string?)r.Attribute("Title");
        l.Largeur = Nombre(r, "Width") ?? NombreD(r, "DesignWidth") ?? 800;
        l.Hauteur = Nombre(r, "Height") ?? NombreD(r, "DesignHeight") ?? 450;
        var locaux = espacesLocaux.ToHashSet(StringComparer.Ordinal);

        foreach (var e in r.DescendantsAndSelf())
        {
            var nom = e.Name.LocalName;
            var ligne = ((IXmlLineInfo)e).LineNumber;
            foreach (var a in e.Attributes())
            {
                if (a.IsNamespaceDeclaration) continue;
                var v = a.Value.Trim();
                if (v.StartsWith("{Binding", StringComparison.Ordinal) || v.StartsWith("{x:Bind", StringComparison.Ordinal)) l.Liaisons++;
                else if (a.Name.Namespace == XNamespace.None && Identifiant.IsMatch(v) && Evenement.IsMatch(a.Name.LocalName)) l.Evenements++;
            }
            if (nom is "Binding" or "MultiBinding") l.Liaisons++;
            if (nom.Contains('.') || e == r) continue;
            if (nom is "Storyboard" or "VisualStateManager.VisualStateGroups" or "Viewport3D" or "VisualStateGroup")
                l.Signaux.Add(("W21", new Occurrence(chemin, ligne, $"<{nom}>")));
            if (nom is "WindowsFormsHost")
                l.Signaux.Add(("W20", new Occurrence(chemin, ligne, $"<{nom}>")));
            if (NonVisuels.Contains(nom) || DansRessources(e)) continue;

            l.Controles++;
            l.Types[nom] = l.Types.GetValueOrDefault(nom) + 1;
            var ns = e.Name.NamespaceName;
            if (ns != NsPresentation && ns.Length > 0)
            {
                var f = Catalogue.Familles.FirstOrDefault(f => f.Interface && f.Prefixes.Any(p => ns.Contains(p.TrimEnd('.'), StringComparison.OrdinalIgnoreCase)));
                if (f != null) l.Tiers.Add($"{f.Famille} : {nom}");
                else if (ns.StartsWith("clr-namespace:", StringComparison.Ordinal))
                {
                    var espace = ns[14..].Split(';')[0];
                    var assemblage = ns.Contains("assembly=") ? ns[(ns.IndexOf("assembly=", StringComparison.Ordinal) + 9)..] : null;
                    if (assemblage == null || locaux.Contains(assemblage) || locaux.Contains(espace)) l.Locaux.Add(nom);
                }
            }
        }
        l.Schema = Disposer(r, l.Largeur, l.Hauteur);
        return l;
    }

    static bool DansRessources(XElement e) => e.Ancestors().Any(a => a.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal) || a.Name.LocalName is "ResourceDictionary");

    static int? Nombre(XElement e, string attribut) =>
        double.TryParse((string?)e.Attribute(attribut), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? (int)v : null;

    static int? NombreD(XElement e, string attribut) =>
        e.Attributes().FirstOrDefault(a => a.Name.LocalName == attribut && a.Name.Namespace != XNamespace.None) is { } a
        && double.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? (int)v : null;

    // ---- Disposition approchée, pour le schéma d'écran ----

    static readonly Dictionary<string, string> Equivalents = new(StringComparer.Ordinal)
    {
        ["TextBlock"] = "Label", ["Label"] = "Label", ["AccessText"] = "Label", ["Menu"] = "MenuStrip", ["ToolBar"] = "ToolStrip", ["ToolBarTray"] = "ToolStrip",
        ["StatusBar"] = "StatusStrip", ["Image"] = "PictureBox", ["DataGrid"] = "DataGridView", ["ListView"] = "ListView", ["ListBox"] = "ListBox",
        ["TreeView"] = "TreeView", ["PasswordBox"] = "TextBox", ["DatePicker"] = "DateTimePicker", ["Slider"] = "TrackBar", ["ProgressBar"] = "ProgressBar",
        ["ToggleButton"] = "Button", ["RepeatButton"] = "Button", ["Hyperlink"] = "LinkLabel", ["GroupBox"] = "GroupBox", ["TabControl"] = "TabControl",
        ["TabItem"] = "TabPage", ["WebBrowser"] = "WebBrowser", ["WebView2"] = "WebView2", ["RichTextBox"] = "RichTextBox", ["ComboBox"] = "ComboBox",
        ["TextBox"] = "TextBox", ["Button"] = "Button", ["CheckBox"] = "CheckBox", ["RadioButton"] = "RadioButton", ["Expander"] = "GroupBox",
    };

    static readonly HashSet<string> Conteneurs = new(StringComparer.Ordinal)
    { "Grid", "StackPanel", "DockPanel", "WrapPanel", "UniformGrid", "Canvas", "Border", "ScrollViewer", "Viewbox", "ContentControl", "GroupBox", "Expander", "TabControl", "TabItem", "UserControl", "Window", "Page", "VirtualizingStackPanel" };

    static int Defaut(string type) => type switch
    {
        "Button" or "ToggleButton" or "RepeatButton" => 26, "TextBox" or "ComboBox" or "PasswordBox" or "DatePicker" => 24,
        "TextBlock" or "Label" or "CheckBox" or "RadioButton" => 20, "Menu" => 22, "ToolBar" or "ToolBarTray" => 28, "StatusBar" => 24,
        "Slider" or "ProgressBar" => 20, _ => 0,
    };

    static IEnumerable<XElement> Enfants(XElement e) =>
        e.Elements().Where(c => !c.Name.LocalName.Contains('.') && !NonVisuels.Contains(c.Name.LocalName))
            .Concat(e.Elements().Where(c => c.Name.LocalName.EndsWith(".Content", StringComparison.Ordinal) || c.Name.LocalName.EndsWith(".Child", StringComparison.Ordinal)).SelectMany(Enfants));

    static string? Libelle(XElement e)
    {
        foreach (var a in new[] { "Content", "Text", "Header", "Title" })
            if ((string?)e.Attribute(a) is { Length: > 0 } v && !v.StartsWith('{')) return v.Replace("_", "");
        if (!e.HasElements && e.Value.Trim() is { Length: > 0 } t) return t;
        return null;
    }

    static int? Taille(XElement e, string a) => Nombre(e, a);

    public static NoeudSchema Disposer(XElement racine, int largeur, int hauteur)
    {
        var compteur = 0;
        var top = new NoeudSchema { Nom = "$this", Type = "Form", Texte = (string?)racine.Attribute("Title"), L = largeur, H = hauteur, Ax = 0, Ay = 0, Al = largeur, Ah = hauteur };
        void Poser(XElement e, NoeudSchema parent, int x, int y, int l, int h, int profondeur)
        {
            if (profondeur > 30 || l < 2 || h < 2) return;
            var type = e.Name.LocalName;
            var n = new NoeudSchema { Nom = $"e{compteur++}", Type = Equivalents.GetValueOrDefault(type, Conteneurs.Contains(type) ? "Panel" : type), Texte = Libelle(e), Ax = x, Ay = y, Al = l, Ah = h, Ancrage = "Calcule" };
            if (type == "Menu") n.Elements.AddRange(e.Elements().Where(m => m.Name.LocalName == "MenuItem").Select(m => "=" + (Libelle(m) ?? "Menu")));
            if (type == "ToolBar") n.Elements.AddRange(e.Elements().Where(m => !m.Name.LocalName.Contains('.')).Select(_ => "outil"));
            if (type == "DataGrid") n.Elements.AddRange(e.Elements().Where(c => c.Name.LocalName.EndsWith(".Columns", StringComparison.Ordinal)).SelectMany(c => c.Elements()).Select(c => "=" + ((string?)c.Attribute("Header") ?? "")));
            parent.Enfants.Add(n);
            var enfants = Enfants(e).ToList();
            if (enfants.Count == 0) return;
            int ix = x, iy = y, il = l, ih = h;
            if (type is "GroupBox" or "Expander") { iy += 18; ih -= 20; ix += 4; il -= 8; }
            if (type == "Border") { ix += 2; iy += 2; il -= 4; ih -= 4; }
            switch (type)
            {
                case "Grid":
                    var lignes = Pistes(e, "Grid.RowDefinitions", "RowDefinition", "Height", ih, enfants, "Grid.Row", true);
                    var colonnes = Pistes(e, "Grid.ColumnDefinitions", "ColumnDefinition", "Width", il, enfants, "Grid.Column", false);
                    foreach (var c in enfants)
                    {
                        int ri = Index(c, "Grid.Row"), ci = Index(c, "Grid.Column");
                        int rs = Math.Max(1, Index(c, "Grid.RowSpan")), cs = Math.Max(1, Index(c, "Grid.ColumnSpan"));
                        ri = Math.Min(ri, lignes.Count - 1); ci = Math.Min(ci, colonnes.Count - 1);
                        var py = iy + lignes.Take(ri).Sum(); var ph = lignes.Skip(ri).Take(rs).Sum();
                        var px = ix + colonnes.Take(ci).Sum(); var pw = colonnes.Skip(ci).Take(cs).Sum();
                        Contenu(c, n, px, py, pw, ph, profondeur);
                    }
                    break;
                case "StackPanel" or "VirtualizingStackPanel" or "WrapPanel" or "ToolBarTray":
                    var horizontal = (string?)e.Attribute("Orientation") == "Horizontal" || type is "WrapPanel";
                    var pos = horizontal ? ix : iy;
                    foreach (var c in enfants)
                    {
                        if (horizontal)
                        {
                            var w = Taille(c, "Width") ?? Math.Max(Math.Min(il / Math.Max(enfants.Count, 1), 160), 40);
                            if (pos + w > ix + il) break;
                            Contenu(c, n, pos + 2, iy, w - 4, Math.Min(ih, Taille(c, "Height") ?? Math.Max(Defaut(c.Name.LocalName), Math.Min(ih, 26))), profondeur);
                            pos += w;
                        }
                        else
                        {
                            var hh = Taille(c, "Height") ?? (Defaut(c.Name.LocalName) is > 0 and var dd ? dd : 60);
                            if (pos + hh > iy + ih) break;
                            Contenu(c, n, ix, pos + 2, Math.Min(il, Taille(c, "Width") ?? il), hh - 4, profondeur);
                            pos += hh;
                        }
                    }
                    break;
                case "DockPanel":
                    var reste = (X: ix, Y: iy, L: il, H: ih);
                    for (var i = 0; i < enfants.Count; i++)
                    {
                        var c = enfants[i];
                        var dock = (string?)c.Attribute("DockPanel.Dock") ?? (i == enfants.Count - 1 && (string?)e.Attribute("LastChildFill") != "False" ? "Fill" : "Left");
                        if (i == enfants.Count - 1 && (string?)e.Attribute("LastChildFill") != "False") dock = "Fill";
                        int th = Taille(c, "Height") ?? (Defaut(c.Name.LocalName) is > 0 and var d1 ? d1 : 30), tw = Taille(c, "Width") ?? 160;
                        switch (dock)
                        {
                            case "Top": Contenu(c, n, reste.X, reste.Y, reste.L, th, profondeur); reste.Y += th; reste.H -= th; break;
                            case "Bottom": Contenu(c, n, reste.X, reste.Y + reste.H - th, reste.L, th, profondeur); reste.H -= th; break;
                            case "Right": Contenu(c, n, reste.X + reste.L - tw, reste.Y, tw, reste.H, profondeur); reste.L -= tw; break;
                            case "Left": Contenu(c, n, reste.X, reste.Y, tw, reste.H, profondeur); reste.X += tw; reste.L -= tw; break;
                            default: Contenu(c, n, reste.X, reste.Y, reste.L, reste.H, profondeur); break;
                        }
                    }
                    break;
                case "Canvas":
                    foreach (var c in enfants)
                        Contenu(c, n, ix + (int)(Taille(c, "Canvas.Left") ?? 0), iy + (int)(Taille(c, "Canvas.Top") ?? 0), Taille(c, "Width") ?? 80, Taille(c, "Height") ?? (Defaut(c.Name.LocalName) is > 0 and var d2 ? d2 : 40), profondeur);
                    break;
                case "TabControl":
                    var items = enfants.Where(c => c.Name.LocalName == "TabItem").ToList();
                    foreach (var (it, i) in items.Select((it, i) => (it, i)))
                    {
                        var page = new NoeudSchema { Nom = $"e{compteur++}", Type = "TabPage", Texte = Libelle(it), Ancrage = "Page", Visible = i == 0, Ax = ix + 2, Ay = iy + 20, Al = il - 4, Ah = ih - 22 };
                        n.Enfants.Add(page);
                        if (i == 0) foreach (var c in Enfants(it)) Contenu(c, page, page.Ax + 4, page.Ay + 4, page.Al - 8, page.Ah - 8, profondeur + 1);
                    }
                    break;
                case "Menu" or "ToolBar" or "StatusBar" or "ListBox" or "ListView" or "TreeView" or "DataGrid" or "ComboBox":
                    break;
                default:
                    foreach (var c in enfants) Contenu(c, n, ix, iy, il, ih, profondeur);
                    break;
            }
        }
        void Contenu(XElement c, NoeudSchema parent, int x, int y, int l, int h, int profondeur)
        {
            var w = Taille(c, "Width"); var hh = Taille(c, "Height");
            var d = Defaut(c.Name.LocalName);
            var ha = (string?)c.Attribute("HorizontalAlignment"); var va = (string?)c.Attribute("VerticalAlignment");
            if (hh == null && d > 0 && !Conteneurs.Contains(c.Name.LocalName)) hh = Math.Min(d, h);
            var ww = Math.Min(w ?? l, l); var hv = Math.Min(hh ?? h, h);
            var px = ha switch { "Right" => x + l - ww, "Center" => x + (l - ww) / 2, _ => x };
            var py = va switch { "Bottom" => y + h - hv, "Center" => y + (h - hv) / 2, _ => y };
            Poser(c, parent, px, py, ww, hv, profondeur + 1);
        }
        foreach (var c in Enfants(racine)) Contenu(c, top, 0, 0, largeur, hauteur, 0);
        return top;
    }

    static int Index(XElement e, string a) => int.TryParse((string?)e.Attribute(a), out var v) ? v : 0;

    static List<int> Pistes(XElement grille, string prop, string elem, string attr, int total, List<XElement> enfants, string attache, bool vertical)
    {
        var defs = grille.Elements().FirstOrDefault(c => c.Name.LocalName == prop)?.Elements().Where(c => c.Name.LocalName == elem).ToList() ?? new();
        if (defs.Count == 0) return new List<int> { total };
        var tailles = new double[defs.Count];
        var etoiles = new double[defs.Count];
        for (var i = 0; i < defs.Count; i++)
        {
            var v = ((string?)defs[i].Attribute(attr) ?? "*").Trim();
            if (v == "Auto")
            {
                var max = enfants.Where(c => Index(c, attache) == i)
                    .Select(c => (vertical ? Taille(c, "Height") : Taille(c, "Width")) ?? (vertical ? (Defaut(c.Name.LocalName) is > 0 and var d ? d : 28) : 100)).DefaultIfEmpty(vertical ? 28 : 100).Max();
                tailles[i] = max + 4;
            }
            else if (v.EndsWith('*')) etoiles[i] = v.Length == 1 ? 1 : double.TryParse(v[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 1;
            else tailles[i] = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var px) ? px : 0;
        }
        var fixe = tailles.Sum();
        var reste = Math.Max(total - fixe, 0);
        var somme = etoiles.Sum();
        var res = new List<int>();
        for (var i = 0; i < defs.Count; i++) res.Add((int)Math.Round(etoiles[i] > 0 ? reste * etoiles[i] / somme : tailles[i]));
        var surplus = res.Sum() - total;
        if (surplus > 0) for (var i = res.Count - 1; i >= 0 && surplus > 0; i--) { var r = Math.Min(res[i], surplus); res[i] -= r; surplus -= r; }
        return res;
    }
}
