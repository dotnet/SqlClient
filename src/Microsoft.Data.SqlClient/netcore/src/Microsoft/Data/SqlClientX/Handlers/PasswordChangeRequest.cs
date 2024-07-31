// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Handlers
{
    internal class PasswordChangeRequest
    {
        public string NewPassword { get; internal set; }

        public SqlCredential Credential { get; internal set; }

        public SecureString NewSecurePassword { get; internal set; }

        public PasswordChangeType? ChangeType { get; internal set; }

        private PasswordChangeRequest()
        {
        }

        public static PasswordChangeRequest FromString(string newPassword)
        {
            var request = new PasswordChangeRequest()
            {
                NewPassword = newPassword,
                ChangeType = PasswordChangeType.String
            };
            return request;
        }

        public static PasswordChangeRequest FromSecureString(SecureString newPassword)
        {
            var request = new PasswordChangeRequest()
            {
                NewSecurePassword = newPassword,
                ChangeType = PasswordChangeType.SecureString
            };
            return request;
        }

        public static PasswordChangeRequest FromString(SqlCredential newPassword)
        {
            var request = new PasswordChangeRequest()
            {
                Credential = newPassword,
                ChangeType = PasswordChangeType.SqlCredential
            };
            return request;
        }
    }

    internal enum PasswordChangeType
    {
        SqlCredential = 0,
        SecureString = 1,
        String = 2,
    }
}
