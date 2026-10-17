// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Ensures enclave sessions are isolated by the attestation policy that established them.
/// </summary>
public class EnclaveSessionCacheTest
{
    /// <summary>
    /// The shared command helper must carry the connection's policy into every session-cache
    /// operation, rather than constructing a protocol-neutral identity.
    /// </summary>
    [Theory]
    [InlineData(SqlConnectionAttestationProtocol.None)]
    [InlineData(SqlConnectionAttestationProtocol.HGS)]
    [InlineData(SqlConnectionAttestationProtocol.AAS)]
    public void CommandSessionParameters_IncludeConnectionAttestationProtocol(
        SqlConnectionAttestationProtocol protocol)
    {
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = "server",
            InitialCatalog = "database",
            EnclaveAttestationUrl = "https://attestation",
            AttestationProtocol = protocol,
            ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled
        };
        using SqlConnection connection = new(builder.ConnectionString);
        using SqlCommand command = connection.CreateCommand();
        MethodInfo? getParameters = typeof(SqlCommand).GetMethod(
            "GetEnclaveSessionParameters", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(getParameters);

        EnclaveSessionParameters parameters = Assert.IsType<EnclaveSessionParameters>(
            getParameters.Invoke(command, null));

        Assert.Equal(protocol, parameters.AttestationProtocol);
        Assert.Equal(builder.DataSource, parameters.ServerName);
        Assert.Equal(builder.InitialCatalog, parameters.Database);
        Assert.Equal(builder.EnclaveAttestationUrl, parameters.AttestationUrl);
    }

    /// <summary>
    /// A cached session cannot satisfy a different attestation policy, and sessions for both
    /// policies can coexist without overwriting or invalidating one another.
    /// </summary>
    [Theory]
    [InlineData(SqlConnectionAttestationProtocol.None, SqlConnectionAttestationProtocol.HGS)]
    [InlineData(SqlConnectionAttestationProtocol.None, SqlConnectionAttestationProtocol.AAS)]
    [InlineData(SqlConnectionAttestationProtocol.HGS, SqlConnectionAttestationProtocol.None)]
    [InlineData(SqlConnectionAttestationProtocol.HGS, SqlConnectionAttestationProtocol.AAS)]
    [InlineData(SqlConnectionAttestationProtocol.AAS, SqlConnectionAttestationProtocol.None)]
    [InlineData(SqlConnectionAttestationProtocol.AAS, SqlConnectionAttestationProtocol.HGS)]
    public void DifferentAttestationProtocols_IsolateLookupCreationAndInvalidation(
        SqlConnectionAttestationProtocol firstProtocol,
        SqlConnectionAttestationProtocol secondProtocol)
    {
        EnclaveSessionCache cache = new EnclaveSessionCache();
        EnclaveSessionParameters first = new("server", "https://attestation", "database", firstProtocol);
        EnclaveSessionParameters second = new("server", "https://attestation", "database", secondProtocol);
        SqlEnclaveSession firstSession = cache.CreateSession(first, new byte[] { 1 }, 1, out _);

        Assert.Null(cache.GetEnclaveSession(second, out _));
        cache.InvalidateSession(second, firstSession);
        Assert.Same(firstSession, cache.GetEnclaveSession(first, out _));

        // Even equal server-issued session IDs must not allow cross-policy eviction.
        SqlEnclaveSession secondSession = cache.CreateSession(second, new byte[] { 2 }, 1, out _);
        Assert.Same(firstSession, cache.GetEnclaveSession(first, out _));
        Assert.Same(secondSession, cache.GetEnclaveSession(second, out _));

        cache.InvalidateSession(first, firstSession);
        Assert.Null(cache.GetEnclaveSession(first, out _));
        Assert.Same(secondSession, cache.GetEnclaveSession(second, out _));
    }

    /// <summary>
    /// Equivalent parameters reuse the session and preserve the existing case-insensitive
    /// matching and increasing message counter.
    /// </summary>
    [Theory]
    [InlineData(SqlConnectionAttestationProtocol.None)]
    [InlineData(SqlConnectionAttestationProtocol.HGS)]
    [InlineData(SqlConnectionAttestationProtocol.AAS)]
    public void SameAttestationProtocol_EquivalentParameters_ReuseSession(
        SqlConnectionAttestationProtocol protocol)
    {
        EnclaveSessionCache cache = new EnclaveSessionCache();
        EnclaveSessionParameters parameters = new("SERVER", "https://ATTESTATION", "DATABASE", protocol);
        EnclaveSessionParameters equivalent = new("server", "https://attestation", "database", protocol);
        SqlEnclaveSession session = cache.CreateSession(parameters, new byte[] { 1 }, 1, out long created);

        Assert.Same(session, cache.GetEnclaveSession(equivalent, out long firstRead));
        Assert.Same(session, cache.GetEnclaveSession(parameters, out long secondRead));
        Assert.True(firstRead > created);
        Assert.True(secondRead > firstRead);

        cache.InvalidateSession(equivalent, session);
        Assert.Null(cache.GetEnclaveSession(parameters, out _));
    }

