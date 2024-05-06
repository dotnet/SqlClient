using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace simplesqlclient
{
    internal struct TdsPacketHeader
    {
        public byte PacketType;
        public byte Status;
        public ushort Length;
        public ushort Spid; 
        public byte PacketNumber;
        public byte Window;
    }

    internal struct TdsToken
    {
        public byte TokenType;
        public int Length;

        public readonly byte SQLLenMask => 0x30;

        public readonly byte SQLFixedLen => 0x30;    // Mask to check for fixed token
        public readonly byte SQLVarLen => 0x20;    // Value to check for variable length token
        public readonly byte SQLZeroLen => 0x10;    // Value to check for zero length token
        public readonly byte SQLVarCnt => 0x00;
    }

    internal static class TdsTokens
    {
        public const byte SQLERROR = 0xaa;                   // Token for error messages
        public const byte SQLINFO = 0xab;                    // Token for informational messages
        public const byte SQLLOGINACK = 0xad;                // Token to acknowledge login success
        public const byte SQLENVCHANGE = 0xe3;               // Token for environmental change notifications
        public const byte SQLRETURNVALUE = 0xac;             // Token for return values from procedures
        public const byte SQLRETURNSTATUS = 0x79;            // Token for return status of a procedure
        public const byte SQLCOLNAME = 0xa0;                 // Token for column names in result set
        public const byte SQLCOLFMT = 0xa1;                  // Token for column formatting
        public const byte SQLRESCOLSRCS = 0xa2;              // Token for column source information
        public const byte SQLDATACLASSIFICATION = 0xa3;      // Token for data classification (security, privacy)
        public const byte SQLCOLMETADATA = 0x81;             // Token for column metadata
        public const byte SQLALTMETADATA = 0x88;             // Token for alternative format column metadata
        public const byte SQLTABNAME = 0xa4;                 // Token for table names
        public const byte SQLCOLINFO = 0xa5;                 // Token for additional column info
        public const byte SQLORDER = 0xa9;                   // Token for specifying order in queries
        public const byte SQLALTROW = 0xd3;                  // Token for alternative rows format (deprecated)
        public const byte SQLROW = 0xd1;                     // Token for row data
        public const byte SQLNBCROW = 0xd2;                  // Same as ROW with null-bit-compression support
        public const byte SQLDONE = 0xfd;                    // Token indicating the completion of a command
        public const byte SQLDONEPROC = 0xfe;                // Token indicating the completion of a stored procedure
        public const byte SQLDONEINPROC = 0xff;              // Token indicating the completion of a command within a stored procedure
        public const byte SQLROWCRC = 0x39;                  // Token for row CRC (checksum, unclear usage)
        public const byte SQLSECLEVEL = 0xed;                // Token for security level (unclear usage)
        public const byte SQLPROCID = 0x7c;                  // Token for procedure identification
        public const byte SQLOFFSET = 0x78;                  // Token for data offsets in text and image data
        public const byte SQLSSPI = 0xed;                    // Token for SSPI (Security Support Provider Interface) data
        public const byte SQLFEATUREEXTACK = 0xae;           // Token for acknowledging feature extensions (TDS 7.4)
        public const byte SQLSESSIONSTATE = 0xe4;            // Token for session state data (TDS 7.4)
        public const byte SQLFEDAUTHINFO = 0xee;             // Token for Federal Authentication Information
        public const byte SQLUDT = 0xF0;                     // Token for UDTs
        public const byte SQLXMLTYPE = 0xf1;                // Token for XML data type
    

    }

    internal static class TdsEnvChangeTypes
    {
        public const byte ENV_DATABASE = 1;
        public const byte ENV_LANG = 2;
        public const byte ENV_PACKETSIZE = 4;
        public const byte ENV_COLLATION = 7;
    }


    internal struct PreLoginOption
    {
        public byte Option;
        public int Offset;
        public int Length;
    }

    internal class FeatureExtensionsData
    {
        public SessionRecoveryData sessionRecoveryData = new SessionRecoveryData();
        public ColumnEncryptionData colEncryptionData = new ColumnEncryptionData();
        public GlobalTransactionsFeature globalTransactionsFeature = new GlobalTransactionsFeature();
        public FedAuthFeature fedAuthFeature = new FedAuthFeature();
        public DataClassificationFeature dataClassificationFeature = new DataClassificationFeature();
        public UTF8SupportFeature uTF8SupportFeature = new UTF8SupportFeature();
        public SQLDNSCachingFeature sQLDNSCaching = new SQLDNSCachingFeature();

        public TdsEnums.FeatureExtension requestedFeatures;

        public int Length
        {
            get
            {
                int length = 0;
                if ((requestedFeatures & TdsEnums.FeatureExtension.SessionRecovery) != 0)
                {
                    length += sessionRecoveryData.Length;
                }
                if ((requestedFeatures & TdsEnums.FeatureExtension.FedAuth) != 0)
                {
                    length += fedAuthFeature.Length;
                }
                if ((requestedFeatures & TdsEnums.FeatureExtension.Tce) != 0)
                {
                    length += colEncryptionData.Length;
                }
                if ((requestedFeatures & TdsEnums.FeatureExtension.GlobalTransactions) != 0)
                {
                    length += globalTransactionsFeature.Length;
                }
                if ((requestedFeatures & TdsEnums.FeatureExtension.DataClassification) != 0)
                {
                    length += dataClassificationFeature.Length;
                }
                if ((requestedFeatures & TdsEnums.FeatureExtension.UTF8Support) != 0)
                {
                    length += uTF8SupportFeature.Length;
                }

                if ((requestedFeatures & TdsEnums.FeatureExtension.SQLDNSCaching) != 0)
                {
                    length += sQLDNSCaching.Length;
                }

                length++; // for terminator
                return length;
            }

            set
            {

            }
        }
    }

    
    internal class SQLDNSCachingFeature : IServerFeature
    {
        public uint FeatureExtensionFlag => TdsEnums.FEATUREEXT_SQLDNSCACHING;

        public int Length => 5;

        public int Data => 0;

        public byte[] AckData { get; private set; }

        public void FillData(Span<byte> buffer)
        {
            Debug.Assert(buffer.Length == 4, "Expected a 4 byte buffer for int");
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer, 0);
        }

        public ReadOnlySpan<byte> GetAckData()
        {
            throw new NotImplementedException();
        }

        public void SetAcknowledgedData(Span<byte> buffer)
        {
            this.AckData = buffer.ToArray();
            
        }
    }

    internal class ColumnEncryptionData : IServerFeature
    {
        public static uint FeatureExtensionFlag => TdsEnums.FEATUREEXT_TCE;

        public int Length
        {
            get
            {
                return 6;
            }
        }

        public byte[] AckData { get; private set; }

        public void FillData(Span<byte> buffer)
        {
            Debug.Assert(buffer.Length == 5, "Expected a 5 byte buffer for int");
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer, 1);
            buffer[4] = TdsEnums.MAX_SUPPORTED_TCE_VERSION;
        }

        public ReadOnlySpan<byte> GetAckData()
        {
            return AckData;
        }

        public void SetAcknowledgedData(Span<byte> buffer)
        {
            this.AckData = buffer.ToArray();
            
        }

    }

    internal class GlobalTransactionsFeature : IServerFeature
    {
        public uint FeatureExtensionFlag => TdsEnums.FEATUREEXT_GLOBALTRANSACTIONS;

        public int Length
        {
            get
            {
                return 5;
            }
        }

        public byte[] AckData { get; private set; }

        public void FillData(Span<byte> buffer)
        {
            Debug.Assert(buffer.Length == 4, "Expected a 4 byte buffer for int");
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer, 0);
        }

        public ReadOnlySpan<byte> GetAckData()
        {
            return AckData;
        }

        public void SetAcknowledgedData(Span<byte> buffer)
        {
            this.AckData = buffer.ToArray();
        }
    }

    internal class FedAuthFeature : IServerFeature
    {
        public int Length
        {
            get
            {
                int length = 0;
                int dataLen = 0;
                switch (FedAuthLibrary)
                {
                    case TdsEnums.FedAuthLibrary.MSAL:
                        dataLen = 2;
                        break;
                    case TdsEnums.FedAuthLibrary.SecurityToken:
                        if (AccessToken == null)
                        {
                            Debug.Fail("Access token is null for fedauth feature extension request");
                        }
                        dataLen = 1 + sizeof(int) + AccessToken.Length;
                        break;
                    default:
                        Debug.Fail("Unrecognized library type for fedauth feature extension request");
                        break;
                }
                length = dataLen + 5; // length of feature id (1 byte), data length field (4 bytes), and feature data (dataLen)

                return length;
            }
        }

        internal byte[] AccessToken;
        internal TdsEnums.FedAuthLibrary FedAuthLibrary;
        
        private byte[] AckData { get; set; }

        public void FillData(Span<byte> buffer)
        {
            throw new NotImplementedException();
        }

        public void SetAcknowledgedData(Span<byte> buffer)
        {
            this.AckData = buffer.ToArray();
        }

        public ReadOnlySpan<byte> GetAckData()
        {
            return this.AckData;
        }
    }

    internal struct DataClassificationFeature : IServerFeature
    {
        public uint FeatureExtensionFlag => TdsEnums.FEATUREEXT_DATACLASSIFICATION;

        public int Length
        {
            get
            {
                return 6;
            }
        }

        private byte[] AckData { get; set; }

        public void FillData(Span<byte> buffer)
        {
            Debug.Assert(buffer.Length == 5, "Expected a 5 byte buffer");
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer.Slice(0,4), 1);
            buffer[4] = TdsEnums.DATA_CLASSIFICATION_VERSION_MAX_SUPPORTED;
        }

        public ReadOnlySpan<byte> GetAckData()
        {
            return AckData;
        }

        void IServerFeature.SetAcknowledgedData(Span<byte> buffer)
        {
            this.AckData = buffer.ToArray();
        }
    }

    internal interface IServerFeature
    {
        public int Length { get; }

        public ReadOnlySpan<byte> GetAckData();

        public void FillData(Span<byte> buffer);

        public void SetAcknowledgedData(Span<byte> buffer);
    }

    internal struct UTF8SupportFeature : IServerFeature
    {
        public uint FeatureExtensionFlag => TdsEnums.FEATUREEXT_UTF8SUPPORT;

        public byte[] AckData { get; private set; }

        public int Length
        {
            get
            {
                return 5;
            }
        }

        public void FillData(Span<byte> buffer)
        {
            Debug.Assert(buffer.Length == 4, "Expected a 4 byte buffer for int");
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer, 0);
        }

        public ReadOnlySpan<byte> GetAckData()
        {
            return AckData;
        }

        public void SetAcknowledgedData(Span<byte> buffer)
        {
            this.AckData = buffer.ToArray();
        }
    }

    internal struct SessionRecoveryData
    {
        public int Length
        {
            get
            {
                return 0;
            }
        }
    }


    /// <summary>
    /// Representation of the TDS outgoing login packet.
    /// </summary>
    internal struct LoginPacket
    {
        public int Length
        {
            get
            {
                int length = 94; // Default length of the head of the packet.
                length += 
                    (ClientHostName.Length 
                    + ApplicationName.Length
                    + ServerHostname.Length 
                    + ClientInterfaceName.Length
                    + Language.Length
                    + Database.Length) * 2;
                if (RequestedFeatures != TdsEnums.FeatureExtension.None)
                {
                    length += 4;
                }
                if (UserName != null)
                {
                    length += UserName.Length * 2;
                    length += ObfuscatedPassword.Length;
                }
                if (NewPassword != null || NewPassword.Length != 0)
                {
                    length += NewPassword.Length;
                }
                if (RequestedFeatures != TdsEnums.FeatureExtension.None)
                {
                    length += FeatureExtensionData.Length;
                }
                return length;
            }

            set { }
        }

        public readonly int ProtocolVersion => (0x74 << 24) | (0x00 << 16) | 0x0004;

        public readonly string Language => string.Empty;
        internal string Database { get; set; }
        public byte[] NewPassword { get; set; }
        internal string UserInstance { get; set; }
        public bool IsIntegratedSecurity { get; set; }
        public TdsEnums.FeatureExtension RequestedFeatures { get; set; }
        public int FeatureExtensionOffset { get; internal set; }

        public int PacketSize;

        public int ClientProgramVersion => TdsEnums.CLIENT_PROG_VER;

        public int ProcessIdForTdsLogin;

        public int Login7Flags;

        public byte[] ObfuscatedPassword;

        internal string ClientHostName;
        internal string UserName;
        internal string ServerHostname;
        internal string ApplicationName;
        internal string ClientInterfaceName;

        public FeatureExtensionsData FeatureExtensionData;
        
    }
}
