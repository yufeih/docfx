// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Microsoft.Docs.Build
{
    internal class LocalizationProvider
    {
        /// <summary>
        /// Gets the lower-case culture name computed from <see cref="CommandLineOptions.Locale" or <see cref="Config.DefaultLocale"/>/>
        /// </summary>
        public string Locale { get; }

        public CultureInfo Culture { get; }

        public Repository? FallbackRepository { get; }

        public bool IsLocalizationBuild { get; }

        public bool EnableSideBySide { get; }

        public LocalizationProvider(PackageResolver packageResolver, Config config, string? locale, string docsetPath, Repository? repository)
        {
            Locale = !string.IsNullOrEmpty(locale) ? locale.ToLowerInvariant() : config.DefaultLocale;
            Culture = CreateCultureInfo(Locale);

            if (!string.IsNullOrEmpty(locale) && !string.Equals(locale, config.DefaultLocale))
            {
                IsLocalizationBuild = true;
            }

            EnableSideBySide = repository != null &&
                LocalizationUtility.TryGetContributionBranch(repository.Branch, out var contributionBranch) &&
                contributionBranch != repository.Branch;

            FallbackRepository = repository is null ? null : GetFallbackRepository(repository, packageResolver);
        }

        private static Repository? GetFallbackRepository(Repository repository, PackageResolver packageResolver)
        {
            if (LocalizationUtility.TryGetFallbackRepository(repository.Remote, repository.Branch, out var fallbackRemote, out var fallbackBranch))
            {
                foreach (var branch in new[] { fallbackBranch, "master" })
                {
                    if (packageResolver.TryResolvePackage(
                        new PackagePath(fallbackRemote, branch), PackageFetchOptions.None, out var fallbackRepoPath))
                    {
                        return Repository.Create(fallbackRepoPath, branch, fallbackRemote);
                    }
                }
            }
            return default;
        }

        private CultureInfo CreateCultureInfo(string locale)
        {
            try
            {
                return new CultureInfo(locale);
            }
            catch (CultureNotFoundException)
            {
                throw Errors.Config.LocaleInvalid(locale).ToException();
            }
        }
    }
}
