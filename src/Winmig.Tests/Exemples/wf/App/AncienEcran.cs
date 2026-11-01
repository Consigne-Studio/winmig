using System.Windows.Forms;

namespace Atelier
{
    // Jamais ouvert depuis l'application : doit sortir du périmètre.
    public class AncienEcran : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool MessageBeep(uint type);
    }
}
