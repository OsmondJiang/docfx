// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Docs.Build
{
    internal static class Host
    {
        public static void CreateHostWebService(string docset, string siteBasePath, int port, Config config)
        {
            // create depots based on current config
            var serverDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../dep/OpenPublishing.DocumentHostingEmulator/Source/DocumentHostingEmulator/DocumentHostingEmulator"));
            var depotsFilePath = Path.Combine(serverDir, "Database/storage.json");

            if (File.Exists(depotsFilePath))
            {
                File.Delete(depotsFilePath);
            }

            var depot = new
            {
                LocalPath = Path.GetFullPath(Path.Combine(docset, $"_site/{siteBasePath}")),
                DepotId = Guid.NewGuid().ToString(),
                DepotName = "test",
                SiteBasePath = "docs.microsoft.com/" + siteBasePath,
                SiteId = Guid.NewGuid().ToString(),
                PartitionNumber = 0,
                Metadata = new
                {
                    site_name = "Docs",
                    theme = "Docs.Theme",
                    s_dynamic_rendering = true,
                },
                Priority = 0,
            };

            var dependencyLock = DependencyLock.Load(docset, string.IsNullOrEmpty(config.DependencyLock) ? new SourceInfo<string>(AppData.GetDependencyLockFile(docset, default)) : config.DependencyLock) ?? new DependencyLockModel();
            var restoreMap = RestoreMap.Create(dependencyLock);
            var (template, branch, _) = UrlUtility.SplitGitUrl(config.Template);
            var theme = new
            {
                ThemeName = "Docs.Theme",
                LocalPath = restoreMap.GetGitRestorePath(template, branch, docset).path,
            };

            PathUtility.CreateDirectoryFromFilePath(depotsFilePath);
            File.WriteAllText(depotsFilePath, JsonConvert.SerializeObject(new { Depots = new[] { depot }, Themes = new[] { theme } }));

            // create host file
            var hostFile = Path.Combine(serverDir, "hosting.json");

            PathUtility.CreateDirectoryFromFilePath(hostFile);
            File.WriteAllText(hostFile, JsonConvert.SerializeObject(new JObject { ["server.urls"] = $"http://localhost:{port}" }));

            // create dhs emulator process
            var serverArgs = $"run -p {serverDir} --no-launch-profile --no-build --no-restore";
            var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = docset, Arguments = serverArgs };
            psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
            Process.Start(psi);
        }
    }
}
