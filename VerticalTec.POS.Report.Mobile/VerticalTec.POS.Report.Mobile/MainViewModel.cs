using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Xamarin.Forms;

namespace VerticalTec.POS.Report.Mobile
{
    public class MainViewModel : INotifyPropertyChanged
    {
        string _url;

        public MainViewModel()
        {
            LoadUrl();
        }

        public void LoadUrl()
        {
            Url = "http://203.151.92.65/vtecmobilereport";
        }

        public ICommand RefreshCommand => new Command(() =>
        {
            LoadUrl();
        });

        public string Url
        {
            get => _url;
            set
            {
                _url = value;
                NotifyPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
