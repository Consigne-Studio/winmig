namespace Winmig.Core;

/// <summary>Une famille de dépendances reconnue par son préfixe (paquet NuGet, assemblage ou espace de noms).</summary>
public sealed record FamilleDependance(
    string Famille,
    string[] Prefixes,
    string Categorie,
    bool Interface,
    Gravite Risque,
    string Remplacement);

/// <summary>Une règle de détection d'API propre au poste Windows.</summary>
public sealed record RegleApi(
    string Code,
    string Titre,
    Gravite Gravite,
    string Pourquoi,
    string Solution,
    double JoursBas,
    double JoursHaut,
    string[] Identifiants,
    string[] Membres,
    string[] Attributs,
    string[] Surcharges,
    string[] Usings,
    bool EcranSeulement = false,
    double UnitesParEcran = 0)
{
    public static readonly string[] Aucun = Array.Empty<string>();
}

public static class Catalogue
{
    public static readonly FamilleDependance[] Familles =
    {
        new("DevExpress", new[] { "DevExpress" }, "Suite de composants d'interface", true, Gravite.Elevee,
            "DevExtreme (même éditeur, licence distincte) ou AG Grid et composants Angular"),
        new("Telerik", new[] { "Telerik" }, "Suite de composants d'interface", true, Gravite.Elevee,
            "Kendo UI (même éditeur, licence distincte) ou AG Grid et Angular CDK"),
        new("Infragistics", new[] { "Infragistics" }, "Suite de composants d'interface", true, Gravite.Elevee,
            "Ignite UI (même éditeur) ou AG Grid"),
        new("ComponentOne", new[] { "C1.", "ComponentOne" }, "Suite de composants d'interface", true, Gravite.Elevee,
            "Wijmo (même éditeur) ou AG Grid"),
        new("Syncfusion", new[] { "Syncfusion" }, "Suite de composants d'interface", true, Gravite.Elevee,
            "Essential JS 2 (même éditeur) ou composants Angular"),
        new("Xceed / Extended WPF Toolkit", new[] { "Xceed", "Extended.Wpf.Toolkit", "DotNetProjects.Extended.Wpf.Toolkit" }, "Contrôles complémentaires", true, Gravite.Moyenne,
            "Angular CDK et composants maison"),
        new("Thème WPF", new[] { "MahApps.Metro", "MaterialDesignThemes", "MaterialDesignColors", "ModernWpf", "HandyControl", "Wpf.Ui", "WPF-UI", "Fluent.Ribbon" }, "Thème et style", true, Gravite.Info,
            "Thème du design system web (jetons CSS)"),
        new("Graphiques", new[] { "ZedGraph", "OxyPlot", "LiveCharts", "LiveChartsCore", "ScottPlot", "Steema", "TeeChart", "NationalInstruments", "InteractiveDataDisplay", "System.Windows.Forms.DataVisualization", "WinForms.DataVisualization" }, "Graphiques et courbes", true, Gravite.Moyenne,
            "uPlot (courbes temps réel) ou Apache ECharts"),
        new("Ancrage de fenêtres", new[] { "WeifenLuo", "DockPanelSuite", "AvalonDock", "Dirkster.AvalonDock", "Krypton" }, "Ancrage et fenêtres", true, Gravite.Moyenne,
            "dockview ou Golden Layout"),
        new("Grilles et listes", new[] { "ObjectListView", "SourceGrid", "BrightIdeasSoftware", "XPTable" }, "Grilles et listes", true, Gravite.Moyenne,
            "AG Grid Community ou table Angular CDK"),
        new("Éditeurs de texte", new[] { "ScintillaNET", "Scintilla", "FastColoredTextBox", "AvalonEdit", "ICSharpCode.AvalonEdit" }, "Éditeur de texte", true, Gravite.Moyenne,
            "Monaco Editor ou CodeMirror 6"),
        new("Navigateur intégré", new[] { "CefSharp", "Microsoft.Web.WebView2", "EO.WebBrowser" }, "Navigateur intégré", true, Gravite.Info,
            "Contenu web intégré directement dans l'application"),
        new("États et impression", new[] { "Microsoft.ReportViewer", "Microsoft.ReportingServices", "CrystalDecisions", "Stimulsoft", "FastReport", "ReportViewerCore" }, "États et impression", true, Gravite.Elevee,
            "PDF généré côté serveur (QuestPDF) ou états HTML imprimables"),
        new("Office", new[] { "Microsoft.Office.Interop", "Office", "Interop.Excel", "Interop.Word" }, "Automatisation Office", false, Gravite.Elevee,
            "Fichiers générés côté serveur (ClosedXML, Open XML SDK)"),
        new("Cadre MVVM", new[] { "Prism", "Caliburn.Micro", "MvvmLight", "MvvmLightLibs", "CommunityToolkit.Mvvm", "Microsoft.Toolkit.Mvvm", "ReactiveUI", "Stylet", "Catel" }, "Cadre MVVM", false, Gravite.Info,
            "La logique des ViewModels se transpose en services et signaux Angular"),
        new("Accès aux données", new[] { "EntityFramework", "Microsoft.EntityFrameworkCore", "Dapper", "System.Data.SqlClient", "Microsoft.Data.SqlClient", "Npgsql", "Oracle.", "MySql.Data", "MySqlConnector", "System.Data.SQLite", "Microsoft.Data.Sqlite", "NHibernate", "LiteDB" }, "Accès aux données", false, Gravite.Info,
            "Reste côté serveur, derrière une API ASP.NET Core"),
        new("Protocoles industriels", new[] { "OPCFoundation", "Opc.Ua", "OpcLabs", "OPCAutomation", "Interop.OPCAutomation", "NModbus", "EasyModbus", "FluentModbus", "S7netplus", "S7.Net", "Sharp7", "libplctag", "Beckhoff.TwinCAT", "TwinCAT.Ads", "MQTTnet", "M2Mqtt" }, "Protocole industriel", false, Gravite.Moyenne,
            "Reste dans un service .NET serveur, valeurs poussées à l'IHM par SignalR"),
        new("Matériel", new[] { "System.IO.Ports", "LibUsbDotNet", "HidLibrary", "HidSharp", "System.Management", "InTheHand" }, "Accès matériel", false, Gravite.Elevee,
            "Service local ou passerelle .NET, exposé par API"),
        new("Journalisation", new[] { "log4net", "NLog", "Serilog", "Microsoft.Extensions.Logging" }, "Journalisation", false, Gravite.Info,
            "Conservée côté serveur ; journal du client web à ajouter"),
        new("Sérialisation", new[] { "Newtonsoft.Json", "System.Text.Json", "protobuf-net", "Google.Protobuf", "YamlDotNet", "CsvHelper" }, "Sérialisation", false, Gravite.Info,
            "Conservée côté serveur"),
        new("Injection et hébergement", new[] { "Microsoft.Extensions.", "Autofac", "Unity", "Ninject", "SimpleInjector", "Castle." }, "Infrastructure .NET", false, Gravite.Info,
            "Conservée côté serveur (ASP.NET Core l'intègre)"),
        new("Compatibilité Windows", new[] { "Microsoft.Windows.Compatibility", "Microsoft.Win32.Registry", "System.Drawing.Common" }, "Compatibilité Windows", false, Gravite.Moyenne,
            "À remplacer par des équivalents multiplateformes côté serveur"),
        new("Tests", new[] { "xunit", "NUnit", "MSTest", "Moq", "FluentAssertions", "NSubstitute", "coverlet", "Microsoft.NET.Test.Sdk" }, "Tests", false, Gravite.Info,
            "Les tests de la logique métier sont conservés"),
    };

