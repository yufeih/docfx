// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Markdig;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using Markdig.Syntax;
using Microsoft.DocAsCode.MarkdigEngine.Extensions;
using Microsoft.Docs.Validation;
using Validations.DocFx.Adapter;

#pragma warning disable CS0618

namespace Microsoft.Docs.Build
{
    internal class MarkdownEngine
    {
        private readonly LinkResolver _linkResolver;
        private readonly XrefResolver _xrefResolver;
        private readonly DocumentProvider _documentProvider;
        private readonly Input _input;
        private readonly MonikerProvider _monikerProvider;
        private readonly TemplateEngine _templateEngine;
        private readonly string _markdownValidationRules;
        private readonly ContentValidator _contentValidator;

        private readonly MarkdownContext _markdownContext;
        private readonly OnlineServiceMarkdownValidatorProvider? _validatorProvider;
        private readonly MarkdownPipeline[] _pipelines;

        private static readonly ThreadLocal<Stack<Status>> t_status = new ThreadLocal<Stack<Status>>(() => new Stack<Status>());

        public MarkdownEngine(
            Config config,
            Input input,
            FileResolver fileResolver,
            LinkResolver linkResolver,
            XrefResolver xrefResolver,
            DocumentProvider documentProvider,
            MonikerProvider monikerProvider,
            TemplateEngine templateEngine,
            ContentValidator contentValidator)
        {
            _input = input;
            _linkResolver = linkResolver;
            _xrefResolver = xrefResolver;
            _documentProvider = documentProvider;
            _monikerProvider = monikerProvider;
            _templateEngine = templateEngine;
            _contentValidator = contentValidator;

            _markdownContext = new MarkdownContext(GetToken, LogInfo, LogSuggestion, LogWarning, LogError, ReadFile, GetLink);
            _markdownValidationRules = ContentValidator.GetMarkdownValidationRulesFilePath(fileResolver, config);

            if (!string.IsNullOrEmpty(_markdownValidationRules))
            {
                _validatorProvider = new OnlineServiceMarkdownValidatorProvider(
                    new ContentValidationContext(_markdownValidationRules),
                    new ContentValidationLogger(_markdownContext));
            }

            _pipelines = new[]
            {
                CreateMarkdownPipeline(),
                CreateInlineMarkdownPipeline(),
                CreateTocMarkdownPipeline(),
            };
        }

        public (List<Error> errors, MarkdownDocument ast) Parse(string content, MarkdownPipelineType piplineType)
        {
            try
            {
                var status = new Status();

                t_status.Value!.Push(status);

                var ast = Markdown.Parse(content, _pipelines[(int)piplineType]);

                return (status.Errors, ast);
            }
            finally
            {
                t_status.Value!.Pop();
            }
        }

        public (List<Error> errors, string html) ToHtml(string markdown, Document file, MarkdownPipelineType pipelineType)
        {
            using (InclusionContext.PushFile(file))
            {
                try
                {
                    var status = new Status();

                    t_status.Value!.Push(status);

                    var html = Markdown.ToHtml(markdown, _pipelines[(int)pipelineType]);

                    ValidateHeadings();

                    return (status.Errors, html);
                }
                finally
                {
                    t_status.Value!.Pop();
                }
            }
        }

        private MarkdownPipeline CreateMarkdownPipeline()
        {
            return new MarkdownPipelineBuilder()
                .UseYamlFrontMatter()
                .UseDocfxExtensions(_markdownContext)
                .UseTelemetry()
                .UseXref(GetXref)
                .UseHtml(GetErrors, GetLink, GetXref)
                .UseMonikerZone(GetMonikerRange)
                .UseContentValidation(_validatorProvider, GetValidationNodes, ReadFile)
                .Build();
        }

        private MarkdownPipeline CreateInlineMarkdownPipeline()
        {
            return new MarkdownPipelineBuilder()
                .UseYamlFrontMatter()
                .UseDocfxExtensions(_markdownContext)
                .UseTelemetry()
                .UseXref(GetXref)
                .UseHtml(GetErrors, GetLink, GetXref)
                .UseMonikerZone(GetMonikerRange)
                .UseContentValidation(_validatorProvider, GetValidationNodes, ReadFile)
                .UseInlineOnly()
                .Build();
        }

        private static MarkdownPipeline CreateTocMarkdownPipeline()
        {
            var builder = new MarkdownPipelineBuilder();

            // Only supports heading block and link inline
            builder.BlockParsers.RemoveAll(parser => !(
                parser is HeadingBlockParser || parser is ParagraphBlockParser ||
                parser is ThematicBreakParser || parser is HtmlBlockParser));

            builder.InlineParsers.RemoveAll(parser => !(parser is LinkInlineParser || parser is EscapeInlineParser));

            builder.BlockParsers.Find<HeadingBlockParser>().MaxLeadingCount = int.MaxValue;

            builder.UseYamlFrontMatter()
                   .UseXref()
                   .UsePreciseSourceLocation();

            return builder.Build();
        }

