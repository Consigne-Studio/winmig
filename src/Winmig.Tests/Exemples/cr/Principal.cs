using System.Windows.Forms;
using CrystalDecisions.CrystalReports.Engine;
namespace Etats
{
    public class Principal : Form
    {
        void ImprimerFactures()
        {
            var r = new ReportDocument();
            r.Load(Application.StartupPath + "\\Rapports\\Factures.rpt");
            r.PrintToPrinter(1, false, 0, 0);
        }
        void ImprimerEtiquettes() { var e = new Rapports.Etiquettes(); e.PrintToPrinter(1, false, 0, 0); }
    }
}
