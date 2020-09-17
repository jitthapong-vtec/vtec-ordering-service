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
    public class NoUpdateViewModel : BindableBase, INavigationAware
    {
        public ICommand OkCommand { get; set; }

        public NoUpdateViewModel()
        {
            OkCommand = new DelegateCommand(OnOk);
        }

        private void OnOk()
        {
            App.Current.Shutdown();
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
