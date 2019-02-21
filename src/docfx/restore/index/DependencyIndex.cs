// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Docs.Build
{
    internal abstract class DependencyIndex
    {
        public string Id { get; set; }

        public string Url { get; set; }

        public DateTime LastAccessDate { get; set; }

        public bool Restored { get; set; }
    }
}
