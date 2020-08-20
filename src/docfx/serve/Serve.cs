// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Docs.Build
{
    internal static class Serve
    {
        public static bool Run(string workingDirectory, CommandLineOptions options)
        {
            if (Build.Run(workingDirectory, options))
            {
                return true;
            }

            RunHttpServer(workingDirectory, options);
            return false;
        }

        private static void RunHttpServer(string workingDirectory, CommandLineOptions options)
        {
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(builder =>
                    builder
                        .UseEnvironment(Environments.Production)
                        .Configure(Configure))
                .RunConsoleAsync().GetAwaiter().GetResult();

            void Configure(IApplicationBuilder app)
            {
                // Theme files
                app.UseFileServer(new FileServerOptions
                {
                    FileProvider = new PhysicalFileProvider(Path.Combine(workingDirectory, options.Output ?? "_site")),
                    RequestPath = "/",
                    EnableDirectoryBrowsing = options.List,
                });

                // Content files
                app.UseFileServer(new FileServerOptions
                {
                    FileProvider = new PhysicalFileProvider(Path.Combine(workingDirectory, options.Output ?? "_site")),
                    RequestPath = "/",
                    EnableDirectoryBrowsing = options.List,
                });
            }
        }
    }
}
