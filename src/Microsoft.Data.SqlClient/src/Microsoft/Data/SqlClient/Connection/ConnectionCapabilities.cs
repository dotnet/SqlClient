// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    /// This TDS version is used by SQL Server 2005.
    /// </summary>
    private const uint SqlServer2005TdsVersion = 0x72_09_0002;

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
    private const uint SqlServer2012TdsVersion = TdsEnums.TDS7X_VERSION;

    /// <summary>
    /// This TDS version is used by SQL Server 2022 and onwards,
    /// when responding with the TDS 8.x protocol.
    /// </summary>
    private const uint SqlServer2022TdsVersion = TdsEnums.TDS80_VERSION;

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
    /// Indicates that Global Transactions are available (even if not currently enabled.)
    /// Global Transactions are only supported by Azure SQL, and are only available if a
    /// FEATUREEXTACK token of value <c>0x05</c> is received.
    /// </summary>
    public bool GlobalTransactionsAvailable { get; private set; }

    /// <summary>
    /// Indicates support for Global Transactions. This is only supported by
    /// Azure SQL, and is only available if a FEATUREEXTACK token of value
    /// <c>0x05</c> is received.
    /// </summary>
    public bool GlobalTransactionsSupported { get; private set; }

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
    public byte ColumnEncryptionVersion { get; private set; }

    /// <summary>
    /// If column encryption is enabled, the type of enclave reported by the server. This
    /// was introduced in SQL Server 2019, and is only available if a FEATUREEXTACK token
    /// of value <c>0x04</c> is received, and the resultant <see cref="ColumnEncryptionVersion"/>
    /// is <c>2</c> or <c>3</c>.
    /// </summary>
    public string? ColumnEncryptionEnclaveType { get; private set; }

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
        GlobalTransactionsAvailable = false;
        GlobalTransactionsSupported = false;
        EnhancedRouting = false;
        ReadOnlyFailoverPartnerConnection = false;
        Float32VectorType = false;
        JsonType = false;
        ColumnEncryptionVersion = TdsEnums.TCE_NOT_ENABLED;
        ColumnEncryptionEnclaveType = null;
    }

    /// <summary>
    /// Updates the connection capability record based upon the LOGINACK
    /// token stream sent by the server.
    /// </summary>
    /// <param name="loginAck">The LOGINACK token stream sent by the server</param>
    public void ProcessLoginAck(SqlLoginAck loginAck)
    {
        if (loginAck.tdsVersion is not SqlServer2005TdsVersion
            and not SqlServer2008R2TdsVersion
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
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ADV | " +
                    $"Object ID {_objectId}, " +
                    $"Received feature extension acknowledgement for UTF8 support");

                if (featureData.Length < 1)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ERR | " +
                        $"Object ID {_objectId}, " +
                        $"Unknown value for UTF8 support");

                    throw SQL.ParsingError();
                }

                // The server can send and receive UTF8-encoded data if bit 0 of the
                // feature data is set.
                Utf8 = (featureData[0] & 0x01) == 0x01;
                break;

            case TdsEnums.FEATUREEXT_SQLDNSCACHING:
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ADV | " +
                    $"Object ID {_objectId}, " +
                    $"Received feature extension acknowledgement for SQLDNSCACHING");

                if (featureData.Length < 1)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ERR | " +
                        $"Object ID {_objectId}, " +
                        $"Unknown token for SQLDNSCACHING");

                    throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                }

                // The client may cache DNS resolution responses if bit 0 of the feature
                // data is set.
                DnsCaching = (featureData[0] & 0x01) == 0x01;
                break;

            case TdsEnums.FEATUREEXT_DATACLASSIFICATION:
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ADV | " +
                    $"Object ID {_objectId}, " +
                    $"Received feature extension acknowledgement for DATACLASSIFICATION");

                if (featureData.Length != 2)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ERR | " +
                        $"Object ID {_objectId}, " +
                        $"Unknown token for DATACLASSIFICATION");

                    throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                }

                byte dcVersion = featureData[0];

                if (dcVersion == 0x00 ||
                    dcVersion > TdsEnums.DATA_CLASSIFICATION_VERSION_MAX_SUPPORTED)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ERR | " +
                        $"Object ID {_objectId}, " +
                        $"Invalid version number for DATACLASSIFICATION");

                    throw SQL.ParsingErrorValue(
                        ParsingErrorState.DataClassificationInvalidVersion,
                        dcVersion);
                }

                // The feature data is comprised of a single byte containing the version,
                // followed by another byte indicating whether or not data classification is
                // enabled.
                DataClassificationVersion = featureData[1] == 0x00
                    ? TdsEnums.DATA_CLASSIFICATION_NOT_ENABLED
                    : dcVersion;
                break;

            case TdsEnums.FEATUREEXT_GLOBALTRANSACTIONS:
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ADV | " +
                    $"Object ID {_objectId}, " +
                    $"Received feature extension acknowledgement for GlobalTransactions");

                if (featureData.Length < 1)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ERR | " +
                        $"Object ID {_objectId}, " +
                        $"Unknown version number for GlobalTransactions");

                    throw SQL.ParsingError();
                }

                // Feature data is comprised of a single byte which indicates whether
                // global transactions are available.
                GlobalTransactionsAvailable = true;

                GlobalTransactionsSupported = featureData[0] == 0x01;
                break;

            case TdsEnums.FEATUREEXT_AZURESQLSUPPORT:
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ADV | " +
                    $"Object ID {_objectId}, " +
                    $"Received feature extension acknowledgement for AzureSQLSupport");

                if (featureData.Length < 1)
                {
                    throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                }

                IsAzureSql = true;
                // Clients can connect to the failover partner with a read-only AppIntent if bit 0
                // of the only byte in the feature data is set.
                ReadOnlyFailoverPartnerConnection = (featureData[0] & 0x01) == 0x01;

                if (ReadOnlyFailoverPartnerConnection && SqlClientEventSource.Log.IsTraceEnabled())
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ADV | " +
                        $"Object ID {_objectId}, " +
                        $"FailoverPartner enabled with Readonly intent for AzureSQL DB");
                }
                break;

            case TdsEnums.FEATUREEXT_VECTORSUPPORT:
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ADV | " +
                    $"Object ID {_objectId}, " +
                    $"Received feature extension acknowledgement for VECTORSUPPORT");

                if (featureData.Length != 1)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ERR | " +
                        $"Object ID {_objectId}, " +
                        $"Unknown token for VECTORSUPPORT");

                    throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                }

                // Feature data is comprised of a single byte which specifies the version of the vector
                // type which is available.
                Float32VectorType = featureData[0] != 0x00
                    && featureData[0] <= TdsEnums.MAX_SUPPORTED_VECTOR_VERSION;

                if (!Float32VectorType)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ERR | " +
                        $"Object ID {_objectId}, " +
                        $"Invalid version number {featureData[0]} for VECTORSUPPORT, " +
                        $"Max supported version is {TdsEnums.MAX_SUPPORTED_VECTOR_VERSION}");

                    throw SQL.ParsingError();
                }
                break;

            case TdsEnums.FEATUREEXT_JSONSUPPORT:
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ADV | " +
                    $"Object ID {_objectId}, " +
                    $"Received feature extension acknowledgement for JSONSUPPORT");

                if (featureData.Length != 1)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ERR | " +
                        $"Object ID {_objectId}, " +
                        $"Unknown token for JSONSUPPORT");

                    throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                }

                // Feature data is comprised of a single byte which specifies the version of the JSON
                // type which is available.
                JsonType = featureData[0] != 0x00
                    && featureData[0] <= TdsEnums.MAX_SUPPORTED_JSON_VERSION;

                if (!JsonType)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ERR | " +
                        $"Object ID {_objectId}, " +
                        $"Invalid version number for JSONSUPPORT");

                    throw SQL.ParsingError();
                }
                break;

            case TdsEnums.FEATUREEXT_TCE:
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ERR | " +
                    $"Object ID {_objectId}, " +
                    $"Received feature extension acknowledgement for TCE");

                if (featureData.Length < 1)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"{nameof(ConnectionCapabilities)}.{nameof(ProcessFeatureExtAck)} | ERR | " +
                        $"Object ID {_objectId}, " +
                        $"Unknown version number for TCE");

                    throw SQL.ParsingError(ParsingErrorState.TceUnknownVersion);
                }

                // Feature data begins with one byte containing the column encryption version. If
                // this version is 2 or 3, the version is followed by a B_NVARCHAR (i.e., a one-byte
                // string length followed by a Unicode-encoded string containing the enclave type.)
                // NB 1: the MS-TDS specification requires that a client must throw an exception if
                // the column encryption version is 2 and no enclave type is specified. We do not do
                // this.
                // NB 2: although the length is specified, we assume that everything from position 2
                // of the packet and forwards is a Unicode-encoded string.
                ColumnEncryptionVersion = featureData[0];

                if (featureData.Length > 1)
                {
                    ReadOnlySpan<byte> enclaveTypeSpan = featureData.Slice(2);
#if NET
                    ColumnEncryptionEnclaveType = Encoding.Unicode.GetString(enclaveTypeSpan);
#else
                    unsafe
                    {
                        fixed (byte* fDataPtr = enclaveTypeSpan)
                        {
                            ColumnEncryptionEnclaveType = Encoding.Unicode.GetString(fDataPtr, enclaveTypeSpan.Length);
                        }
                    }
#endif
                }

                break;
        }
    }
}
