// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Docs.Build
{
    internal abstract class IErrorBuilder
    {
        public abstract void Add(Error error, ErrorLevel? overwriteLevel = null, PathString? originalPath = null);

        public void AddRange(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                Add(error);
            }
        }

        public void Write(Error error)
        {
            Add(error);
        }

        public void Write(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                Write(error);
            }
        }

        public void Write(IEnumerable<DocfxException> exceptions)
        {
            foreach (var exception in exceptions)
            {
                Log.Write(exception);
                Add(exception.Error, exception.OverwriteLevel);
            }
        }
    }
}
