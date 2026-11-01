using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Supervision.Vues
{
    public partial class FenetreViewModel : ObservableObject
    {
        [ObservableProperty] private string etat = "OK";
        public ObservableCollection<string> Alarmes { get; } = new();

        [RelayCommand]
        private void Acquitter()
        {
            if (Alarmes.Count > 0) Alarmes.Clear();
            System.Windows.Application.Current.Dispatcher.Invoke(() => Etat = "Acquitté");
        }
    }
}
