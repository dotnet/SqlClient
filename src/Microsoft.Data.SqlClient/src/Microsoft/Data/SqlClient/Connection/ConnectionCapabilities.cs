// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Internal;
using System;
using System.Text;

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Describes the capabilities and related information (such as the
/// reported server version and TDS version) of the connection.
/// </summary>
internal sealed class ConnectionCapabilities
{
    /// <summary>
    /// The TDS version reported by the LoginAck response
    /// from the server.
    /// </summary>
    /// <remarks>
    /// <see cref="TdsEnums.SQL2005_VERSION"/> is negotiated for SQL Server 2005.
    /// <see cref="TdsEnums.SQL2008_VERSION"/> is negotiated for SQL Server 2008.
    /// <see cref="TdsEnums.TDS7X_VERSION"/> is negotiated for SQL Server 2012 and upwards using TDS 7.x.
    /// <see cref="TdsEnums.TDS80_VERSION"/> is negotiated for the TDS 8.0 flow in SQL Server 2022 and upwards.
    /// </remarks>
    public uint TdsVersion { get; set; }

    /// <summary>
    /// The SQL Server major version reported by the LoginAck
    /// response from the server.
    /// </summary>
    public byte ServerMajorVersion { get; set; }

    /// <summary>
    /// The SQL Server minor version reported by the LoginAck
    /// response from the server.
    /// </summary>
    public byte ServerMinorVersion { get; set; }

    /// <summary>
    /// The SQL Server build number reported by the LoginAck
    /// response from the server.
    /// </summary>
    public ushort ServerBuildNumber { get; set; }

    /// <summary>
    /// The user-friendly SQL Server version reported by the
    /// LoginAck response from the server.
    /// </summary>
    public string ServerVersion =>
        $"{ServerMajorVersion:00}.{ServerMinorVersion:00}.{ServerBuildNumber:0000}";

    /// <summary>
    /// If true (as determined by the value of <see cref="TdsVersion"/>)
    /// then the connection is to SQL Server 2008 R2 or newer.
    /// </summary>
    public bool Is2008R2OrNewer =>
        Is2012OrNewer || TdsVersion is TdsEnums.SQL2008_VERSION;

    /// <summary>
    /// If true (as determined by the value of <see cref="TdsVersion"/>)
    /// then the connection is to SQL Server 2012 or newer.
    /// </summary>
    public bool Is2012OrNewer =>
        TdsVersion is TdsEnums.TDS7X_VERSION or TdsEnums.TDS80_VERSION;

    /// <summary>
    /// If true, this connection is to an Azure SQL instance. This is determined
    /// by the receipt of a FEATUREEXTACK token of value <c>0x08</c>.
    /// </summary>
    public bool IsAzureSql { get; set; }

    /// <summary>
    /// Indicates support for user-defined CLR types (up to a length of 8000
    /// bytes.) This was introduced in SQL Server 2005.
    /// </summary>
    public bool UserDefinedTypes => true;

    /// <summary>
    /// Indicates support for the <c>xml</c> data type. This was introduced
    /// in SQL Server 2005.
    /// </summary>
    public bool XmlDataType => true;

    /// <summary>
    /// Indicates support for the <c>date</c>, <c>time</c>, <c>datetime2</c>
    /// and <c>datetimeoffset</c> data types. These were introduced in SQL
    /// Server 2008.
    /// </summary>
    public bool ExpandedDateTimeDataTypes => Is2008R2OrNewer;

    /// <summary>
    /// Indicates support for user-defined CLR types of any length. This
    /// was introduced in SQL Server 2008.
    /// </summary>
    public bool LargeUserDefinedTypes => Is2008R2OrNewer;

    /// <summary>
    /// Indicates support for the client to include a TDS trace header,
    /// which is surfaced in XEvents traces to correlate events between
    /// the client and the server. This was introduced in SQL Server 2012.
    /// </summary>
    public bool TraceHeader => Is2012OrNewer;

    /// <summary>
    /// Indicates support for UTF8 collations. This was introduced in SQL
    /// Server 2019, and is only available if a FEATUREEXTACK token of value
    /// <c>0x0A</c> is received.
    /// </summary>
    public bool Utf8 { get; set; }

    /// <summary>
    /// Indicates support for the client to cache DNS resolution responses for
    /// the server. This is only supported by Azure SQL, and is only available
    /// if a FEATUREEXTACK token of value <c>0x0B</c> is received.
    /// </summary>
    public bool DnsCaching { get; set; }

    /// <summary>
    /// Indicates support for Data Classification and specifies the version of
    /// Data Classification which is supported. This was introduced in SQL
    /// Server 2019, and is only available if a FEATUREEXTACK token of value
    /// <c>0x09</c> is received.
    /// </summary>
    /// <remarks>
    /// This should only be <c>1</c> or <c>2</c>.
    /// </remarks>
    public byte DataClassificationVersion { get; set; }

    /// <summary>
    /// Indicates that Global Transactions are available (even if not currently enabled.)
    /// Global Transactions are only supported by Azure SQL, and are only available if a
    /// FEATUREEXTACK token of value <c>0x05</c> is received.
    /// </summary>
    public bool GlobalTransactionsAvailable { get; set; }

    /// <summary>
    /// Indicates support for Global Transactions. This is only supported by
    /// Azure SQL, and is only available if a FEATUREEXTACK token of value
    /// <c>0x05</c> is received.
    /// </summary>
    public bool GlobalTransactionsSupported { get; set; }

    /// <summary>
    /// Indicates support for Enhanced Routing. This is only supported by
    /// Azure SQL, and is only available if a FEATUREEXTACK token of value
    /// <c>0x0F</c> is received.
    /// </summary>
    public bool EnhancedRouting { get; set; }

    /// <summary>
    /// Indicates support for connecting to the current connection's failover
    /// partner with an Application Intent of ReadOnly. This is only supported
    /// by Azure SQL, and is only available if a FEATUREEXTACK token of value
    /// <c>0x08</c> is received, and if bit zero of this token's data is set.
    /// </summary>
    public bool ReadOnlyFailoverPartnerConnection { get; set; }

    /// <summary>
    /// Indicates support for the <c>vector</c> data type, with a backing type
    /// of <c>float32</c>. This was introduced in SQL Server 2022, and is only
    /// available if a FEATUREEXTACK token of value <c>0x0E</c> is received, and
    /// if the version in this token's data is greater than or equal to <c>1</c>.
    /// </summary>
    public bool Float32VectorType { get; set; }

    /// <summary>
    /// Indicates support for the <c>json</c> data type. This was introduced in
    /// SQL Server 2022, and is only available if a FEATUREEXTACK token of value
    /// <c>0x0D</c> is received, and if the version in this token's data is
    /// greater than or equal to <c>1</c>.
    /// </summary>
    public bool JsonType { get; set; }

    /// <summary>
    /// Indicates support for column encryption and specifies the version of column
    /// encryption which is supported. This was introduced in SQL Server 2016, and is
    /// only available if a FEATUREEXTACK token of value <c>0x04</c> is received.
    /// </summary>
    /// <remarks>
    /// This should only be <c>1</c>, <c>2</c> or <c>3</c>. v1 is supported from SQL
    /// Server 2016 upwards, v2 is supported from SQL Server 2019 upwards, v3 is supported
    /// from SQL Server 2022 upwards.
    /// </remarks>
    public byte ColumnEncryptionVersion { get; set; }

    /// <summary>
    /// If column encryption is enabled, the type of enclave reported by the server. This
    /// was introduced in SQL Server 2019, and is only available if a FEATUREEXTACK token
    /// of value <c>0x04</c> is received, and the resultant <see cref="ColumnEncryptionVersion"/>
    /// is <c>2</c> or <c>3</c>.
    /// </summary>
    public string? ColumnEncryptionEnclaveType { get; set; }

    /// <summary>
    /// Returns the capability records to unset values.
    /// </summary>
    public void Reset()
    {
        TdsVersion = 0;
        ServerMajorVersion = 0;
        ServerMinorVersion = 0;
        ServerBuildNumber = 0;

        IsAzureSql = false;
        Utf8 = false;
        DnsCaching = false;
        DataClassificationVersion = TdsEnums.DATA_CLASSIFICATION_NOT_ENABLED;
        GlobalTransactionsAvailable = false;
        GlobalTransactionsSupported = false;
        EnhancedRouting = false;
        ReadOnlyFailoverPartnerConnection = false;
        Float32VectorType = false;
        JsonType = false;
        ColumnEncryptionVersion = TdsEnums.TCE_NOT_ENABLED;
        ColumnEncryptionEnclaveType = null;
    }
}
