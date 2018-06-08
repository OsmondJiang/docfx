// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Docs.Build
{
    internal class DependencyMapBuilder
    {
        private readonly HashSet<DependencyItem> _dependencyItems = new HashSet<DependencyItem>();

        public void AddDependencyItem(Document relativeTo, Document dependencyDoc, DependencyType type)
        {
            Debug.Assert(relativeTo != null);

            if (dependencyDoc == null)
            {
                return;
            }

            _dependencyItems.Add(new DependencyItem(relativeTo, dependencyDoc, type));
        }

        public Dictionary<Document, IEnumerable<DependencyItem>> Build()
        {
            return _dependencyItems.GroupBy(k => k.Source).ToDictionary(k => k.Key, v => (IEnumerable<DependencyItem>)new HashSet<DependencyItem>(v));
        }
    }
}
