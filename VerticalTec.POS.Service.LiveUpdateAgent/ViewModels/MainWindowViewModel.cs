using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        IRegionManager _regionManager;
        IDialogService _dialogService;

        IDatabase _db;
        FrontConfigManager _frontConfig;
        VtecPOSEnv _posEnv;

        public MainWindowViewModel(IDatabase db, FrontConfigManager frontConfig, VtecPOSEnv posEnv,
            IDialogService dialogService, IRegionManager regionManager, IEventAggregator ea)
        {
            _db = db;
            _frontConfig = frontConfig;
            _posEnv = posEnv;
            _regionManager = regionManager;
            _dialogService = dialogService;

            ea.GetEvent<VersionUpdateEvent>().Subscribe((val) =>
            {
                OnUpdating = val == UpdateEvents.Updating;
                //if(val == UpdateEvents.UpdateSuccess)
                //{
                //    var parameters = new DialogParameters()
                //    {
                //        {"title", "การอัพเดต" },
                //        {"message",  "อัพเดตเวอร์ชั่นสำเร็จ"}
                //    };
                //    _dialogService.ShowDialog("Dialog", parameters, (r) =>
                //    {
                //        App.Current.Shutdown();
                //    });
                //}
            }, ThreadOption.UIThread);
        }

        public ICommand WindowLoadedCommand => new DelegateCommand(async () =>
        {
            var currentDir = Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).AbsolutePath));
            _posEnv.SoftwareRootPath = $"{Directory.GetParent(currentDir).FullName}";
            _posEnv.FrontCashierPath = Path.Combine(_posEnv.SoftwareRootPath, "vTec-ResPOS");
            _posEnv.PatchDownloadPath = Path.Combine(_posEnv.SoftwareRootPath, "Downloads");
            _posEnv.BackupPath = Path.Combine(_posEnv.SoftwareRootPath, "Backup");

            try
            {
                var configPath = Path.Combine(_posEnv.FrontCashierPath, "vTec-ResPOS.config");
                await _frontConfig.LoadConfig(configPath);
                var posSetting = _frontConfig.POSDataSetting;
                _db.SetConnectionString($"Port={posSetting.DBPort};Connection Timeout=28800;Allow User Variables=True;default command timeout=28800;UID=vtecPOS;PASSWORD=vtecpwnet;SERVER={posSetting.DBIPServer};DATABASE={posSetting.DBName};old guids=true;");

                _regionManager.RequestNavigate("ContentRegion", "MainView");

                var frontRes = Process.GetProcessesByName("vtec-ResPOS");
                frontRes.FirstOrDefault()?.Kill(true);
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
