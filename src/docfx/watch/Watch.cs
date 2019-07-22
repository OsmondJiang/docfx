// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.Docs.Build
{
    internal class Watch
    {
        public static async Task Run(string docsetPath, CommandLineOptions options, ErrorLog errorLog)
        {
            var (_, config) = ConfigLoader.Load(docsetPath, options);

            // start hosting via dhs emulator
            Host.Config(docsetPath, config, errorLog);
            Host.Run();

            // lanch rending site
            await LaunchWebServer();
        }

        private static Task<int> LaunchWebServer()
        {
            var port = 56789;
            var name = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "App.exe" : "App";
            var exeDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../web"));
            var exe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../web", name));
            var serverDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../dep/Docs.Rendering/Source/App"));
            var serverArgs = "run --no-launch-profile --no-build --no-restore";
            var psi = File.Exists(exe)
                ? new ProcessStartInfo { FileName = exe, WorkingDirectory = exeDir, }
                : new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = serverDir, Arguments = serverArgs };

            psi.UseShellExecute = false;
            psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
            psi.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://*:{port}";
            psi.EnvironmentVariables["APPSETTING_DocumentHostingServiceClientOptions__BaseUri"] = "http://localhost:56344";
            psi.EnvironmentVariables["APPSETTING_DocumentHostingServiceApiAccessKey"] = "c2hvd21ldGhlbW9uZXk=";

            var tcs = new TaskCompletionSource<int>();
            var process = Process.Start(psi);
            process.EnableRaisingEvents = true;
            process.Exited += (a, b) => tcs.TrySetResult(process.ExitCode);
            return tcs.Task;
        }
    }
}
