// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
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
            var (_, config) = ConfigLoader.Load(docsetPath, options);

            // start hosting via dhs emulator
            Host.CreateHostWebService(docsetPath, config.BaseUrl, 5000).Start();

            // creat host proxy
            CreateHostProxy(docsetPath, config.BaseUrl, config.DocumentId.SourceBasePath, options, errorLog).Start();

            // luanch docs rending site
            await CreateRenderingServicer(options);
        }

        private static IWebHost CreateHostProxy(string docset, string siteBasePath, string sourceBasePath, CommandLineOptions options, ErrorLog errorLog)
        {
            var httpClient = new HttpClient()
            {
                // dhs emulator
                BaseAddress = new Uri("http://localehost:5000"),
            };

            return new WebHostBuilder()
                .UseUrls($"http://*:{options.Port}")
                .Configure(Configure)
                .Build();

            void Configure(IApplicationBuilder app)
            {
                app.Use(next => async http =>
                {
                    if (http.Request.Path.StartsWithSegments("/" + siteBasePath, out var remainingPath))
                    {
                        var filePath = Path.Combine(sourceBasePath, remainingPath.Value);

                        try
                        {
                            // build file before go to dhs emulator
                            await Build.Run(docset, options, errorLog, new List<string> { filePath });
                            var request = new HttpRequestMessage
                            {
                                Method = HttpMethod.Get,
                                RequestUri = new Uri(http.Request.Path, UriKind.Relative),
                            };

                            using (var response = await httpClient.SendAsync(request))
                            {
                                http.Response.StatusCode = (int)response.StatusCode;
                                await response.Content.CopyToAsync(http.Response.Body);
                            }

                            await next(http);
                        }
                        catch
                        {
                            http.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            await http.Response.WriteAsync("build failed, check the build log");
                        }
                    }
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
            psi.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://*:{options.Port}";
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
