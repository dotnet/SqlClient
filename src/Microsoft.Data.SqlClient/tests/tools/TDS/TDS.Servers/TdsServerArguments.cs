// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.SqlServer.TDS.PreLogin;

namespace Microsoft.SqlServer.TDS.Servers
{
    /// <summary>
    /// Common arguments for TDS Server
    /// </summary>
    public class TdsServerArguments
    {
        /// <summary>
        /// Service Principal Name, representing Azure SQL Database in Azure Active Directory.
        /// </summary>
        public const string AzureADServicePrincipalName = @"https://database.windows.net/";

        /// <summary>
        /// The Azure Active Directory production token endpoint to re-direct the client to fetch a token from.
        /// </summary>
        public const string AzureADProductionTokenEndpoint = @"https://login.windows.net/common";

        /// <summary>
        /// Log to which send TDS conversation
        /// </summary>
        /// TODO: change this to expect ITestOutputHelper?
        public TextWriter Log { get; set; } = null;

        /// <summary>
        /// Server name
        /// </summary>
        public string ServerName { get; set; } = Environment.MachineName;

        /// <summary>
        /// Server version
        /// </summary>
        public Version ServerVersion { get; set; } = new Version(11, 0, 1083);

        /// <summary>
        /// Server principal name
        /// </summary>
        public string ServerPrincipalName { get; set; } = AzureADServicePrincipalName;

        /// <summary>
        /// Sts Url
        /// </summary>
        public string StsUrl { get; set; } = AzureADProductionTokenEndpoint;

        /// <summary>
        /// Size of the TDS packet server should operate with
        /// </summary>
        public int PacketSize { get; set; } = 4096;

        /// <summary>
        /// Transport encryption
        /// </summary>
        public TDSPreLoginTokenEncryptionType Encryption { get; set; } = TDSPreLoginTokenEncryptionType.NotSupported;

        /// <summary>
        /// Specifies the FedAuthRequired option
        /// </summary>
        public TdsPreLoginFedAuthRequiredOption FedAuthRequiredPreLoginOption { get; set; } = TdsPreLoginFedAuthRequiredOption.FedAuthNotRequired;

        /// <summary>
        /// Certificate to use for transport encryption
        /// </summary>
        public X509Certificate EncryptionCertificate { get; set; } = null;

        /// <summary>
        /// SSL/TLS protocols to use for transport encryption
        /// </summary>
        public SslProtocols EncryptionProtocols { get; set; } = SslProtocols.Tls12;

        /// <summary>
        /// Routing destination protocol
        /// </summary>
        public string FailoverPartner { get; set; } = string.Empty;
    }
}
