// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.RateLimiters;

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
            if (ConnectionStringBuilder.Pooling)
            {
                //TODO: pool group layer

                DbConnectionPoolGroupOptions poolGroupOptions = new DbConnectionPoolGroupOptions(
                    ConnectionStringBuilder.IntegratedSecurity,
                    ConnectionStringBuilder.MinPoolSize,
                    ConnectionStringBuilder.MaxPoolSize,
                    //TODO: carry over connect timeout conversion logic from SqlConnectionFactory? if not, don't need an extra allocation for this object, just use connection string builder
                    ConnectionStringBuilder.ConnectTimeout,
                    ConnectionStringBuilder.LoadBalanceTimeout,
                    ConnectionStringBuilder.Enlist);

                //TODO: evaluate app context switch for concurrency limit
                IRateLimiter rateLimiter = IsBlockingPeriodEnabled() ? new BlockingPeriodRateLimiter() : new PassthroughRateLimiter();

                return new PoolingDataSource(ConnectionStringBuilder,
                    Credential,
                    poolGroupOptions,
                    rateLimiter);
            }
            else
            {
                return new UnpooledDataSource(
                    ConnectionStringBuilder,
                    Credential);
            }
        }

        private bool IsBlockingPeriodEnabled()
        {
            var policy = ConnectionStringBuilder.PoolBlockingPeriod;

            switch (policy)
            {
                case PoolBlockingPeriod.Auto:
                    {
                        if (ADP.IsAzureSqlServerEndpoint(ConnectionStringBuilder.DataSource))
                        {
                            return false; // in Azure it will be Disabled
                        }
                        else
                        {
                            return true; // in Non Azure, it will be Enabled
                        }
                    }
                case PoolBlockingPeriod.AlwaysBlock:
                    {
                        return true; //Enabled
                    }
                case PoolBlockingPeriod.NeverBlock:
                    {
                        return false; //Disabled
                    }
                default:
                    {
                        //we should never get into this path.
                        Debug.Fail("Unknown PoolBlockingPeriod. Please specify explicit results in above switch case statement.");
                        return true;
                    }
            }
        }
    }
}

#endif
