// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Newtonsoft.Json;

namespace Microsoft.Docs.Build
{
    internal class JTokenJsonReader : JsonReader, IJsonLineInfo
    {
        private readonly JToken _root;

        public int LineNumber => throw new NotImplementedException();

        public int LinePosition => throw new NotImplementedException();

        public bool HasLineInfo()
        {
            throw new NotImplementedException();
        }

        public JTokenJsonReader(JToken token)
        {
            _root = token;
        }

        public override bool Read()
        {
            throw new NotImplementedException();
        }
    }
}
