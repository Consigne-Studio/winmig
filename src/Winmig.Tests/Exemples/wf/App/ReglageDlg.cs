using System.Data.SqlClient;
using System.Windows.Forms;

namespace Atelier
{
    public partial class ReglageDlg : Form
    {
        public int Valeur { get; private set; }

        public ReglageDlg() { InitializeComponent(); }

        private void Ok_Click(object sender, System.EventArgs e)
        {
            using (var cnx = new SqlConnection("Server=.;Database=atelier"))
            {
                cnx.Open();
                Valeur = int.Parse(valeur.Text);
            }
            DialogResult = DialogResult.OK;
        }
    }
}
