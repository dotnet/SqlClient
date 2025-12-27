// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Describes the capabilities and related information (such as the
/// reported server version and TDS version) of the connection.
/// </summary>
internal sealed class ConnectionCapabilities
{
    /// <summary>
    /// This TDS version is used by SQL Server 2008 R2.
    /// </summary>
    private const uint SqlServer2008R2TdsVersion = 0x73_0B_0003;
    /// <summary>
    /// This TDS version is used by SQL Server 2012 and onwards.
    /// In SQL Server 2022 and SQL Server 2025, this is used when
    /// the SQL Server instance is responding with the TDS 7.x
    /// protocol.
    /// </summary>
    private const uint SqlServer2012TdsVersion = 0x74_00_0004;
    /// <summary>
    /// This TDS version is used by SQL Server 2022 and onwards,
    /// when responding with the TDS 8.x protocol.
    /// </summary>
    private const uint SqlServer2022TdsVersion = 0x08_00_0000;

    private readonly int _objectId;

    /// <summary>
    /// The TDS version reported by the LoginAck response
    /// from the server.
    /// </summary>
    public uint TdsVersion { get; private set; }

    /// <summary>
    /// The SQL Server major version reported by the LoginAck
    /// response from the server.
    /// </summary>
    public byte ServerMajorVersion { get; private set; }

    /// <summary>
    /// The SQL Server minor version reported by the LoginAck
    /// response from the server.
    /// </summary>
    public byte ServerMinorVersion { get; private set; }

    /// <summary>
    /// The SQL Server build number reported by the LoginAck
    /// response from the server.
    /// </summary>
    public ushort ServerBuildNumber { get; private set; }

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
        Is2012OrNewer || TdsVersion == SqlServer2008R2TdsVersion;

    /// <summary>
    /// If true (as determined by the value of <see cref="TdsVersion"/>)
    /// then the connection is to SQL Server 2012 or newer.
    /// </summary>
    public bool Is2012OrNewer =>
        Is2022OrNewer || TdsVersion == SqlServer2012TdsVersion;

    /// <summary>
    /// If true (as determined by the value of <see cref="TdsVersion"/>)
    /// then the connection is to SQL Server 2022 or newer.
    /// </summary>
    public bool Is2022OrNewer =>
        TdsVersion == SqlServer2022TdsVersion;

    /// <summary>
    /// If true, this connection is to an Azure SQL instance. This is determined
    /// by the receipt of a FEATUREEXTACK token of value <c>0x08</c>.
    /// </summary>
    public bool IsAzureSql { get; private set; }

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
    public bool Utf8 { get; private set; }

    /// <summary>
    /// Indicates support for the client to cache DNS resolution responses for
    /// the server. This is only supported by Azure SQL, and is only available
    /// if a FEATUREEXTACK token of value <c>0x0B</c> is received.
    /// </summary>
    public bool DnsCaching { get; private set; }

    /// <summary>
    /// Indicates support for Data Classification and specifies the version of
    /// Data Classification which is supported. This was introduced in SQL
    /// Server 2019, and is only available if a FEATUREEXTACK token of value
    /// <c>0x09</c> is received.
    /// </summary>
    /// <remarks>
    /// This should only be <c>1</c> or <c>2</c>.
    /// </remarks>
    public byte DataClassificationVersion { get; private set; }

    /// <summary>
    /// Indicates support for Global Transactions. This is only supported by
    /// Azure SQL, and is only available if a FEATUREEXTACK token of value
    /// <c>0x05</c> is received.
    /// </summary>
    public bool GlobalTransactions { get; private set; }

    /// <summary>
    /// Indicates support for Enhanced Routing. This is only supported by
    /// Azure SQL, and is only available if a FEATUREEXTACK token of value
    /// <c>0x0F</c> is received.
    /// </summary>
    public bool EnhancedRouting { get; private set; }

    /// <summary>
    /// Indicates support for connecting to the current connection's failover
    /// partner with an Application Intent of ReadOnly. This is only supported
    /// by Azure SQL, and is only available if a FEATUREEXTACK token of value
    /// <c>0x08</c> is received, and if bit zero of this token's data is set.
    /// </summary>
    public bool ReadOnlyFailoverPartnerConnection { get; private set; }

