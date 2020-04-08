// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Microsoft.Docs.Build
{
    internal class TableOfContentsLoader
    {
        private readonly LinkResolver _linkResolver;
        private readonly XrefResolver _xrefResolver;
        private readonly TableOfContentsParser _parser;
        private readonly MonikerProvider _monikerProvider;
        private readonly DependencyMapBuilder _dependencyMapBuilder;

        private readonly ConcurrentDictionary<FilePath, (List<Error>, TableOfContentsNode)> _cache =
                     new ConcurrentDictionary<FilePath, (List<Error>, TableOfContentsNode)>();

        private static readonly HashSet<string> s_tocFileNames = new HashSet<string>(PathUtility.PathComparer)
        {
            "TOC.md", "TOC.json", "TOC.yml",
            "TOC.experimental.md", "TOC.experimental.json", "TOC.experimental.yml",
        };

        private static ThreadLocal<Stack<Document>> t_recursionDetector = new ThreadLocal<Stack<Document>>(() => new Stack<Document>());

        private static Document InclusionRoot => t_recursionDetector.Value?.Last() ?? throw new InvalidOperationException();

        public TableOfContentsLoader(
            LinkResolver linkResolver, XrefResolver xrefResolver, TableOfContentsParser parser, MonikerProvider monikerProvider, DependencyMapBuilder dependencyMapBuilder)
        {
            _linkResolver = linkResolver;
            _xrefResolver = xrefResolver;
            _parser = parser;
            _monikerProvider = monikerProvider;
            _dependencyMapBuilder = dependencyMapBuilder;
        }

        public (List<Error> errors, TableOfContentsNode node) Load(Document file)
        {
            return _cache.GetOrAdd(file.FilePath, _ =>
            {
                var errors = new List<Error>();
                var node = LoadTocFile(file, errors);

                return (errors, node);
            });
        }

        private TableOfContentsNode LoadTocFile(Document file, List<Error> errors)
        {
            // add to parent path
            var recursionDetector = t_recursionDetector.Value!;
            if (recursionDetector.Contains(file))
            {
                throw Errors.Link.CircularReference(new SourceInfo(file.FilePath, 1, 1), file, recursionDetector).ToException();
            }

            try
            {
                recursionDetector.Push(file);

                var node = _parser.Parse(file.FilePath, errors);
                node.Items = LoadTocNode(node.Items, file, errors);
                return node;
            }
            finally
            {
                recursionDetector.Pop();
            }
        }

        private List<SourceInfo<TableOfContentsNode>> LoadTocNode(List<SourceInfo<TableOfContentsNode>> nodes, Document filePath, List<Error> errors)
        {
            var newItems = new List<SourceInfo<TableOfContentsNode>>();
            foreach (var node in nodes)
            {
                var (nestedToc, linkedToc) = ResolveToc(filePath, node, errors);
                var newItem = new TableOfContentsNode(node)
                {
                    NestedToc = nestedToc,
                    LinkedToc = linkedToc,
                    TocHref = default,
                    TopicHref = default,
                };

                // Resolve items
                if (nestedToc != null)
                {
                    newItem.Items = LoadTocFile(nestedToc, errors).Items;
                }
                else
                {
                    newItem.Items = LoadTocNode(node.Value.Items, filePath, errors);
                }

                // Resolve href and homepage
                var (href, name, document) = ResolveTopic(filePath, node, errors);
                newItem.Href = href;
                newItem.Document = document;

                if (string.IsNullOrEmpty(node.Value.Href) && !string.IsNullOrEmpty(node.Value.TopicHref))
                {
                    newItem.Homepage = href;
                }

                if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(newItem.Name))
                {
                    newItem.Name = name;
                }

                // Resolve linked TOC first child
                if (linkedToc != null && newItem.Document is null)
                {
                    var firstChild = GetFirstItem(LoadTocFile(linkedToc, errors).Items);
                    if (firstChild != null)
                    {
                        newItem.Document = firstChild.Document;
                        newItem.Href = firstChild.Href;
                    }
                }

                // resolve monikers
                newItem.Monikers = GetMonikers(newItem, errors);
                newItems.Add(new SourceInfo<TableOfContentsNode>(newItem, node.Source));

                // validate
                if (string.IsNullOrEmpty(newItem.Name))
                {
                    errors.Add(Errors.TableOfContents.MissingTocName(newItem.Name.Source ?? node.Source));
                }
            }

            return newItems;
        }

        private IReadOnlyList<string> GetMonikers(TableOfContentsNode currentItem, List<Error> errors)
        {
            var monikers = new HashSet<string>();
            if (currentItem.Document != null)
            {
                var (monikerErrors, referenceFileMonikers) = _monikerProvider.GetFileLevelMonikers(currentItem.Document.FilePath);
                errors.AddRange(monikerErrors);

                if (referenceFileMonikers.Length == 0)
                {
                    return Array.Empty<string>();
                }
                monikers.AddRange(referenceFileMonikers);
            }

            // Union with children's monikers
            foreach (var item in currentItem.Items)
            {
                if (item.Value.Monikers.Count == 0)
                {
                    return Array.Empty<string>();
                }
                monikers.AddRange(item.Value.Monikers);
            }

            return monikers.OrderBy(item => item).ToArray();
        }

        private (SourceInfo<string?> href, SourceInfo<string?> name, Document? file) ResolveTopic(Document filePath, TableOfContentsNode node, List<Error> errors)
        {
            // Process uid
            if (!string.IsNullOrEmpty(node.Uid.Value))
            {
                var (uidError, uidLink, display, declaringFile) = _xrefResolver.ResolveXref(node.Uid!, filePath, InclusionRoot);
                errors.AddIfNotNull(uidError);

                if (!string.IsNullOrEmpty(uidLink))
                {
                    return (new SourceInfo<string?>(uidLink, node.Uid), new SourceInfo<string?>(display, node.Uid), declaringFile);
                }
            }

            // Process topicHref or href
            var href = node.TopicHref.Or(node.Href);
            if (string.IsNullOrEmpty(href))
            {
                return default;
            }

            switch (GetTocLinkType(href))
            {
                case TableOfContentsLinkType.Folder:
                case TableOfContentsLinkType.TocFile:
                    if (!string.IsNullOrEmpty(node.TopicHref))
                    {
                        errors.Add(Errors.TableOfContents.InvalidTopicHref(node.TopicHref));
                    }
                    return default;
            }

            var (error, link, resolvedFile) = _linkResolver.ResolveLink(href!, filePath, InclusionRoot);
            errors.AddIfNotNull(error);

            return (new SourceInfo<string?>(link, href), default, resolvedFile);
        }

        private (Document? nestedTocFile, Document? linkedTocFile) ResolveToc(Document filePath, TableOfContentsNode node, List<Error> errors)
        {
            var href = node.TocHref.Or(node.Href);

            switch (GetTocLinkType(href))
            {
                case TableOfContentsLinkType.TocFile:
                    var (error, nestedTocFile) = _linkResolver.ResolveContent(href!, filePath, DependencyType.TocInclusion);
                    errors.AddIfNotNull(error);
                    return (nestedTocFile, null);

                case TableOfContentsLinkType.Folder:
                    return (null, FindTocInFolder(href!, filePath));

                case TableOfContentsLinkType.AbsolutePath:
                    return default;

                default:
                    if (!string.IsNullOrEmpty(node.TocHref))
                    {
                        errors.Add(Errors.TableOfContents.InvalidTocHref(node.TocHref));
                    }
                    return default;
            }
        }

        private Document? FindTocInFolder(SourceInfo<string> href, Document filePath)
        {
            var result = default(Document);
            var (hrefPath, _, _) = UrlUtility.SplitUrl(href);
            foreach (var name in s_tocFileNames)
            {
                var tocHref = new SourceInfo<string>(Path.Combine(hrefPath, name), href);
                var (_, subToc) = _linkResolver.ResolveContent(tocHref, filePath, DependencyType.TocInclusion);
                if (subToc != null)
                {
                    if (!subToc.FilePath.IsGitCommit)
                    {
                        return subToc;
                    }
                    else if (result is null)
                    {
                        result = subToc;
                    }
                }
            }
            return result;
        }

        private static TableOfContentsNode? GetFirstItem(List<SourceInfo<TableOfContentsNode>> items)
        {
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.Value.Href))
                    return item;
            }

            foreach (var item in items)
            {
                return GetFirstItem(item.Value.Items);
            }

            return null;
        }

        private static TableOfContentsLinkType GetTocLinkType(string? href)
        {
            if (string.IsNullOrEmpty(href))
            {
                return TableOfContentsLinkType.Other;
            }

            switch (UrlUtility.GetLinkType(href))
            {
                case LinkType.AbsolutePath:
                    return TableOfContentsLinkType.AbsolutePath;

                case LinkType.RelativePath:
                    var (path, _, _) = UrlUtility.SplitUrl(href);
                    if (path.EndsWith('/') || path.EndsWith('\\'))
                    {
                        return TableOfContentsLinkType.Folder;
                    }

                    if (s_tocFileNames.Contains(Path.GetFileName(path)))
                    {
                        return TableOfContentsLinkType.TocFile;
                    }
                    return TableOfContentsLinkType.Other;

                default:
                    return TableOfContentsLinkType.Other;
            }
        }
    }
}
