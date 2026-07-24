// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;

#nullable enable

namespace Microsoft.Data.SqlClient.TestUtilities
{
    public class Config
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            AllowTrailingCommas = true,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        public string? TCPConnectionString = null;
        public string? NPConnectionString = null;
        public string? TCPConnectionStringHGSVBS = null;
        public string? TCPConnectionStringNoneVBS = null;
        public string? TCPConnectionStringAASSGX = null;
        public bool EnclaveEnabled = false;
        public bool TracingEnabled = false;
        public string? AADServicePrincipalId = null;
        public string? AADServicePrincipalSecret = null;
        public string? AzureKeyVaultURL = null;
        public string? AzureKeyVaultTenantId = null;
        public bool SupportsIntegratedSecurity = false;
        public string? LocalDbAppName = null;
        public string? LocalDbSharedInstanceName = null;
        public string? FileStreamDirectory = null;
        public bool UseManagedSNIOnWindows = false;
        public string? DNSCachingConnString = null;
        public string? DNSCachingServerCR = null;  // this is for the control ring
        public string? DNSCachingServerTR = null;  // this is for the tenant ring
        public bool IsDNSCachingSupportedCR = false;  // this is for the control ring
        public bool IsDNSCachingSupportedTR = false;  // this is for the tenant ring
        public string? EnclaveAzureDatabaseConnString = null;
        public bool ManagedIdentitySupported = true;
        public string? UserManagedIdentityClientId = null;
        public string? PowerShellPath = null;
        public string? AliasName = null;
        public string? KerberosDomainPassword = null;
        public string? KerberosDomainUser = null;
        public bool IsManagedInstance = false;

        public static Config Load(string configPath)
        {
            Config config = LoadInternal(Environment.GetEnvironmentVariable("TEST_MDS_CONFIG")) ??
                            LoadInternal(configPath) ??
                            throw new FileNotFoundException("Could not find test configuration file.");

            // Allow environment variables to override individual config values.
            SetFromEnv("MDS_TCPConnectionString", ref config.TCPConnectionString);

            return config;
        }

        public static Config Load()
        {
            // Load config from environment variable first, jsonc file second, json file last.
            Config config = LoadInternal(Environment.GetEnvironmentVariable("TEST_MDS_CONFIG")) ??
                            LoadInternal("config.jsonc") ??
                            LoadInternal("config.json") ??
                            throw new FileNotFoundException("Could not find test configuration file.");

            // Allow environment variables to override individual config values.
            SetFromEnv("MDS_TCPConnectionString", ref config.TCPConnectionString);

            return config;
        }

        public static void UpdateConfig(Config updatedConfig, string configPath = @"config.jsonc")
        {
            string config = JsonSerializer.Serialize(updatedConfig, JsonSerializerOptions);
            File.WriteAllText(configPath, config);
        }

        private static Config? LoadInternal(string? configPath)
        {
            if (configPath is null)
            {
                return null;
            }

            try
            {
                using StreamReader sr = new StreamReader(configPath);
                return JsonSerializer.Deserialize<Config>(sr.ReadToEnd(), JsonSerializerOptions) ??
                       throw new InvalidOperationException($"Failed to deserialize config from '{configPath}'");
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                // File did not exist at the path given. We will try a different location.
                return null;
            }
        }

        private static void SetFromEnv(string envVar, ref string? configValue)
        {
            string? envValue = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(envValue))
            {
                configValue = envValue;
            }
        }
    }
}
