// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// Represents the password change request for password change on the server.
    /// </summary>
    internal class PasswordChangeRequest
    {
        /// <summary>
        /// New password to be set.
        /// </summary>
        public string NewPassword { get; internal set; }

        /// <summary>
        /// New password in SecureString format.
        /// </summary>
        public SecureString NewSecurePassword { get; internal set; }

        /// <summary>
        /// Represents the user for which the password change is requested.
        /// </summary>
        public SqlCredential Credential { get; internal set; }
    }
}
