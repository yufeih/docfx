// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Docs.Build
{
    internal enum OutputType
    {
        /// <summary>
        /// Output final HTML file.
        /// </summary>
        Html,

        /// <summary>
        /// Output JSON file before applying javascript in template.
        /// </summary>
        Json,

        /// <summary>
        /// Output JSON file after applying javascript in template.
        /// </summary>
        JsonPage,
    }
}
