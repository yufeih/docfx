// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Docs.Build
{
    internal static class Restore
    {
        public static int Run(string workingDirectory, CommandLineOptions options)
        {
            using var errorWriter = new ErrorWriter(options.Log);
            var errors = new HasErrorHelper(errorWriter);

            var docsets = ConfigLoader.FindDocsets(errors, workingDirectory, options);
            if (docsets.Length == 0)
            {
                errors.Add(Errors.Config.ConfigNotFound(workingDirectory));
                return 1;
            }

            ParallelUtility.ForEach(errors, docsets, docset =>
            {
                RestoreDocset(errors, docset.docsetPath, docset.outputPath, options, FetchOptions.Latest);
            });

            return errors.HasError ? 1 : 0;
        }

        public static void RestoreDocset(
            IErrorBuilder errorBuilder, string docsetPath, string? outputPath, CommandLineOptions options, FetchOptions fetchOptions)
        {
            var stopwatch = Stopwatch.StartNew();
            var errors = new HasErrorHelper(errorBuilder);
            using var disposables = new DisposableCollector();

            var (config, buildOptions, packageResolver, fileResolver) = ConfigLoader.Load(
                errors, disposables, docsetPath, outputPath, options, fetchOptions);

            if (errors.HasError)
            {
                return;
            }

            // download dependencies to disk
            Parallel.Invoke(
                () => RestoreFiles(errors, config, fileResolver),
                () => RestorePackages(errors, buildOptions, config, packageResolver));

            Telemetry.TrackOperationTime("restore", stopwatch.Elapsed);
            Log.Important($"Restore done in {Progress.FormatTimeSpan(stopwatch.Elapsed)}", ConsoleColor.Green);
            errors.PrintSummary();
        }

        private static void RestoreFiles(IErrorBuilder errorLog, Config config, FileResolver fileResolver)
        {
            ParallelUtility.ForEach(errorLog, config.GetFileReferences(), fileResolver.Download);
        }

        private static void RestorePackages(IErrorBuilder errorLog, BuildOptions buildOptions, Config config, PackageResolver packageResolver)
        {
            ParallelUtility.ForEach(
                errorLog,
                GetPackages(config).Distinct(),
                item => packageResolver.DownloadPackage(item.package, item.flags));

            LocalizationUtility.EnsureLocalizationContributionBranch(config, buildOptions.Repository);
        }

        private static IEnumerable<(PackagePath package, PackageFetchOptions flags)> GetPackages(Config config)
        {
            foreach (var (_, package) in config.Dependencies)
            {
                yield return (package, package.PackageFetchOptions);
            }

            if (config.Template.Type == PackageType.Git)
            {
                yield return (config.Template, PackageFetchOptions.DepthOne);
            }
        }
    }
}
