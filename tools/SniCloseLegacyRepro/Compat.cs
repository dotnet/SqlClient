// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using LegacyBuilder = System.Data.SqlClient.SqlConnectionStringBuilder;

namespace SniCloseLegacyRepro.Compat
{
    /// <summary>
    /// Minimal stand-in for Microsoft.Data.SqlClient's SqlConnectionEncryptOption
    /// enum, which does not exist in the legacy System.Data.SqlClient driver.
    /// </summary>
    public enum SqlConnectionEncryptOption
    {
        Optional,
        Mandatory,
        Strict,
    }

    /// <summary>
    /// A connection-string builder facade over the LEGACY driver's (sealed)
    /// SqlConnectionStringBuilder. It exposes only the keywords the linked test
    /// files use, and accepts the Microsoft.Data-style
    /// <see cref="SqlConnectionEncryptOption"/> enum for <c>Encrypt</c> so those
    /// files compile unchanged. The legacy builder is sealed, so this uses
    /// composition rather than inheritance.
    /// </summary>
    public sealed class ShimSqlConnectionStringBuilder
    {
        private readonly LegacyBuilder _inner;

        public ShimSqlConnectionStringBuilder() => _inner = new LegacyBuilder();

        public ShimSqlConnectionStringBuilder(string connectionString) =>
            _inner = new LegacyBuilder(connectionString);

        public string ConnectionString => _inner.ConnectionString;

        public string DataSource
        {
            get => _inner.DataSource;
            set => _inner.DataSource = value;
        }

        public bool Pooling
        {
            get => _inner.Pooling;
            set => _inner.Pooling = value;
        }

        public bool TrustServerCertificate
        {
            get => _inner.TrustServerCertificate;
            set => _inner.TrustServerCertificate = value;
        }

        public int ConnectTimeout
        {
            get => _inner.ConnectTimeout;
            set => _inner.ConnectTimeout = value;
        }

        public int ConnectRetryCount
        {
            get => _inner.ConnectRetryCount;
            set => _inner.ConnectRetryCount = value;
        }

        public bool MultipleActiveResultSets
        {
            get => _inner.MultipleActiveResultSets;
            set => _inner.MultipleActiveResultSets = value;
        }

#if NETFRAMEWORK
        // Only present on the in-box (netfx) legacy builder; the tests guard its
        // use with #if NETFRAMEWORK, so this facade matches that guard.
        public bool TransparentNetworkIPResolution
        {
            get => _inner.TransparentNetworkIPResolution;
            set => _inner.TransparentNetworkIPResolution = value;
        }
#endif

        /// <summary>
        /// Maps the Microsoft.Data encryption enum onto the legacy boolean
        /// <c>Encrypt</c> keyword: Optional =&gt; false; Mandatory/Strict =&gt; true.
        /// </summary>
        public SqlConnectionEncryptOption Encrypt
        {
            get => _inner.Encrypt
                ? SqlConnectionEncryptOption.Mandatory
                : SqlConnectionEncryptOption.Optional;
            set => _inner.Encrypt = value != SqlConnectionEncryptOption.Optional;
        }
    }
}

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Stand-in for the real test-infrastructure DataTestUtility, providing just
    /// the members the linked MARS test and the stress tests reference. The live
    /// connection string is taken from the SNICLOSE_CONNSTR environment variable.
    /// </summary>
    public static class DataTestUtility
    {
        public static string TCPConnectionString =>
            Environment.GetEnvironmentVariable("SNICLOSE_CONNSTR") ?? string.Empty;

        public static bool AreConnStringsSetup() =>
            !string.IsNullOrWhiteSpace(TCPConnectionString);

        public static bool IsNotAzureSynapse() => true;
    }
}
