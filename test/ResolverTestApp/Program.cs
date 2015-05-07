﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;

namespace ResolverTestApp
{
    public class Program
    {
        [ImportMany]
        public Lazy<INuGetResourceProvider>[] ResourceProviders;

        private static void Main(string[] args)
        {
            // Import Dependencies  
            var p = new Program();

            // hold onto the container, otherwise the lazy objects will be disposed!
            var container = p.Initialize();

            // Json.NET is already installed
            var installed = new List<PackageReference>();
            installed.Add(new PackageReference(new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("6.0.5")), NuGetFramework.Parse("portable-net40+win8")));

            // build the repo provider instead of importing it so that it has only v3
            var repositoryProvider = new SourceRepositoryProvider(new V3OnlyPackageSourceProvider(), p.ResourceProviders.ToArray());

            // package to install
            var target = new PackageIdentity("WindowsAzure.Storage", NuGetVersion.Parse("4.0.1"));

            // project target framework
            var framework = NuGetFramework.Parse("net451");

            // build repos
            var repos = repositoryProvider.GetRepositories();
            var timer = new Stopwatch();

            // get a distinct set of packages from all repos
            var packages = new HashSet<PackageDependencyInfo>(PackageIdentity.Comparer);

            // find all needed packages from online
            foreach (var repo in repos)
            {
                // get the resolver data resource
                var depInfo = repo.GetResource<DependencyInfoResource>();

                // resources can always be null
                if (depInfo != null)
                {
                    timer.Restart();

                    var task = depInfo.ResolvePackage(target, framework, CancellationToken.None);
                    task.Wait();

                    packages.Add(task.Result);

                    timer.Stop();
                    Console.WriteLine("Online fetch time: " + timer.Elapsed);
                }
            }

            timer.Restart();

            // find the best set to install
            var resolver = new PackageResolver(DependencyBehavior.Lowest);
            var toInstall = resolver.Resolve(new[] { target }, packages, installed, CancellationToken.None);

            timer.Stop();
            Console.WriteLine("Resolve time: " + timer.Elapsed);
            Console.WriteLine("------------------------");

            foreach (var pkg in toInstall)
            {
                Console.WriteLine(pkg.Id + " " + pkg.Version.ToNormalizedString());
            }
        }

        private CompositionContainer Initialize()
        {
            var assemblyName = Assembly.GetEntryAssembly().FullName;

            using (var catalog = new AggregateCatalog(new AssemblyCatalog(Assembly.GetExecutingAssembly().Location),
                new AssemblyCatalog(Assembly.Load(assemblyName)),
                new DirectoryCatalog(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "*.dll")))
            {
                var container = new CompositionContainer(catalog);
                container.ComposeParts(this);
                return container;
            }
        }

        /// <summary>
        /// Provider that only returns V3 as a source
        /// </summary>
        private class V3OnlyPackageSourceProvider : IPackageSourceProvider
        {
            public void DisablePackageSource(PackageSource source)
            {
                throw new NotImplementedException();
            }

            public bool IsPackageSourceEnabled(PackageSource source)
            {
                return true;
            }

            public IEnumerable<PackageSource> LoadPackageSources()
            {
                return new List<PackageSource> { new PackageSource("https://az320820.vo.msecnd.net/ver3-preview/index.json", "v3") };
            }

#pragma warning disable 0067
            public event EventHandler PackageSourcesChanged;
#pragma warning restore 0067

            public void SavePackageSources(IEnumerable<PackageSource> sources)
            {
                throw new NotImplementedException();
            }

            public string ActivePackageSourceName
            {
                get { throw new NotImplementedException(); }
            }

            public void SaveActivePackageSource(PackageSource source)
            {
                throw new NotImplementedException();
            }
        }
    }
}
