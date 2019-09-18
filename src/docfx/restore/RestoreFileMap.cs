// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO;

namespace Microsoft.Docs.Build
{
    internal class RestoreFileMap
    {
        private readonly string _docsetPath;
        private readonly string _fallbackDocsetPath;

        public RestoreFileMap(string docsetPath, string fallbackDocsetPath = null)
        {
            Debug.Assert(docsetPath != null);
            _docsetPath = docsetPath;
            _fallbackDocsetPath = fallbackDocsetPath;
        }

        public string GetRestoredFileContent(SourceInfo<string> url)
        {
            return GetRestoredFileContent(_docsetPath, url, _fallbackDocsetPath);
        }

        public string GetRestoredFilePath(SourceInfo<string> url)
        {
            var fromUrl = UrlUtility.IsHttp(url);
            if (!fromUrl)
            {
                // directly return the relative path
                var fullPath = Path.Combine(_docsetPath, url);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }

                if (!string.IsNullOrEmpty(_fallbackDocsetPath))
                {
                    fullPath = Path.Combine(_fallbackDocsetPath, url);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }

                throw Errors.FileNotFound(url).ToException();
            }

            var filePath = RestoreFile.GetRestorePathFromUrl(url);
            if (!File.Exists(filePath))
            {
                throw Errors.NeedRestore(url).ToException();
            }

            return filePath;
        }

        public static string GetRestoredFileContent(Input input, SourceInfo<string> url)
        {
            var fromUrl = UrlUtility.IsHttp(url);
            if (!fromUrl)
            {
                // directly return the relative path
                var localFilePath = new FilePath(url, FileOrigin.Default);
                if (input.Exists(localFilePath))
                {
                    return input.ReadString(localFilePath);
                }

                localFilePath = new FilePath(url, FileOrigin.Fallback);
                if (input.Exists(localFilePath))
                {
                    return input.ReadString(localFilePath);
                }

                throw Errors.FileNotFound(url).ToException();
            }

            var filePath = RestoreFile.GetRestorePathFromUrl(url);
            if (!File.Exists(filePath))
            {
                throw Errors.NeedRestore(url).ToException();
            }

            using (InterProcessMutex.Create(filePath))
            {
                return File.ReadAllText(filePath);
            }
        }
    }
}
