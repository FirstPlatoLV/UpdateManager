// UpdateManager
// Copyright 2021 Maris Melbardis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using UpdateManager;

namespace Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Updater updater;

        private int downloadProgressValue = 0;
        public int DownloadProgressValue { get => downloadProgressValue; set { downloadProgressValue = value; OnPropertyChanged(); } }
       
        private string statusText;
        public string StatusText { get => statusText; set { statusText = value; OnPropertyChanged(); } }

        private void OnPropertyChanged([CallerMemberName] string caller = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(caller));
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += MainWindowLoaded;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private async void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            StatusText = "Checking for updates...";
            updater = new Updater();
            updater.Downloader.ProgressChanged += DownloaderProgressChanged;

            await updater.GetUpdatesAsync();
            await DownloadUpdates();

            StatusText = "Launching application...";
            updater.StartMainApp();
            Application.Current.Shutdown();
        }

        private void DownloaderProgressChanged(object sender, UpdateManager.ProgressChangedEventArgs e)
        {
            DownloadProgressValue = e.ProgressPercentage;
        }

        private async Task DownloadUpdates()
        {
            int updateNumber = 1;
            // Here we just download all the AvailableUpdates (updates that need to be applied based on CurrentVersion).
            foreach (UpdateInfo update in updater.AvailableUpdates)
            {
                DownloadProgressValue = 0;
                StatusText = string.Format("Downloading update {0} of {1}", updateNumber, updater.AvailableUpdates.Count);
                if (!await updater.DownloadUpdate(update))
                {
                    MessageBox.Show("Update failed. Please try again later", "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
                updateNumber++;
            }
        }
    }
}
