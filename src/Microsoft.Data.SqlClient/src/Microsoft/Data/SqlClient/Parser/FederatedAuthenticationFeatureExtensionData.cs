// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Parser;

/// <summary>
/// Class encapsulating the data to be sent to the server as part of Federated Authentication
/// Feature Extension.
/// </summary>
internal class FederatedAuthenticationFeatureExtensionData
{
    internal TdsEnums.FedAuthLibrary libraryType;
    internal bool fedAuthRequiredPreLoginResponse;
    internal SqlAuthenticationMethod authentication;
    internal byte[] accessToken;
}
