// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Collections.Extensions;

namespace Microsoft.Docs.Build
{
    public class JObject : JToken, IReadOnlyCollection<KeyValuePair<string, JToken>>, IEnumerable<KeyValuePair<string, JToken>>
    {
        private readonly DictionarySlim<string, JToken> _properties = new DictionarySlim<string, JToken>();

        public int Count => _properties.Count;

        public override JTokenType Type => JTokenType.Object;

        public JToken? this[string key]
        {
            get => _properties.TryGetValue(key, out var result) ? result : null;
            set => _properties.GetOrAddValueRef(key) = value ?? JValue.Null;
        }

        public JObject() { }

        public JObject(IEnumerable<KeyValuePair<string, JToken>> properties)
        {
            foreach (var (key, value) in properties)
            {
                _properties.GetOrAddValueRef(key) = value;
            }
        }

        public bool ContainsKey(string key) => _properties.ContainsKey(key);

        public bool TryGetValue(string key, [NotNullWhen(true)] out JToken? value) => _properties.TryGetValue(key, out value);

        public T Value<T>(string key) => _properties.TryGetValue(key, out var token) && token is JValue value && value.Value != null
            ? (T)value.Value
            : throw new ArgumentOutOfRangeException(key);

        public bool Remove(string key) => _properties.Remove(key);

        public IEnumerator<KeyValuePair<string, JToken>> GetEnumerator() => _properties.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _properties.GetEnumerator();
    }
}
