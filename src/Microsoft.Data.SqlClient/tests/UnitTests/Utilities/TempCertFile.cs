// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities;

/// <summary>
/// Writes a temporary public certificate for ServerCertificate pinning and deletes it on disposal.
/// </summary>
internal sealed class TempCertFile : IDisposable
{
    public string Path { get; }

    /// <summary>
    /// Exports only the public certificate, leaving the private key in memory.
    /// </summary>
    /// <param name="cert">The certificate to pin.</param>
    public TempCertFile(X509Certificate2 cert)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "SqlClient_Pin_" + Guid.NewGuid().ToString("N") + ".cer");
        File.WriteAllBytes(Path, cert.Export(X509ContentType.Cert));
    }

    /// <summary>
    /// Removes the temporary pin; cleanup errors remain visible to the test runner.
    /// </summary>
    public void Dispose() => File.Delete(Path);
}

#endif
