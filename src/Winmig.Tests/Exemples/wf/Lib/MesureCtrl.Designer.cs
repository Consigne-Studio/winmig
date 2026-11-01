namespace Atelier.Lib
{
    partial class MesureCtrl
    {
        private void InitializeComponent()
        {
            this.valeur = new System.Windows.Forms.Label();
            this.valeur.Dock = System.Windows.Forms.DockStyle.Fill;
            this.valeur.Text = "0,0 bar";
            this.Controls.Add(this.valeur);
            this.Size = new System.Drawing.Size(300, 60);
        }

        private System.Windows.Forms.Label valeur;
    }
}
