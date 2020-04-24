using VerticalTec.POS.Service.LiveUpdateAgent.Views;
using Prism.Ioc;
using Prism.Modularity;
using System.Windows;
using VerticalTec.POS.Service.LiveUpdateAgent.ViewModels;

namespace VerticalTec.POS.Service.LiveUpdateAgent
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<MainView>();
        }
    }
}
