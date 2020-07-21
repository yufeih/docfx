// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Microsoft.Docs.Build
{
    internal class ErrorWriter : IErrorBuilder, IDisposable
    {
        private readonly object _outputLock = new object();
        private readonly Lazy<TextWriter> _output;

        public ErrorWriter(string? logPath)
        {
            _output = new Lazy<TextWriter>(() => logPath is null ? TextWriter.Null : CreateOutput(logPath));
        }

        public override void Add(Error error, ErrorLevel? overwriteLevel = null, PathString? originalPath = null)
        {
            var level = overwriteLevel ?? error.Level;

            Telemetry.TrackErrorCount(error.Code, level, error.Name);

            if (_output != null)
            {
                lock (_outputLock)
                {
                    _output.Value.WriteLine(error.ToString(level, originalPath));
                }
            }

            PrintError(error, level);
        }

        public void Dispose()
        {
            lock (_outputLock)
            {
                if (_output.IsValueCreated)
                {
                    _output.Value.Dispose();
                }
            }
        }

        [SuppressMessage("Reliability", "CA2002", Justification = "Lock Console.Out")]
        private static void PrintError(Error error, ErrorLevel? level = null)
        {
            lock (Console.Out)
            {
                var errorLevel = level ?? error.Level;
                var output = errorLevel == ErrorLevel.Error ? Console.Error : Console.Out;
                Console.ForegroundColor = GetColor(errorLevel);
                output.Write(error.Code + " ");
                Console.ResetColor();
                output.WriteLine($"./{error.FilePath}({error.Line},{error.Column}): {error.Message}");
            }
        }

        private TextWriter CreateOutput(string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            return File.AppendText(outputPath);
        }

        private static ConsoleColor GetColor(ErrorLevel level)
        {
            return level switch
            {
                ErrorLevel.Error => ConsoleColor.Red,
                ErrorLevel.Warning => ConsoleColor.Yellow,
                ErrorLevel.Suggestion => ConsoleColor.Magenta,
                ErrorLevel.Info => ConsoleColor.DarkGray,
                _ => ConsoleColor.DarkGray,
            };
        }
    }
}
