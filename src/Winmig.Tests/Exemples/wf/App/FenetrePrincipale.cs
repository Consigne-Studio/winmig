using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Atelier
{
    public partial class FenetrePrincipale : Form
    {
        public event EventHandler Change;

        public FenetrePrincipale()
        {
            InitializeComponent();
        }

        private void Ouvrir_Click(object sender, EventArgs e)
        {
            using (var d = new ReglageDlg())
            {
                if (d.ShowDialog() == DialogResult.OK && d.Valeur > 10)
                    Change?.Invoke(this, EventArgs.Empty);
            }
            var cle = Registry.CurrentUser.OpenSubKey("Software\\Atelier");
        }

        private void Minuterie_Tick(object sender, EventArgs e)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => Minuterie_Tick(sender, e))); return; }
            for (int i = 0; i < 3; i++) { }
        }
    }
}
