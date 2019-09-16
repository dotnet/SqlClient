// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// A callback to validate server certificate.
    /// </summary>
    /// <param name="certificate"></param>
    /// <returns></returns>
#if ADONET_CERT_AUTH
    public 
#else
    internal
#endif
    delegate bool ServerCertificateValidationCallback(X509Certificate2 certificate);

    /// <summary>
    /// A callback to provide client certificate on demand from a store normally different from system certificate store.
    /// </summary>
    /// <returns></returns>
#if ADONET_CERT_AUTH
    public 
#else
    internal
#endif
    delegate X509Certificate2 ClientCertificateRetrievalCallback();
}
