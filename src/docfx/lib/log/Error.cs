// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Docs.Build
{
    internal record Error
    {
        private const int MaxMessageArgumentLength = 100;

        public ErrorLevel Level { get; init; }

        public string Code { get; init; }

        public string Message { get; init; }

        public string? PropertyPath { get; init; }

        public SourceInfo? Source { get; init; }

        public PathString? OriginalPath { get; init; }

        public bool PullRequestOnly { get; init; }

        public string? DocumentUrl { get; init; }

        public object?[] MessageArguments { get; init; } = Array.Empty<object?>();

        public AdditionalErrorInfo? AdditionalErrorInfo { get; init; }

        private Error(ErrorLevel level, string code, string message, SourceInfo? source)
        {
            Level = level;
            Code = code;
            Message = message;
            Source = source;
        }

        public Error(ErrorLevel level, string code, FormattableString message, SourceInfo? source = null, string? propertyPath = null)
        {
            Level = level;
            Code = code;
            Message = string.Format(message.Format, Array.ConvertAll(message.GetArguments(), LimitLength));
            MessageArguments = message.GetArguments();
            Source = source;
            PropertyPath = propertyPath;

            static string? LimitLength(object? arg)
            {
                var str = arg?.ToString();
                if (str is null || str.Length <= MaxMessageArgumentLength)
                {
                    return str;
                }
                return str.Substring(0, MaxMessageArgumentLength) + "...";
            }
        }

        public static Error CreateFromExisting(ErrorLevel level, string code, string message, SourceInfo? source)
        {
            return new Error(level, code, message, source);
        }

        public override string ToString()
        {
            var file = OriginalPath ?? Source?.File?.Path;
            var source = OriginalPath is null ? Source : null;
            var line = source?.Line ?? 0;
            var end_line = source?.EndLine ?? 0;
            var column = source?.Column ?? 0;
            var end_column = source?.EndColumn ?? 0;

            return JsonUtility.Serialize(new
            {
                message_severity = Level,
                Code,
                message = Message,
                file,
                line,
                end_line,
                column,
                end_column,
                log_item_type = "user",
                pull_request_only = PullRequestOnly ? (bool?)true : null,
                property_path = PropertyPath,
                ms_author = AdditionalErrorInfo?.MsAuthor,
                ms_prod = AdditionalErrorInfo?.MsProd,
                ms_technology = AdditionalErrorInfo?.MsTechnology,
                ms_service = AdditionalErrorInfo?.MsService,
                ms_subservice = AdditionalErrorInfo?.MsSubservice,
                document_url = DocumentUrl,
                date_time = DateTime.UtcNow, // Leave data_time as the last field to make regression test stable
            }).Replace("\"ms_", "\"ms.");
        }

        public DocfxException ToException(Exception? innerException = null, bool isError = true)
        {
            var error = isError && Level != ErrorLevel.Error ? this with { Level = ErrorLevel.Error } : this;
            return new DocfxException(error, innerException);
        }
    }
}
