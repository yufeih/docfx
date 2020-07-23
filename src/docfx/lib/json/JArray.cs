// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Docs.Build
{
    public class JArray : JToken, IReadOnlyList<JToken>
    {
        private readonly List<JToken> _items = new List<JToken>();

        public JArray() { }

        public JArray(IEnumerable<JToken> items)
        {
            _items.AddRange(items);
        }

        public override JTokenType Type => JTokenType.Array;

        public JToken this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        public int Count => _items.Count;

        public void Add(JToken token) => _items.Add(token);

        public IEnumerator<JToken> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
    }
}
