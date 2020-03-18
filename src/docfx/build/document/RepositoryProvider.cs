// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.IO;

namespace Microsoft.Docs.Build
{
    internal class RepositoryProvider
    {
        private readonly string _docsetPath;
        private readonly PathString? _docsetPathToDefaultRepository;
        private readonly PackageResolver? _packageResolver;
        private readonly Config? _config;
        private readonly LocalizationProvider? _localizationProvider;
        private readonly ConcurrentDictionary<string, Repository?> _repositores = new ConcurrentDictionary<string, Repository?>(PathUtility.PathComparer);
        private readonly ConcurrentDictionary<PathString, Repository?> _dependencyRepositories = new ConcurrentDictionary<PathString, Repository?>();

        public Repository? DefaultRepository { get; }

        public RepositoryProvider(
            string docsetPath,
            Repository? repository,
            Config? config = null,
            PackageResolver? packageResolver = null,
            LocalizationProvider? localizationProvider = null)
        {
            _docsetPath = docsetPath;
            DefaultRepository = repository;
            _packageResolver = packageResolver;
            _config = config;
            _localizationProvider = localizationProvider;

            if (DefaultRepository != null)
            {
                _docsetPathToDefaultRepository = new PathString(Path.GetRelativePath(DefaultRepository.Path, _docsetPath));
            }
        }

        public Repository? GetRepository(FileOrigin origin, PathString? dependencyName = null)
        {
            return origin switch
            {
                FileOrigin.Default => DefaultRepository,
                FileOrigin.Fallback when _localizationProvider != null => _localizationProvider.FallbackRepository,
                FileOrigin.Dependency when _config != null && _packageResolver != null && dependencyName != null
                    => _dependencyRepositories.GetOrAdd(dependencyName.Value, key => GetDependencyRepository(key, _config, _packageResolver)),
                _ => throw new InvalidOperationException(),
            };
        }

        public (Repository? repository, PathString? pathToRepository) GetRepository(FilePath path)
        {
            return path.Origin switch
            {
                FileOrigin.Default => GetRepository(Path.Combine(_docsetPath, path.Path)),
                FileOrigin.Fallback when _localizationProvider != null && _docsetPathToDefaultRepository != null
                    => (_localizationProvider.FallbackRepository, _docsetPathToDefaultRepository.Value.Concat(path.Path)),
                FileOrigin.Dependency => (GetRepository(path.Origin, path.DependencyName), path.GetPathToOrigin()),
                _ => throw new InvalidOperationException(),
            };
        }

        private (Repository? repository, PathString? pathToRepository) GetRepository(string fullPath)
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (directory is null)
            {
                return default;
            }

            var repository = _repositores.GetOrAdd(directory, GetRepositoryCore);
            if (repository is null)
            {
                return default;
            }

            return (repository, new PathString(Path.GetRelativePath(repository.Path, fullPath)));
        }

        private Repository? GetRepositoryCore(string directory)
        {
            var repoPath = GitUtility.FindRepository(directory);
            if (repoPath is null)
            {
                return null;
            }

            if (repoPath == DefaultRepository?.Path)
            {
                return DefaultRepository;
            }

            return Repository.Create(repoPath);
        }

        private Repository? GetDependencyRepository(PathString dependencyName, Config config, PackageResolver packageResolver)
        {
            var dependency = config.Dependencies[dependencyName];
            var dependencyPath = packageResolver.ResolvePackage(dependency, dependency.PackageFetchOptions);

            return dependency.Type != PackageType.Git ? null : Repository.Create(dependencyPath, dependency.Branch, dependency.Url);
        }
    }
}
