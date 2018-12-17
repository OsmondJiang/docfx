// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Docs.Build
{
    /// <summary>
    /// A dependency map generated by one file building
    /// </summary>
    internal class DependencyMapBuilder
    {
        private readonly ConcurrentHashSet<DependencyItem> _dependencyItems = new ConcurrentHashSet<DependencyItem>();

        public void AddDependencyItem(Document from, Document to, DependencyType type)
        {
            Debug.Assert(from != null);

            if (from == null || to == null)
            {
                return;
            }

            var isLocalizedBuild = from.Docset.IsLocalizedBuild() || to.Docset.IsLocalizedBuild();
            if (isLocalizedBuild && !from.Docset.IsLocalized())
            {
                return;
            }

            _dependencyItems.TryAdd(new DependencyItem(from, to, type));
        }

        public DependencyMap Build()
        {
            return new DependencyMap(_dependencyItems
                .GroupBy(k => k.Source)
                .ToDictionary(
                    k => k.Key,
                    v => (from r in v.Distinct()
                         orderby r.Dest.FilePath, r.Type
                         select r).ToList()));
        }
    }
}
