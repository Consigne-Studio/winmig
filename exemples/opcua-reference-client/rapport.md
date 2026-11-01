# Audit de migration : OPC UA Reference Client

DEV-605 · 24/10/2026 · pour Exemple public · winmig 1.0.0

## Avis

**Migration faisable, sans rupture technique.** La bibliothèque client OPC UA (.NET Standard) passe telle quelle côté serveur et devient la passerelle du lot 1. Le vrai sujet est la logique des grilles d'édition de valeurs (EditComplexValueCtrl, DataListCtrl), à reprendre avec soin.

- Lot 1 (19,5 unités, le plus gros) : socle web, passerelle OPC UA et fenêtre principale. Les suivants font 10 unités ou moins.
- Retirés à la relecture : SampleForm et SampleUserControl, deux gabarits vides. ClientUtils et GuiUtils, sans interface, restent au prix : leur logique part dans des services.
- Les 67 autres écrans hors périmètre viennent de la bibliothèque partagée : aucun n'est ajouté sans votre accord.

## Ce que dit l'inventaire

- **25 écrans WinForms** à migrer : 1 fenêtre, 11 boîtes de dialogue, 13 composants, sur 5 projets et 36 823 lignes de code.
- **69 autres écrans sont dans le code mais jamais appelés** depuis le point d'entrée (Program). Ils sont exclus du prix et chiffrés à part (58 unités), à confirmer avec vous.
- Complexité : 14 S, 8 M, 3 L. Les plus lourds : EditComplexValueCtrl (L, 892 lignes de logique), DataListCtrl (L, 687 lignes de logique), BaseListCtrl (L, 526 lignes de logique).
- **Ce qui ne passe pas tel quel dans un navigateur** : protocole industriel utilisé depuis le poste (39 fichiers). Chaque point a sa solution et son coût en page « Points d'attention ».
- Chantiers transverses chiffrés une fois, au premier lot : protocole industriel utilisé depuis le poste (6 u.) ; synchronisation avec le fil d'interface (1 u.) ; fichiers du poste et boîtes de dialogue (1 u.) ; lancement de programmes (1 u.) ; DataSet et liaison ADO.NET (1 u.) ; presse-papiers et glisser-déposer (0,5 u.).
- Aucune suite de composants commerciale dans l'interface : pas de licence à racheter pour le web.
- L'application reçoit des données en continu (14 points de synchronisation avec l'interface) : le socle web prévoit un canal SignalR dès le premier lot.
- 51 gestionnaires d'événements à reprendre, et 5 554 lignes de logique d'écran : c'est cette logique qui fait l'essentiel du travail, pas le dessin des écrans.

## Plan de lots

| Lot | Contenu | Écrans | Unités | Jours | Prix HT |
|---|---|---:|---:|---:|---:|
| 01 | Socle web, temps réel et chantiers transverses ; fenêtre principale MainForm ; 6 écrans de UA Client Controls et Reference Client | 6 | 19,5 | 47–62 | 48 750 € |
| 02 | 12 écrans de UA Client Controls | 12 | 10 | 24–32 | 25 000 € |
| 03 | 7 écrans de UA Client Controls | 7 | 6 | 14–19 | 15 000 € |
| | **Total** | 25 | 35,5 | 85–114 | **88 750 €** |

## Écrans

| Lot | Écran | Nature | Contrôles | Événements | Lignes | Classe | Unités | Points |
|---:|---|---|---:|---:|---:|---|---:|---|
| 01 | `AttributesListViewCtrl` | Composant | 3 | 2 | 204 | M | 1 | W18 |
| 01 | `BrowseTreeViewCtrl` | Composant | 3 | 3 | 331 | M | 1 | W18 |
| 01 | `BrowseNodeCtrl` | Composant | 3 | 0 | 93 | S | 0,5 | W18 |
| 01 | `ConnectServerCtrl` | Composant | 3 | 1 | 368 | M | 1 | W07 W18 |
| 01 | `HeaderBranding` | Composant | 5 | 3 | 35 | S | 0,5 | W12 W18 |
| 01 | `MainForm` | Fenêtre | 16 | 9 | 252 | M | 1 | W09 W12 W18 |
| 02 | `ClientUtils` | Composant | 0 | 0 | 482 | M | 1 | W18 |
| 02 | `ExceptionDlg` | Dialogue | 5 | 1 | 148 | S | 0,5 | W18 |
| 02 | `DataListCtrl` | Composant | 6 | 6 | 687 | L | 2 | W18 |
| 02 | `NodeIdCtrl` | Composant | 2 | 2 | 114 | S | 0,5 | W18 |
| 02 | `BaseListCtrl` | Composant | 1 | 7 | 526 | L | 2 | W11 W18 |
| 02 | `GuiUtils` | Composant | 0 | 0 | 391 | M | 1 | W18 |
| 02 | `ComplexValueEditDlg` | Dialogue | 5 | 1 | 57 | S | 0,5 | W18 |
| 02 | `DateTimeValueEditDlg` | Dialogue | 5 | 0 | 41 | S | 0,5 | W18 |
| 02 | `NodeIdValueEditDlg` | Dialogue | 5 | 0 | 42 | S | 0,5 | W18 |
| 02 | `NumericValueEditDlg` | Dialogue | 5 | 0 | 109 | S | 0,5 | W18 |
| 02 | `SimpleValueEditDlg` | Dialogue | 6 | 1 | 73 | S | 0,5 | W18 |
| 02 | `StringValueEditDlg` | Dialogue | 5 | 0 | 30 | S | 0,5 | W18 |
| 03 | `DiscoverServerDlg` | Dialogue | 6 | 4 | 141 | M | 1 | W18 |
| 03 | `SelectNodeCtrl` | Composant | 1 | 1 | 123 | S | 0,5 | W18 |
| 03 | `EditComplexValueCtrl` | Composant | 3 | 3 | 892 | L | 2 | W14 W18 |
| 03 | `EditComplexValueDlg` | Dialogue | 9 | 5 | 162 | M | 1 | W18 |
| 03 | `SelectNodeDlg` | Dialogue | 4 | 0 | 79 | S | 0,5 | W18 |
| 03 | `SetTypeDlg` | Dialogue | 11 | 1 | 112 | S | 0,5 | W18 |
| 03 | `SelectFileCtrl` | Composant | 1 | 1 | 62 | S | 0,5 | W09 W18 |

