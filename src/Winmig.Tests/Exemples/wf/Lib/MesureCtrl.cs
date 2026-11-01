using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;

namespace Atelier.Lib
{
    public partial class MesureCtrl : UserControl
    {
        private readonly SerialPort port = new SerialPort("COM3");

        public MesureCtrl() { InitializeComponent(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawRectangle(Pens.Black, 0, 0, Width - 1, Height - 1);
        }
    }
}
