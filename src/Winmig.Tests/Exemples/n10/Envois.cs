using System;
using System.Data.OleDb;
using System.Windows.Forms;

namespace Expedition;

public class Envois : Form
{
    const string Base = "Provider=SQLNCLI11;Server=SRV-PROD;Database=Expedition;Trusted_Connection=yes;";

    public Envois() { Text = "Envois du jour"; }

    void Charger()
    {
        using var cx = new OleDbConnection(Base);
        cx.Open();
        new Avis().Envoyer("quai@exemple.fr", "Envois chargés");
    }
}
