// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Docs.Build
{
    internal class PublishModelBuilder
    {
        private readonly ConcurrentDictionary<string, ConcurrentBag<Document>> _outputPathConflicts = new ConcurrentDictionary<string, ConcurrentBag<Document>>(PathUtility.PathComparer);
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Document, List<string>>> _filesBySiteUrl = new ConcurrentDictionary<string, ConcurrentDictionary<Document, List<string>>>(PathUtility.PathComparer);
        private readonly ConcurrentDictionary<string, Document> _filesByOutputPath = new ConcurrentDictionary<string, Document>(PathUtility.PathComparer);
        private readonly ConcurrentDictionary<Document, PublishItem> _publishItems = new ConcurrentDictionary<Document, PublishItem>();
        private readonly ListBuilder<Document> _filesWithErrors = new ListBuilder<Document>();

        public void MarkError(Document file)
        {
            // TODO: If Error has a Document identifier, we can retrieve files with errors from
            //       error log without explicitly call PublishModelBuilder.MarkError
            _filesWithErrors.Add(file);
        }

        public bool TryAdd(Document file, PublishItem item)
        {
            _publishItems[file] = item;

            if (item.Path != null)
            {
                // Find output path conflicts
                if (!_filesByOutputPath.TryAdd(item.Path, file))
                {
                    if (_filesByOutputPath.TryGetValue(item.Path, out var existingFile) && existingFile != file)
                    {
                        _outputPathConflicts.GetOrAdd(item.Path, _ => new ConcurrentBag<Document>()).Add(file);
                    }
                    return false;
                }
            }

            var monikers = item.Monikers;
            if (monikers.Count == 0)
            {
                // TODO: report a warning if there are multiple files published to same url, one of them have no version
                monikers = new List<string> { "NONE_VERSION" };
            }
            _filesBySiteUrl.GetOrAdd(item.Url, _ => new ConcurrentDictionary<Document, List<string>>()).TryAdd(file, monikers);

            return true;
        }

        public (PublishModel, Dictionary<Document, PublishItem>) Build(Context context, bool legacy)
        {
            // Handle publish url conflicts
            // TODO: Report more detail info for url conflict
            foreach (var (siteUrl, files) in _filesBySiteUrl)
            {
                var conflictMoniker = files
                    .SelectMany(file => file.Value)
                    .GroupBy(moniker => moniker)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key);

                if (conflictMoniker.Count() > 0)
                {
                    context.ErrorLog.Write(Errors.PublishUrlConflict(siteUrl, files.Keys, conflictMoniker));

                    foreach (var conflictingFile in files.Keys)
                    {
                        if (_publishItems.TryGetValue(conflictingFile, out var item))
                        {
                            item.HasError = true;
                            if (item.Path != null && IsInsideOutputFolder(item, conflictingFile.Docset))
                                context.Output.Delete(item.Path, legacy);
                        }
                    }
                }
            }

            // Handle output path conflicts
            foreach (var (outputPath, conflict) in _outputPathConflicts)
            {
                var conflictingFiles = new HashSet<Document>();

                foreach (var conflictingFile in conflict)
                {
                    conflictingFiles.Add(conflictingFile);
                }

                if (_filesByOutputPath.TryRemove(outputPath, out var removed))
                {
                    conflictingFiles.Add(removed);
                }

                context.ErrorLog.Write(Errors.OutputPathConflict(outputPath, conflictingFiles));

                foreach (var conflictingFile in conflictingFiles)
                {
                    if (_publishItems.TryGetValue(conflictingFile, out var item))
                    {
                        item.HasError = true;

                        if (item.Path != null && IsInsideOutputFolder(item, conflictingFile.Docset))
                            context.Output.Delete(item.Path, legacy);
                    }
                }
            }

            // Handle files with errors
            foreach (var file in _filesWithErrors.ToList())
            {
                if (_filesBySiteUrl.TryRemove(file.SiteUrl, out _))
                {
                    if (_publishItems.TryGetValue(file, out var item))
                    {
                        item.HasError = true;

                        if (item.Path != null && IsInsideOutputFolder(item, file.Docset))
                            context.Output.Delete(item.Path, legacy);
                    }
                }
            }

            var model = new PublishModel
            {
                Files = _publishItems.Values
                    .OrderBy(item => item.Locale)
                    .ThenBy(item => item.Path)
                    .ThenBy(item => item.Url)
                    .ThenBy(item => item.RedirectUrl)
                    .ThenBy(item => item.MonikerGroup)
                    .ToArray(),
                MonikerGroups = _publishItems.Values
                    .Where(item => !string.IsNullOrEmpty(item.MonikerGroup))
                    .GroupBy(item => item.MonikerGroup)
                    .ToDictionary(g => g.Key, g => g.First().Monikers),
            };

            var fileManifests = _publishItems.ToDictionary(item => item.Key, item => item.Value);

            return (model, fileManifests);
        }

        private bool IsInsideOutputFolder(PublishItem item, Docset docset)
        {
            var outputFilePath = PathUtility.NormalizeFolder(Path.Combine(docset.DocsetPath, docset.Config.Output.Path, item.Path));
            var outputFolderPath = PathUtility.NormalizeFolder(Path.Combine(docset.DocsetPath, docset.Config.Output.Path));
            return outputFilePath.StartsWith(outputFolderPath);
        }
    }
}
