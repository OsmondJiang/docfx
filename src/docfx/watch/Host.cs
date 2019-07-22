// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Microsoft.Docs.Build
{
    internal static class Host
    {
        private static string _docset;
        private static string _baseUrl;
        private static string _baseFolder;
        private static ErrorLog _errorLog;

        public static void Config(string docset, Config config, ErrorLog errorLog)
        {
            _docset = docset;
            _baseUrl = config.BaseUrl;
            _baseFolder = config.DocumentId.SourceBasePath;
            _errorLog = errorLog;
        }

        public static void Run()
        {
            // create depots based on current config
            var depotsFilePath = "./Database/storage.json";

            if (File.Exists(depotsFilePath))
            {
                File.Delete(depotsFilePath);
            }

            var depot = new
            {
                LocalPath = Path.GetFullPath(_docset),
                DepotId = Guid.NewGuid().ToString(),
                DepotName = "test",
                SiteBasePath = _baseUrl,
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

            // start dhs emulator
            DocumentHostingEmulator.Program.Run<HostStartup>();
        }

        private class HostStartup : DocumentHostingEmulator.Startup
        {
            public HostStartup(IConfiguration configuration)
                : base(configuration)
            {
            }

            public new void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                if (env.IsDevelopment())
                {
                    app.UseBrowserLink();
                    app.UseDeveloperExceptionPage();
                }
                else
                {
                    app.UseExceptionHandler("/Home/Error");
                }

                app.UseStaticFiles();

                app.Use(next => async http =>
                {
                    if (http.Request.Path.StartsWithSegments("/" + _baseUrl, out var remainingPath))
                    {
                        var sourceFilePath = _baseFolder + remainingPath.Value;
                        await Build.Run(_docset, null, _errorLog, new List<string> { sourceFilePath });
                        await next(http);
                    }
                });

                app.UseMvc(routes =>
                {
                    routes.MapRoute(
                        name: "default",
                        template: "{controller=Home}/{action=Index}/{id?}");
                });
            }
        }
    }
}
