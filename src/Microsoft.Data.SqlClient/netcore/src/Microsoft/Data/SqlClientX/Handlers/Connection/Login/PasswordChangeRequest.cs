// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;

namespace Microsoft.Data.SqlClient.Microsoft.Data.SqlClientX.Handlers.Connection.LoginSubHandlers
{
    internal class PasswordChangeRequest
    {
        public string NewPassword { get; internal set; }
        public SqlCredential Credential { get; internal set; }
        public SecureString NewSecurePassword { get; internal set; }
    }
}
