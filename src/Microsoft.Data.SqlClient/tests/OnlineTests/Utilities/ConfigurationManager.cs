// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Microsoft.Data.SqlClient.OnlineTests.Utilities;

public static class ConfigurationManager
{
    #region Constants

    public const string ConfigPathEnvironmentVariable = "MDS_ONLINE_TEST_CONFIG";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    #endregion

    #region Properties

    public static Configuration? Configuration { get; private set; }

    #endregion

    #region Methods

    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void Initialize()
    {
        // Skip initialization if we're already initialized
        if (Configuration is not null)
        {
            return;
        }

        // Deserialize the configuration and cache metadata about the server
        DeserializedConfig config = LoadConfig();

    }

    private static DeserializedConfig LoadConfig()
    {
        // Determine list of paths to try:
        List<string?> configPaths = [
            Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable),
            "config.jsonc",
            "config.json"
        ];

        // Go down the list to load the config
        foreach (string? config in configPaths)
        {
            DeserializedConfig? deserializedConfig = LoadConfigInternal(config);
            if (deserializedConfig is not null)
            {
                return deserializedConfig;
            }
        }

        // Made to the end without loading anything. Throw.
        IEnumerable<string> expandedPaths = configPaths.OfType<string>().Select(Path.GetFullPath);
        string triedPaths = string.Join(", ", expandedPaths);

        throw new FileNotFoundException($"Failed to load configuration from: {triedPaths}");
    }

    private static DeserializedConfig? LoadConfigInternal(string? configPath)
    {
        if (configPath is null)
        {
            return null;
        }

        try
        {
            using StreamReader sr = new StreamReader(configPath);
            return JsonSerializer.Deserialize<DeserializedConfig>(sr.ReadToEnd(), JsonSerializerOptions)
                   ?? throw new InvalidOperationException($"Failed to deserialize config from '{configPath}");
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            // File did not exist at the give path. Return null to signal to try a different location.
            return null;
        }
    }

    #endregion

    public class DeserializedConfig
    {
        public string[] ConnectionStrings { get; set; }
    }
}
