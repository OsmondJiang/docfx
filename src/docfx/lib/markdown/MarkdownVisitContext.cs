// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Docs.Build
{
    internal class MarkdownVisitContext
    {
        public List<SourceInfo?>? Parents { get; set; }

        public Document Document { get; set; }

        public bool IsInclude { get; set; }

        public MarkdownVisitContext(Document document, bool isInclude, List<SourceInfo?>? parents = null)
        {
            Parents = parents;
            Document = document;
            IsInclude = isInclude;
        }
    }
}
