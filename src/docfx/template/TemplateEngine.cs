// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Docs.Build
{
    internal class TemplateEngine
    {
        private static readonly string[] s_resourceFolders = new[] { "global", "css", "fonts" };

        private readonly string _templateDir;
        private readonly string _locale;
        private readonly LiquidTemplate _liquid;
        private readonly JavascriptEngine _js;

        public JObject Global { get; }

        private TemplateEngine(string templateDir, string locale)
        {
            var contentTemplateDir = Path.Combine(templateDir, "ContentTemplate");

            _templateDir = templateDir;
            _locale = locale.ToLowerInvariant();
            _liquid = new LiquidTemplate(templateDir);
            _js = new JavascriptEngine(contentTemplateDir);
            Global = LoadGlobalTokens(templateDir, _locale);
        }

        public static TemplateEngine Create(Docset docset)
        {
            Debug.Assert(docset != null);

            if (string.IsNullOrEmpty(docset.Config.Theme))
            {
                return null;
            }

            var (themeRemote, themeBranch) = LocalizationUtility.GetLocalizedTheme(docset.Config.Theme, docset.Locale, docset.Config.Localization.DefaultLocale);
            var (themePath, themeLock) = docset.RestoreMap.GetGitRestorePath($"{themeRemote}#{themeBranch}", docset.DependencyLock);
            Log.Write($"Using theme '{themeRemote}#{themeLock.Commit}' at '{themePath}'");

            return new TemplateEngine(themePath, docset.Locale);
        }

        public string Render(PageModel model, Document file)
        {
            // TODO: only works for conceptual
            var content = model.Content.ToString();
            var (templateModel, metadata) = TemplateTransform.Transform(this, model, file);

            var layout = templateModel.RawMetadata.Value<string>("layout");
            var themeRelativePath = PathUtility.GetRelativePathToFile(file.SitePath, "_themes");

            var liquidModel = new JObject
            {
                ["content"] = content,
                ["page"] = templateModel.RawMetadata,
                ["metadata"] = metadata,
                ["theme_rel"] = themeRelativePath,
            };

            return _liquid.Render(layout, liquidModel);
        }

        public JObject TransformMetadata(string scriptPath, JObject model)
        {
            return JObject.Parse(((JObject)_js.Run(scriptPath, "transform", model)).Value<string>("content"));
        }

        public void CopyTo(string outputPath)
        {
            foreach (var resourceDir in s_resourceFolders)
            {
                var srcDir = Path.Combine(_templateDir, resourceDir);
                if (Directory.Exists(srcDir))
                {
                    Parallel.ForEach(Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories), file =>
                    {
                        var outputFilePath = Path.Combine(outputPath, "_themes", file.Substring(_templateDir.Length + 1));
                        PathUtility.CreateDirectoryFromFilePath(outputFilePath);
                        File.Copy(file, outputFilePath, overwrite: true);
                    });
                }
            }
        }

        public string GetToken(string key)
        {
            return Global[key]?.ToString();
        }

        private JObject LoadGlobalTokens(string templateDir, string locale)
        {
            var path = Path.Combine(templateDir, $"LocalizedTokens/docs({locale}).html/tokens.json");
            return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : new JObject();
        }
    }
}
