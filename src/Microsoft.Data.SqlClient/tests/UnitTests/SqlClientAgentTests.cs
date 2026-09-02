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
    /// <summary>Ensures published Microsoft agent identifiers remain stable.</summary>
    [Fact]
    public void KnownAgentIdentifiers_AreStable()
    {
        Assert.Equal((ushort)1, (ushort)SqlClientAgent.EntityFramework);
        Assert.Equal((ushort)2, (ushort)SqlClientAgent.SemanticKernel);
        Assert.Equal((ushort)3, (ushort)SqlClientAgent.ManagementStudio);
        Assert.Equal((ushort)4, (ushort)SqlClientAgent.SqlManagementObjects);
        Assert.Equal((ushort)5, (ushort)SqlClientAgent.DataTierApplicationFramework);
        Assert.Equal((ushort)6, (ushort)SqlClientAgent.SqlToolsService);
        Assert.Equal((ushort)7, (ushort)SqlClientAgent.AspNetCoreDistributedSqlServerCache);
        Assert.Equal((ushort)8, (ushort)SqlClientAgent.EntityFramework6);
        Assert.Equal((ushort)9, (ushort)SqlClientAgent.AzureFunctionsSqlExtension);
        Assert.Equal((ushort)10, (ushort)SqlClientAgent.OrleansAdoNet);
        Assert.Equal((ushort)11, (ushort)SqlClientAgent.DurableTaskSqlServer);
    }

    /// <summary>Verifies configuration accepts enum names and forward-compatible numeric identifiers.</summary>
    [Theory]
    [InlineData("SqlToolsService", 6)]
    [InlineData("42", 42)]
    public void Parse_AcceptsNamedAndNumericIdentifiers(string value, ushort expected)
        => Assert.Equal(expected, (ushort)SqlClientAgentRegistration.Parse(value));

    /// <summary>Verifies invalid and zero identifiers are rejected.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("not-an-agent")]
    [InlineData("0")]
    // Enum.TryParse would otherwise combine these into an unrelated agent.
    [InlineData("EntityFramework,SemanticKernel")]
    [InlineData("1,2")]
    public void Parse_RejectsInvalidIdentifiers(string value)
        => Assert.ThrowsAny<ArgumentException>(() => SqlClientAgentRegistration.Parse(value));

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
    public void ConfigurationSection_YieldsAgent(string id, ushort expected)
    {
        SqlClientAgentConfigurationSection section = LoadSection(id);

        Assert.Equal(expected, (ushort)SqlClientAgentRegistration.Parse(section.Id));
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
    /// Load the SqlClientAgent section from a temporary configuration file containing the given id.
    /// </summary>
    private static SqlClientAgentConfigurationSection LoadSection(string id)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.config");
        File.WriteAllText(
            path,
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
            Configuration configuration = ConfigurationManager.OpenMappedExeConfiguration(
                new ExeConfigurationFileMap { ExeConfigFilename = path },
                ConfigurationUserLevel.None);

            return Assert.IsType<SqlClientAgentConfigurationSection>(
                configuration.GetSection(SqlClientAgentConfigurationSection.Name));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
