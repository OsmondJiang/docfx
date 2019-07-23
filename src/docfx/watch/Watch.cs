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

namespace Microsoft.Docs.Build
{
    internal class Watch
    {
        public static async Task Run(string docsetPath, CommandLineOptions options, ErrorLog errorLog)
        {
            // restore before watch
            await Restore.Run(docsetPath, options, errorLog);

            var (_, config) = ConfigLoader.Load(docsetPath, options);
            var (_, siteBasePath) = SplitBaseUrl(config.BaseUrl);

            // start hosting via dhs emulator
            Host.CreateHostWebService(docsetPath, siteBasePath, 5000, config, options);

            // creat host proxy
            CreateHostProxy(docsetPath, siteBasePath, options, errorLog).Start();

            // luanch docs rending site
            await CreateRenderingServicer(options);
        }

        private static IWebHost CreateHostProxy(string docset, string siteBasePath, CommandLineOptions options, ErrorLog errorLog)
        {
            var httpClient = new HttpClient()
            {
                // dhs emulator
                BaseAddress = new Uri("http://localhost:5000"),
            };

            return new WebHostBuilder()
                .UseUrls($"http://*:{options.Port}")
                .UseKestrel()
                .Configure(Configure)
                .Build();

            void Configure(IApplicationBuilder app)
            {
                app.Use(next => async http =>
                {
                    if (http.Request.Path.StartsWithSegments($"/depots/test/documents", out var remainingPath))
                    {
                        // build file before go to dhs emulator
                        var sitePath = "/" + siteBasePath + remainingPath.Value;
                        var fileName = Path.GetFileName(sitePath);
                        if (string.Equals(fileName, "index", PathUtility.PathComparison))
                        {
                            sitePath = sitePath.Substring(0, sitePath.Length - 5);
                        }
                        await Build.Run(docset, options, errorLog, new List<string> { sitePath });
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
        }

        private static Task<int> CreateRenderingServicer(CommandLineOptions options)
        {
            var serverDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../dep/Docs.Rendering/Source/App"));
            var serverArgs = "run --no-launch-profile --no-build --no-restore";
            var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = serverDir, Arguments = serverArgs };

            psi.UseShellExecute = false;
            psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
            psi.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://*:5001";
            psi.EnvironmentVariables["APPSETTING_DocumentHostingServiceClientOptions__BaseUri"] = $"http://localhost:{options.Port}";
            psi.EnvironmentVariables["APPSETTING_DocumentHostingServiceClientOptions:ApiAccessKey"] = "fV0/VWFb7W9xKkJek6fEVSHYUviVRDQQ4aSDmkmeRl0tCDDJUTiM7p1EcIh+SYnD8/9Y7hXNB3eOvUwVzR+5Aw==";

            var tcs = new TaskCompletionSource<int>();
            var process = Process.Start(psi);
            process.EnableRaisingEvents = true;
            process.Exited += (a, b) => tcs.TrySetResult(process.ExitCode);
            return tcs.Task;
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
