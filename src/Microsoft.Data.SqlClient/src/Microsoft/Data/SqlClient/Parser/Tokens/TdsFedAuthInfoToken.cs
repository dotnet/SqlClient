// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient.Parser.Tokens;

/// <summary>
/// Represents a token used for Federated Authentication in TDS protocol communication.
/// This corresponds with the SQLFEDAUTHINFO token in the TDS spec.
/// </summary>
/// <remarks>
/// The <c>TdsFedAuthInfoToken</c> encapsulates information required for Federated Authentication
/// when establishing a connection with a SQL Server. It contains the Service Principal Name (SPN)
/// and the Security Token Service (STS) URL required for obtaining authentication tokens.
/// </remarks>
internal sealed class TdsFedAuthInfoToken
{
    /// <summary>
    /// Represents a token used for Federated Authentication in the TDS (Tabular Data Stream) protocol.
    /// </summary>
    /// <remarks>
    /// The TdsFedAuthInfoToken is responsible for encapsulating the Service Principal Name (SPN)
    /// and Security Token Service (STS) URL, both of which are essential for Federated Authentication.
    /// This token is used when negotiating secure connections with SQL Server instances.
    /// </remarks>
    internal TdsFedAuthInfoToken(string spn, string stsUrl)
    {
        Spn = spn;
        StsUrl = stsUrl;
    }

    /// <summary>
    /// Gets the Service Principal Name (SPN) associated with the federated authentication token.
    /// The SPN is used in authentication processes to uniquely identify a service instance
    /// in the context of Kerberos-based security mechanisms.
    /// </summary>
    internal string Spn { get; }

    /// <summary>
    /// Gets the Security Token Service (STS) URL associated with the federated authentication token.
    /// The STS URL is used to identify the authentication service endpoint responsible
    /// for issuing security tokens in the federated authentication process.
    /// </summary>
    internal string StsUrl { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"SPN: {Spn}, STSURL: {StsUrl}";
    }
}
