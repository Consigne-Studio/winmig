namespace Atelier
{
    partial class FenetrePrincipale
    {
        private System.ComponentModel.IContainer components = null;

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.menu = new System.Windows.Forms.MenuStrip();
            this.fichierMI = new System.Windows.Forms.ToolStripMenuItem();
            this.ouvrirMI = new System.Windows.Forms.ToolStripMenuItem();
            this.split = new System.Windows.Forms.SplitContainer();
            this.arbre = new System.Windows.Forms.TreeView();
            this.grille = new System.Windows.Forms.DataGridView();
            this.colNom = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colValeur = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.mesure = new Atelier.Lib.MesureCtrl();
            this.etat = new System.Windows.Forms.StatusStrip();
            this.minuterie = new System.Windows.Forms.Timer(this.components);
            this.courbe = new ZedGraph.ZedGraphControl();
            ((System.ComponentModel.ISupportInitialize)(this.split)).BeginInit();
            this.split.Panel1.SuspendLayout();
            this.SuspendLayout();
            // menu
            this.menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fichierMI});
            this.menu.Location = new System.Drawing.Point(0, 0);
            this.menu.Name = "menu";
            this.menu.Size = new System.Drawing.Size(800, 24);
            this.fichierMI.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ouvrirMI});
            this.fichierMI.Text = "&Fichier";
            this.ouvrirMI.Text = "Ouvrir…";
            this.ouvrirMI.Click += new System.EventHandler(this.Ouvrir_Click);
            // split
            this.split.Dock = System.Windows.Forms.DockStyle.Fill;
            this.split.Location = new System.Drawing.Point(0, 24);
            this.split.SplitterDistance = 250;
            this.split.Panel1.Controls.Add(this.arbre);
            this.split.Panel2.Controls.Add(this.grille);
            this.split.Panel2.Controls.Add(this.mesure);
            this.split.Panel2.Controls.Add(this.courbe);
            this.arbre.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grille.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colNom,
            this.colValeur});
            this.grille.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grille.DataSource = this.source;
            this.colNom.HeaderText = "Nom";
            this.colValeur.HeaderText = "Valeur";
            this.mesure.Dock = System.Windows.Forms.DockStyle.Top;
            this.mesure.Size = new System.Drawing.Size(546, 60);
            this.courbe.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.courbe.Size = new System.Drawing.Size(546, 150);
            this.minuterie.Tick += new System.EventHandler(this.Minuterie_Tick);
            // FenetrePrincipale
            this.ClientSize = new System.Drawing.Size(800, 500);
            this.Controls.Add(this.split);
            this.Controls.Add(this.etat);
            this.Controls.Add(this.menu);
            this.MainMenuStrip = this.menu;
            this.Text = "Atelier \"pilote\"";
            this.Load += new System.EventHandler(this.FenetrePrincipale_Load);
            ((System.ComponentModel.ISupportInitialize)(this.split)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.MenuStrip menu;
        private System.Windows.Forms.ToolStripMenuItem fichierMI;
        private System.Windows.Forms.ToolStripMenuItem ouvrirMI;
        private System.Windows.Forms.SplitContainer split;
        private System.Windows.Forms.TreeView arbre;
        private System.Windows.Forms.DataGridView grille;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNom;
        private System.Windows.Forms.DataGridViewTextBoxColumn colValeur;
        private Atelier.Lib.MesureCtrl mesure;
        private System.Windows.Forms.StatusStrip etat;
        private System.Windows.Forms.Timer minuterie;
        private ZedGraph.ZedGraphControl courbe;
        private System.Windows.Forms.BindingSource source;
    }
}
