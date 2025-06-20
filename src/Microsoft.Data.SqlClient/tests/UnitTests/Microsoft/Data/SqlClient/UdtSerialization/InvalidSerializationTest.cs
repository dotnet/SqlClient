// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Data.SqlClient.Server;
using Microsoft.Data.SqlClient.UnitTests.UdtSerialization.SerializedTypes;
using Microsoft.SqlServer.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.UdtSerialization;

/// <summary>
/// Attempts to serialize types which do not meet the requirements for either user-defined or native serialization.
/// </summary>
public class InvalidSerializationTest
{
    /// <summary>
    /// Attempts to serialize a class that does not have the SqlUserDefinedType attribute. Verifies that this fails.
    /// </summary>
    [Fact]
    public void RequiresSqlUserDefinedTypeAttribute()
    {
        using MemoryStream stream = new();

        var exception = Assert.Throws<InvalidUdtException>(
            () => SerializationHelperSql9.Serialize(stream, new ClassMissingSqlUserDefinedTypeAttribute()));

        Assert.Equal($"'{typeof(ClassMissingSqlUserDefinedTypeAttribute).FullName}' is an invalid user defined type, reason: no UDT attribute.", exception.Message);
    }

    /// <summary>
    /// Attempts to serialize a class that has a SqlUserDefinedType attribute, but specifies a Format enumeration value of
    /// Unknown. Verifies that this fails.
    /// </summary>
    [Fact]
    public void CannotSerializeUnknownFormattedType()
    {
        using MemoryStream stream = new();

        var exception = Assert.Throws<ArgumentOutOfRangeException>("Format",
            () => SerializationHelperSql9.Serialize(stream, new UnknownFormattedClass()));

#if NET
        Assert.Equal("The Format enumeration value, 0, is not supported by the format method. (Parameter 'Format')", exception.Message);
#else
        Assert.Equal("The Format enumeration value, Unknown, is not supported by the format method.\r\nParameter name: Format", exception.Message);
#endif
    }
}