        private string? GetToken(string key)
        {
            return _templateEngine.GetToken(key);
        }

        private static void LogInfo(string code, string message, MarkdownObject origin, int? line)
        {
            Log.Write($"{code}: {message}");
        }

        private static void LogError(string code, string message, MarkdownObject origin, int? line)
        {
            t_status.Value!.Peek().Errors.Add(new Error(ErrorLevel.Error, code, message, origin.ToSourceInfo(line)));
        }

        private static void LogWarning(string code, string message, MarkdownObject origin, int? line)
        {
            t_status.Value!.Peek().Errors.Add(new Error(ErrorLevel.Warning, code, message, origin.ToSourceInfo(line)));
        }

        private static void LogSuggestion(string code, string message, MarkdownObject origin, int? line)
        {
            t_status.Value!.Peek().Errors.Add(new Error(ErrorLevel.Suggestion, code, message, origin.ToSourceInfo(line)));
        }

        private static List<Error> GetErrors()
        {
            return t_status.Value!.Peek().Errors;
        }

        private (string? content, object? file) ReadFile(string path, MarkdownObject origin)
        {
            var status = t_status.Value!.Peek();
            var referencingFile = (FilePath)InclusionContext.File;

            var (error, file) = _linkResolver.ResolveContent(new SourceInfo<string>(path, origin.ToSourceInfo()), _documentProvider.GetDocument(referencingFile));
            status.Errors.AddIfNotNull(error);

            return file is null ? default : (_input.ReadString(file.FilePath).Replace("\r", ""), file);
        }

        private string GetLink(string path, MarkdownObject origin)
        {
            var status = t_status.Value!.Peek();

            var (error, link, _) = _linkResolver.ResolveLink(
                new SourceInfo<string>(path, origin.ToSourceInfo()),
                _documentProvider.GetDocument((FilePath)InclusionContext.File),
                _documentProvider.GetDocument((FilePath)InclusionContext.RootFile));
            status.Errors.AddIfNotNull(error);

            return link;
        }

        private string GetLink(SourceInfo<string> href)
        {
            var status = t_status.Value!.Peek();

            var (error, link, _) = _linkResolver.ResolveLink(
                href,
                _documentProvider.GetDocument((FilePath)InclusionContext.File),
                _documentProvider.GetDocument((FilePath)InclusionContext.RootFile));
            status.Errors.AddIfNotNull(error);

            return link;
        }

        private (string? href, string display) GetXref(SourceInfo<string>? href, SourceInfo<string>? uid, bool isShorthand)
        {
            var status = t_status.Value!.Peek();

            var (error, link, display, _) = href.HasValue
                ? _xrefResolver.ResolveXrefByHref(href.Value, _documentProvider.GetDocument((FilePath)InclusionContext.File), _documentProvider.GetDocument((FilePath)InclusionContext.RootFile))
                : _xrefResolver.ResolveXrefByUid(uid ?? default, _documentProvider.GetDocument((FilePath)InclusionContext.File), _documentProvider.GetDocument((FilePath)InclusionContext.RootFile));

            if (!isShorthand)
            {
                status.Errors.AddIfNotNull(error);
            }
            return (link, display);
        }

        private IReadOnlyList<string> GetMonikerRange(SourceInfo<string?> monikerRange)
        {
            var status = t_status.Value!.Peek();
            var (monikerErrors, monikers) = _monikerProvider.GetZoneLevelMonikers((FilePath)InclusionContext.RootFile, monikerRange);
            status.Errors.AddRange(monikerErrors);
            return monikers;
        }

        private Dictionary<FilePath, (List<ValidationNode> nodes, bool isIncluded)> GetValidationNodes(List<ValidationNode> nodes)
        {
            var status = t_status.Value!.Peek();

            if (!status.Nodes.ContainsKey((FilePath)InclusionContext.File))
            {
                status.Nodes.Add((FilePath)InclusionContext.File, (nodes, InclusionContext.IsInclude));
            }

            return status.Nodes;
        }

        private void ValidateHeadings()
        {
            var status = t_status.Value!.Peek();
            foreach (var (document, (nodes, isIncluded)) in status.Nodes)
            {
                _contentValidator.ValidateHeadings(document, nodes.OfType<ContentNode>().ToList(), isIncluded);
            }
        }

        private class Status
        {
            public List<Error> Errors { get; } = new List<Error>();

            public Dictionary<FilePath, (List<ValidationNode> nodes, bool isIncluded)> Nodes { get; } = new Dictionary<FilePath, (List<ValidationNode> nodes, bool isIncluded)>();
        }
    }
}
