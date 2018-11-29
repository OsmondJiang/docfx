// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Docs.Build
{
    internal class RepositoryProvider
    {
        private readonly ConcurrentDictionary<string, Repository> _repositoryByFolder = new ConcurrentDictionary<string, Repository>();

        private readonly ConcurrentDictionary<string, GitCommitProvider> _gitCommitProvider = new ConcurrentDictionary<string, GitCommitProvider>();

        public GitCommitProvider GetGitCommitProvider(string repoPath, string repoRemote = null, ConcurrentDictionary<string, Dictionary<(long commit, long blob), (long[] commitHistory, int lruOrder)>> commitCache = null)
        {
            return _gitCommitProvider.GetOrAdd(repoPath, p => new GitCommitProvider(repoPath, repoRemote, commitCache));
        }

        public (Repository repo, string pathToRepo) GetRepository(Document document)
            => GetRepository(document.Docset, document.FilePath);

        public (Repository repo, string pathToRepo) GetRepository(Docset docset, string pathToDocset)
        {
            var fullPath = PathUtility.NormalizeFile(Path.Combine(docset.DocsetPath, pathToDocset));
            var repo = GetRepository(fullPath);
            if (repo == null)
                return default;

            return (repo, PathUtility.NormalizeFile(Path.GetRelativePath(repo.Path, fullPath)));
        }

        private Repository GetRepository(string fullPath)
        {
            if (GitUtility.IsRepo(fullPath))
                return Repository.Create(fullPath);

            var parent = Path.GetDirectoryName(fullPath);
            return !string.IsNullOrEmpty(parent)
                ? _repositoryByFolder.GetOrAdd(parent, GetRepository)
                : null;
        }
    }
}
