using MySql.Data.MySqlClient;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        IDialogService _dialogService;

        LiveUpdateDbContext _liveUpdateContext;
        POSDataSetting _posSetting;
        VtecPOSEnv _posEnv;

        VersionLiveUpdate _versionLiveUpdate;

        bool _isBusy;

        bool _updateButtonEnable;
        ObservableCollection<string> _processInfoMessages;
        string _currentVersion;
        string _updateVersion;
        string _buttonText = "Start Update";

        public MainViewModel(IEventAggregator ea, IDatabase db, IDialogService dialogService,
            LiveUpdateDbContext liveupdateContext, FrontConfigManager frontConfig, VtecPOSEnv posEnv)
        {
            _eventAggregator = ea;
            _db = db;
            _dialogService = dialogService;
            _liveUpdateContext = liveupdateContext;
            _posSetting = frontConfig.POSDataSetting;
            _posEnv = posEnv;

            _processInfoMessages = new ObservableCollection<string>();
        }

        public string ButtonText
        {
            get => _buttonText;
            set => SetProperty(ref _buttonText, value);
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

        public ObservableCollection<string> ProcessInfoMessages
        {
            get => _processInfoMessages;
            set => SetProperty(ref _processInfoMessages, value);
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
            if (_versionLiveUpdate?.UpdateStatus == 2)
            {
                var frontPath = Path.Combine(_posEnv.FrontCashierPath, "vtec-ResPOS.exe");
                Process.Start(frontPath);

                App.Current.Shutdown();
                return;
            }
            await Task.Run(async () =>
            {
                _eventAggregator.GetEvent<VersionUpdateEvent>().Publish(UpdateEvents.Updating);

                UpdateInfoMessage("Extracting file...");
                UpdateButtonEnable = false;

                var isExtractSuccess = false;
                var updateFilePath = _versionLiveUpdate.DownloadFilePath;
                var posPath = _posEnv.FrontCashierPath;
                var extractPath = Path.Combine(Path.GetTempPath(), $"vTec-ResPOS-{DateTime.Now.ToString("yyyyMMdd")}");
                var sqlPath = "";

                if (!Directory.Exists(extractPath))
                    Directory.CreateDirectory(extractPath);

                try
                {
                    using (var archive = ZipFile.Open(updateFilePath, ZipArchiveMode.Update))
                    {
                        var entry = archive.Entries.Where(a => a.FullName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)).SingleOrDefault();
                        if (entry != null)
                        {
                            UpdateInfoMessage($"found sql file {entry.Name}");
                            sqlPath = Path.GetFullPath(Path.Combine(extractPath, entry.Name));
                            entry.ExtractToFile(sqlPath, true);
                        }
                    }

                    if (!string.IsNullOrEmpty(sqlPath))
                    {
                        using (var conn = await _db.ConnectAsync())
                        {
                            var content = File.ReadAllText(sqlPath);
                            var cmd = new MySqlCommand("", conn as MySqlConnection);
                            foreach (var line in content.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (string.IsNullOrEmpty(line))
                                    continue;
                                try
                                {
                                    UpdateInfoMessage($"exec {line}");
                                    cmd.CommandText = line;
                                    cmd.ExecuteNonQuery();
                                    UpdateInfoMessage($"exec success");
                                }
                                catch (Exception ex)
                                {
                                    UpdateInfoMessage($"Error exec sql {ex.Message}");
                                }
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    UpdateInfoMessage($"Extract script error {ex.Message}");
                }

                try
                {
                    var totalFile = 0;
                    using (var archive = ZipFile.Open(updateFilePath, ZipArchiveMode.Update))
                    {
                        totalFile = archive.Entries.Count();
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.EndsWith(".config"))
                                continue;
                            if (entry.FullName.EndsWith(".sql"))
                                continue;

                            var destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                if (!Directory.Exists(destinationPath))
                                    Directory.CreateDirectory(destinationPath);
                            }
                            else
                            {
                                entry.ExtractToFile(destinationPath, true);
                                UpdateInfoMessage($"Extract file {entry.Name}");
                            }
                        }
                    }
                    isExtractSuccess = true;
                }
                catch (Exception ex)
                {
                    UpdateInfoMessage($"Extract file error {ex.Message}");
                    _eventAggregator.GetEvent<VersionUpdateEvent>().Publish(UpdateEvents.UpdateFail);
                }

                if (!isExtractSuccess)
                    return;

                var isCopySuccess = false;
                try
                {
                    UpdateInfoMessage("Start copy file");
                    // copy file from temp
                    foreach (string dirPath in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
                    {
                        var destinationPath = dirPath.Replace(extractPath, posPath);
                        if (!Directory.Exists(destinationPath))
                            Directory.CreateDirectory(destinationPath);
                    }

                    foreach (string newPath in Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories))
                    {
                        var destinationPath = newPath.Replace(extractPath, posPath);
                        File.Copy(newPath, destinationPath, true);
                        UpdateInfoMessage($"Copy file {destinationPath}");
                    }
                    isCopySuccess = true;
                }
                catch (Exception ex)
                {
                    UpdateInfoMessage($"Copy error {ex.Message}");
                    _eventAggregator.GetEvent<VersionUpdateEvent>().Publish(UpdateEvents.UpdateFail);
                }

                try
                {
                    Directory.Delete(extractPath);
                }
                catch { }

                if (isCopySuccess)
                {
                    using (var conn = await _db.ConnectAsync())
                    {
                        _versionLiveUpdate.UpdateStatus = 2;
                        await _liveUpdateContext.AddOrUpdateVersionLiveUpdate(conn, _versionLiveUpdate);
                    }

                    ButtonText = "สำเร็จ!";
                    UpdateButtonEnable = true;

                    UpdateInfoMessage($"Successfully");
                    _eventAggregator.GetEvent<VersionUpdateEvent>().Publish(UpdateEvents.UpdateSuccess);
                }
                else
                {
                    ButtonText = "เริ่มอัพเดต";
                    UpdateButtonEnable = true;
                }
            });
        });

        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            await GetVersionInfoAsync();
        }

        private async Task GetVersionInfoAsync()
        {
            try
            {
                IsBusy = true;
                //UpdateInfoMessage("Collecting version information...");
                using (var conn = await _db.ConnectAsync())
                {
                    var versionDeploy = await _liveUpdateContext.GetActiveVersionDeploy(conn);
                    _versionLiveUpdate = await _liveUpdateContext.GetVersionLiveUpdate(conn, versionDeploy.BatchId, _posSetting.ShopID, _posSetting.ComputerID, ProgramTypes.Front);
                    if (_versionLiveUpdate != null)
                    {
                        var newVersionAvailable = _versionLiveUpdate.ReadyToUpdate == 1;

                        if (newVersionAvailable)
                        {
                            UpdateButtonEnable = newVersionAvailable;
                            UpdateVersion = _versionLiveUpdate.UpdateVersion;

                            UpdateInfoMessage($"New version {UpdateVersion} available");
                        }
                        else
                        {
                            UpdateInfoMessage($"No update available");
                            UpdateVersion = "-";
                        }

                        var versionInfo = await _liveUpdateContext.GetVersionInfo(conn, _posSetting.ShopID, _posSetting.ComputerID, ProgramTypes.Front);
                        CurrentVersion = versionInfo.FirstOrDefault()?.ProgramVersion;
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
            Application.Current.Dispatcher.Invoke(() => ProcessInfoMessages.Add(message));
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
