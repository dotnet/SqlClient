// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

internal static class SqlConnectionClientCertificateAuthentication
{
    // <ClientCertificateAuthentication>
    internal static async Task OpenLoopbackConnectionAsync()
    {
        string dataSource = Environment.GetEnvironmentVariable("SQLCLIENT_LOOPBACK_DATA_SOURCE")
            ?? throw new InvalidOperationException("Set SQLCLIENT_LOOPBACK_DATA_SOURCE.");
        string clientCertificate = Environment.GetEnvironmentVariable("SQLCLIENT_CLIENT_CERTIFICATE")
            ?? throw new InvalidOperationException("Set SQLCLIENT_CLIENT_CERTIFICATE.");
        string clientKey = Environment.GetEnvironmentVariable("SQLCLIENT_CLIENT_KEY") ?? string.Empty;
        string clientKeyPassword = Environment.GetEnvironmentVariable("SQLCLIENT_CLIENT_KEY_PASSWORD") ?? string.Empty;

        SqlConnectionStringBuilder builder = new()
        {
            DataSource = dataSource,
            InitialCatalog = "master",
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            ClientCertificate = clientCertificate,
            ClientKey = clientKey,
            ClientKeyPassword = clientKeyPassword,
        };

        using SqlConnection connection = new(builder.ConnectionString);
        await connection.OpenAsync();
    }
    // </ClientCertificateAuthentication>
}
