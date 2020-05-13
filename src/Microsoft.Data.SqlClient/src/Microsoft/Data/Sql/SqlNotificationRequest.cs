// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.Sql
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/SqlNotificationRequest/*' />
    public sealed class SqlNotificationRequest
    {
        private string _userData;
        private string _options;
        private int _timeout;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/ctor1/*' />
        public SqlNotificationRequest()
                : this(null, null, SQL.SqlDependencyTimeoutDefault) { }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/ctor2/*' />
        public SqlNotificationRequest(string userData, string options, int timeout)
        {
            UserData = userData;
            Timeout = timeout;
            Options = options;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/Options/*' />
        public string Options
        {
            get
            {
                return _options;
            }
            set
            {
                if ((null != value) && (ushort.MaxValue < value.Length))
                {
                    throw ADP.ArgumentOutOfRange(string.Empty, nameof(Options));
                }
                _options = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/Timeout/*' />
        public int Timeout
        {
            get
            {
                return _timeout;
            }
            set
            {
                if (0 > value)
                {
                    throw ADP.ArgumentOutOfRange(string.Empty, nameof(Timeout));
                }
                _timeout = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/UserData/*' />
        public string UserData
        {
            get
            {
                return _userData;
            }
            set
            {
                if ((null != value) && (ushort.MaxValue < value.Length))
                {
                    throw ADP.ArgumentOutOfRange(string.Empty, nameof(UserData));
                }
                _userData = value;
            }
        }
    }
}
