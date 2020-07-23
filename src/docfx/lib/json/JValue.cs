// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Docs.Build
{
    public class JValue : JToken, IEquatable<JValue>
    {
        public static readonly JValue Null = new JValue(JTokenType.Null);
        public static readonly JValue True = new JValue(true);
        public static readonly JValue False = new JValue(false);

        public object? Value { get; }

        public override JTokenType Type { get; }

        public JValue(string value)
        {
            Value = value;
            Type = JTokenType.String;
        }

        public JValue(bool value)
        {
            Value = value;
            Type = value ? JTokenType.True : JTokenType.False;
        }

        public JValue(int value)
        {
            Value = value;
            Type = JTokenType.Number;
        }

        public JValue(long value)
        {
            Value = value;
            Type = JTokenType.Number;
        }

        public JValue(double value)
        {
            Value = value;
            Type = JTokenType.Number;
        }

        private JValue(JTokenType type) => Type = type;

        public bool Equals(JValue? other) => other != null && Equals(Value, other.Value);

        public override bool Equals(object? obj) => Equals(obj as JValue);

        public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode();
    }
}
