using System.Windows;
using Microsoft.Win32;

namespace Supervision.Vues
{
    public partial class FenetreVue : Window
    {
        public FenetreVue() { InitializeComponent(); DataContext = new FenetreViewModel(); }

        private void Exporter_Click(object sender, RoutedEventArgs e)
        {
            var d = new SaveFileDialog();
            if (d.ShowDialog() == true) { }
        }
    }
}
