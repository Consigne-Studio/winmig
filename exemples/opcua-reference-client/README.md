# Exemple : OPC UA Reference Client

Rapport d'audit produit par `winmig` 1.0.0 sur l'application open source **OPC UA Reference Client** du dépôt [OPCFoundation/UA-.NETStandard-Samples](https://github.com/OPCFoundation/UA-.NETStandard-Samples) (copyright OPC Foundation, licence OPC Foundation MIT 1.00). Aucun lien avec l'OPC Foundation : c'est un exemple public, choisi parce qu'il ressemble à une vraie application industrielle (client OPC UA en WinForms, bibliothèque de contrôles partagée, 5 projets, 37 000 lignes).

| Fichier | Contenu |
|---|---|
| [rapport.pdf](rapport.pdf) | Le rapport tel qu'il est livré, 19 pages A4 |
| `rapport.html`, `rapport.md` | Le même rapport en HTML autonome et en Markdown |
| `inventaire.json`, `ecrans.csv`, `constats.csv` | Les mesures brutes |
| `schemas/` | Le schéma de chacun des 94 écrans trouvés |
| [avis.md](avis.md) | L'avis rédigé par l'auditeur, imprimé en tête de la synthèse |
| [maquettes/](maquettes/) | Les deux écrans maquettés en web (fenêtre principale et écriture d'une valeur) |

## Comment il a été produit

Exactement comme pour un client à distance : l'inventaire est fait sans extrait de code, puis le rapport est refait depuis l'inventaire avec l'avis et le périmètre corrigé à la relecture (deux gabarits vides retirés).

```powershell
winmig analyser "Samples/ReferenceClient/Reference Client.csproj" -o envoi --sans-code -q
winmig rapport envoi/inventaire.json -o opcua-reference-client --application "OPC UA Reference Client" --client "Exemple public" --auteur Consigne --avis avis.md --date 2026-10-24 --exclure SampleForm,SampleUserControl
node scripts/pdf.mjs opcua-reference-client
```

Les maquettes sont des pages HTML statiques ([maquettes.html](maquettes/maquettes.html)) aux données fictives ; dans un audit, elles sont faites avec vos vrais libellés et validées avec vous.
