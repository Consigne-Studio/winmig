using DevExpress.XtraGrid;
namespace Ancien
{
    partial class Principal
    {
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Principal));
            this.grille = new GridControl();
            this.graphe = new AxMSChart20Lib.AxMSChart();
            this.titre = new System.Windows.Forms.Label();
            resources.ApplyResources(this.grille, "grille");
            resources.ApplyResources(this.titre, "titre");
            resources.ApplyResources(this, "$this");
            this.Controls.Add(this.grille);
            this.Controls.Add(this.titre);
        }

        private GridControl grille;
        private AxMSChart20Lib.AxMSChart graphe;
        private System.Windows.Forms.Label titre;
    }
}
