// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Docs.Build
{
    internal static class Host
    {
        public static Task<int> CreateHostWebService(string docset, string baseUrl, int port)
        {
            // create depots based on current config
            var depotsFilePath = "./Database/storage.json";

            if (File.Exists(depotsFilePath))
            {
                File.Delete(depotsFilePath);
            }

            var depot = new
            {
                LocalPath = Path.GetFullPath(docset),
                DepotId = Guid.NewGuid().ToString(),
                DepotName = "test",
                SiteBasePath = baseUrl,
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

            File.WriteAllText(depotsFilePath, JsonConvert.SerializeObject(new { Depots = new[] { depot } }));

            // create host file
            var hostFile = "hosting.json";

            File.WriteAllText(hostFile, JsonConvert.SerializeObject(new JObject { ["server.urls"] = $"http://localhost:{port}" }));

            // create dhs emulator process
            var serverDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../dep/OpenPublishing.DocumentHostingEmulator/Source/DocumentHostingEmulator/DocumentHostingEmulator"));
            var serverArgs = "run --no-launch-profile --no-build --no-restore";
            var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = serverDir, Arguments = serverArgs };

            var tcs = new TaskCompletionSource<int>();
            var process = Process.Start(psi);
            process.EnableRaisingEvents = true;
            process.Exited += (a, b) => tcs.TrySetResult(process.ExitCode);
            return tcs.Task;
        }
    }
}
