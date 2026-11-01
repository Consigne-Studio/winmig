# Méthode : migrer une application WinForms ou WPF vers le web, au forfait

Cette méthode décrit comment Consigne audite puis migre une application de bureau .NET vers une application web, entièrement à distance. Elle est publique : le client sait à l'avance comment son prix est calculé et ce qu'il reçoit à chaque étape.

## 1. Principes

1. **On mesure avant de promettre.** Le prix d'un lot sort de l'inventaire du code (outil libre `winmig`), relu par un développeur senior. Pas de « ça dépend » ni de régie déguisée.
2. **On migre à l'identique, puis on améliore.** Chaque écran est réécrit avec les mêmes fonctions qu'aujourd'hui. Les évolutions fonctionnelles sont chiffrées à part, après.
3. **Le premier lot prouve la chaîne entière.** Il livre le socle web et la fenêtre principale, qui tourne avec des données de test représentatives. Le client juge sur pièce avant d'engager la suite.
4. **Chaque lot est indépendant.** Commandé, livré, recetté et payé séparément. Le client peut s'arrêter après n'importe quel lot sans pénalité.
5. **Tout se fait à distance.** Pas de déplacement, pas de formation. Les échanges passent par écrit et par visio ; le code passe par votre dépôt Git.

## 2. L'audit (4 900 € HT, 10 jours ouvrés)

### Ce que fait le client (1 heure environ)

1. Il lance `winmig analyser <solution> -o envoi --sans-code` sur son poste. L'outil ne compile rien, n'exécute rien, n'envoie rien.
2. Il remplit le questionnaire de cadrage (20 minutes) : utilisateurs, postes, matériel raccordé, hébergement visé, contraintes de sécurité.
3. Il envoie `envoi/inventaire.json` et le questionnaire. Aucune ligne de code ni aucun chemin de son poste ne quitte ses machines ; `--sans-libelles` retire aussi les textes des écrans.
4. Facultatif : 3 à 5 captures d'écran des écrans les plus utilisés, et un accès en lecture au dépôt si le client le souhaite (jamais exigé).

### Ce que fait l'auditeur

1. **Relecture du périmètre** : écrans réellement utilisés, écrans abandonnés, écrans ouverts par configuration que l'analyse ne voit pas. Correction avec `winmig rapport --inclure / --exclure`.
2. **Relecture des écrans lourds** (classes L et XL) et des points d'attention : vrai risque ou faux positif, solution retenue.
3. **Choix d'architecture** parmi les cinq options du rapport (réécriture Angular et ASP.NET Core, Blazor, transposition automatique, passage à .NET récent en gardant le client lourd, API d'abord), argumenté pour ce client.
4. **Maquettes web de deux écrans** : la fenêtre principale et l'écran le plus utilisé, en HTML, avec les vrais libellés.
5. **Avis rédigé** (une demi-page, imprimé en tête de la synthèse) et plan de lots validé.
6. **Visio de restitution** de 45 minutes, enregistrée si le client le souhaite.

### Ce que reçoit le client

* Le rapport PDF (15 à 60 pages selon la taille) : synthèse, architecture cible, plan de lots et prix, inventaire et schéma de chaque écran, dépendances, points d'attention, écrans hors périmètre, méthode et limites.
* Les maquettes des deux écrans (HTML et PNG).
* `inventaire.json`, `ecrans.csv`, `constats.csv` pour ses propres outils.
* Une proposition ferme pour le lot 1, valable 3 mois.

Le prix de l'audit est **déduit du premier lot** commandé dans les 3 mois.

## 3. Le chiffrage

### Unités d'écran

Chaque écran reçoit un score :

> score = contrôles + 2 × gestionnaires d'événements + liaisons de données + lignes de logique ÷ 15 + 0,5 × branches + 4 × contrôles tiers

| Classe | Score | Unités |
|---|---|---|
| S | moins de 30 | 0,5 |
| M | 30 à 79 | 1 |
| L | 80 à 179 | 2 |
| XL | 180 et plus | 3 |

Chaque point lourd propre à l'écran (accès direct à une base de données, dessin personnalisé, animation ou 3D) ajoute 0,5 unité.

### Socle et chantiers transverses

* **Socle** : 4 unités (10 à 13 jours) au premier lot : application Angular, API ASP.NET Core, authentification, thème, canal temps réel SignalR, intégration continue, tests de bout en bout.
* **Chantiers transverses** : ce qui se traite une fois pour toute l'application (registre, impression, COM, protocole industriel, fichiers locaux…). Chaque règle a un coût de catalogue, multiplié par 1 + 0,15 par fichier touché au-delà du premier, plafonné à 3 fois.

### Prix

