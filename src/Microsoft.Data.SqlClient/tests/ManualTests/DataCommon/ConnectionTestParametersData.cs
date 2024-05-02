// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.PreLogin;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.DataCommon
{
    public class ConnectionTestParametersData
    {
        private const int CASES = 30;
        private string _empty = string.Empty;
        private static readonly string s_fullPathToCer = Path.Combine(Directory.GetCurrentDirectory(), "localhostcert.cer");
        private static readonly string s_mismatchedcert = Path.Combine(Directory.GetCurrentDirectory(), "mismatchedcert.cer");

        private static string s_hostName = System.Net.Dns.GetHostName();
        public static ConnectionTestParametersData Data { get; } = new ConnectionTestParametersData();
        public List<ConnectionTestParameters> ConnectionTestParametersList { get; set; }

        public static IEnumerable<object[]> GetConnectionTestParameters()
        {
            for(int i=0; i < CASES; i++)
            {
                yield return new object[] { Data.ConnectionTestParametersList[i] };
            }
        }

        public ConnectionTestParametersData() 
        {
            // Test cases possible field values for connection parameters:
            //     Possible values for TdsEncryptionType are Off, On, Required
            //     Possible values for Encrypt are Optional, Mandatory
            //     Possible values for TrustServerCertificate are true, false
            //     Possible values for Certificate are valid path to certificate, mismatched certificate, or empty
            //     Possible values for HostNameInCertificate are valid hostname, or empty
            //     TestResult is the expected result of the connection test
            // These combinations are based on the possible values of Encrypt, TrustServerCertificate, Certificate, HostNameInCertificate
            ConnectionTestParametersList = new List<ConnectionTestParameters>
            {
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    TrustServerCertificate = false,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = false
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    TrustServerCertificate = true,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = s_fullPathToCer,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = s_fullPathToCer,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = _empty,
                    HostNameInCertificate = s_hostName,
                    TestResult = false
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = _empty,
                    HostNameInCertificate = s_hostName,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    TrustServerCertificate = false,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = false
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = false
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    TrustServerCertificate = true,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = s_fullPathToCer,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = s_fullPathToCer,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = _empty,
                    HostNameInCertificate = s_hostName,
                    TestResult = false
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = _empty,
                    HostNameInCertificate = s_hostName,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Required,
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    TrustServerCertificate = false,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = false
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Required,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = false
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Required,
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    TrustServerCertificate = true,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Required,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = _empty,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Required,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = s_fullPathToCer,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Required,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = s_fullPathToCer,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Required,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = _empty,
                    HostNameInCertificate = s_hostName,
                    TestResult = false
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Required,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = _empty,
                    HostNameInCertificate = s_hostName,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = s_mismatchedcert,
                    HostNameInCertificate = _empty,
                    TestResult = false,
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = s_mismatchedcert,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = s_mismatchedcert,
                    HostNameInCertificate = _empty,
                    TestResult = false,
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = s_mismatchedcert,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Required,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = s_mismatchedcert,
                    HostNameInCertificate = _empty,
                    TestResult = false,
                },
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Required,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = s_mismatchedcert,
                    HostNameInCertificate = _empty,
                    TestResult = true
                },
            };
        }
    }
}
