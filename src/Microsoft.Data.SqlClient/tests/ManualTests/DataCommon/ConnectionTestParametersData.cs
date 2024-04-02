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
        private static readonly string s_fullPathToCer = Path.Combine(Directory.GetCurrentDirectory(), "localhostcert.cer");
        private static readonly string s_mismatchedcert = Path.Combine(Directory.GetCurrentDirectory(), "mismatchedcert.cer");

        private static string s_hostName = System.Net.Dns.GetHostName();
        public static ConnectionTestParametersData Data { get; } = new ConnectionTestParametersData();
        public List<ConnectionTestParameters> ConnectionTestParametersList { get; set; }

        public static IEnumerable<object[]> GetConnectionTestParameters()
        {
            yield return new object[] { Data.ConnectionTestParametersList[0] };
            yield return new object[] { Data.ConnectionTestParametersList[1] };
            yield return new object[] { Data.ConnectionTestParametersList[2] };
            yield return new object[] { Data.ConnectionTestParametersList[3] };
            yield return new object[] { Data.ConnectionTestParametersList[4] };
            yield return new object[] { Data.ConnectionTestParametersList[5] };
            yield return new object[] { Data.ConnectionTestParametersList[6] };
            yield return new object[] { Data.ConnectionTestParametersList[7] };
            yield return new object[] { Data.ConnectionTestParametersList[8] };
            yield return new object[] { Data.ConnectionTestParametersList[9] };
            yield return new object[] { Data.ConnectionTestParametersList[10] };
            yield return new object[] { Data.ConnectionTestParametersList[11] };
            yield return new object[] { Data.ConnectionTestParametersList[12] };
            yield return new object[] { Data.ConnectionTestParametersList[13] };
            yield return new object[] { Data.ConnectionTestParametersList[14] };
            yield return new object[] { Data.ConnectionTestParametersList[15] };
            yield return new object[] { Data.ConnectionTestParametersList[16] };
            yield return new object[] { Data.ConnectionTestParametersList[17] };
            yield return new object[] { Data.ConnectionTestParametersList[18] };
            yield return new object[] { Data.ConnectionTestParametersList[19] };
        }

        public ConnectionTestParametersData() 
        {
            ConnectionTestParametersList = new List<ConnectionTestParameters>
            {
                // 1
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    TrustServerCertificate = false,
                    Certificate = null,
                    HostNameInCertificate = null,
                    TestResult = true
                },
                // 2
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = null,
                    HostNameInCertificate = null,
                    TestResult = false
                },
                // 3
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    TrustServerCertificate = true,
                    Certificate = null,
                    HostNameInCertificate = null,
                    TestResult = true
                },
                // 4
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = null,
                    HostNameInCertificate = null,
                    TestResult = true
                },
                // 5
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = s_fullPathToCer,
                    HostNameInCertificate = null,
                    TestResult = true
                },
                // 6
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = s_fullPathToCer,
                    HostNameInCertificate = null,
                    TestResult = true
                },
                // 7
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = null,
                    HostNameInCertificate = s_hostName,
                    TestResult = false
                },
                // 8
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = null,
                    HostNameInCertificate = s_hostName,
                    TestResult = true
                },
                // 9
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    TrustServerCertificate = false,
                    Certificate = null,
                    HostNameInCertificate = null,
                    TestResult = true
                },
                // 10
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = null,
                    HostNameInCertificate = null,
                    TestResult = false
                },
                // 11
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    TrustServerCertificate = true,
                    Certificate = null,
                    HostNameInCertificate = null,
                    TestResult = true
                },
                // 12
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = null,
                    HostNameInCertificate = null,
                    TestResult = true
                },
                // 13
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = s_fullPathToCer,
                    HostNameInCertificate = null,
                    TestResult = true
                },
                // 14
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = s_fullPathToCer,
                    HostNameInCertificate = null,
                    TestResult = true
                },
                // 15
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = null,
                    HostNameInCertificate = s_hostName,
                    TestResult = false
                },
                // 16
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = null,
                    HostNameInCertificate = s_hostName,
                    TestResult = true
                },
                // 17
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = s_mismatchedcert,
                    HostNameInCertificate = null,
                    TestResult = false,
                },
                // 18
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.Off,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = s_mismatchedcert,
                    HostNameInCertificate = null,
                    TestResult = true
                },
                // 19
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = false,
                    Certificate = s_mismatchedcert,
                    HostNameInCertificate = null,
                    TestResult = false,
                },
                // 20
                new ConnectionTestParameters
                {
                    TdsEncryptionType = TDSPreLoginTokenEncryptionType.On,
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    Certificate = s_mismatchedcert,
                    HostNameInCertificate = null,
                    TestResult = true
                },
            };
        }
    }
}
