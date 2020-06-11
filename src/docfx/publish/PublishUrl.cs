// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Docs.Build
{
    internal class PublishUrl : IEquatable<PublishUrl>
    {
        private readonly string _hostName;
        private readonly string _locale;
        private readonly MonikerList _monikers;
        private readonly PathString _basePath;
        private readonly PathString _urlPath;

        public PublishUrl(Config config, string locale, PathString urlPath)
        {
            _hostName = config.HostName;
            _locale = config.locale
        }

        public bool Equals(PublishUrl? other)
        {
            if (other is null)
            {
                return false;
            }

            return
                _urlPath.Equals(other._urlPath) &&
                _hostName.Equals(other._hostName) &&
                _locale.Equals(other._locale) &&
                _monikers.Equals(other._monikers) &&
                _basePath.Equals(other._basePath);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PublishUrl);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_hostName, _locale, _monikers, _basePath, _urlPath);
        }
    }
}
