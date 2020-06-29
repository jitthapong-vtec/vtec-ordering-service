using ImTools;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace VerticalTec.POS.Service.LiveUpdateAgent.ViewModels
{
    public class AskForUpdateViewModel : BindableBase, INavigationAware
    {
        IRegionManager _regionManager;
        IDialogService _dialogService;
        
        public ICommand OkCommand { get; set; }
        public ICommand CancelCommand { get; set; }

        public AskForUpdateViewModel(IRegionManager regionManager, IDialogService dialogService)
        {
            _regionManager = regionManager;
            _dialogService = dialogService;

            OkCommand = new DelegateCommand(OnOk);
            CancelCommand = new DelegateCommand(OnCancel);
        }

        private void OnCancel()
        {
            App.Current.Shutdown();
        }

        private void OnOk()
        {
            try
            {
                bool doneCloseProcess = false;
                var frontRes = Process.GetProcessesByName("vtec-ResPOS");
                var syncClientProcess = Process.GetProcessesByName("vTec-SyncClient");
                try
                {
                    syncClientProcess.ForEach(sync => sync.Kill());
                    frontRes.ForEach(front => front.Kill());
                    doneCloseProcess = true;
                }
                catch (Exception ex)
                {
                    var parameters = new DialogParameters()
                        {
                            {"title", "Error" },
                            {"message", $"Could not kill vTec-ResPOS! => {ex.Message}" }
                        };
                    _dialogService.ShowDialog("Dialog", parameters, (r) =>
                    {
                        App.Current.Shutdown();
                    });
                }

                if (doneCloseProcess)
                {
                    _regionManager.RequestNavigate("ContentRegion", "MainView");
                }
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
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
        }
    }
}
