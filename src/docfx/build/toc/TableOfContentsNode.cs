// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Docs.Build
{
    internal class TableOfContentsNode
    {
        public SourceInfo<string?> Name { get; set; }

        public string? DisplayName { get; set; }

        public SourceInfo<string?> Href { get; set; }

        public SourceInfo<string?> TopicHref { get; set; }

        public SourceInfo<string?> TocHref { get; set; }

        public string? Homepage { get; set; }

        public SourceInfo<string?> Uid { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool Expanded { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool MaintainContext { get; set; }

        public IReadOnlyList<string> Monikers { get; set; } = Array.Empty<string>();

        public List<SourceInfo<TableOfContentsNode>> Items { get; set; } = new List<SourceInfo<TableOfContentsNode>>();

        [JsonExtensionData]
        public JObject ExtensionData { get; set; } = new JObject();

        /// <summary>
        /// The article that this TOC node links to.
        /// </summary>
        [JsonIgnore]
        public Document? Document { get; set; }

        /// <summary>
        /// The TOC file that is embedded inside this TOC node.
        /// </summary>
        [JsonIgnore]
        public Document? NestedToc { get; set; }

        /// <summary>
        /// The TOC file that is referenced by this TOC node as a link.
        /// </summary>
        [JsonIgnore]
        public Document? LinkedToc { get; set; }

        public TableOfContentsNode() { }

        public TableOfContentsNode(TableOfContentsNode item)
        {
            Name = item.Name;
            DisplayName = item.DisplayName;
            Href = item.Href;
            TopicHref = item.TopicHref;
            TocHref = item.TocHref;
            Homepage = item.Homepage;
            Uid = item.Uid;
            Expanded = item.Expanded;
            MaintainContext = item.MaintainContext;
            ExtensionData = item.ExtensionData;
            Items = item.Items;
            Document = item.Document;
            NestedToc = item.NestedToc;
            LinkedToc = item.LinkedToc;
        }

        public void Walk(Action<TableOfContentsNode> action)
        {
            foreach (var item in Items)
            {
                item.Value.Walk(action);
            }

            action(this);
        }
    }
}
