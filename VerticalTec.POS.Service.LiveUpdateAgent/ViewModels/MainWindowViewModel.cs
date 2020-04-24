using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System.Windows.Input;

namespace VerticalTec.POS.Service.LiveUpdateAgent.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private string _title = "Vtec Live Update";
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private readonly IRegionManager _regionManager;

        public MainWindowViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public ICommand WindowLoadedCommand => new DelegateCommand(() =>
        {
            _regionManager.RequestNavigate("ContentRegion", "MainView");
        });
    }
}