    /// <summary>Assemblages du framework, ignorés dans la liste des dépendances.</summary>
    public static bool EstFramework(string nom) =>
        nom is "mscorlib" or "WindowsBase" or "PresentationCore" or "PresentationFramework" or "UIAutomationProvider"
            or "UIAutomationTypes" or "ReachFramework" or "Accessibility" or "Microsoft.CSharp" or "Microsoft.VisualBasic"
            or "netstandard" or "WindowsFormsIntegration" or "System"
        || (nom.StartsWith("System.", StringComparison.Ordinal) && !nom.StartsWith("System.IO.Ports", StringComparison.Ordinal)
            && !nom.StartsWith("System.Management", StringComparison.Ordinal) && !nom.StartsWith("System.Data.SQLite", StringComparison.Ordinal)
            && !nom.StartsWith("System.Data.SqlClient", StringComparison.Ordinal) && !nom.StartsWith("System.Windows.Forms.DataVisualization", StringComparison.Ordinal)
            && !nom.StartsWith("System.Text.Json", StringComparison.Ordinal) && !nom.StartsWith("System.Drawing.Common", StringComparison.Ordinal));

    public static FamilleDependance? Famille(string nom)
    {
        FamilleDependance? meilleure = null;
        var longueur = 0;
        foreach (var f in Familles)
            foreach (var p in f.Prefixes)
                if (nom.StartsWith(p, StringComparison.OrdinalIgnoreCase) && p.Length > longueur)
                {
                    meilleure = f;
                    longueur = p.Length;
                }
        return meilleure;
    }

