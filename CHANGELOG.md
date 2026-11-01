# Journal des versions

## 1.0.0 — 2026-10-24

Première version publique.

* `winmig analyser` : solutions `.sln` et `.slnx`, projets `.csproj` et `.vbproj` (format SDK et ancien format, `packages.config`, `Directory.Build.props`, `Directory.Packages.props`), C# et VB.NET, WinForms et WPF.
* Écrans : fenêtres, dialogues, composants, pages ; contrôles, événements, liaisons, logique, complexité S à XL ; schéma redessiné depuis le concepteur (`InitializeComponent`, `.resx` localisés, XAML), composants imbriqués dessinés à leur place.
* Périmètre calculé depuis les points d'entrée (`Main`, `App.xaml`, `StartupObject` VB) ; écrans jamais appelés listés à part.
* 21 règles d'usages propres au poste Windows, avec chaque emplacement.
* Dépendances classées par famille (suites de composants, grilles, graphiques, protocoles industriels, matériel…) et leur sort côté web.
* Chiffrage en unités d'écran, plan de lots, calendrier ; rapport A4 HTML autonome, Markdown, JSON, CSV, SVG.
* `winmig rapport` : refait le rapport depuis `inventaire.json` (avis de l'auditeur, `--inclure`, `--exclure`), sans le code source.
* `--sans-code` : aucun extrait de code, ni dans le rapport ni dans `inventaire.json`.
* Exécutable Windows autonome (empreintes dans `SHA256SUMS.txt`) et outil .NET à construire depuis les sources.
* Rapports Crystal Reports (`.rpt`) inventoriés avec le code qui les ouvre, chiffrés à part au forfait par rapport.
* `scripts/comparer-pdf.mjs` : comparaison d'un PDF migré avec son PDF de référence (pages rendues une par une, texte mot à mot, pixels), codes de sortie 0, 1 et 2.
* `inventaire.json` sans aucun chemin absolu ; `--sans-libelles` retire aussi les textes des écrans.
* Page « Passage à .NET 10 » : cibles en fin de support, 32 bits, COM, Office, Crystal Reports, pilotes SQL retirés, envoi de mails en SMTP, WCF et System.Web, API retirées, `System.Data.SqlClient`, projets .NET Framework ; chaque point a une portée (à faire, à vérifier, pour le web ou Linux, pour information), ce qu'en fait GitHub Copilot app modernization, et une phrase de verdict avec le palier du forfait (`pointsNet10` dans `inventaire.json`). Commentaires ignorés, fichiers rattachés au seul projet qui les contient.
