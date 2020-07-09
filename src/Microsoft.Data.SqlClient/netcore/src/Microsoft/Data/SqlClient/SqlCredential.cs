// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCredential.xml' path='docs/members[@name="SqlCredential"]/SqlCredential/*' />
    public sealed class SqlCredential
    {
        string _userId;
        SecureString _password;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCredential.xml' path='docs/members[@name="SqlCredential"]/ctor/*' />
        public SqlCredential(string userId, SecureString password)
        {
            if (userId == null)
            {
                throw ADP.ArgumentNull(nameof(userId));
            }

            if (userId.Length > TdsEnums.MAXLEN_CLIENTID)
            {
                throw ADP.InvalidArgumentLength(nameof(userId), TdsEnums.MAXLEN_CLIENTID);
            }

            if (password == null)
            {
                throw ADP.ArgumentNull(nameof(password));
            }

            if (password.Length > TdsEnums.MAXLEN_CLIENTSECRET)
            {
                throw ADP.InvalidArgumentLength(nameof(password), TdsEnums.MAXLEN_CLIENTSECRET);
            }

            if (!password.IsReadOnly())
            {
                throw ADP.MustBeReadOnly(nameof(password));
            }

            _userId = userId;
            _password = password;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCredential.xml' path='docs/members[@name="SqlCredential"]/UserId/*' />
        public string UserId => _userId;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCredential.xml' path='docs/members[@name="SqlCredential"]/Password/*' />
        public SecureString Password => _password;

    }
}