## Points d'attention

### W18 — Protocole industriel utilisé depuis le poste (élevée)

Le fichier manipule directement les objets du protocole (session, nœuds, abonnements, registres) : en web, la connexion à l'automate ou au serveur OPC doit partir d'un serveur. **Solution :** Une passerelle .NET garde la connexion (OPC UA, Modbus, S7) et pousse les valeurs à l'IHM par SignalR ; les écritures passent par une API qui contrôle les droits.

78 occurrences dans le périmètre · coût : 9 à 24 jours.

### W07 — Synchronisation avec le fil d'interface (moyenne)

Invoke, BeginInvoke et Dispatcher signalent des données qui arrivent d'un autre fil : c'est le temps réel de l'application. **Solution :** Pousser les données par SignalR depuis le serveur ; côté Angular, les recevoir dans des signaux. Le modèle d'arrivée des données est à redessiner une fois, pour toute l'application.

14 occurrences dans le périmètre · coût : 1 à 3,5 jours.

### W09 — Fichiers du poste et boîtes de dialogue (moyenne)

Le navigateur n'a pas accès au disque : il ne fait que téléverser et télécharger. **Solution :** Remplacer les ouvertures et enregistrements par un téléversement ou un téléchargement ; ranger les fichiers partagés côté serveur.

2 occurrences dans le périmètre · coût : 1 à 3,5 jours.

### W12 — Lancement de programmes (moyenne)

Un navigateur ne lance pas d'exécutable sur le poste. **Solution :** Remplacer par un lien, un téléchargement ou une action exécutée côté serveur.

3 occurrences dans le périmètre · coût : 0,5 à 2,5 jours.

### W14 — DataSet et liaison ADO.NET (moyenne)

Les DataSet, DataTable et BindingSource lient l'écran à la forme des tables. **Solution :** Définir des objets de transfert (DTO) servis par l'API ; les grilles web se lient à ces objets.

5 occurrences dans le périmètre · coût : 1 à 3 jours.

### W11 — Presse-papiers et glisser-déposer (faible)

Ces échanges existent sur le web, avec des API et des droits différents. **Solution :** API Clipboard du navigateur et glisser-déposer HTML (Angular CDK).

5 occurrences dans le périmètre · coût : 0,5 à 1,5 jours.

## Passage à .NET 10

**Rien n'empêche .NET 10.** Les projets ciblent déjà .NET 10 ; les anciennes cibles listées sont à retirer quand plus rien ne les utilise.

| Point | Portée | Projet | Détail | Copilot | Ce que nous faisons | Fichiers |
|---|---|---|---|---|---|---|
| N01 Cible .NET hors support ou en fin de support | Pour information | Opc.Ua.Samples.Client | net8.0 : fin de support le 10/11/2026 (net10.0 déjà ciblé : ancienne cible à retirer quand plus rien ne l'utilise) | Le fait : changement de cible et mise à jour des paquets. | Passer à net10.0 (LTS, supporté jusqu'en novembre 2028), mettre à jour les paquets, corriger les changements cassants, relancer tests et pipeline. | — |
| N01 Cible .NET hors support ou en fin de support | Pour information | Opc.Ua.Samples.Hosting | net8.0 : fin de support le 10/11/2026 (net10.0 déjà ciblé : ancienne cible à retirer quand plus rien ne l'utilise) | Le fait : changement de cible et mise à jour des paquets. | Passer à net10.0 (LTS, supporté jusqu'en novembre 2028), mettre à jour les paquets, corriger les changements cassants, relancer tests et pipeline. | — |

