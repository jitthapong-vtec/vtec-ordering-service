using VerticalTec.POS.Service.LiveUpdateAgent.Views;
using Prism.Ioc;
using Prism.Modularity;
using System.Windows;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.LiveUpdateAgent.ViewModels;
using VerticalTec.POS.LiveUpdate;

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
            containerRegistry.RegisterSingleton<IDatabase, MySqlDatabase>();
            containerRegistry.RegisterSingleton<FrontConfigManager>();
            containerRegistry.RegisterSingleton<LiveUpdateDbContext>();
            containerRegistry.RegisterSingleton<VtecPOSEnv>();

            containerRegistry.RegisterDialog<Dialog, DialogViewModel>();
            containerRegistry.RegisterForNavigation<AskForUpdateView>();
            containerRegistry.RegisterForNavigation<NoUpdateView>();
            containerRegistry.RegisterForNavigation<MainView>();
        }
    }
}
