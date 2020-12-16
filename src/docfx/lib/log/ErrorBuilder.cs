// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Docs.Build
{
    internal abstract class ErrorBuilder
    {
        public static readonly ErrorBuilder Null = new NullErrorBuilder();

        public abstract void Add(Error error);

        public abstract bool FileHasError(FilePath file);

        public void AddIfNotNull(Error? error)
        {
            if (error != null)
            {
                Add(error);
            }
        }

        public void AddRange(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                Add(error);
            }
        }

        public void AddRange(IEnumerable<DocfxException> exceptions)
        {
            foreach (var exception in exceptions)
            {
                Log.Write(exception);
                Add(exception.Error);
            }
        }

        public ErrorBuilder With(Func<Error, Error> convert)
        {
            return new DelegatingErrorBuilder(this, convert);
        }

        public ErrorBuilder WithDocsetPath(string workingDirectory, string docsetPath)
        {
            var docsetBasePath = new PathString(Path.GetRelativePath(workingDirectory, docsetPath));

            return With(error =>
            {
                // Convert from path relative to docset to path relative to working directory
                if (!docsetBasePath.IsDefault)
                {
                    if (error.Source != null)
                    {
                        var path = docsetBasePath.Concat(error.Source.File.Path);
                        error = error with { Source = error.Source with { File = error.Source.File with { Path = path } } };
                    }

                    if (error.OriginalPath != null)
                    {
                        error = error with { OriginalPath = docsetBasePath.Concat(error.OriginalPath.Value) };
                    }
                }
                return error;
            });
        }

        private class NullErrorBuilder : ErrorBuilder
        {
            public override void Add(Error error) { }

            public override bool FileHasError(FilePath file) => throw new NotSupportedException();
        }

        private class DelegatingErrorBuilder : ErrorBuilder
        {
            private readonly ErrorBuilder _errors;
            private readonly Func<Error, Error> _convert;

            public override bool FileHasError(FilePath file) => throw new NotSupportedException();

            public override void Add(Error error) => _errors.Add(_convert(error));

            public DelegatingErrorBuilder(ErrorBuilder errors, Func<Error, Error> convert)
            {
                _errors = errors;
                _convert = convert;
            }
        }
    }
}
