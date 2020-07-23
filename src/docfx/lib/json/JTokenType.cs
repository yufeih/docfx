// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Docs.Build
{
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Spec Conformance")]
    public enum JTokenType
    {
        Null,

        Array,

        Object,

        String,

        Number,

        True,

        False,
    }
}
