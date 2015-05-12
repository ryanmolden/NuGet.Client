﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    internal class PackageLoader : ILoader
    {
        private readonly SourceRepository _sourceRepository;

        private readonly NuGetProject[] _projects;

        // The list of all installed packages. This variable is used for the package status calculation.
        private readonly HashSet<PackageIdentity> _installedPackages;
        private readonly HashSet<string> _installedPackageIds;

        private readonly NuGetPackageManager _packageManager;

        private readonly PackageLoaderOption _option;

        private readonly string _searchText;

        // The list of packages that have updates available
        private List<UISearchMetadata> _packagesWithUpdates;

        public PackageLoader(PackageLoaderOption option,
            NuGetPackageManager packageManager,
            IEnumerable<NuGetProject> projects,
            SourceRepository sourceRepository,
            string searchText)
        {
            _sourceRepository = sourceRepository;
            _packageManager = packageManager;
            _projects = projects.ToArray();
            _option = option;
            _searchText = searchText;

            LoadingMessage = string.IsNullOrWhiteSpace(searchText) ?
                Resources.Text_Loading :
                string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.Text_Searching,
                    searchText);

            _installedPackages = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
            _installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public string LoadingMessage { get; }

        private async Task<SearchResult> SearchAsync(int startIndex, CancellationToken ct)
        {
            if (_sourceRepository == null)
            {
                return SearchResult.Empty;
            }

            if (_option.Filter == Filter.Installed)
            {
                // show only the installed packages
                return await SearchInstalledAsync(startIndex, ct);
            }
            if (_option.Filter == Filter.UpdatesAvailable)
            {
                return await SearchUpdatesAsync(startIndex, ct);
            }
            // normal search
            var searchResource = await _sourceRepository.GetResourceAsync<UISearchResource>();

            // search in source
            if (searchResource == null)
            {
                return SearchResult.Empty;
            }
            else
            {
                var searchFilter = new SearchFilter();
                searchFilter.IncludePrerelease = _option.IncludePrerelease;
                searchFilter.SupportedFrameworks = GetSupportedFrameworks();

                var searchResults = await searchResource.Search(
                    _searchText,
                    searchFilter,
                    startIndex,
                    _option.PageSize + 1,
                    ct);

                var items = searchResults.ToList();

                var hasMoreItems = items.Count > _option.PageSize;

                if (hasMoreItems)
                {
                    items.RemoveAt(items.Count - 1);
                }

                return new SearchResult
                {
                    Items = items,
                    HasMoreItems = hasMoreItems,
                };
            }
        }

        // Returns the list of frameworks that we need to pass to the server during search
        private IEnumerable<string> GetSupportedFrameworks()
        {
            var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in _projects)
            {
                NuGetFramework framework;
                if (project.TryGetMetadata(NuGetProjectMetadataKeys.TargetFramework,
                    out framework))
                {
                    if (framework != null
                        && framework.IsAny)
                    {
                        // One of the project's target framework is AnyFramework. In this case, 
                        // we don't need to pass the framework filter to the server.
                        return Enumerable.Empty<string>();
                    }

                    if (framework != null
                        && framework.IsSpecificFramework)
                    {
                        frameworks.Add(framework.DotNetFrameworkName);
                    }
                }
                else
                {
                    // we also need to process SupportedFrameworks
                    IEnumerable<NuGetFramework> supportedFrameworks;
                    if (project.TryGetMetadata(
                        NuGetProjectMetadataKeys.SupportedFrameworks,
                        out supportedFrameworks))
                    {
                        foreach (var f in supportedFrameworks)
                        {
                            if (f.IsAny)
                            {
                                return Enumerable.Empty<string>();
                            }

                            frameworks.Add(f.DotNetFrameworkName);
                        }
                    }
                }
            }

            return frameworks;
        }

        /// <summary>
        /// Returns the grouped list of installed packages.
        /// </summary>
        /// <param name="latest">
        /// If true, the latest version is returned. Otherwise, the oldest
        /// version is returned.
        /// </param>
        /// <returns></returns>
        private async Task<IEnumerable<PackageIdentity>> GetInstalledPackagesAsync(bool latest, CancellationToken token)
        {
            var installedPackages = new Dictionary<string, PackageIdentity>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var project in _projects)
            {
                foreach (var package in (await project.GetInstalledPackagesAsync(token)))
                {
                    if (!(project is INuGetIntegratedProject)
                        &&
                        !_packageManager.PackageExistsInPackagesFolder(package.PackageIdentity))
                    {
                        continue;
                    }

                    PackageIdentity p;
                    if (installedPackages.TryGetValue(package.PackageIdentity.Id, out p))
                    {
                        if (latest)
                        {
                            if (p.Version < package.PackageIdentity.Version)
                            {
                                installedPackages[package.PackageIdentity.Id] = package.PackageIdentity;
                            }
                        }
                        else
                        {
                            if (p.Version > package.PackageIdentity.Version)
                            {
                                installedPackages[package.PackageIdentity.Id] = package.PackageIdentity;
                            }
                        }
                    }
                    else
                    {
                        installedPackages[package.PackageIdentity.Id] = package.PackageIdentity;
                    }
                }
            }

            return installedPackages.Values;
        }

        private async Task<SearchResult> SearchInstalledAsync(int startIndex, CancellationToken cancellationToken)
        {
            var installedPackages = (await GetInstalledPackagesAsync(latest: true, token: cancellationToken))
                .Where(p => p.Id.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) != -1)
                .OrderBy(p => p.Id)
                .Skip(startIndex)
                .Take(_option.PageSize + 1)
                .ToArray();

            var results = new List<UISearchMetadata>();
            var localResource = await _packageManager.PackagesFolderSourceRepository
                .GetResourceAsync<UIMetadataResource>();

            var metadataResource = await _sourceRepository.GetResourceAsync<UIMetadataResource>();
            var tasks = new List<Task<UISearchMetadata>>();

            for (int i = 0; i < installedPackages.Length; i++)
            {
                var packageIdentity = installedPackages[i];

                tasks.Add(
                    Task.Run(() =>
                        GetPackageMetadataAsync(cancellationToken,
                                                localResource,
                                                metadataResource,
                                                packageIdentity)));
            }

            await Task.WhenAll(tasks);

            foreach (var task in tasks)
            {
                results.Add(task.Result);
            }

            return new SearchResult
            {
                Items = results,
                HasMoreItems = installedPackages.Length > _option.PageSize,
            };
        }

        private async Task<UISearchMetadata> GetPackageMetadataAsync(CancellationToken ct,
                                                               UIMetadataResource localResource,
                                                               UIMetadataResource metadataResource,
                                                               PackageIdentity identity)
        {
            UIPackageMetadata packageMetadata = null;
            if (localResource != null)
            {
                // try get metadata from local resource
                var localMetadata = await localResource.GetMetadata(identity.Id,
                includePrerelease: true,
                includeUnlisted: true,
                token: ct);
                packageMetadata = localMetadata.FirstOrDefault(p => p.Identity.Version == identity.Version);
            }

            var metadata = await metadataResource.GetMetadata(
                identity.Id,
                _option.IncludePrerelease,
                false,
                ct);

            if (packageMetadata == null)
            {
                // package metadata can't be found in local resource. Try find it in remote resource.
                packageMetadata = metadata.FirstOrDefault(p => p.Identity.Version == identity.Version);
            }

            string summary = string.Empty;
            string title = identity.Id;
            if (packageMetadata != null)
            {
                summary = packageMetadata.Summary;
                if (String.IsNullOrEmpty(summary))
                {
                    summary = packageMetadata.Description;
                }
                if (!string.IsNullOrEmpty(packageMetadata.Title))
                {
                    title = packageMetadata.Title;
                }
            }

            var versions = metadata.OrderByDescending(m => m.Identity.Version)
                .Select(m => new VersionInfo(m.Identity.Version, m.DownloadCount));

            return new UISearchMetadata(
                identity,
                title: title,
                summary: summary,
                iconUrl: packageMetadata == null ? null : packageMetadata.IconUrl,
                versions: versions,
                latestPackageMetadata: packageMetadata);
        }

        // Search in installed packages that have updates available
        private async Task<SearchResult> SearchUpdatesAsync(int startIndex, CancellationToken ct)
        {
            if (_packagesWithUpdates == null)
            {
                await CreatePackagesWithUpdatesAsync(ct);
            }

            var items = _packagesWithUpdates.Skip(startIndex).Take(_option.PageSize + 1).ToList();

            var hasMoreItems = items.Count > _option.PageSize + startIndex;

            if (hasMoreItems)
            {
                items.RemoveAt(items.Count - 1);
            }

            return new SearchResult
            {
                Items = items,
                HasMoreItems = hasMoreItems,
            };
        }

        // Creates the list of installed packages that have updates available
        private async Task CreatePackagesWithUpdatesAsync(CancellationToken ct)
        {
            _packagesWithUpdates = new List<UISearchMetadata>();
            var metadataResource = await _sourceRepository.GetResourceAsync<UIMetadataResource>();

            if (metadataResource == null)
            {
                return;
            }

            var installedPackages = (await GetInstalledPackagesAsync(latest: false, token: ct))
                .Where(p => p.Id.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) != -1)
                .OrderBy(p => p.Id);
            foreach (var package in installedPackages)
            {
                // only release packages respect the prerel option
                var includePre = _option.IncludePrerelease || package.Version.IsPrerelease;

                var data = await metadataResource.GetMetadata(package.Id, includePre, false, ct);
                var highest = data.OrderByDescending(e => e.Identity.Version, VersionComparer.VersionRelease).FirstOrDefault();

                if (highest != null)
                {
                    if (VersionComparer.VersionRelease.Compare(package.Version, highest.Identity.Version) < 0)
                    {
                        var allVersions = data
                            .OrderByDescending(e => e.Identity.Version, VersionComparer.VersionRelease)
                            .Select(e => new VersionInfo(e.Identity.Version, e.DownloadCount));
                        var summary = string.IsNullOrEmpty(highest.Summary) ? highest.Description : highest.Summary;
                        var title = string.IsNullOrEmpty(highest.Title) ? highest.Identity.Id : highest.Title;

                        _packagesWithUpdates.Add(new UISearchMetadata(highest.Identity, title, summary, highest.IconUrl, allVersions, highest));
                    }
                }
            }
        }

        public async Task<LoadResult> LoadItemsAsync(int startIndex, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadBegin);

            List<SearchResultPackageMetadata> packages = new List<SearchResultPackageMetadata>();

            var results = await SearchAsync(startIndex, ct);

            int resultCount = 0;

            foreach (var package in results.Items)
            {
                ct.ThrowIfCancellationRequested();
                resultCount++;

                var searchResultPackage = new SearchResultPackageMetadata(_sourceRepository);
                searchResultPackage.Id = package.Identity.Id;
                searchResultPackage.Version = package.Identity.Version;
                searchResultPackage.IconUrl = package.IconUrl;

                // get other versions
                var versionList = package.Versions.ToList();
                if (!_option.IncludePrerelease)
                {
                    // remove prerelease version if includePrelease is false
                    versionList.RemoveAll(v => v.Version.IsPrerelease);
                }

                if (!versionList.Select(v => v.Version).Contains(searchResultPackage.Version))
                {
                    versionList.Add(new VersionInfo(searchResultPackage.Version, downloadCount: null));
                }

                searchResultPackage.Versions = versionList;
                searchResultPackage.Status = CalculatePackageStatus(searchResultPackage);

                // filter out prerelease version when needed.
                if (searchResultPackage.Version.IsPrerelease &&
                    !_option.IncludePrerelease &&
                    searchResultPackage.Status == PackageStatus.NotInstalled)
                {
                    continue;
                }

                if (_option.Filter == Filter.UpdatesAvailable &&
                    searchResultPackage.Status != PackageStatus.UpdateAvailable)
                {
                    continue;
                }

                searchResultPackage.Summary = package.Summary;
                packages.Add(searchResultPackage);
            }

            ct.ThrowIfCancellationRequested();
            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadEnd);
            return new LoadResult()
            {
                Items = packages,
                HasMoreItems = results.HasMoreItems,
                NextStartIndex = startIndex + resultCount
            };
        }

        // Returns the package status for the searchPackageResult
        private PackageStatus CalculatePackageStatus(SearchResultPackageMetadata searchPackageResult)
        {
            if (_installedPackageIds.Contains(searchPackageResult.Id))
            {
                var highestAvailableVersion = searchPackageResult.Versions
                    .Select(v => v.Version)
                    .Max();

                var highestInstalled = _installedPackages
                    .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, searchPackageResult.Id))
                    .OrderByDescending(p => p.Version, VersionComparer.Default)
                    .First();

                if (VersionComparer.VersionRelease.Compare(highestInstalled.Version, highestAvailableVersion) < 0)
                {
                    return PackageStatus.UpdateAvailable;
                }

                return PackageStatus.Installed;
            }

            return PackageStatus.NotInstalled;
        }

        public async Task InitializeAsync()
        {
            // create _installedPackages and _installedPackageIds
            foreach (var project in _projects)
            {
                var installedPackagesInProject = await project.GetInstalledPackagesAsync(CancellationToken.None);
                if (!(project is INuGetIntegratedProject))
                {
                    installedPackagesInProject = installedPackagesInProject.Where(
                        p =>
                            _packageManager.PackageExistsInPackagesFolder(p.PackageIdentity));
                }

                foreach (var package in installedPackagesInProject)
                {
                    _installedPackages.Add(package.PackageIdentity);
                    _installedPackageIds.Add(package.PackageIdentity.Id);
                }
            }
        }
    }
}
