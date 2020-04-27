using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.LiveUpdateAgent.Events;

namespace VerticalTec.POS.Service.LiveUpdateAgent.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        bool _onUpdating;

        public bool OnUpdating
        {
            get => _onUpdating;
            set => SetProperty(ref _onUpdating, value);
        }

        private string _title = "Vtec Live Update";
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private readonly IRegionManager _regionManager;
        private readonly IDialogService _dialogService;

        private readonly IDatabase _db;
        private readonly FrontConfigManager _frontConfig;

        public MainWindowViewModel(IDatabase db, FrontConfigManager frontConfig, IDialogService dialogService,
            IRegionManager regionManager, IEventAggregator ea)
        {
            _db = db;
            _frontConfig = frontConfig;
            _regionManager = regionManager;
            _dialogService = dialogService;

            ea.GetEvent<VersionUpdateEvent>().Subscribe((val) =>
            {
                OnUpdating = val;
            }, ThreadOption.UIThread);
        }

        public ICommand WindowLoadedCommand => new DelegateCommand(async () =>
        {
            var currentDir = Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).AbsolutePath));
            var rootPath = $"{Directory.GetParent(currentDir).FullName}\\";
            var frontCashierPath = $"{rootPath}vTec-ResPOS\\";

            try
            {
                await _frontConfig.LoadConfig($"{frontCashierPath}vTec-ResPOS.config");
                var posSetting = _frontConfig.POSDataSetting;
                _db.SetConnectionString($"Port={posSetting.DBPort};Connection Timeout=28800;Allow User Variables=True;default command timeout=28800;UID=vtecPOS;PASSWORD=vtecpwnet;SERVER={posSetting.DBIPServer};DATABASE={posSetting.DBName};old guids=true;");

                _regionManager.RequestNavigate("ContentRegion", "MainView");
            }
            catch (Exception ex)
            {
                var parameters = new DialogParameters()
                {
                    {"title", "Error" },
                    {"message", $"Could not load front configuration file! => {ex.Message}" }
                };
                _dialogService.ShowDialog("Dialog", parameters, (r) =>
                {
                    App.Current.Shutdown();
                });
            }
        });
    }
}
