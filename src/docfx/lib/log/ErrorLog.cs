// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;

namespace Microsoft.Docs.Build
{
    internal class ErrorLog : ErrorBuilder
    {
        private readonly ErrorBuilder _errors;
        private readonly Config _config;
        private readonly SourceMap? _sourceMap;
        private readonly MetadataProvider? _metadataProvider;
        private readonly Func<CustomRuleProvider>? _customRuleProvider;

        private readonly ErrorSink _errorSink = new();
        private readonly ConcurrentDictionary<FilePath, ErrorSink> _fileSink = new();

        public override bool FileHasError(FilePath file) => _fileSink.TryGetValue(file, out var sink) && sink.ErrorCount > 0;

        public ErrorLog(
            ErrorBuilder errors,
            Config config,
            SourceMap? sourceMap = null,
            MetadataProvider? metadataProvider = null,
            Func<CustomRuleProvider>? customRuleProvider = null)
        {
            _errors = errors;
            _config = config;
            _sourceMap = sourceMap;
            _metadataProvider = metadataProvider;
            _customRuleProvider = customRuleProvider;
        }

        public override void Add(Error error)
        {
            try
            {
                if (_metadataProvider != null && error.Source?.File is FilePath source)
                {
                    var msAuthor = _metadataProvider.GetMetadata(Null, source).MsAuthor;
                    if (msAuthor != default)
                    {
                        error = error with { MsAuthor = msAuthor };
                    }
                }

                error = _customRuleProvider?.Invoke().ApplyCustomRule(error) ?? error;
            }
            catch (Exception ex) when (DocfxException.IsDocfxException(ex, out var dex))
            {
                Log.Write(ex);
            }

            if (error.Level == ErrorLevel.Off)
            {
                return;
            }

            if (_config.WarningsAsErrors && error.Level == ErrorLevel.Warning)
            {
                error = error with { Level = ErrorLevel.Error };
            }

            if (error.Source?.File != null && error.Source?.File.Origin == FileOrigin.Fallback)
            {
                if (error.Level == ErrorLevel.Error)
                {
                    Add(Errors.Logging.FallbackError(_config.DefaultLocale));
                }
                return;
            }

            if (error.Source != null)
            {
                error = error with { OriginalPath = _sourceMap?.GetOriginalFilePath(error.Source.File)?.Path };
            }

            var errorSink = error.Source?.File is null ? _errorSink : _fileSink.GetOrAdd(error.Source.File, _ => new ErrorSink());

            switch (errorSink.Add(error.Source?.File is null ? null : _config, error))
            {
                case ErrorSinkResult.Ok:
                    _errors.Add(error);
                    break;

                case ErrorSinkResult.Exceed when error.Source?.File != null:
                    var maxAllowed = error.Level switch
                    {
                        ErrorLevel.Error => _config.MaxFileErrors,
                        ErrorLevel.Warning => _config.MaxFileWarnings,
                        ErrorLevel.Suggestion => _config.MaxFileSuggestions,
                        ErrorLevel.Info => _config.MaxFileInfos,
                        _ => 0,
                    };
                    _errors.Add(Errors.Logging.ExceedMaxFileErrors(maxAllowed, error.Level, error.Source.File));
                    break;
            }
        }
    }
}
