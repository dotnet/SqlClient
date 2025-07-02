// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public static class Loader
    {
        /// <summary>
        ///   Load a JSON config file into the given type.
        /// </summary>
        ///
        /// <typeparam name="T">
        ///   The type to deserialize the JSON into.
        /// </typeparam>
        /// 
        /// <param name="path">
        ///   The path to the JSON config file.
        /// </param>
        /// 
        /// <param name="envOverride">
        ///   An optional environment variable that, if set, will be used as
        ///   the config file path, ignoring path.
        /// </param>
        ///
        /// <returns>
        ///   The T instance populated from the JSON config file.
        /// </returns>
        /// 
        /// <exception cref="InvalidOperationException">
        ///   Thrown if the config file cannot be read or deserialized.
        /// </exception>
        ///
        public static T FromJsonFile<T>(
            string path,
            string envOverride = null)
            where T : class
        {
            string configFile =
              envOverride is null
              ? path
              : Environment.GetEnvironmentVariable(envOverride)
                ?? path;

            T config = null;
            Exception error = null;
            try
            {
                using var stream = File.OpenRead(configFile);
                config =
                    JsonSerializer.Deserialize<T>(
                        stream,
                        new JsonSerializerOptions
                        {
                            IncludeFields = true,
                            ReadCommentHandling = JsonCommentHandling.Skip
                        });
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (config is null || error is not null)
            {
                throw new InvalidOperationException(
                    $"Failed to load {typeof(T).Name} config from file=" +
                    $"{configFile}", error);
            }

            return config;
        }
    }
}
