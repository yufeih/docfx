// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Docs.Build
{
    public abstract class JToken
    {
        public abstract JTokenType Type { get; }

        public SourceInfo? SourceInfo { get; set; }

        public static implicit operator JToken(string? value) => value is null ? JValue.Null : new JValue(value);

        public static implicit operator JToken(bool value) => value ? JValue.True : JValue.False;

        public static implicit operator JToken(int value) => new JValue(value);

        public static implicit operator JToken(long value) => new JValue(value);

        public static implicit operator JToken(double value) => new JValue(value);
    }
}