    /// <summary>
    /// Indicates support for the <c>vector</c> data type, with a backing type
    /// of <c>float32</c>. This was introduced in SQL Server 2022, and is only
    /// available if a FEATUREEXTACK token of value <c>0x0E</c> is received, and
    /// if the version in this token's data is greater than or equal to <c>1</c>.
    /// </summary>
    public bool Float32VectorType { get; private set; }

    /// <summary>
    /// Indicates support for the <c>json</c> data type. This was introduced in
    /// SQL Server 2022, and is only available if a FEATUREEXTACK token of value
    /// <c>0x0D</c> is received, and if the version in this token's data is
    /// greater than or equal to <c>1</c>.
    /// </summary>
    public bool JsonType { get; private set; }

    public ConnectionCapabilities(int parentObjectId)
    {
        _objectId = parentObjectId;
    }

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
        GlobalTransactions = false;
        EnhancedRouting = false;
        ReadOnlyFailoverPartnerConnection = false;
        Float32VectorType = false;
        JsonType = false;
    }

    /// <summary>
    /// Updates the connection capability record based upon the LOGINACK
    /// token stream sent by the server.
    /// </summary>
    /// <param name="loginAck">The LOGINACK token stream sent by the server</param>
    public void ProcessLoginAck(SqlLoginAck loginAck)
    {
        if (loginAck.tdsVersion is not SqlServer2008R2TdsVersion
            and not SqlServer2012TdsVersion
            and not SqlServer2022TdsVersion)
        {
            throw SQL.InvalidTDSVersion();
        }

        TdsVersion = loginAck.tdsVersion;
        ServerMajorVersion = loginAck.majorVersion;
        ServerMinorVersion = loginAck.minorVersion;
        ServerBuildNumber = (ushort)loginAck.buildNum;
    }

    public void ProcessFeatureExtAck(byte featureId, ReadOnlySpan<byte> featureData)
    {
        switch (featureId)
        {
            case TdsEnums.FEATUREEXT_UTF8SUPPORT:
                // The server can send and receive UTF8-encoded data if bit 0 of the
                // feature data is set.
                Utf8 = !featureData.IsEmpty && (featureData[0] & 0x01) == 0x01;
                break;

            case TdsEnums.FEATUREEXT_SQLDNSCACHING:
                // The client may cache DNS resolution responses if bit 0 of the feature
                // data is set.
                DnsCaching = !featureData.IsEmpty && (featureData[0] & 0x01) == 0x01;
                break;

            case TdsEnums.FEATUREEXT_DATACLASSIFICATION:
                // The feature data is comprised of a single byte containing the version,
                // followed by another byte indicating whether or not data classification is
                // enabled.
                DataClassificationVersion =
                    featureData.Length == 2
                        && featureData[1] == 0x00
                        && featureData[0] > 0x00
                        && featureData[0] <= TdsEnums.DATA_CLASSIFICATION_VERSION_MAX_SUPPORTED
                    ? featureData[0]
                    : TdsEnums.DATA_CLASSIFICATION_NOT_ENABLED;
                break;

            case TdsEnums.FEATUREEXT_GLOBALTRANSACTIONS:
                // Feature data is comprised of a single byte which indicates whether
                // global transactions are available.
                GlobalTransactions = !featureData.IsEmpty && featureData[0] == 0x01;
                break;

            case TdsEnums.FEATUREEXT_AZURESQLSUPPORT:
                IsAzureSql = true;
                // Clients can connect to the failover partner with a read-only AppIntent if bit 0
                // of the only byte in the feature data is set.
                ReadOnlyFailoverPartnerConnection = !featureData.IsEmpty && (featureData[0] & 0x01) == 0x01;
                break;

            case TdsEnums.FEATUREEXT_VECTORSUPPORT:
                // Feature data is comprised of a single byte which specifies the version of the vector
                // type which is available.
                Float32VectorType = !featureData.IsEmpty && featureData[0] != 0x00
                    && featureData[0] <= TdsEnums.MAX_SUPPORTED_VECTOR_VERSION;
                break;

            case TdsEnums.FEATUREEXT_JSONSUPPORT:
                // Feature data is comprised of a single byte which specifies the version of the JSON
                // type which is available.
                JsonType = !featureData.IsEmpty && featureData[0] != 0x00
                    && featureData[0] <= TdsEnums.MAX_SUPPORTED_JSON_VERSION;
                break;

            default:
                throw SQL.ParsingError(ParsingErrorState.UnrequestedFeatureAckReceived);
        }
    }
}
