// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Docs.Validation;

namespace Microsoft.Docs.Build
{
    internal sealed class ErrorLog : IErrorBuilder
    {
        private readonly IErrorBuilder _builder;
        private readonly Config _config;
        private readonly SourceMap? _sourceMap;
        private readonly Dictionary<string, CustomRule> _customRules = new Dictionary<string, CustomRule>();

        private readonly ErrorSink _errorSink = new ErrorSink();
        private readonly ConcurrentDictionary<FilePath, ErrorSink> _fileSink = new ConcurrentDictionary<FilePath, ErrorSink>();

        public bool HasError(FilePath file) => _fileSink.TryGetValue(file, out var sink) && sink.ErrorCount > 0;

        public ErrorLog(IErrorBuilder builder, Config config, SourceMap? sourceMap = null, Dictionary<string, ValidationRules>? contentValidationRules = null)
        {
            _builder = builder;
            _config = config;
            _sourceMap = sourceMap;
            _customRules = MergeCustomRules(config, contentValidationRules);
        }

        public override void Add(Error error, ErrorLevel? overwriteLevel = null, PathString? unused = null)
        {
            var config = _config;
            if (_customRules.TryGetValue(error.Code, out var customRule))
            {
                error = error.WithCustomRule(customRule);
            }

            var level = overwriteLevel ?? error.Level;
            if (level == ErrorLevel.Off)
            {
                return;
            }

            if (config.WarningsAsErrors && level == ErrorLevel.Warning)
            {
                level = ErrorLevel.Error;
            }

            if (error.FilePath != null && error.FilePath.Origin == FileOrigin.Fallback)
            {
                if (level == ErrorLevel.Error)
                {
                    _builder.Add(Errors.Logging.FallbackError(config.DefaultLocale));
                    return;
                }
            }

            var errorSink = error.FilePath is null ? _errorSink : _fileSink.GetOrAdd(error.FilePath, _ => new ErrorSink());
            var originalPath = error.FilePath is null ? null : _sourceMap?.GetOriginalFilePath(error.FilePath);

            switch (errorSink.Add(error.FilePath is null ? null : config, error, level))
            {
                case ErrorSinkResult.Ok:
                    _builder.Add(error, level, originalPath);
                    break;

                case ErrorSinkResult.Exceed when error.FilePath != null && config != null:
                    var maxAllowed = level switch
                    {
                        ErrorLevel.Error => config.MaxFileErrors,
                        ErrorLevel.Warning => config.MaxFileWarnings,
                        ErrorLevel.Suggestion => config.MaxFileSuggestions,
                        ErrorLevel.Info => config.MaxFileInfos,
                        _ => 0,
                    };
                    _builder.Add(Errors.Logging.ExceedMaxFileErrors(maxAllowed, level, error.FilePath), ErrorLevel.Info, originalPath);
                    break;
            }
        }

        private Dictionary<string, CustomRule> MergeCustomRules(Config config, Dictionary<string, ValidationRules>? validationRules)
        {
            var customRules = new Dictionary<string, CustomRule>(config.CustomRules);

            if (validationRules == null)
            {
                return customRules;
            }

            foreach (var validationRule in validationRules.SelectMany(rules => rules.Value.Rules).Where(rule => rule.PullRequestOnly))
            {
                if (customRules.TryGetValue(validationRule.Code, out var customRule))
                {
                    customRules[validationRule.Code] = new CustomRule(
                            customRule.Severity,
                            customRule.Code,
                            customRule.AdditionalMessage,
                            customRule.CanonicalVersionOnly,
                            validationRule.PullRequestOnly);
                }
                else
                {
                    customRules.Add(validationRule.Code, new CustomRule(null, null, null, false, validationRule.PullRequestOnly));
                }
            }
            return customRules;
        }
    }
}
