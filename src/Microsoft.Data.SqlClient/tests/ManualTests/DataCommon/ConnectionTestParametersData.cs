// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Security.Authentication;
using Microsoft.SqlServer.TDS.PreLogin;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.DataCommon
{
    public class ConnectionTestParametersData
    {
        private string _empty = string.Empty;
        // It was advised to store the client certificate in its own folder.
        private static readonly string s_fullPathToCer = Path.Combine(Directory.GetCurrentDirectory(), "clientcert", "localhostcert.cer");
        private static readonly string s_mismatchedcert = Path.Combine(Directory.GetCurrentDirectory(), "clientcert", "mismatchedcert.cer");

        private static readonly string s_hostName = System.Net.Dns.GetHostName();
        public static ConnectionTestParametersData Data { get; } = new ConnectionTestParametersData();
        public List<ConnectionTestParameters> ConnectionTestParametersList { get; set; }

        public static IEnumerable<object[]> GetConnectionTestParameters()
        {
            for (int i = 0; i < Data.ConnectionTestParametersList.Count; i++)
            {
                yield return new object[] { Data.ConnectionTestParametersList[i] };
            }
        }

        public ConnectionTestParametersData()
        {
            // Test cases possible field values for connection parameters:
            // These combinations are based on the possible values of Encrypt, TrustServerCertificate, Certificate, HostNameInCertificate
            /*
             * TDSEncryption | Encrypt    | TrustServerCertificate | Certificate  | HNIC         | SSL Protocols    | TestResults
             * ---------------------------------------------------------------------------------------------------------------
             *   Off         | Optional   |  true                  | valid        | valid name   | TLS 1.2          | true
             *   On          | Mandatory  |  false                 | mismatched   | empty        | TLS 1.0, TLS 1.1 | false
             *   Required    |            |    x                   | ChainError?  | wrong name?  |                  |
             */
            ConnectionTestParametersList = new List<ConnectionTestParameters>
            {
                // TDSPreLoginTokenEncryptionType.Off
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Optional, false, _empty, _empty, true),
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Mandatory, false, _empty, _empty, false),
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Optional, true, _empty, _empty, true),
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Mandatory, true, _empty, _empty, true),
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Mandatory, false, s_fullPathToCer, _empty, true),
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Mandatory, true, s_fullPathToCer, _empty, true),
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Mandatory, false, _empty, s_hostName, false),
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Mandatory, true, _empty, s_hostName, true),

                // TDSPreLoginTokenEncryptionType.On
                new(TDSPreLoginTokenEncryptionType.On, SqlConnectionEncryptOption.Optional, false, _empty, _empty, false),
                new(TDSPreLoginTokenEncryptionType.On, SqlConnectionEncryptOption.Mandatory, false, _empty, _empty, false),
                new(TDSPreLoginTokenEncryptionType.On, SqlConnectionEncryptOption.Optional, true, _empty, _empty, true),
                new(TDSPreLoginTokenEncryptionType.On, SqlConnectionEncryptOption.Mandatory, true, _empty, _empty, true),
                new(TDSPreLoginTokenEncryptionType.On, SqlConnectionEncryptOption.Mandatory, false, s_fullPathToCer, _empty, true),
                new(TDSPreLoginTokenEncryptionType.On, SqlConnectionEncryptOption.Mandatory, true, s_fullPathToCer, _empty, true),
                new(TDSPreLoginTokenEncryptionType.On, SqlConnectionEncryptOption.Mandatory, false, _empty, s_hostName, false),
                new(TDSPreLoginTokenEncryptionType.On, SqlConnectionEncryptOption.Mandatory, true, _empty, s_hostName, true),

                // TDSPreLoginTokenEncryptionType.Required
                new(TDSPreLoginTokenEncryptionType.Required, SqlConnectionEncryptOption.Optional, false, _empty, _empty, false),
                new(TDSPreLoginTokenEncryptionType.Required, SqlConnectionEncryptOption.Mandatory, false, _empty, _empty, false),
                new(TDSPreLoginTokenEncryptionType.Required, SqlConnectionEncryptOption.Optional, true, _empty, _empty, true),
                new(TDSPreLoginTokenEncryptionType.Required, SqlConnectionEncryptOption.Mandatory, true, _empty, _empty, true),
                new(TDSPreLoginTokenEncryptionType.Required, SqlConnectionEncryptOption.Mandatory, false, s_fullPathToCer, _empty, true),
                new(TDSPreLoginTokenEncryptionType.Required, SqlConnectionEncryptOption.Mandatory, true, s_fullPathToCer, _empty, true),
                new(TDSPreLoginTokenEncryptionType.Required, SqlConnectionEncryptOption.Mandatory, false, _empty, s_hostName, false),
                new(TDSPreLoginTokenEncryptionType.Required, SqlConnectionEncryptOption.Mandatory, true, _empty, s_hostName, true),

                // Mismatched certificate test
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Mandatory, false, s_mismatchedcert, _empty, false),
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Mandatory, true, s_mismatchedcert, _empty, false),
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Mandatory, true, s_mismatchedcert, _empty, true),
                new(TDSPreLoginTokenEncryptionType.On, SqlConnectionEncryptOption.Mandatory, false, s_mismatchedcert, _empty, false),
                new(TDSPreLoginTokenEncryptionType.On, SqlConnectionEncryptOption.Mandatory, true, s_mismatchedcert, _empty, true),
                new(TDSPreLoginTokenEncryptionType.Required, SqlConnectionEncryptOption.Mandatory, false, s_mismatchedcert, _empty, false),
                new(TDSPreLoginTokenEncryptionType.Required, SqlConnectionEncryptOption.Mandatory, true, s_mismatchedcert, _empty, true),

                // Multiple SSL protocols test
#pragma warning disable CA5397 // Do not use deprecated SslProtocols values
#pragma warning disable CA5398 // Avoid hardcoded SslProtocols values
#if NET
#pragma warning disable SYSLIB0039 // Type or member is obsolete: TLS 1.0 & 1.1 are deprecated
#endif
                new(TDSPreLoginTokenEncryptionType.Off, SqlConnectionEncryptOption.Mandatory, false, s_fullPathToCer, _empty, SslProtocols.Tls | SslProtocols.Tls11, true),
                new(TDSPreLoginTokenEncryptionType.On, SqlConnectionEncryptOption.Mandatory, false, s_fullPathToCer, _empty, SslProtocols.Tls | SslProtocols.Tls11, true),
                new(TDSPreLoginTokenEncryptionType.Required, SqlConnectionEncryptOption.Mandatory, false, s_fullPathToCer, _empty, SslProtocols.Tls | SslProtocols.Tls11, true),
#if NET
#pragma warning restore SYSLIB0039 // Type or member is obsolete: TLS 1.0 & 1.1 are deprecated
#endif
#pragma warning restore CA5397 // Do not use deprecated SslProtocols values
#pragma warning restore CA5398 // Avoid hardcoded SslProtocols values
            };
        }
    }
}
