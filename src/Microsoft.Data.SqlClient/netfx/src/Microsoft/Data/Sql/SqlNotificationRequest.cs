//------------------------------------------------------------------------------
// <copyright file="SqlNotificationRequest.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">kphil</owner>
// <owner current="true" primary="true">blained</owner>
// <owner current="false" primary="false">laled</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.Sql {

    using System;
    using Microsoft.Data.Common;
    using Microsoft.Data.SqlClient;
    
//      [System.ComponentModel.TypeConverterAttribute(typeof(Microsoft.Data.Sql.SqlNotificationRequest.SqlNotificationRequestConverter))]
    public sealed class SqlNotificationRequest {
        private string _userData;
        private string _options;
        private int    _timeout;
        private System.Data.Sql.SqlNotificationRequest _sysSqlNotificationRequest;

        public SqlNotificationRequest() 
                : this(null, null, SqlClient.SQL.SqlDependencyTimeoutDefault) {}

        public SqlNotificationRequest(string userData, string options, int timeout) {
            UserData = userData;
            Timeout  = timeout;    
            Options  = options;
        }

        internal SqlNotificationRequest(System.Data.Sql.SqlNotificationRequest sqlNotificationRequest)
        {
            SysSqlNotificationRequest = sqlNotificationRequest;
        }

        internal System.Data.Sql.SqlNotificationRequest SysSqlNotificationRequest {
            get {
                return _sysSqlNotificationRequest;
            }
            set {
                _sysSqlNotificationRequest = value;
            }
        }


        public string Options {
            get {
                return SysSqlNotificationRequest?.Options ?? _options;
            }
            set {
                if (SysSqlNotificationRequest != null)
                {
                    SysSqlNotificationRequest.Options = value;
                }
                else
                {
                    if ((null != value) && (UInt16.MaxValue < value.Length))
                    {
                        throw ADP.ArgumentOutOfRange(String.Empty, ADP.ParameterService);
                    }
                    _options = value;
                }
            }
        }

        public int Timeout {
            get {
                return SysSqlNotificationRequest?.Timeout ?? _timeout;
            }
            set {
                if (SysSqlNotificationRequest != null)
                {
                    SysSqlNotificationRequest.Timeout = value;
                }
                else
                {
                    if (0 > value)
                    {
                        throw ADP.ArgumentOutOfRange(String.Empty, ADP.ParameterTimeout);
                    }
                    _timeout = value;
                }
            }
        }

        public string UserData {
            get {
                return SysSqlNotificationRequest?.UserData ?? _userData;
            }
            set {
                if (SysSqlNotificationRequest != null)
                {
                    SysSqlNotificationRequest.UserData = value;
                }
                else
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
}

