using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;
using VerticalTec.POS.Service.LiveUpdateAgent.Events;

namespace VerticalTec.POS.Service.LiveUpdateAgent.ViewModels
{
    public class MainViewModel : BindableBase, INavigationAware
    {
        IEventAggregator _eventAggregator;
        IDatabase _db;
        LiveUpdateDbContext _liveUpdateContext;
        POSDataSetting _posSetting;

        bool _isBusy;

        bool _updateButtonEnable;
        string _updateProcessInfoMessage;
        string _currentVersion;
        string _updateVersion;

        public MainViewModel(IEventAggregator ea, IDatabase db, LiveUpdateDbContext liveupdateContext, FrontConfigManager frontConfig)
        {
            _eventAggregator = ea;
            _db = db;
            _liveUpdateContext = liveupdateContext;
            _posSetting = frontConfig.POSDataSetting;
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool UpdateButtonEnable
        {
            get => _updateButtonEnable;
            set => SetProperty(ref _updateButtonEnable, value);
        }

        public string UpdateProcessInfoMessage
        {
            get => _updateProcessInfoMessage;
            set => SetProperty(ref _updateProcessInfoMessage, value);
        }

        public string CurrentVersion
        {
            get => _currentVersion;
            set => SetProperty(ref _currentVersion, value);
        }

        public string UpdateVersion
        {
            get => _updateVersion;
            set => SetProperty(ref _updateVersion, value);
        }

        public ICommand StartUpdateCommand => new DelegateCommand(async () =>
        {
            _eventAggregator.GetEvent<VersionUpdateEvent>().Publish(true);

            UpdateInfoMessage("Starting update...");
            UpdateButtonEnable = false;
            for (var i = 0; i < 100; i++)
            {
                await Task.Delay(100);
                UpdateInfoMessage($"Extract file {i}...");
                _eventAggregator.GetEvent<UpdateInfoMessageEvent>().Publish("");
            }
            _eventAggregator.GetEvent<VersionUpdateEvent>().Publish(false);
        });

        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            try
            {
                IsBusy = true;
                UpdateInfoMessage("Collecting version information...");
                using (var conn = await _db.ConnectAsync())
                {
                    var versionDeploys = await _liveUpdateContext.GetVersionDeploy(conn, _posSetting.ShopID);
                    var lastDeploy = versionDeploys.Where(v => v.BatchStatus == 1).OrderByDescending(v => v.UpdateDate).FirstOrDefault();
                    if (lastDeploy != null)
                    {
                        var versionLiveUpdate = await _liveUpdateContext.GetVersionLiveUpdate(conn, lastDeploy.BatchId, lastDeploy.ShopId, _posSetting.ComputerID, ProgramTypes.Front);
                        if (versionLiveUpdate != null)
                        {
                            var newVersionAvailable = versionLiveUpdate.ReadyToUpdate == 1;
                            UpdateButtonEnable = newVersionAvailable;
                            UpdateVersion = versionLiveUpdate.UpdateVersion;

                            if (newVersionAvailable)
                                UpdateInfoMessage($"New version {UpdateVersion} available");
                            else
                                UpdateInfoMessage($"No update available");

                            var versionInfo = await _liveUpdateContext.GetVersionInfo(conn, lastDeploy.ShopId, _posSetting.ComputerID, ProgramTypes.Front);
                            CurrentVersion = versionInfo.FirstOrDefault()?.ProgramVersion;
                        }
                        else
                        {
                            UpdateInfoMessage("Not found version information!");
                        }
                    }
                    else
                    {
                        UpdateInfoMessage("Not found version information!");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateInfoMessage(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        void UpdateInfoMessage(string message)
        {
            UpdateProcessInfoMessage += message + "\n";
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }
    }
}
