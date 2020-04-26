// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Yunit;

namespace Microsoft.Docs.Build
{
    public partial class TestGitCommit
    {
        public string Message { get; set; }

        public string Author { get; set; } = "docfx";

        public string Email { get; set; } = "docfx@microsoft.com";

        public DateTimeOffset Time { get; set; } = new DateTimeOffset(2018, 10, 30, 0, 0, 0, TimeSpan.Zero);

        public Dictionary<string, string> Files { get; } = new Dictionary<string, string>();
    }

    internal partial class TestUtility
    {
        public static void MakeDebugAssertThrowException()
        {
            // This only works for .NET core
            // https://github.com/dotnet/corefx/blob/master/src/Common/src/CoreLib/System/Diagnostics/Debug.cs
            // https://github.com/dotnet/corefx/blob/8dbeee99ce48a46c3cee9d1b765c3b31af94e172/src/System.Diagnostics.Debug/tests/DebugTests.cs
            var showDialogHook = typeof(Debug).GetField("s_ShowDialog", BindingFlags.Static | BindingFlags.NonPublic);
            showDialogHook?.SetValue(null, new Action<string, string, string, string>(Throw));

            static void Throw(string stackTrace, string message, string detailMessage, string info)
            {
                throw new Exception($"Debug.Assert failed: {message} {detailMessage}\n{stackTrace}");
            }
        }

        public static void CreateFiles(
            string path,
            IEnumerable<KeyValuePair<string, string>> files,
            IEnumerable<KeyValuePair<string, string>> variables = null)
        {
            foreach (var file in files)
            {
                var filePath = Path.GetFullPath(Path.Combine(path, file.Key));
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                if (file.Key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    CreateZipFile(file, filePath);
                }
                else
                {
                    File.WriteAllText(filePath, ApplyVariables(file.Value, variables)?.Replace("\r", "") ?? "");
                }
            }
        }

        private static void CreateZipFile(KeyValuePair<string, string> file, string filePath)
        {
            var token = YamlUtility.ToJToken(file.Value);
            if (token is JObject obj)
            {
                using var memoryStream = new MemoryStream();
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (JProperty child in obj.Children())
                    {
                        var entry = archive.CreateEntry(child.Name);

                        using var entryStream = entry.Open();
                        using var sw = new StreamWriter(entryStream);
                        sw.Write(child.Value);
                    }
                }

                using var fileStream = new FileStream(filePath, FileMode.Create);
                memoryStream.Seek(0, SeekOrigin.Begin);
                memoryStream.CopyTo(fileStream);
            }
        }

        public static void CreateGitRepository(
            string path,
            TestGitCommit[] commits,
            string remote,
            string branch,
            IEnumerable<KeyValuePair<string, string>> variables = null)
        {
            Directory.CreateDirectory(path);
            Git(path, "init");
            Git(path, $"checkout --orphan \"{branch ?? "master"}\"");
            Git(path, $"remote add origin {remote}");

            foreach (var commit in commits.Reverse())
            {
                var commitIndex = 0;
                var commitMessage = commit.Message ?? $"Commit {commitIndex++}";

                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    if (!file.Contains(".git"))
                    {
                        File.Delete(file);
                    }
                }

                foreach (var (file, value) in commit.Files)
                {
                    var filePath = Path.Combine(path, file);
                    var content = ApplyVariables(value, variables)?.Replace("\r", "") ?? "";
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    File.WriteAllText(filePath, content);
                }

                var env = new Dictionary<string, string>
                {
                    ["GIT_AUTHOR_NAME"] = commit.Author,
                    ["GIT_AUTHOR_EMAIL"] = commit.Email,
                    ["GIT_AUTHOR_DATE"] = commit.Time.ToString("o"),
                    ["GIT_COMMITTER_NAME"] = commit.Author,
                    ["GIT_COMMITTER_EMAIL"] = commit.Email,
                    ["GIT_COMMITTER_DATE"] = commit.Time.ToString("o"),
                };

                Git(path, "add -A");
                Git(path, $"commit -m \"{commitMessage}\"", env);
            }
        }

        public static IDisposable EnsureFilesNotChanged(string path, bool skipInputCheck)
        {
            var before = GetFileLastWriteTimes(path);

            return new Disposable(() =>
            {
                if (!skipInputCheck)
                {
                    var after = GetFileLastWriteTimes(path);
                    new JsonDiff().Verify(before, after, "Input files changes");
                }
            });

            static Dictionary<string, DateTime> GetFileLastWriteTimes(string dir)
            {
                return new DirectoryInfo(dir)
                    .GetFiles("*", SearchOption.AllDirectories)
                    .Where(file => !file.FullName.Contains(".git"))
                    .ToDictionary(file => file.FullName, file => file.LastWriteTimeUtc);
            }
        }

        private static string ApplyVariables(string value, IEnumerable<KeyValuePair<string, string>> variables)
        {
            if (variables != null && value != null)
            {
                foreach (var variable in variables)
                {
                    value = value.Replace($"{{{variable.Key}}}", variable.Value);
                }
            }
            return value;
        }

        private static void Git(string cwd, string args, Dictionary<string, string> env = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = cwd,
                UseShellExecute = false,
            };

            if (env != null)
            {
                foreach (var (key, value) in env)
                {
                    psi.EnvironmentVariables.Add(key, value);
                }
            }

            Process.Start(psi).WaitForExit();
        }

        private class Disposable : IDisposable
        {
            private readonly Action _dispose;

            public Disposable(Action dispose) => _dispose = dispose;

            public void Dispose() => _dispose();
        }
    }
}
