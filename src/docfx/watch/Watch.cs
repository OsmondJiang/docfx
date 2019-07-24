// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Docs.Build
{
    internal class Watch
    {
        private static int _dhsPort = 56344;
        private static int _watchPort = 5002;

        public static async Task Run(string docsetPath, string template, int port, ErrorLog errorLog)
        {
            // restore before watch
            var buildOptions = new CommandLineOptions { Legacy = true, Verbose = true, CopyResource = true };
            await Restore.Run(docsetPath, buildOptions, errorLog);

            var (_, config) = ConfigLoader.Load(docsetPath, buildOptions);
            var (_, siteBasePath) = SplitBaseUrl(config.BaseUrl);

            // start hosting via dhs emulator
            CreateHostWebService(docsetPath, siteBasePath, config, template);

            // creat building file proxy
            CreateBuildFilesProxy(docsetPath, config.DocumentId.SourceBasePath, buildOptions, errorLog);

            // luanch docs rending site
            await CreateRenderingService(port);
        }

        private static void CreateBuildFilesProxy(string docset, string sourceBasePath, CommandLineOptions options, ErrorLog errorLog)
        {
            var httpClient = new HttpClient()
            {
                // dhs emulator
                BaseAddress = new Uri($"http://localhost:{_dhsPort}"),
            };

            var host = new WebHostBuilder()
                .UseUrls($"http://*:{_watchPort}")
                .UseKestrel()
                .Configure(Configure)
                .Build();

            host.Start();

            void Configure(IApplicationBuilder app)
            {
                app.Use(next => async http =>
                {
                    if (http.Request.Path.StartsWithSegments($"/depots/test/documents", out var remainingPath))
                    {
                        // build file before go to dhs emulator
                        if (TryGetSourceFilePath(remainingPath.Value, out var filePath))
                        {
                            await Build.Run(docset, options, errorLog, new List<string> { filePath });
                        }
                    }
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri(http.Request.Path + http.Request.QueryString.Value, UriKind.Relative),
                    };

                    using (var response = await httpClient.SendAsync(request))
                    {
                        http.Response.StatusCode = (int)response.StatusCode;

                        if (response.IsSuccessStatusCode)
                        {
                            await response.Content.CopyToAsync(http.Response.Body);
                        }
                        else
                        {
                            var ex = await response.Content.ReadAsStringAsync();
                            await http.Response.WriteAsync(ex);
                        }
                    }

                    return;
                });
            }

            bool TryGetSourceFilePath(string assetId, out string filePath)
            {
                assetId = System.Net.WebUtility.UrlDecode(assetId.Substring(1));
                filePath = null;
                var fullPath = Path.Combine(docset, sourceBasePath, assetId);

                if (File.Exists(fullPath))
                {
                    filePath = Path.GetRelativePath(docset, fullPath);
                    return true;
                }

                foreach (var ext in new[] { ".md", ".yml", ".json" })
                {
                    fullPath = ChangeExtension(fullPath, ext);
                    if (File.Exists(fullPath))
                    {
                        filePath = Path.GetRelativePath(docset, fullPath);
                        return true;
                    }
                }

                return false;
            }

            string ChangeExtension(string filePath, string ext)
            {
                var fileName = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath));

                return fileName + ext;
            }
        }

        private static Task<int> CreateRenderingService(int port)
        {
            var serverDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../dep/Docs.Rendering/Source/App"));
            var serverArgs = "run --no-launch-profile --no-build --no-restore";
            var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = serverDir, Arguments = serverArgs };

            psi.UseShellExecute = false;
            psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "local-preview";
            psi.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://*:{port}";
            psi.EnvironmentVariables["APPSETTING_DocumentHostingServiceClientOptions__BaseUri"] = $"http://localhost:{_watchPort}";
            psi.EnvironmentVariables["APPSETTING_DocumentHostingServiceClientOptions:ApiAccessKey"] = "fV0/VWFb7W9xKkJek6fEVSHYUviVRDQQ4aSDmkmeRl0tCDDJUTiM7p1EcIh+SYnD8/9Y7hXNB3eOvUwVzR+5Aw==";

            var tcs = new TaskCompletionSource<int>();
            var process = Process.Start(psi);
            process.EnableRaisingEvents = true;
            process.Exited += (a, b) => tcs.TrySetResult(process.ExitCode);
            return tcs.Task;
        }

        private static void CreateHostWebService(string docset, string siteBasePath, Config config, string template)
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
                SiteBasePath = "docs.microsoft.com/" + siteBasePath + "/",
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

            var theme = new
            {
                ThemeName = "Docs.Theme",
                LocalPath = template,
            };

            PathUtility.CreateDirectoryFromFilePath(depotsFilePath);
            File.WriteAllText(depotsFilePath, JsonConvert.SerializeObject(new { Depots = new[] { depot }, Themes = new[] { theme } }));

            // create host file
            var hostFile = Path.Combine(serverDir, "hosting.json");

            PathUtility.CreateDirectoryFromFilePath(hostFile);
            File.WriteAllText(hostFile, JsonConvert.SerializeObject(new JObject { ["server.urls"] = $"http://localhost:{_dhsPort}" }));

            // create dhs emulator process
            var serverArgs = $"run -p {serverDir} --no-launch-profile --no-build --no-restore";
            var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = docset, Arguments = serverArgs };
            psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
            Process.Start(psi);
        }

        private static (string hostName, string siteBasePath) SplitBaseUrl(string baseUrl)
        {
            string hostName = string.Empty;
            string siteBasePath = ".";
            if (!string.IsNullOrEmpty(baseUrl)
                && Uri.TryCreate(baseUrl, UriKind.Absolute, out var uriResult))
            {
                if (uriResult.AbsolutePath != "/")
                {
                    siteBasePath = uriResult.AbsolutePath.Substring(1);
                }
                hostName = $"{uriResult.Scheme}://{uriResult.Host}";
            }
            return (hostName, siteBasePath);
        }
    }
}