## Dépendances

| Paquet | Version | Catégorie | Risque | Côté web |
|---|---|---|---|---|
| OPCFoundation.NetStandard.Opc.Ua.Bindings.Https | 2.0.1 | Protocole industriel | Moyenne | Reste dans un service .NET serveur, valeurs poussées à l'IHM par SignalR |
| OPCFoundation.NetStandard.Opc.Ua.Client | 2.0.1 | Protocole industriel | Moyenne | Reste dans un service .NET serveur, valeurs poussées à l'IHM par SignalR |
| OPCFoundation.NetStandard.Opc.Ua.Configuration | 2.0.1 | Protocole industriel | Moyenne | Reste dans un service .NET serveur, valeurs poussées à l'IHM par SignalR |
| OPCFoundation.NetStandard.Opc.Ua.Core | 2.0.1 | Protocole industriel | Moyenne | Reste dans un service .NET serveur, valeurs poussées à l'IHM par SignalR |
| OPCFoundation.NetStandard.Opc.Ua.Server | 2.0.1 | Protocole industriel | Moyenne | Reste dans un service .NET serveur, valeurs poussées à l'IHM par SignalR |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.13 | Infrastructure .NET | Faible | Conservée côté serveur (ASP.NET Core l'intègre) |
| Microsoft.Extensions.Hosting | 10.0.13 | Infrastructure .NET | Faible | Conservée côté serveur (ASP.NET Core l'intègre) |
| Microsoft.Extensions.Logging.Abstractions | 10.0.13 | Journalisation | Faible | Conservée côté serveur ; journal du client web à ajouter |
| Microsoft.Extensions.Logging.Console | 10.0.13 | Journalisation | Faible | Conservée côté serveur ; journal du client web à ajouter |
| Serilog | 4.4.0 | Journalisation | Faible | Conservée côté serveur ; journal du client web à ajouter |
| Serilog.Extensions.Logging | 10.0.0 | Journalisation | Faible | Conservée côté serveur ; journal du client web à ajouter |
| Serilog.Sinks.Debug | 3.0.0 | Journalisation | Faible | Conservée côté serveur ; journal du client web à ajouter |
| Serilog.Sinks.File | 7.0.0 | Journalisation | Faible | Conservée côté serveur ; journal du client web à ajouter |

## Hors périmètre (69 écrans, 58 unités)

`AttributeListCtrl`, `BaseTreeCtrl`, `BrowseListCtrl`, `BrowseTreeCtrl`, `CallRequestDlg`, `CallRequestListViewCtrl`, `CertificateDlg`, `CertificateListCtrl`, `CertificateListDlg`, `CertificatePropertiesListCtrl`, `CertificateStoreCtrl`, `CertificateStoreDlg`, `CertificateStoreTreeCtrl`, `CertificateStoreTreeDlg`, `ConfiguredServerDlg`, `ConfiguredServerListCtrl`, `ConfiguredServerListDlg`, `DiscoveredServerListCtrl`, `DiscoveredServerListDlg`, `DiscoveredServerOnNetworkListCtrl`, `DiscoveredServerOnNetworkListDlg`, `EditAnnotationDlg`, `EditArrayDlg`, `EditComplexValue2Dlg`, `EditDataValueCtrl`, `EditDataValueDlg`, `EditMonitoredItemDlg`, `EditReadValueIdDlg`, `EditSubscriptionDlg`, `EditValue2Ctrl`, `EditValueCtrl`, `EditWriteValueDlg`, `EndpointSelectorCtrl`, `EventFilterListViewCtrl`, `EventListView`, `EventListViewCtrl`, `GdsDiscoverServersDlg`, `HistoryDataDlg`, `HistoryDataListView`, `HistoryEventCtrl`, `HostListCtrl`, `HostListDlg`, `NodeListCtrl`, `ReadRequestDlg`, `ReadRequestListViewCtrl`, `ReferenceListCtrl`, `ReferenceTypeCtrl`, `SelectCertificateStoreCtrl`, `SelectHostCtrl`, `SelectLocaleDlg`, `SelectNodesDlg`, `SelectProfileCtrl`, `SelectProfileDlg`, `SelectUrlsCtrl`, `SetFilterOperatorDlg`, `SubscribeDataDlg`, `SubscribeDataListViewCtrl`, `SubscribeEventsDlg`, `TypeFieldsListViewCtrl`, `UserNamePasswordDlg`, `UsernameTokenDlg`, `ViewCertificateDlg`, `ViewEventDetailsDlg`, `ViewNodeStateDlg`, `WriteRequestDlg`, `WriteRequestListViewCtrl`, `YesNoDlg`, `SampleForm`, `SampleUserControl`
