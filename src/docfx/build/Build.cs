// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Docs.Validation;

namespace Microsoft.Docs.Build
{
    internal static class Build
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
                BuildDocset(errors, docset.docsetPath, docset.outputPath, options);
            });

            return errors.HasError ? 1 : 0;
        }

        private static void BuildDocset(IErrorBuilder errorBuilder, string docsetPath, string? outputPath, CommandLineOptions options)
        {
            var stopwatch = Stopwatch.StartNew();
            var errors = new HasErrorHelper(errorBuilder);
            using var disposables = new DisposableCollector();

            if (!options.NoRestore)
            {
                Restore.RestoreDocset(errors, docsetPath, outputPath, options, options.NoCache ? FetchOptions.Latest : FetchOptions.UseCache);
                if (errors.HasError)
                {
                    return;
                }
            }

            var (config, buildOptions, packageResolver, fileResolver) = ConfigLoader.Load(
                errors, disposables, docsetPath, outputPath, options, options.NoRestore ? FetchOptions.NoFetch : FetchOptions.UseCache);

            if (errors.HasError)
            {
                return;
            }

            new OpsPreProcessor(config, errors, buildOptions).Run();

            var sourceMap = new SourceMap(new PathString(buildOptions.DocsetPath), config, fileResolver);
            var validationRules = GetContentValidationRules(config, fileResolver);
            var errorLog = new ErrorLog(errors, config, sourceMap, validationRules);

            using var context = new Context(errorLog, config, buildOptions, packageResolver, fileResolver, sourceMap);
            Run(context);

            new OpsPostProcessor(config, errors, buildOptions).Run();

            Telemetry.TrackOperationTime("build", stopwatch.Elapsed);
            Log.Important($"Build done in {Progress.FormatTimeSpan(stopwatch.Elapsed)}", ConsoleColor.Green);
            errors.PrintSummary();
        }

        private static void Run(Context context)
        {
            using (Progress.Start("Building files"))
            {
                ParallelUtility.ForEach(
                    context.ErrorLog,
                    context.PublishUrlMap.GetAllFiles(),
                    file => BuildFile(context, file));
            }

            Parallel.Invoke(
                () => context.BookmarkValidator.Validate(),
                () => context.ContentValidator.PostValidate(),
                () => context.ErrorLog.Write(context.MetadataValidator.PostValidate()),
                () => context.ContributionProvider.Save(),
                () => context.RepositoryProvider.Save(),
                () => context.ErrorLog.Write(context.GitHubAccessor.Save()),
                () => context.ErrorLog.Write(context.MicrosoftGraphAccessor.Save()));

            // TODO: explicitly state that ToXrefMapModel produces errors
            var xrefMapModel = context.XrefResolver.ToXrefMapModel(context.BuildOptions.IsLocalizedBuild);
            var (publishModel, fileManifests) = context.PublishModelBuilder.Build();

            if (context.Config.DryRun)
            {
                return;
            }

            // TODO: decouple files and dependencies from legacy.
            var dependencyMap = context.DependencyMapBuilder.Build();

            Parallel.Invoke(
                () => context.Output.WriteJson(".xrefmap.json", xrefMapModel),
                () => context.Output.WriteJson(".publish.json", publishModel),
                () => context.Output.WriteJson(".dependencymap.json", dependencyMap.ToDependencyMapModel()),
                () => context.Output.WriteJson(".links.json", context.FileLinkMapBuilder.Build(context.PublishUrlMap.GetAllFiles())),
                () => Legacy.ConvertToLegacyModel(context.BuildOptions.DocsetPath, context, fileManifests, dependencyMap));

            using (Progress.Start("Waiting for pending outputs"))
            {
                context.Output.WaitForCompletion();
            }
        }

        private static void BuildFile(Context context, FilePath path)
        {
            var file = context.DocumentProvider.GetDocument(path);
            switch (file.ContentType)
            {
                case ContentType.TableOfContents:
                    BuildTableOfContents.Build(context, file);
                    break;
                case ContentType.Resource:
                    BuildResource.Build(context, file);
                    break;
                case ContentType.Page:
                    BuildPage.Build(context, file);
                    break;
                case ContentType.Redirection:
                    BuildRedirection.Build(context, file);
                    break;
            }
        }

        private static Dictionary<string, ValidationRules>? GetContentValidationRules(Config? config, FileResolver fileResolver)
            => !string.IsNullOrEmpty(config?.MarkdownValidationRules.Value)
            ? JsonUtility.DeserializeData<Dictionary<string, ValidationRules>>(
                fileResolver.ReadString(config.MarkdownValidationRules),
                config.MarkdownValidationRules.Source?.File)
            : null;
    }
}
