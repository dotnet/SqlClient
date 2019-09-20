// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.Sql
{

    using System;
    using Microsoft.Data.Common;

    /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.Sql\SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/SqlNotificationRequest/*' />
    //[System.ComponentModel.TypeConverterAttribute(typeof(Microsoft.Data.Sql.SqlNotificationRequest.SqlNotificationRequestConverter))]
    public sealed class SqlNotificationRequest
    {
        private string _userData;
        private string _options;
        private int _timeout;

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.Sql\SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/ctor1/*' />
        public SqlNotificationRequest()
                : this(null, null, SqlClient.SQL.SqlDependencyTimeoutDefault) { }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.Sql\SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/ctor2/*' />
        public SqlNotificationRequest(string userData, string options, int timeout)
        {
            UserData = userData;
            Timeout = timeout;
            Options = options;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.Sql\SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/Options/*' />
        public string Options
        {
            get
            {
                return _options;
            }
            set
            {
                if ((null != value) && (UInt16.MaxValue < value.Length))
                {
                    throw ADP.ArgumentOutOfRange(String.Empty, ADP.ParameterService);
                }
                _options = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.Sql\SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/Timeout/*' />
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
                    throw ADP.ArgumentOutOfRange(String.Empty, ADP.ParameterTimeout);
                }
                _timeout = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.Sql\SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/UserData/*' />
        public string UserData
        {
            get
            {
                return _userData;
            }
            set
            {
                if ((null != value) && (UInt16.MaxValue < value.Length))
                {
                    throw ADP.ArgumentOutOfRange(String.Empty, ADP.ParameterUserData);
                }
                _userData = value;
            }
        }
    }
}
