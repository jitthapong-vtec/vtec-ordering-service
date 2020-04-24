using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace VerticalTec.POS.Service.LiveUpdateAgent.ViewModels
{
    public class MainViewModel : BindableBase, INavigationAware
    {
        public MainViewModel()
        {
        }

        public ICommand OkCommand => new DelegateCommand(() =>
        {

        });

        public ICommand CancelCommand => new DelegateCommand(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
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
