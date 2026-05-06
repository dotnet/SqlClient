// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading;

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// Base class for a transient database object (such as a table, type or
/// stored procedure.)
/// </summary>
public abstract class DatabaseObject : IDisposable
{
    private readonly bool _shouldDrop;

    protected SqlConnection Connection { get; }

    public string Name { get; }

    protected DatabaseObject(SqlConnection connection, string name, string definition, bool shouldCreate, bool shouldDrop)
    {
        _shouldDrop = shouldDrop;

        Connection = connection;
        Name = name;

        if (shouldCreate)
        {
            EnsureConnectionOpen();
            DropObject();
            CreateObject(definition);
        }
    }

    private void EnsureConnectionOpen()
    {
        const int MaxWaits = 2;
        int counter = MaxWaits;

        if (Connection.State is System.Data.ConnectionState.Closed)
        {
            Connection.Open();
        }
        while (counter-- > 0 && Connection.State is System.Data.ConnectionState.Connecting)
        {
            Thread.Sleep(80);
        }
    }

    /// <summary>
    /// Generate a new GUID and return the characters from its 1st and 4th
    /// parts, as shown here:
    ///
    /// <code>
    ///   7ff01cb8-88c7-11f0-b433-00155d7e531e
    ///   ^^^^^^^^           ^^^^
    /// </code>
    ///
    /// These 12 characters are concatenated together without any
    /// separators.  These 2 parts typically comprise a timestamp and clock
    /// sequence, most likely to be unique for tests that generate names in
    /// quick succession.
    /// </summary>
    private static string GetGuidParts()
    {
        var guid = Guid.NewGuid().ToString();
        // GOTCHA: The slice operator is inclusive of the start index and
        // exclusive of the end index!
        return guid.Substring(0, 8) + guid.Substring(19, 4);
    }

    /// <summary>
    /// Generate a long unique database object name, whose maximum length is
    /// 96 characters, with the format:
    ///
    ///   <c>{Prefix}_{GuidParts}_{UserName}_{MachineName}</c>
    ///
    /// The Prefix will be truncated to satisfy the overall maximum length.
    ///
    /// The GUID Parts will be the characters from the 1st and 4th blocks
    /// from a traditional string representation, as shown here:
    ///
    /// <code>
    ///   7ff01cb8-88c7-11f0-b433-00155d7e531e
    ///   ^^^^^^^^           ^^^^
    /// </code>
    ///
    /// These 2 parts typically comprise a timestamp and clock sequence,
    /// most likely to be unique for tests that generate names in quick
    /// succession.  The 12 characters are concatenated together without any
    /// separators.
    ///
    /// The UserName and MachineName are obtained from the Environment,
    /// and will be truncated to satisfy the maximum overall length.
    /// </summary>
    ///
    /// <param name="prefix">
    /// The prefix to use when generating the unique name, truncated to at
    /// most 32 characters.
    ///
    /// This should not contain any characters that cannot be used in
    /// database object names.  See:
    ///
    /// https://learn.microsoft.com/en-us/sql/relational-databases/databases/database-identifiers?view=sql-server-ver17#rules-for-regular-identifiers
    /// </param>
    ///
    /// <param name="escape">
    /// When true, the entire generated name will be enclosed in square
    /// brackets, for example:
    ///
    ///   <c>[MyPrefix_7ff01cb811f0_test_user_ci_agent_machine_name]</c>
    /// </param>
    ///
    /// <returns>
    /// A unique database object name, no more than 96 characters long.
    /// </returns>
    public static string GenerateLongName(string prefix, bool escape = true)
    {
        StringBuilder name = new(96);

        if (escape)
        {
            name.Append('[');
        }

        if (prefix.Length > 32)
        {
            prefix = prefix.Substring(0, 32);
        }

        name.Append(prefix);
        name.Append('_');
        name.Append(GetGuidParts());
        name.Append('_');

        var suffix =
          Environment.UserName + '_' +
          Environment.MachineName;

        int maxSuffixLength = 96 - name.Length;
        if (escape)
        {
            --maxSuffixLength;
        }
        if (suffix.Length > maxSuffixLength)
        {
            suffix = suffix.Substring(0, maxSuffixLength);
        }

        name.Append(suffix);

        if (escape)
        {
            name.Append(']');
        }

        return name.ToString();
    }

    /// <summary>
    /// Generate a short unique database object name, whose maximum length
    /// is 30 characters, with the format:
    ///
    ///   <c>{Prefix}_{GuidParts}</c>
    ///
    /// The Prefix will be truncated to satisfy the overall maximum length.
    ///
    /// The GUID parts will be the characters from the 1st and 4th blocks
    /// from a traditional string representation, as shown here:
    /// 
    /// <code>
    ///   7ff01cb8-88c7-11f0-b433-00155d7e531e
    ///   ^^^^^^^^           ^^^^
    /// </code>
    ///
    /// These 2 parts typically comprise a timestamp and clock sequence,
    /// most likely to be unique for tests that generate names in quick
    /// succession.  The 12 characters are concatenated together without any
    /// separators.
    /// </summary>
    ///
    /// <param name="prefix">
    /// The prefix to use when generating the unique name, truncated to at
    /// most 18 characters when withBracket is false, and 16 characters when
    /// withBracket is true.
    ///
    /// This should not contain any characters that cannot be used in
    /// database object names.  See:
    ///
    /// https://learn.microsoft.com/en-us/sql/relational-databases/databases/database-identifiers?view=sql-server-ver17#rules-for-regular-identifiers
    /// </param>
    ///
    /// <param name="escape">
    /// When true, the entire generated name will be enclosed in square
    /// brackets, for example:
    ///
    ///   <c>[MyPrefix_7ff01cb811f0]</c>
    /// </param>
    ///
    /// <returns>
    /// A unique database object name, no more than 30 characters long.
    /// </returns>
    public static string GenerateShortName(string prefix, bool escape = true)
    {
        StringBuilder name = new(30);

        if (escape)
        {
            name.Append('[');
        }

        int maxPrefixLength = escape ? 16 : 18;
        if (prefix.Length > maxPrefixLength)
        {
            prefix = prefix.Substring(0, maxPrefixLength);
        }

        name.Append(prefix);
        name.Append('_');
        name.Append(GetGuidParts());

        if (escape)
        {
            name.Append(']');
        }

        return name.ToString();
    }

    /// <summary>
    /// Creates the object with a given definition.
    /// </summary>
    /// <param name="definition">Definition of the object to create.</param>
    /// <remarks>
    /// By the time this is called, <see cref="Connection"/> will be open.
    /// </remarks>
    protected abstract void CreateObject(string definition);

    /// <summary>
    /// Drops the object created by <see cref="CreateObject"/>.
    /// </summary>
    /// <remarks>
    /// By the time this is called, <see cref="Connection"/> will be open.
    /// Must not throw an exception if the object does not exist.
    /// </remarks>
    protected abstract void DropObject();

    public void Dispose()
    {
        if (_shouldDrop)
        {
            EnsureConnectionOpen();
            DropObject();
        }
        // This explicitly does not drop the wrapped SqlConnection; this is sometimes
        // used in a loop to create multiple UDTs.

        GC.SuppressFinalize(this);
    }
}
