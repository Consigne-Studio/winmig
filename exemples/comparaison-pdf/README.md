# Exemple : comparaison de deux PDF d'un état

Démonstration de `scripts/comparer-pdf.mjs` sur deux PDF **fictifs**, produits pour l'exemple (ce ne sont pas des sorties de Crystal Reports) : un bon de livraison de référence et sa version « migrée », dont le total a une erreur de saisie (1 106,00 au lieu de 1 160,00).

```bash
node scripts/comparer-pdf.mjs exemples/comparaison-pdf/reference.pdf exemples/comparaison-pdf/nouveau.pdf -o exemples/comparaison-pdf
```

Résultat : [comparaison.html](comparaison.html), verdict « écarts à valider ». La ligne de texte qui diffère est listée, et la zone d'écart est encadrée en rouge sur la page, alors qu'elle ne couvre que quelques pixels. Le texte est comparé mot à mot, page par page, sans tenir compte des espaces de mise en page. Code de sortie 1 en cas d'écart, ce qui permet de bloquer une intégration continue, et 2 en cas d'erreur d'usage (fichier absent, PDF illisible, Poppler manquant).

Dans une migration réelle, la référence est le PDF imprimé par Crystal Reports et le nouveau PDF est celui de la cible (QuestPDF, rendu web, RDLC), produits tous les deux sur les mêmes données de test.
