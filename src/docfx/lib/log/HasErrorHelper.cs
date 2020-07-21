// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Microsoft.Docs.Build
{
    internal class HasErrorHelper : IErrorBuilder
    {
        private readonly IErrorBuilder _errors;

        private int _errorCount;
        private int _warningCount;
        private int _suggestionCount;

        public int ErrorCount => _errorCount;

        public int WarningCount => _warningCount;

        public int SuggestionCount => _suggestionCount;

        public bool HasError => Volatile.Read(ref _errorCount) > 0;

        public HasErrorHelper(IErrorBuilder errors)
        {
            _errors = errors;
        }

        public override void Add(Error error, ErrorLevel? overwriteLevel = null, PathString? originalPath = null)
        {
            var level = overwriteLevel ?? error.Level;
            var count = level switch
            {
                ErrorLevel.Error => Interlocked.Increment(ref _errorCount),
                ErrorLevel.Warning => Interlocked.Increment(ref _warningCount),
                ErrorLevel.Suggestion => Interlocked.Increment(ref _suggestionCount),
                _ => 0,
            };

            _errors.Add(error, overwriteLevel, originalPath);
        }

        [SuppressMessage("Reliability", "CA2002", Justification = "Lock Console.Out")]
        public void PrintSummary()
        {
            lock (Console.Out)
            {
                if (_errorCount > 0 || _warningCount > 0 || _suggestionCount > 0)
                {
                    Console.ForegroundColor = _errorCount > 0 ? ConsoleColor.Red
                                            : _warningCount > 0 ? ConsoleColor.Yellow
                                            : ConsoleColor.Magenta;
                    Console.WriteLine();
                    Console.WriteLine($"  {_errorCount} Error(s), {_warningCount} Warning(s), {_suggestionCount} Suggestion(s)");
                }

                Console.ResetColor();
            }
        }
    }
}