    /// <summary>
    /// Missing attestation URLs remain supported for None, including the existing equivalence
    /// between null and empty URLs.
    /// </summary>
    [Fact]
    public void NoneAttestation_EmptyAndNullUrl_ReuseSession()
    {
        EnclaveSessionCache cache = new EnclaveSessionCache();
        EnclaveSessionParameters parameters = new(
            "server", null, "database", SqlConnectionAttestationProtocol.None);
        EnclaveSessionParameters equivalent = new(
            "server", string.Empty, "database", SqlConnectionAttestationProtocol.None);
        SqlEnclaveSession session = cache.CreateSession(parameters, new byte[] { 1 }, 1, out _);

        Assert.Same(session, cache.GetEnclaveSession(equivalent, out _));
        cache.InvalidateSession(equivalent, session);
        Assert.Null(cache.GetEnclaveSession(parameters, out _));
    }

    /// <summary>
    /// Cache identity retains each field separately, even when a field contains a delimiter
    /// or different field boundaries would produce identical concatenated strings.
    /// </summary>
    [Theory]
    [InlineData("server", "database", "https://attestation",
        "other-server", "database", "https://attestation")]
    [InlineData("server", "database", "https://attestation",
        "server", "other-database", "https://attestation")]
    [InlineData("server", "database", "https://attestation",
        "server", "database", "https://other-attestation")]
    [InlineData("server+part", "database", "https://attestation",
        "server", "part+database", "https://attestation")]
    [InlineData("server", "database", "https://attestation/path+https://other",
        "server", "database+https://attestation/path", "https://other")]
    [InlineData("server", "database", "https://attestation",
        "server", "databasehttps://attestation", "")]
    public void DifferentFields_DoNotShareSessions(
        string firstServer, string firstDatabase, string firstUrl,
        string secondServer, string secondDatabase, string secondUrl)
    {
        EnclaveSessionCache cache = new EnclaveSessionCache();
        EnclaveSessionParameters first = new(
            firstServer, firstUrl, firstDatabase, SqlConnectionAttestationProtocol.None);
        EnclaveSessionParameters second = new(
            secondServer, secondUrl, secondDatabase, SqlConnectionAttestationProtocol.None);
        SqlEnclaveSession firstSession = cache.CreateSession(first, new byte[] { 1 }, 1, out _);

        Assert.Null(cache.GetEnclaveSession(second, out _));
        SqlEnclaveSession secondSession = cache.CreateSession(second, new byte[] { 2 }, 1, out _);
        Assert.Same(firstSession, cache.GetEnclaveSession(first, out _));
        Assert.Same(secondSession, cache.GetEnclaveSession(second, out _));

        cache.InvalidateSession(second, secondSession);
        Assert.Same(firstSession, cache.GetEnclaveSession(first, out _));
    }

    /// <summary>
    /// Invalidating a stale session must not evict a replacement session with a different ID.
    /// </summary>
    [Theory]
    [InlineData(SqlConnectionAttestationProtocol.None)]
    [InlineData(SqlConnectionAttestationProtocol.HGS)]
    [InlineData(SqlConnectionAttestationProtocol.AAS)]
    public void InvalidateSession_StaleSession_PreservesReplacement(
        SqlConnectionAttestationProtocol protocol)
    {
        EnclaveSessionCache cache = new EnclaveSessionCache();
        EnclaveSessionParameters parameters = new("server", "https://attestation", "database", protocol);
        SqlEnclaveSession stale = cache.CreateSession(parameters, new byte[] { 1 }, 1, out _);
        SqlEnclaveSession replacement = cache.CreateSession(parameters, new byte[] { 2 }, 2, out _);

        cache.InvalidateSession(parameters, stale);
        Assert.Same(replacement, cache.GetEnclaveSession(parameters, out _));
        cache.InvalidateSession(parameters, replacement);
        Assert.Null(cache.GetEnclaveSession(parameters, out _));
    }
}
