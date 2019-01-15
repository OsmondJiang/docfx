// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.Docs.Build
{
    internal static class RestoreLock
    {
        public static async Task<DependencyLock> Load(string docset, Config config)
        {
            if (string.IsNullOrEmpty(config.DependencyLock))
            {
                return null;
            }

            var (_, restoredLockFile) = RestoreMap.GetFileRestorePath(docset, config.DependencyLock);

            var content = await ProcessUtility.ReadFile(restoredLockFile);

            return JsonUtility.Deserialize<DependencyLock>(content);
        }

        public static Task Save(DependencyLock dependencyLock, string dependencyLockFilePath)
        {
            Debug.Assert(dependencyLock != null);
            Debug.Assert(!HrefUtility.IsHttpHref(dependencyLockFilePath));

            return ProcessUtility.WriteFile(dependencyLockFilePath, JsonUtility.Serialize(dependencyLock));
        }
    }
}
