// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;

using static Microsoft.Docs.Build.LibGit2;

namespace Microsoft.Docs.Build
{
    internal class GitBlobTrie
    {
        // Intern path strings by given each path segment a string ID. For faster string lookup.
        private static readonly ConcurrentDictionary<string, int> s_stringPool = new ConcurrentDictionary<string, int>();
        private static readonly Dictionary<int, Node> s_emptyNode = new Dictionary<int, Node>();
        private static int s_nextStringId;

        private readonly IntPtr _repo;

        private readonly ConcurrentDictionary<long, Dictionary<int, Node>> _nodeCache = new ConcurrentDictionary<long, Dictionary<int, Node>>();
        private readonly ConcurrentDictionary<long, Node> _trees = new ConcurrentDictionary<long, Node>();

        public static int GetStringId(string value)
        {
            return s_stringPool.GetOrAdd(value, _ => Interlocked.Increment(ref s_nextStringId));
        }

        public GitBlobTrie(IntPtr repo)
        {
            _repo = repo;
        }

        public long GetBlob(git_oid treeId, int[] pathSegments)
        {
            if (!_trees.TryGetValue(treeId.a, out var node))
            {
                _trees[treeId.a] = node = new Node { Value = treeId };
            }

            var last = pathSegments.Length - 1;

            for (var i = 0; i < pathSegments.Length; i++)
            {
                if (node.Children is null)
                {
                    ExpandTree(node);
                }

                if (!node.Children!.TryGetValue(pathSegments[i], out node))
                {
                    return default;
                }

                if (i == last)
                {
                    return node.Value.a;
                }
            }

            return default;
        }

        private unsafe void ExpandTree(Node node)
        {
            if (_nodeCache.TryGetValue(node.Value.a, out var result))
            {
                node.Children = result;
                return;
            }

            var treeId = node.Value;
            if (git_object_lookup(out var tree, _repo, &treeId, 2 /* GIT_OBJ_TREE */) != 0)
            {
                _nodeCache[treeId.a] = node.Children = s_emptyNode;
                return;
            }

            var n = (int)git_tree_entrycount(tree);
            var blobs = new Dictionary<int, Node>(n);

            for (var i = 0; i < n; i++)
            {
                if (node.Children != null)
                {
                    git_object_free(tree);
                    return;
                }

                var entry = git_tree_entry_byindex(tree, (IntPtr)i);
                var name = Marshal.PtrToStringUTF8(git_tree_entry_name(entry)) ?? "";
                blobs[GetStringId(name)] = new Node { Value = *git_tree_entry_id(entry) };
            }

            git_object_free(tree);

            blobs.TrimExcess();
            _nodeCache[treeId.a] = node.Children = blobs;
        }

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Internal data structure")]
        private class Node
        {
            public git_oid Value;
            public Dictionary<int, Node>? Children;
        }
    }
}
