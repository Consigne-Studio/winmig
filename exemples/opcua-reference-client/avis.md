**Migration faisable, sans rupture technique.** La bibliothèque client OPC UA (.NET Standard) passe telle quelle côté serveur et devient la passerelle du lot 1. Le vrai sujet est la logique des grilles d'édition de valeurs (EditComplexValueCtrl, DataListCtrl), à reprendre avec soin.

- Lot 1 (19,5 unités, le plus gros) : socle web, passerelle OPC UA et fenêtre principale. Les suivants font 10 unités ou moins.
- Retirés à la relecture : SampleForm et SampleUserControl, deux gabarits vides. ClientUtils et GuiUtils, sans interface, restent au prix : leur logique part dans des services.
- Les 67 autres écrans hors périmètre viennent de la bibliothèque partagée : aucun n'est ajouté sans votre accord.
