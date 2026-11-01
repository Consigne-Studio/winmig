# Après votre commande

Merci pour votre commande de l'audit de migration. Il vous reste trois étapes, environ une heure en tout. Votre code source ne quitte pas vos machines.

## 1. Lancer l'inventaire (15 minutes)

Sur un poste Windows 10 ou Windows Server 2012 (ou plus récent, x64) qui a accès aux sources de l'application :

1. Téléchargez `winmig-1.0.0-win-x64.zip` depuis https://github.com/Consigne-Studio/winmig/releases et décompressez-le, par exemple dans `C:\outils\winmig`. Rien à installer. Pour vérifier le fichier, comparez `Get-FileHash winmig-1.0.0-win-x64.zip` avec l'empreinte publiée dans `SHA256SUMS.txt` sur la même page.
2. Ouvrez PowerShell et lancez, en remplaçant le chemin par celui de votre solution :

```
C:\outils\winmig\winmig.exe analyser C:\sources\MonAppli\MonAppli.sln -o C:\temp\envoi --sans-code
```

3. Ouvrez `C:\temp\envoi\rapport.html` dans votre navigateur et vérifiez que vous reconnaissez vos écrans. C'est le rapport brut, avant relecture.

Si votre application tient en plusieurs solutions, lancez la commande une fois par solution, dans des dossiers d'envoi différents, et envoyez-les tous : c'est un seul audit pour une même application, jusqu'à 80 écrans dans le périmètre.

L'option `--sans-code` garantit qu'aucune ligne de code n'est écrite dans les fichiers. `inventaire.json` contient des noms de classes, de contrôles et de méthodes, les libellés de vos écrans, des chemins relatifs à la solution (jamais le chemin de votre poste), des numéros de ligne et des comptages. Si les libellés eux-mêmes sont sensibles, ajoutez `--sans-libelles`. Vous pouvez ouvrir `inventaire.json` dans un éditeur de texte pour le vérifier avant l'envoi.

## 2. Remplir le questionnaire de cadrage (20 minutes)

Le questionnaire est joint à votre commande (`questionnaire-cadrage.html`, à ouvrir dans votre navigateur). Répondez directement dans un e-mail, en reprenant les numéros des questions : des réponses courtes suffisent, « je ne sais pas » est une réponse acceptable.

## 3. Nous envoyer les fichiers (5 minutes)

Envoyez à support@consignestudio.fr :

* `C:\temp\envoi\inventaire.json` (compressé en .zip s'il dépasse 10 Mo) ;
* vos réponses au questionnaire ;
* facultatif : 3 à 5 captures des écrans les plus utilisés, avec les données masquées si besoin.

Si votre politique interne interdit l'e-mail, dites-le : un lien de dépôt chiffré vous est envoyé.

## Ensuite

* Accusé de réception sous un jour ouvré, avec la date de livraison : **10 jours ouvrés** après la réception de ces fichiers.
* Si une information manque, une seule série de questions écrites.
* Livraison : le rapport PDF relu et annoté, les maquettes web de deux écrans, la proposition ferme pour le lot 1 et une visio de restitution de 45 minutes à la date de votre choix.
* Le prix de l'audit est déduit du premier lot de migration commandé dans les 3 mois.

## En cas de souci

| Problème | Solution |
|---|---|
| Windows bloque l'exécutable (SmartScreen, AppLocker) | L'exécutable n'est pas signé. Faites-le autoriser par votre service informatique d'après son empreinte SHA-256, ou, s'il dispose du SDK .NET 8, il peut construire l'outil depuis les sources (README du dépôt, option 2). |
| `Entrée non reconnue` | Indiquez le fichier `.sln`, `.csproj` ou `.vbproj`, pas un fichier `.cs`. |
| Peu d'écrans trouvés | Partez de la solution complète ou du projet exécutable, pas d'une bibliothèque. |
| Autre chose | Écrivez à support@consignestudio.fr avec le message affiché. |
