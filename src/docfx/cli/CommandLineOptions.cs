// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace Microsoft.Docs.Build
{
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1401:FieldsMustBePrivate", Justification = "<Skipping>")]
    internal class CommandLineOptions
    {
        public string? Output;
        public string? Log;
        public bool Legacy;
        public bool Verbose;
        public bool DryRun;
        public bool Stdin;
        public bool NoCache;
        public bool NoRestore;
        public string? Template;

        public int Port = 8080;
        public string Address = "0.0.0.0";
        public bool List;

        public JObject? StdinConfig;

        public JObject ToJObject()
        {
            var config = new JObject
            {
                ["legacy"] = Legacy,
                ["dryRun"] = DryRun,
            };

            if (Output != null)
            {
                config["outputPath"] = Output;
            }

            if (Legacy)
            {
                config["outputType"] = "Json";
                config["outputUrlType"] = "Docs";
                config["copyResources"] = false;
            }

            if (Template != null)
            {
                config["template"] = Template;
            }

            return config;
        }
    }
}