* **Une unité = 2 500 € HT**, soit 2,4 à 3,2 jours de travail. Les jours d'un lot sont ses unités facturées multipliées par ces deux bornes.
* **Un lot ≈ 10 unités ≈ 25 000 € HT.** Un lot se facture à ses unités réelles, avec un minimum de 5 unités ; un dernier lot de moins de 5 unités rejoint le précédent.
* Le lot 1 porte le socle, les chantiers transverses et la fenêtre principale avec ses composants : 15 à 20 unités en général (37 500 à 50 000 € HT, 9 à 13 semaines). Les lots suivants font environ 10 unités (5 à 7 semaines).
* Paiement : 30 % à la commande, 40 % à la livraison de la moitié du lot, 30 % à la recette.

## 4. Un lot de migration

### Avant de démarrer (au plus 5 jours ouvrés)

* Liste des écrans et des fonctions du lot, écran par écran, validée par écrit.
* Accès en écriture à un dépôt Git du client (GitHub, GitLab, Azure DevOps) ou à un dépôt fourni par Consigne et transféré à la fin.
* Données de test anonymisées, ou accès à un environnement de recette du client par VPN si le client le propose.
* Pour le matériel raccordé (automate, port série, lecteur) : un simulateur, un serveur OPC UA de test, ou une passerelle déjà en place côté client.

### Pendant le lot

* Livraison continue dans le dépôt, une démonstration enregistrée par semaine, un point écrit hebdomadaire.
* Chaque écran livré : composant Angular, API serveur avec contrôle d'accès, test Playwright de bout en bout, capture de référence pour la non-régression visuelle.

### Recette et garantie

* Recette de 10 jours ouvrés par le client, sur la liste validée au départ. Les écarts à cette liste sont corrigés sans frais ; le reste est une évolution, chiffrée à part en unités.
* Garantie de 3 mois sur les anomalies du lot recetté.
* Sans retour écrit à la fin de la recette, le lot est réputé accepté.

## 5. Ce qui n'est pas dans le forfait

* Les évolutions fonctionnelles (nouveaux écrans, nouvelles règles) : chiffrées en unités, sur demande.
* La migration des données et la reprise de la base : chiffrée à part si nécessaire.
* L'hébergement, les licences tierces (suites de composants web, serveurs), le matériel.
* Toute intervention sur site et toute formation.

## 6. Pourquoi cette méthode plutôt qu'une autre

| Option | Ce qu'elle fait bien | Sa limite |
|---|---|---|
| Transposition automatique (outils commerciaux) | Rapide, le code C# reste | Licence d'exécution aux conditions de l'éditeur ; l'ergonomie reste celle d'un écran de bureau |
| Avalonia ou .NET récent | Garde le client lourd, multiplateforme | Reste une application à installer, pas une application web |
| Blazor | Le C# jusque dans l'interface | Écosystème de composants plus petit ; même réécriture écran par écran |
| GitHub Copilot app modernization | Change la cible, met à jour les paquets, convertit WCF et Web Forms | Ni COM, ni 32 bits, ni Crystal, ni interface web ; tâches mails et bases tournées vers Azure ; aucun prix garanti |
| **Réécriture Angular + ASP.NET Core, au forfait (cette méthode)** | Vraie application web ; prix ferme par lot ; arrêt possible à chaque lot | Demande une recette sérieuse côté client, écran par écran |

## 7. États Crystal Reports

Le moteur Crystal Reports ne fonctionne ni sur .NET 5 et suivants, ni dans un navigateur. `winmig` liste les fichiers `.rpt` et le code qui les ouvre ; ils sont chiffrés à part, hors unités d'écran.

* **Pilote, 1 490 € HT, 10 jours ouvrés** : socle de rendu dans la cible retenue (QuestPDF, rendu HTML vers PDF, RDLC, ou outil de reporting sous licence), 3 rapports refaits, et classement ferme de tous les autres. Son prix est déduit de la première commande de rapports passée sous 3 mois.
* **Forfait par rapport** : simple 250 €, moyen 550 €, complexe 1 100 € HT, selon des critères publics (sources, sous-rapports, formules, graphiques, tableaux croisés, codes-barres, imprimé imposé).
* **Preuve** : chaque rapport migré est comparé à son PDF de référence par `scripts/comparer-pdf.mjs` (nombre de pages, texte comparé mot à mot page par page, pixels), sur les mêmes données de test. Les écarts de mise en page qui restent sont listés et validés à la recette.

## 8. Applications VB6

`winmig` ne lit pas les projets Visual Basic 6. Pour une application VB6 de 5 000 à 60 000 lignes :

