// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Configuration;
using System.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies agent identifier parsing and validation.
/// </summary>
public class SqlClientAgentTests
{
    /// <summary>
    /// Ensures the published underlying type stays Int32, which keeps the enum CLS-compliant.
    /// </summary>
    [Fact]
    public void UnderlyingType_IsInt32()
        => Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(SqlClientAgent)));

    /// <summary>Ensures published Microsoft agent identifiers remain stable.</summary>
    [Fact]
    public void KnownAgentIdentifiers_AreStable()
    {
        Assert.Equal(1, (int)SqlClientAgent.EntityFramework);
        Assert.Equal(2, (int)SqlClientAgent.SemanticKernel);
        Assert.Equal(3, (int)SqlClientAgent.ManagementStudio);
        Assert.Equal(4, (int)SqlClientAgent.SqlManagementObjects);
        Assert.Equal(5, (int)SqlClientAgent.DataTierApplicationFramework);
        Assert.Equal(6, (int)SqlClientAgent.SqlToolsService);
        Assert.Equal(7, (int)SqlClientAgent.AspNetCoreDistributedSqlServerCache);
        Assert.Equal(8, (int)SqlClientAgent.EntityFramework6);
        Assert.Equal(9, (int)SqlClientAgent.AzureFunctionsSqlExtension);
        Assert.Equal(10, (int)SqlClientAgent.OrleansAdoNet);
        Assert.Equal(11, (int)SqlClientAgent.DurableTaskSqlServer);
    }

    /// <summary>Verifies configuration accepts enum names and forward-compatible numeric identifiers.</summary>
    [Theory]
    [InlineData("SqlToolsService", 6)]
    [InlineData("42", 42)]
    public void Parse_AcceptsNamedAndNumericIdentifiers(string value, int expected)
        => Assert.Equal(expected, (int)SqlClientAgentRegistration.Parse(value));

    /// <summary>Verifies invalid and zero identifiers are rejected.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("not-an-agent")]
    [InlineData("0")]
    // Enum.TryParse would otherwise combine these into an unrelated agent.
    [InlineData("EntityFramework,SemanticKernel")]
    [InlineData("1,2")]
    // Identifiers are 16-bit and positive.
    [InlineData("-1")]
    [InlineData("70000")]
    public void Parse_RejectsInvalidIdentifiers(string value)
        => Assert.ThrowsAny<ArgumentException>(() => SqlClientAgentRegistration.Parse(value));

    /// <summary>Verifies undeclared identifiers are rejected by the public registration API.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    public void Register_RejectsUndeclaredIdentifiers(int id)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => SqlConnection.RegisterSqlClientAgent((SqlClientAgent)id));

    /// <summary>
    /// Verifies the SqlClientAgent configuration section is declared correctly and yields the
    /// expected agent.
    ///
    /// The host configuration file cannot be exercised here because the test host substitutes its
    /// own, so the section is loaded from a mapped configuration file instead.
    /// </summary>
    [Theory]
    [InlineData("EntityFramework", 1)]
    [InlineData("managementstudio", 3)]
    [InlineData("42", 42)]
    public void ConfigurationSection_YieldsAgent(string id, int expected)
    {
        SqlClientAgentConfigurationSection section = LoadSection(id);

        Assert.Equal(expected, (int)SqlClientAgentRegistration.Parse(section.Id));
    }

    /// <summary>
    /// Verifies an invalid configured identifier is rejected rather than silently accepted.
    /// </summary>
    [Fact]
    public void ConfigurationSection_RejectsInvalidId()
    {
        SqlClientAgentConfigurationSection section = LoadSection("not-an-agent");

        Assert.ThrowsAny<ArgumentException>(() => SqlClientAgentRegistration.Parse(section.Id));
    }

    /// <summary>
    /// Verifies a malformed configuration file is consumed rather than escaping as a
    /// TypeInitializationException on the login path.
    /// </summary>
    [Fact]
    public void LoadAgent_MalformedConfigurationFile_YieldsNoAgent()
    {
        string path = WriteConfig("<configuration><SqlClientAgent id=");

        try
        {
            Assert.Equal(0, SqlClientAgentRegistration.LoadAgent(() => OpenSection(path)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies an invalid configured identifier is consumed and yields no agent.</summary>
    [Fact]
    public void LoadAgent_InvalidId_YieldsNoAgent()
        => Assert.Equal(0, SqlClientAgentRegistration.LoadAgent(() => LoadSection("not-an-agent")));

    /// <summary>Verifies a valid configured identifier is loaded.</summary>
    [Fact]
    public void LoadAgent_ValidId_YieldsAgent()
        => Assert.Equal(6, SqlClientAgentRegistration.LoadAgent(() => LoadSection("SqlToolsService")));

    /// <summary>Verifies an absent section yields no agent.</summary>
    [Fact]
    public void LoadAgent_NoSection_YieldsNoAgent()
        => Assert.Equal(0, SqlClientAgentRegistration.LoadAgent(() => null));

    /// <summary>Verifies a section of an unexpected type is consumed and yields no agent.</summary>
    [Fact]
    public void LoadAgent_UnexpectedSectionType_YieldsNoAgent()
        => Assert.Equal(0, SqlClientAgentRegistration.LoadAgent(() => "not-a-section"));

    /// <summary>Verifies a throwing section loader is consumed and yields no agent.</summary>
    [Fact]
    public void LoadAgent_ThrowingLoader_YieldsNoAgent()
        => Assert.Equal(
            0,
            SqlClientAgentRegistration.LoadAgent(
                () => throw new ConfigurationErrorsException("bad configuration")));

    /// <summary>
    /// Load the SqlClientAgent section from a temporary configuration file containing the given id.
    /// </summary>
    private static SqlClientAgentConfigurationSection LoadSection(string id)
    {
        string path = WriteConfig(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<configuration>" +
            "<configSections>" +
            $"<section name=\"{SqlClientAgentConfigurationSection.Name}\" " +
            "type=\"Microsoft.Data.SqlClient.SqlClientAgentConfigurationSection,Microsoft.Data.SqlClient\" />" +
            "</configSections>" +
            $"<{SqlClientAgentConfigurationSection.Name} id=\"{id}\" />" +
            "</configuration>");

        try
        {
            return Assert.IsType<SqlClientAgentConfigurationSection>(OpenSection(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Write the given content to a temporary configuration file and return its path.
    /// </summary>
    private static string WriteConfig(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.config");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Read the SqlClientAgent section from the configuration file at the given path.
    ///
    /// The host configuration file cannot be exercised here because the test host substitutes its
    /// own, so a mapped configuration file is used instead.
    /// </summary>
    private static object OpenSection(string path)
        => ConfigurationManager.OpenMappedExeConfiguration(
                new ExeConfigurationFileMap { ExeConfigFilename = path },
                ConfigurationUserLevel.None)
            .GetSection(SqlClientAgentConfigurationSection.Name);
}
