// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions;

internal class EnclaveSessionParameters
{
    internal string ServerName { get; set; }  // The name of the SQL Server instance containing the enclave.
    internal string AttestationUrl { get; set; }  // The endpoint of an attestation service for attesting the enclave.
    internal string Database { get; set; }  //  The database that SqlClient contacts to request an enclave session.

    internal EnclaveSessionParameters(string serverName, string attestationUrl, string database)
    {
        ServerName = serverName;
        AttestationUrl = attestationUrl;
        Database = database;
    }
}
