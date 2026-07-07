// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;

namespace Microsoft.Data.SqlClient.OnlineTests.Utilities;

public class ConnectionMetadata
{
    private static int s_configuredConnections = 0;

    private readonly HashSet<string> _cachedTypeNames;
    private readonly Dictionary<ConnectionTraits, Lazy<bool>> _cachedConnectionTraits;

    public ConnectionMetadata(string? connectionString, string? name)
    {
        ConnectionString = connectionString ??
                           throw new ArgumentNullException("Config contains null connection string.");

        int connectionNumber = Interlocked.Increment(ref s_configuredConnections);
        Name = name ?? $"Connection {connectionNumber}";

        // Verify that the connection is usable by caching the metadata
        using SqlConnection connection = new(connectionString);
        connection.Open();

        _cachedTypeNames = ReadTypeNames(connection);

        // Initialize trait handlers
        _cachedConnectionTraits = new Dictionary<ConnectionTraits, Lazy<bool>>
        {
            { ConnectionTraits.SupportsJson, new Lazy<bool>(() => _cachedTypeNames.Contains("json")) },
            { ConnectionTraits.SupportsVector, new Lazy<bool>(() => _cachedTypeNames.Contains("vector")) }
        };
    }

    #region Properties

    public string ConnectionString { get; private set; }

    public string Name { get; private set; }

    #endregion

    #region Methods

    public bool HasTrait(ConnectionTraits trait) =>
        _cachedConnectionTraits.TryGetValue(trait, out Lazy<bool>? lazyTraitValue)
            ? lazyTraitValue.Value
            : false;

    private static HashSet<string> ReadTypeNames(SqlConnection connection)
    {
        using SqlCommand command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = "SELECT [name] FROM SYS.TYPES";

        using SqlDataReader reader = command.ExecuteReader();

        HashSet<string> typeNames = new();
        while (reader.Read())
        {
            typeNames.Add(reader.GetString(0));
        }

        return typeNames;
    }

    #endregion
}