    /// <summary>Famille d'un type de contrôle trouvé dans un écran (espace de noms complet ou préfixe de type).</summary>
    public static FamilleDependance? FamilleControle(string type)
    {
        var f = Famille(type);
        return f is { Interface: true } ? f : null;
    }

    public static readonly RegleApi[] Regles =
    {
        new("W01", "Port série et périphériques du poste", Gravite.Elevee,
            "Un navigateur n'ouvre ni port COM, ni périphérique USB ou HID du poste.",
            "Déplacer l'accès dans un service .NET installé près du matériel (Worker Service) ; l'IHM web le pilote par API et reçoit les mesures par SignalR.",
            4, 8,
            new[] { "SerialPort", "HidDevice", "UsbDevice", "ManagementObjectSearcher", "ManagementObject" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun,
            new[] { "System.IO.Ports", "System.Management", "LibUsbDotNet", "HidLibrary", "HidSharp" }),
        new("W02", "Appels natifs (P/Invoke)", Gravite.Elevee,
            "Les DLL natives et les API Win32 ne sont pas appelables depuis un navigateur.",
            "Classer chaque appel : les appels métier passent dans un service serveur, ceux qui servent l'interface (fenêtres, curseur, son) sont remplacés par leur équivalent web.",
            2, 6,
            RegleApi.Aucun, RegleApi.Aucun, new[] { "DllImport", "LibraryImport" }, RegleApi.Aucun, RegleApi.Aucun),
        new("W03", "Composants COM et ActiveX", Gravite.Elevee,
            "Un contrôle ActiveX ou un serveur COM ne s'exécute que sous Windows, dans le processus de l'application.",
            "Remplacer le contrôle ActiveX par un composant web ; isoler les serveurs COM restants dans un service Windows exposé par API.",
            4, 10,
            new[] { "AxHost", "GetTypeFromProgID", "ReleaseComObject", "FinalReleaseComObject" }, RegleApi.Aucun, new[] { "ComImport" }, RegleApi.Aucun,
            RegleApi.Aucun),
        new("W04", "Registre Windows", Gravite.Moyenne,
            "Le registre du poste n'existe pas côté navigateur, et chaque utilisateur aura le même serveur.",
            "Ranger les réglages machine dans la configuration du serveur, les préférences utilisateur dans une table servie par l'API.",
            1, 2,
            new[] { "RegistryKey" }, new[] { "Registry.LocalMachine", "Registry.CurrentUser", "Registry.ClassesRoot", "Registry.Users", "Registry.GetValue", "Registry.SetValue" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun),
        new("W05", "Accès aux données depuis l'écran", Gravite.Elevee,
            "L'écran parle directement à la base : côté web, ce code ne peut pas rester dans le navigateur (identifiants exposés, aucun contrôle d'accès).",
            "Extraire chaque requête dans une API ASP.NET Core avec contrôle d'accès ; l'écran web n'appelle que l'API. Compté écran par écran dans le chiffrage.",
            0, 0,
            new[] { "SqlConnection", "SqlCommand", "SqlDataAdapter", "OleDbConnection", "OleDbCommand", "OracleConnection", "OracleCommand", "NpgsqlConnection", "MySqlConnection", "SqliteConnection", "SQLiteConnection", "DbContext", "ExecuteReader", "ExecuteNonQuery", "ExecuteScalar", "TableAdapterManager" },
            RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun, EcranSeulement: true, UnitesParEcran: 0.5),
        new("W06", "Dessin personnalisé", Gravite.Moyenne,
            "Le dessin GDI+ ou WPF (OnPaint, OnRender, Graphics) est propre au bureau.",
            "Redessiner en SVG (schémas, symboles) ou en canvas 2D (courbes denses) ; compté écran par écran dans le chiffrage.",
            0, 0,
            new[] { "PaintEventArgs", "CreateGraphics", "DrawingContext", "GraphicsPath" }, RegleApi.Aucun, RegleApi.Aucun, new[] { "OnPaint", "OnPaintBackground", "OnRender" },
            RegleApi.Aucun, EcranSeulement: true, UnitesParEcran: 0.5),
        new("W07", "Synchronisation avec le fil d'interface", Gravite.Moyenne,
            "Invoke, BeginInvoke et Dispatcher signalent des données qui arrivent d'un autre fil : c'est le temps réel de l'application.",
            "Pousser les données par SignalR depuis le serveur ; côté Angular, les recevoir dans des signaux. Le modèle d'arrivée des données est à redessiner une fois, pour toute l'application.",
            1, 3,
            new[] { "InvokeRequired", "BackgroundWorker", "Dispatcher", "SynchronizationContext" }, new[] { "Invoke", "BeginInvoke" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun),
        new("W08", "Rafraîchissement par minuterie", Gravite.Info,
            "Une minuterie d'écran interroge les données à intervalle fixe.",
            "Remplacer le sondage par un abonnement côté serveur (SignalR) ; garder une minuterie seulement pour l'affichage (horloge, clignotement).",
            0.5, 2,
            new[] { "DispatcherTimer" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun),
        new("W09", "Fichiers du poste et boîtes de dialogue", Gravite.Moyenne,
            "Le navigateur n'a pas accès au disque : il ne fait que téléverser et télécharger.",
            "Remplacer les ouvertures et enregistrements par un téléversement ou un téléchargement ; ranger les fichiers partagés côté serveur.",
            1, 3,
            new[] { "OpenFileDialog", "SaveFileDialog", "FolderBrowserDialog", "OpenFolderDialog" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun),
        new("W10", "Impression et états", Gravite.Moyenne,
            "L'impression pilotée par l'application (PrintDocument, ReportViewer, Crystal) n'existe pas dans un navigateur.",
            "Générer les états en PDF côté serveur (QuestPDF) ; l'impression passe par le navigateur.",
            2, 5,
            new[] { "PrintDocument", "PrintDialog", "PrintPreviewDialog", "PrintPreviewControl", "ReportViewer", "ReportDocument", "PrintVisual", "PageSetupDialog" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun, new[] { "CrystalDecisions", "Microsoft.Reporting" }),
        new("W11", "Presse-papiers et glisser-déposer", Gravite.Info,
            "Ces échanges existent sur le web, avec des API et des droits différents.",
            "API Clipboard du navigateur et glisser-déposer HTML (Angular CDK).",
            0.5, 1.5,
            new[] { "Clipboard", "DoDragDrop", "DragEventArgs" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun),
        new("W12", "Lancement de programmes", Gravite.Moyenne,
            "Un navigateur ne lance pas d'exécutable sur le poste.",
            "Remplacer par un lien, un téléchargement ou une action exécutée côté serveur.",
            0.5, 2,
            RegleApi.Aucun, new[] { "Process.Start" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun),
        new("W13", "WCF ou .NET Remoting", Gravite.Elevee,
            "WCF côté client et .NET Remoting n'existent plus dans .NET récent et ne passent pas dans un navigateur.",
            "Exposer les mêmes opérations en API REST ou gRPC-Web ; CoreWCF peut garder les clients existants pendant la transition.",
            3, 8,
            new[] { "ChannelFactory", "ServiceHost", "RemotingConfiguration" }, RegleApi.Aucun, new[] { "ServiceContract", "OperationContract" }, RegleApi.Aucun,
            new[] { "System.ServiceModel", "System.Runtime.Remoting" }),
        new("W14", "DataSet et liaison ADO.NET", Gravite.Moyenne,
            "Les DataSet, DataTable et BindingSource lient l'écran à la forme des tables.",
            "Définir des objets de transfert (DTO) servis par l'API ; les grilles web se lient à ces objets.",
            1, 3,
            new[] { "DataSet", "DataTable", "BindingSource", "DataView", "DataRowView" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun),
        new("W15", "Configuration locale", Gravite.Info,
            "app.config, Settings et ConfigurationManager lisent des fichiers du poste.",
            "Configuration du serveur (appsettings) et préférences utilisateur servies par l'API.",
            0.5, 1.5,
            new[] { "ConfigurationManager" }, new[] { "Settings.Default" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun),
        new("W16", "Messages Windows et raccourcis système", Gravite.Elevee,
            "WndProc, SendMessage et les crochets clavier dépendent de la boucle de messages Windows.",
            "Raccourcis clavier gérés dans la page ; le reste se traite au cas par cas, rarement à l'identique.",
            1, 3,
            new[] { "SetWindowsHookEx", "RegisterHotKey" }, RegleApi.Aucun, RegleApi.Aucun, new[] { "WndProc", "ProcessCmdKey", "PreProcessMessage" }, RegleApi.Aucun),
        new("W17", "Sockets et réseau bas niveau", Gravite.Moyenne,
            "Un navigateur n'ouvre ni socket TCP ni socket UDP.",
            "Le dialogue réseau passe dans un service serveur ; l'IHM l'interroge par API et s'abonne par SignalR.",
            2, 5,
            new[] { "TcpClient", "UdpClient", "TcpListener", "Socket", "NetworkStream" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun),
        new("W18", "Protocole industriel utilisé depuis le poste", Gravite.Elevee,
            "Le fichier manipule directement les objets du protocole (session, nœuds, abonnements, registres) : en web, la connexion à l'automate ou au serveur OPC doit partir d'un serveur.",
            "Une passerelle .NET garde la connexion (OPC UA, Modbus, S7) et pousse les valeurs à l'IHM par SignalR ; les écritures passent par une API qui contrôle les droits.",
            3, 8,
            RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun,
            new[] { "Opc.Ua", "OpcLabs", "OPCAutomation", "Modbus", "NModbus", "EasyModbus", "S7.Net", "Sharp7", "libplctag", "TwinCAT.Ads", "MQTTnet" }),
        new("W19", "Identité Windows", Gravite.Moyenne,
            "L'application lit l'utilisateur Windows connecté.",
            "Authentification côté serveur : Windows intégrée (Negotiate) sur l'intranet, ou OpenID Connect.",
            1, 3,
            new[] { "WindowsIdentity", "WindowsPrincipal" }, new[] { "Environment.UserName", "Environment.UserDomainName" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun),
        new("W20", "WinForms et WPF mélangés", Gravite.Moyenne,
            "Des écrans hébergent l'autre technologie (WindowsFormsHost, ElementHost).",
            "Les deux parties sont reprises dans le même composant web ; vérifier qu'aucun contrôle hébergé n'est un ActiveX.",
            1, 2,
            new[] { "WindowsFormsHost", "ElementHost" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun),
        new("W21", "Animations et 3D WPF", Gravite.Moyenne,
            "Storyboard, états visuels et Viewport3D n'ont pas d'équivalent direct.",
            "Animations CSS pour l'interface, WebGL (three.js) pour la 3D ; compté écran par écran dans le chiffrage.",
            0, 0,
            new[] { "Storyboard", "Viewport3D", "VisualStateManager", "DoubleAnimation", "ColorAnimation" }, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun, RegleApi.Aucun,
            EcranSeulement: true, UnitesParEcran: 0.5),
    };

    public static RegleApi Regle(string code) => Regles.First(r => r.Code == code);
}
