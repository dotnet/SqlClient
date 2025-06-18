// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.PreLogin;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.DataCommon
{
    public class ConnectionTestParameters
    {
        private SqlConnectionEncryptOption _encryptionOption;
        private TDSPreLoginTokenEncryptionType _encryptionType;
        private string _hnic;
        private string _cert;
        private bool _result;
        private bool _trustServerCert;

        public SqlConnectionEncryptOption Encrypt => _encryptionOption;
        public bool TrustServerCertificate => _trustServerCert;
        public string Certificate => _cert;
        public string HostNameInCertificate => _hnic;
        public bool TestResult => _result;
        public TDSPreLoginTokenEncryptionType TdsEncryptionType => _encryptionType;
        public SslProtocols EncryptionProtocols { get; }

        public ConnectionTestParameters(TDSPreLoginTokenEncryptionType tdsEncryptionType, SqlConnectionEncryptOption encryptOption, bool trustServerCert, string cert, string hnic, bool result)
            : this(tdsEncryptionType, encryptOption, trustServerCert, cert, hnic, SslProtocols.Tls12, result)
        { }

        public ConnectionTestParameters(TDSPreLoginTokenEncryptionType tdsEncryptionType, SqlConnectionEncryptOption encryptOption, bool trustServerCert, string cert, string hnic, SslProtocols sslProtocols, bool result)
        {
            _encryptionOption = encryptOption;
            _trustServerCert = trustServerCert;
            _cert = cert;
            _hnic = hnic;
            _result = result;
            _encryptionType = tdsEncryptionType;
            EncryptionProtocols = sslProtocols;
        }
    }
}