* **Diagnostic, 1 490 € HT, 5 jours ouvrés** : il s'appuie sur une copie des sources transmise sous accord de confidentialité. Le rapport donne l'inventaire module par module (formulaires, OCX et ActiveX, références COM, `Declare`, DAO, ADO ou RDO, ports série, automatisation d'Office), la cible recommandée (C# .NET sur le poste, ou web) et les lots chiffrés. Son prix est déduit du premier lot commandé sous 3 mois.
* **Lots au prix ferme de 0,60 à 0,80 € HT par ligne revue**, de 3 000 à 6 000 € HT chacun. La traduction est outillée, y compris par IA, et chaque module est relu par un humain. Les tests de non-régression partent des captures et des jeux d'entrées et de sorties du client ; les OCX sont remplacés par des composants maintenus. Recette, garantie et arrêt suivent les mêmes règles que les autres lots.

## 9. Passage à .NET 10 et changement de serveur

.NET 8 et .NET 9 ne sont plus supportés après le 10 novembre 2026 ; .NET 10 l'est jusqu'en novembre 2028. `winmig` relève, projet par projet, ce qui bloque ou complique ce passage (page « Passage à .NET 10 » du rapport, champ `pointsNet10` de `inventaire.json`) :

| Code | Point | Portée | GitHub Copilot app modernization |
|---|---|---|---|
| N01 | Cible .NET hors support ou en fin de support (.NET 8 et 9, .NET Core, .NET Framework 4.0 à 4.6.1 ; 4.6.2 jusqu'au 12 janvier 2027 ; 2.0 à 3.5 jusqu'au 9 janvier 2029) | À faire (pour information si .NET 10 est déjà ciblé) | Le fait |
| N02 | Exécution forcée en 32 bits (`PlatformTarget` x86, `RuntimeIdentifier` win-x86, `Prefer32Bit`), hors configuration Debug | À vérifier | Ne le traite pas |
| N03 | Composants COM et ActiveX (références COM, `AxInterop`, `CreateObject`) | Pour le web ou Linux | Ne le traite pas |
| N04 | Automatisation d'Office ou d'Outlook | Pour le web ou Linux | Ne le traite pas |
| N05 | Crystal Reports | À faire | Ne le traite pas |
| N06 | Pilote SQL retiré ou obsolète (SQL Server Native Client, SQLOLEDB, Jet, ACE) | À vérifier | Sa tâche base de données vise Azure SQL |
| N07 | Envoi de mails par `System.Net.Mail` en SMTP (MailKit n'est pas signalé) | À vérifier | Propose Azure Communication Services |
| N08 | Serveur WCF, .NET Remoting, System.Web (client WCF seul : à vérifier) | À faire | Le fait en partie (CoreWCF, Blazor) |
| N09 | API retirées de .NET : `BinaryFormatter`, `SoapFormatter`, `AppDomain.CreateDomain`, etc. | À faire | Ne le traite pas |
| N10 | `System.Data.SqlClient`, remplacé par `Microsoft.Data.SqlClient` | À vérifier | Le fait |
| N11 | Projet .NET Framework sans cible .NET 10 | À faire | Le fait |

« À faire » bloque la compilation ou l'exécution sous .NET 10. « À vérifier » fonctionne souvent tel quel sous Windows mais se teste. « Pour le web ou Linux » ne bloque pas .NET 10 sous Windows : ces points ne comptent que si l'application quitte Windows. Les commentaires du code sont ignorés, et chaque fichier est rattaché au seul projet qui le contient.

La colonne Copilot reprend la documentation publiée par Microsoft au 24 octobre 2026 ; elle est tenue à jour.

* **Passage de .NET 8 ou 9 à .NET 10** : 490 € HT (1 à 3 projets), 990 € HT (4 à 10), 1 490 € HT (11 à 25), livré par pull request en 5 à 10 jours ouvrés, garantie d'un mois. Les points N02 à N10 que Copilot ne traite pas sont signalés avant de commencer et chiffrés à part ; le rapport donne la phrase de verdict et le palier qui s'applique.
* **Bilan de compatibilité avant changement de serveur** (Windows Server 2016, fin le 12 janvier 2027 ; SQL Server 2016, fin le 14 juillet 2026) : 990 € HT par application, sur un environnement de test Windows Server 2025 et SQL Server 2022 ; puis portage simple de 1 500 à 2 900 € HT. La partie système reste à l'infogérant.

## 10. Limites assumées de l'outil

* Les écrans ouverts par réflexion ou configuration, les contrôles créés à l'exécution et la logique en base ne sont pas vus par l'analyse statique : c'est le rôle de la relecture et du questionnaire.
* Les schémas montrent l'état du concepteur, pas l'écran à l'exécution.
* Les unités sont calibrées pour un développeur senior Angular et .NET ; elles ne valent pas pour une équipe débutante. Si l'audit retient Blazor, le chiffrage est revu.

Contact : support@consignestudio.fr
