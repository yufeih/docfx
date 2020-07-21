// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.Docs.Build
{
    internal class ErrorList : IErrorBuilder, IEnumerable<Error>
    {
        private List<Error>? _errors;

        public override void Add(Error error, ErrorLevel? overwriteLevel = null, PathString? originalPath = null)
        {
            LazyInitializer.EnsureInitialized(ref _errors, () => new List<Error>()).Add(error);
        }

        public IEnumerator<Error> GetEnumerator() => _errors is null
            ? Enumerable.Empty<Error>().GetEnumerator()
            : _errors.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _errors is null
            ? Enumerable.Empty<Error>().GetEnumerator()
            : _errors.GetEnumerator();
    }
}
