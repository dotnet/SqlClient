// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Data.SqlClient;

internal static class MiddlewareRegistration
{
    // Register once during application startup, before opening any connections.
    internal static void Register()
    {
        SqlConnection.RegisterSqlClientAgent(SqlClientAgent.EntityFramework);
    }
}
