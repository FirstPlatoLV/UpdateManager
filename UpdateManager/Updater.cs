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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace UpdateManager
{
    public class Updater
    {
        public HttpClientWithProgress Downloader { get; }
        public IList<UpdateInfo> AvailableUpdates { get; private set; } = new List<UpdateInfo>();
       
        private List<UpdateInfo> updateList;
        private Settings appSettings;

        private readonly string settingsFileName = "settings.json";

        public Updater()
        {
            Downloader = new HttpClientWithProgress();
            GetSettings(settingsFileName);
        }

        ~Updater()
        {
            Downloader.Dispose();
        }

        public async Task<bool> GetUpdatesAsync()
        {
            bool result = true;
            string updatesJson;

            updatesJson = await Downloader.GetStringAsync(appSettings.UpdateServerAddress);
            updateList = JsonSerializer.Deserialize<List<UpdateInfo>>(updatesJson);
            
            AvailableUpdates = GetAvailableUpdates();
            return result;
        }

        public async Task<bool> DownloadUpdate(UpdateInfo update)
        {
            // If the path does not exist, let's create it.
            if (!Directory.Exists(appSettings.UpdateInstallPath))
            {
                Directory.CreateDirectory(appSettings.UpdateInstallPath);
            }

            // We need a file name for the file we will be downloading.
            string fileName = await Downloader.GetFileNameForDownload(update.UpdateFileAddress);
            await Downloader.StartDownload(update.UpdateFileAddress, Path.Combine(appSettings.UpdateInstallPath, fileName));

            // If there is MD5 value in UpdateInfo, it will be compared to the hash of downloaded file.
            bool result = await VerifyFileHash(update, Path.Combine(appSettings.UpdateInstallPath, fileName));


            // If no problems occured, let's update our local version, which will filter out updates on consequtive runs.
            if (result)
            {
                UpdateVersion(update, settingsFileName);
            }
            else
            {
                File.Delete(Path.Combine(appSettings.UpdateInstallPath, fileName));
            }

            return result;
        }

        public void StartMainApp()
        {
            if (string.IsNullOrEmpty(appSettings.MainExePath))
            {
                return;
            }
            using Process myProcess = new Process();
            myProcess.StartInfo.FileName = appSettings.MainExePath;
            myProcess.Start();
        }

        private void GetSettings(string filePath)
        {
            string settingsJson;
            settingsJson = File.ReadAllText(filePath);
            appSettings = JsonSerializer.Deserialize<Settings>(settingsJson);
        }

        private IList<UpdateInfo> GetAvailableUpdates()
        {
            // We're only interested in updates which has MinimumVersion equal 
            // or bigger than CurrentVersion of local app.
            updateList = updateList.OrderBy(x => x.MinimumVersion).ToList();
            return updateList
                .Where(x => x.MinimumVersion >= appSettings.CurrentVersion)
                .Select(x => x)
                .ToList();
        }
        
        private void UpdateVersion(UpdateInfo updateInfo, string settingsFile)
        {
            appSettings.CurrentVersion = updateInfo.Version;
            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                WriteIndented = true
            };
            string appSettingsString = JsonSerializer.Serialize(appSettings, options);
            File.WriteAllText(settingsFile, appSettingsString);
        }

        private async Task<bool> VerifyFileHash(UpdateInfo updateInfo, string filePath)
        {
            bool result = true;
            // If no hash is provided, then let's hope for the best.
            if (string.IsNullOrEmpty(updateInfo.MD5))
            {
                return result;
            }

            string fileHash = await Utils.GetMd5(filePath);
            if (!updateInfo.MD5.Equals(fileHash))
            {
                result = false;
            }
            return result;
        }
    }

}
