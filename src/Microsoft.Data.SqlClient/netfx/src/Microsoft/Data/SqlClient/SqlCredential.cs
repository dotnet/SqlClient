// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlCredential.xml' path='docs/members[@name="SqlCredential"]/SqlCredential/*' />
    // Represent a pair of user id and password which to be used for SQL Authentication
    // SqlCredential takes password as SecureString which is better way to store security sensitive information
    // This class is immutable
    public sealed class SqlCredential
    {
        string _userId;
        SecureString _password;

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlCredential.xml' path='docs/members[@name="SqlCredential"]/ctor/*' />
        // PUBLIC CONSTRUCTOR
        // SqlCredential
        //  userId: userId
        //  password: password
        public SqlCredential(string userId, SecureString password)
        {
            if (userId == null)
            {
                throw ADP.ArgumentNull("userId");
            }

            if (userId.Length > TdsEnums.MAXLEN_CLIENTID)
            {
                throw ADP.InvalidArgumentLength("userId", TdsEnums.MAXLEN_CLIENTID);
            }

            if (password == null)
            {
                throw ADP.ArgumentNull("password");
            }

            if (password.Length > TdsEnums.MAXLEN_CLIENTSECRET)
            {
                throw ADP.InvalidArgumentLength("password", TdsEnums.MAXLEN_CLIENTSECRET);
            }

            if (!password.IsReadOnly())
            {
                throw ADP.MustBeReadOnly("password");
            }

            _userId = userId;
            _password = password;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlCredential.xml' path='docs/members[@name="SqlCredential"]/UserId/*' />
        // PUBLIC PROPERTIES
        public string UserId
        {
            get
            {
                return _userId;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlCredential.xml' path='docs/members[@name="SqlCredential"]/Password/*' />
        public SecureString Password
        {
            get
            {
                return _password;
            }
        }
    }
}   // Microsoft.Data.SqlClient namespace


