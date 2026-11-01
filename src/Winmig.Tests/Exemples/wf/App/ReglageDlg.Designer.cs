namespace Atelier
{
    partial class ReglageDlg
    {
        private void InitializeComponent()
        {
            this.libelle = new System.Windows.Forms.Label();
            this.valeur = new System.Windows.Forms.TextBox();
            this.ok = new System.Windows.Forms.Button();
            this.libelle.Location = new System.Drawing.Point(12, 15);
            this.libelle.Size = new System.Drawing.Size(80, 15);
            this.libelle.Text = "Consigne";
            this.valeur.Location = new System.Drawing.Point(100, 12);
            this.valeur.Size = new System.Drawing.Size(120, 23);
            this.ok.Location = new System.Drawing.Point(145, 50);
            this.ok.Size = new System.Drawing.Size(75, 23);
            this.ok.Text = "OK";
            this.ok.Click += new System.EventHandler(this.Ok_Click);
            this.ClientSize = new System.Drawing.Size(232, 85);
            this.Controls.Add(this.ok);
            this.Controls.Add(this.valeur);
            this.Controls.Add(this.libelle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Text = "Réglage";
        }

        private System.Windows.Forms.Label libelle;
        private System.Windows.Forms.TextBox valeur;
        private System.Windows.Forms.Button ok;
    }
}
