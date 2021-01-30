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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UpdateManager
{
    // Adapted from https://stackoverflow.com/a/60935947
    public class HttpClientWithProgress : HttpClient
    {
        // Will report information about the download in progress.
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        public HttpClientWithProgress()
        {
            // Some hosts require user agents to be defined.
            DefaultRequestHeaders.Add("User-Agent", "ua");    
        }

        // Retreives the FileName from the download address used as paramater in StartDownload()
        public async Task<string> GetFileNameForDownload(string downloadUrl)
        {
            string fileName = null;
            using var response = await GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            if (response.Content.Headers.ContentDisposition != null)
            {
                fileName = response.Content.Headers.ContentDisposition.FileName
                    .Replace("\"", "");
            }
            return fileName;
        }
        public async Task StartDownload(string downloadUrl, string destinationFilePath)
        {
            using var response = await GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            await DownloadFileFromHttpResponseMessage(response, destinationFilePath);
        }

        public async Task StartDownload(string downloadUrl, string destinationFilePath, CancellationToken cancellationToken)
        {
            using var response = await GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await DownloadFileFromHttpResponseMessage(response, destinationFilePath);
        }

        private async Task DownloadFileFromHttpResponseMessage(HttpResponseMessage response, string destinationFilePath)
        {
            response.EnsureSuccessStatusCode();
            long? totalBytes = response.Content.Headers.ContentLength;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            await ProcessContentStream(totalBytes, contentStream, destinationFilePath);
        }

        private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream, string destinationFilePath)
        {
            long totalBytesRead = 0L;
            long readCount = 0L;
            byte[] buffer = new byte[8192];
            bool isMoreToRead = true;

            using (FileStream fileStream = 
                new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                do
                {
                    int bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, bytesRead);

                    totalBytesRead += bytesRead;
                    readCount += 1;

                    if (readCount % 10 == 0)
                        TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                }
                while (isMoreToRead);
            }
            TriggerProgressChanged(totalDownloadSize, totalBytesRead);
        }

        private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead)
        {
            if (ProgressChanged == null)
                return;

           int progressPercentage = 0;
            if (totalDownloadSize.HasValue)
                progressPercentage = (int)Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 0);

            if (totalDownloadSize == null)
            {
                totalDownloadSize = 0;
            }
            OnDownloadProgressChanged(new ProgressChangedEventArgs(totalDownloadSize.Value, totalBytesRead, progressPercentage));
        }

        protected virtual void OnDownloadProgressChanged(ProgressChangedEventArgs e)
        {
            EventHandler<ProgressChangedEventArgs> handler = ProgressChanged;
            handler?.Invoke(this, e);
        }
    }
}
