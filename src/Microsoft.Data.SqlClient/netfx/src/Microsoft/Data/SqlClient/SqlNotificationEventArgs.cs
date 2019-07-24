// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient {
    public class SqlNotificationEventArgs : EventArgs {
        private SqlNotificationType _type;
        private SqlNotificationInfo _info;
        private SqlNotificationSource _source;

        public SqlNotificationEventArgs(SqlNotificationType type, SqlNotificationInfo info, SqlNotificationSource source) {
            _info = info;
            _source = source;
            _type = type;
        }

        public SqlNotificationType Type {
            get {
                return _type;
            }
        }

        public SqlNotificationInfo Info {
            get {
                return _info;
            }
        }

        public SqlNotificationSource Source {
            get {
                return _source;
            }
        }

        internal static SqlNotificationEventArgs NotifyError = new SqlNotificationEventArgs(SqlNotificationType.Subscribe, SqlNotificationInfo.Error, SqlNotificationSource.Object);
    }
}


