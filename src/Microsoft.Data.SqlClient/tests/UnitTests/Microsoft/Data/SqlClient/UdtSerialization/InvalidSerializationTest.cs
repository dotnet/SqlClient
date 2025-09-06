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
public sealed class InvalidSerializationTest : IDisposable
{
    private readonly MemoryStream _stream;

    /// <summary>
    /// Initializes the MemoryStream used for all tests in this class.
    /// </summary>
    public InvalidSerializationTest()
    {
        _stream = new MemoryStream();
    }

    void IDisposable.Dispose()
    {
        _stream.Dispose();
    }

    /// <summary>
    /// Attempts to serialize a class that does not have the SqlUserDefinedType attribute. Verifies that this fails.
    /// </summary>
    [Fact]
    public void Serialize_MissingSqlUserDefinedTypeAttribute_Throws()
    {
        Action serialize = () => SerializationHelperSql9.Serialize(_stream, new ClassMissingSqlUserDefinedTypeAttribute());
        var exception = Assert.Throws<InvalidUdtException>(serialize);

        Assert.Equal($"'{typeof(ClassMissingSqlUserDefinedTypeAttribute).FullName}' is an invalid user defined type, reason: no UDT attribute.", exception.Message);
    }

    /// <summary>
    /// Attempts to serialize a class that has a SqlUserDefinedType attribute, but specifies a Format enumeration value of
    /// Unknown. Verifies that this fails.
    /// </summary>
    [Fact]
    public void Serialize_UnknownFormattedType_Throws()
    {
        Action serialize = () => SerializationHelperSql9.Serialize(_stream, new UnknownFormattedClass());
        var exception = Assert.Throws<ArgumentOutOfRangeException>("Format", serialize);

#if NET
        Assert.Equal("The Format enumeration value, 0, is not supported by the format method. (Parameter 'Format')", exception.Message);
#else
        Assert.Equal("The Format enumeration value, Unknown, is not supported by the format method.\r\nParameter name: Format", exception.Message);
#endif
    }
}
