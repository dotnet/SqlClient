// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// Provides a simple API for configuring and creating a <see cref="SqlDataSource" />, from which database connections can be obtained.
    /// </summary>
    internal sealed class SqlDataSourceBuilder
    {
        /// <summary>
        /// A connection string builder that can be used to configure the connection string on the builder.
        /// </summary>
        public SqlConnectionStringBuilder ConnectionStringBuilder { get; }

        // TODO: how does it interact with credentials specified in ConnectionStringBuilder?
        public SqlCredential Credential { get; set; }

        public RemoteCertificateValidationCallback UserCertificateValidationCallback { get; set; }

        public Action<X509CertificateCollection> ClientCertificatesCallback { get; set; }

        /// <summary>
        /// Constructs a new <see cref="SqlDataSourceBuilder" />, optionally starting out from the given <paramref name="connectionString"/>.
        /// </summary>
        public SqlDataSourceBuilder(string connectionString = null, SqlCredential credential = null)
        {
            ConnectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            Credential = credential;
        }

        /// <summary>
        /// Builds and returns a <see cref="SqlDataSource" /> which is ready for use.
        /// </summary>
        public SqlDataSource Build()
        {
            return new UnpooledDataSource(
                ConnectionStringBuilder,
                Credential,
                UserCertificateValidationCallback,
                ClientCertificatesCallback);
        }
    }
}

#endif
