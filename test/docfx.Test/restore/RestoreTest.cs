// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Docs.Build
{
    public static class RestoreTest
    {
        [Theory]
        [InlineData("https://github.com/dotnet/docfx", "https://github.com/dotnet/docfx", "master")]
        [InlineData("https://visualstudio.com/dotnet/docfx", "https://visualstudio.com/dotnet/docfx", "master")]
        [InlineData("https://github.com/dotnet/docfx#master", "https://github.com/dotnet/docfx", "master")]
        [InlineData("https://github.com/dotnet/docfx#live", "https://github.com/dotnet/docfx", "live")]
        [InlineData("https://github.com/dotnet/docfx#", "https://github.com/dotnet/docfx", "master")]
        [InlineData("https://github.com/dotnet/docfx#986127a", "https://github.com/dotnet/docfx", "986127a")]
        [InlineData("https://github.com/dotnet/docfx#a#a", "https://github.com/dotnet/docfx", "a#a")]
        [InlineData("https://github.com/dotnet/docfx#a\\b/d<e>f*h|i%3C", "https://github.com/dotnet/docfx", "a\\b/d<e>f*h|i%3C")]
        public static void GetGitInfo(string remote, string expectedUrl, string expectedRev)
        {
            // Act
            var (url, rev) = GitUtility.GetGitRemoteInfo(remote);

            // Assert
            Assert.Equal(expectedUrl, url);
            Assert.Equal(expectedRev, rev);
        }

        [Fact]
        public static async Task RestoreGitWorkTrees()
        {
            var docsetPath = "restore-worktrees";
            var gitUrl = "https://github.com/docascode/docfx-test-dependencies-clean";
            Directory.CreateDirectory(docsetPath);
            var restorePath = PathUtility.NormalizeFolder(Path.Combine(RestoreGit.GetRestoreRootDir(gitUrl), ".git"));

            File.WriteAllText(Path.Combine(docsetPath, "docfx.yml"), $@"
dependencies:
  dep1: {gitUrl}#test-1-clean
  dep2: {gitUrl}#test-2-clean
  dep3: {gitUrl}#test-3-clean
  dep4: {gitUrl}#test-4-clean
  dep5: {gitUrl}#master
  dep6: {gitUrl}#chi");

            // run restroe and check the work trees
            await Program.Run(new[] { "restore", docsetPath });
            var workTreeList = await GitUtility.ListWorkTrees(restorePath, false);
            Assert.Equal(6, workTreeList.Count);

            foreach(var wirkTreeFolder in workTreeList.Where(w => w.EndsWith("clean")))
            {
                Directory.SetLastWriteTimeUtc(wirkTreeFolder, DateTime.UtcNow - TimeSpan.FromDays(RestoreWorkTree.MaxKeepingDays + 1));
            }

            File.WriteAllText(Path.Combine(docsetPath, "docfx.yml"), $@"
dependencies:
  dep5: {gitUrl}#master
  dep6: {gitUrl}#chi");

            // run restore again to clean up
            // check the work trees
            await Program.Run(new[] { "restore", docsetPath });
            workTreeList = await GitUtility.ListWorkTrees(restorePath, false);
            Assert.Equal(2, workTreeList.Count);

            // check restore lock file
            var restoreLock = await RestoreLocker.Load(docsetPath);
            Assert.Equal(2, restoreLock.Git.Count);
        }

        [Fact]
        public static async Task RestoreUrls()
        {
            // prepare versions
            var docsetPath = "restore-urls";
            Directory.CreateDirectory(docsetPath);
            var url = "https://raw.githubusercontent.com/docascode/docfx-test-dependencies-clean/master/README.md";
            var restoreDir = RestoreUrl.GetRestoreRootDir(url);
            await ParallelUtility.ForEach(Enumerable.Range(0, 10), version =>
            {
                var restorePath = RestoreUrl.GetRestoreVersionPath(restoreDir, version.ToString());
                Directory.CreateDirectory(Path.GetDirectoryName(restorePath));
                File.WriteAllText(restorePath, $"{version}");
                File.SetLastWriteTimeUtc(restorePath, DateTime.UtcNow - TimeSpan.FromDays(RestoreUrl.MaxKeepingDays + 1));
                return Task.CompletedTask;
            });

            File.WriteAllText(Path.Combine(docsetPath, "docfx.yml"), $@"
github:
  userCache: https://raw.githubusercontent.com/docascode/docfx-test-dependencies-clean/master/README.md");

            // run restore again to clean up
            await Program.Run(new[] { "restore", docsetPath });

            Assert.Single(Directory.EnumerateFiles(restoreDir, "*"));

            // check restore lock file
            var restoreLock = await RestoreLocker.Load(docsetPath);
            Assert.Single(restoreLock.Url);
        }
    }
}
