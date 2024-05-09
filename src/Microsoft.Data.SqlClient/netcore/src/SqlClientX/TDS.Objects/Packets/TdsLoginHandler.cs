using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.TDS.Objects.Packets
{
    internal class TdsLoginHandler : OutgoingPacketHandler
    {
        private enum States
        {
            None,
            LoginInformationWritten,

        }
        private TdsWriteStream _writeStream;

        /// <summary>
        /// These are the features requested by default. 
        /// </summary>
        internal LucidTdsEnums.FeatureExtension DefaultRequestedFeatures
        {
            get 
            {
                return LucidTdsEnums.FeatureExtension.None  
                    | LucidTdsEnums.FeatureExtension.GlobalTransactions
                    | LucidTdsEnums.FeatureExtension.DataClassification
                    | LucidTdsEnums.FeatureExtension.Tce
                    | LucidTdsEnums.FeatureExtension.UTF8Support
                    | LucidTdsEnums.FeatureExtension.SQLDNSCaching;
            }
        }

        private AuthenticationOptions _authOptions;

        internal LucidTdsEnums.FeatureExtension RequestedFeatures { get; private set; }

        protected override byte PacketHeaderType => TdsEnums.MT_LOGIN7;

        // We want to make sure that no one can write the data out of order.
        // We will use the state to make sure that each build function can be 
        // called in order that is expected.
        private States _currentState = States.None;

        public TdsLoginHandler(TdsWriteStream writeStream, AuthenticationOptions authOptions,
            LucidTdsEnums.FeatureExtension additionalFeatures = LucidTdsEnums.FeatureExtension.None) 
        {
            _writeStream = writeStream;
            _writeStream.PacketHeaderType = PacketHeaderType;
            _authOptions = authOptions;
            RequestedFeatures = DefaultRequestedFeatures | additionalFeatures;
        }
        
        public async ValueTask<TdsLoginHandler> Send(
            LoginPacket packet, 
            bool isAsync, 
            CancellationToken ct)
        {
            Debug.Assert(_currentState == States.None, "The Set Login information cannot be called if the state is already initialized");
            //TdsEnums.FeatureExtension requestedFeatures = TdsEnums.FeatureExtension.None;
            //requestedFeatures |= TdsEnums.FeatureExtension.GlobalTransactions
            //    | TdsEnums.FeatureExtension.DataClassification
            //    | TdsEnums.FeatureExtension.Tce
            //    | TdsEnums.FeatureExtension.UTF8Support
            //    | TdsEnums.FeatureExtension.SQLDNSCaching;

            packet.RequestedFeatures = RequestedFeatures;
            packet.FeatureExtensionData.requestedFeatures = RequestedFeatures;

            int length = packet.Length;
            await _writeStream.WriteIntAsync(length, isAsync, ct).ConfigureAwait(false);
            // Write TDS Version. We support 7.4
            await _writeStream.WriteIntAsync(packet.ProtocolVersion, isAsync, ct).ConfigureAwait(false);
            // Negotiate the packet size.
            await _writeStream.WriteIntAsync(packet.PacketSize, isAsync, ct).ConfigureAwait(false);
            // Client Prog Version
            await _writeStream.WriteIntAsync(packet.ClientProgramVersion, isAsync, ct).ConfigureAwait(false);
            // Current Process Id
            await _writeStream.WriteIntAsync(packet.ProcessIdForTdsLogin, isAsync, ct).ConfigureAwait(false);
            // Unused Connection Id 
            await _writeStream.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);

            int log7Flags = AssembleLogin7Flags(packet);

            await _writeStream.WriteIntAsync(log7Flags, isAsync, ct).ConfigureAwait(false);
            // Time Zone
            await _writeStream.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);

            // LCID
            await _writeStream.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);

            int offset = LucidTdsEnums.SQL2005_LOG_REC_FIXED_LEN;

            offset = await WriteOffSetAndLengthForString(isAsync, offset, packet.ClientHostName, ct).ConfigureAwait(false);

            offset = await WriteAuthenticationOffsetLengthDetails(isAsync, offset, ct).ConfigureAwait(false);

            offset = await WriteOffSetAndLengthForString(isAsync, offset, packet.ApplicationName, ct).ConfigureAwait(false);

            offset = await WriteOffSetAndLengthForString(isAsync, offset, packet.ServerHostname, ct).ConfigureAwait(false);

            int featureExtenionIsBeingUsed = 4;
            await _writeStream.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false);
            // Feature extension being used 
            await _writeStream.WriteShortAsync(featureExtenionIsBeingUsed, isAsync, ct).ConfigureAwait(false);

            offset += 4;

            offset = await WriteOffSetAndLengthForString(isAsync, offset, packet.ClientInterfaceName, ct).ConfigureAwait(false);
            offset = await WriteOffSetAndLengthForString(isAsync, offset, packet.Language, ct).ConfigureAwait(false);
            offset = await WriteOffSetAndLengthForString(isAsync, offset, packet.Database, ct).ConfigureAwait(false);

            byte[] nicAddress = new byte[LucidTdsEnums.MAX_NIC_SIZE];
            Random random = new Random();
            random.NextBytes(nicAddress);
            await _writeStream.WriteArrayAsync(isAsync, nicAddress, ct).ConfigureAwait(false);

            await _writeStream.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false);

            // No Integrated Auth
            await _writeStream.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);

            // Attach DB Filename
            offset = await WriteOffSetAndLengthForString(isAsync, offset, string.Empty, ct).ConfigureAwait(false);

            await _writeStream.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false);
            await _writeStream.WriteShortAsync(packet.NewPassword.Length / 2, isAsync, ct).ConfigureAwait(false);
            // reserved for chSSPI
            await _writeStream.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);
            await _writeStream.WriteStringAsync(packet.ClientHostName, isAsync, ct).ConfigureAwait(false);
            // Consider User Name auth only
            await _writeStream.WriteStringAsync(packet.UserName, isAsync, ct).ConfigureAwait(false);
            await _writeStream.WriteArrayAsync(isAsync, packet.ObfuscatedPassword, ct).ConfigureAwait(false);
            await _writeStream.WriteStringAsync(packet.ApplicationName, isAsync, ct).ConfigureAwait(false);
            await _writeStream.WriteStringAsync(packet.ServerHostname, isAsync, ct).ConfigureAwait(false);
            await _writeStream.WriteIntAsync(packet.Length - packet.FeatureExtensionData.Length, isAsync, ct).ConfigureAwait(false);
            await _writeStream.WriteStringAsync(packet.ClientInterfaceName, isAsync, ct).ConfigureAwait(false);
            await _writeStream.WriteStringAsync(packet.Language, isAsync, ct).ConfigureAwait(false);
            await _writeStream.WriteStringAsync(packet.Database, isAsync, ct).ConfigureAwait(false);
            // Attach DB File Name
            await _writeStream.WriteStringAsync(string.Empty, isAsync, ct).ConfigureAwait(false);
            await _writeStream.WriteArrayAsync(isAsync, packet.NewPassword, ct).ConfigureAwait(false);
            // Apply feature extension data

            FeatureExtensionsData featureExtensionData = packet.FeatureExtensionData;


            int totalFeatureExtensionDataSize = 5 // TC + 
                + 4 // Global transaction
                + 5 // Data Classification
                + 4 // UTF8
                + 4 // DNS Caching
                + 5; // For the feature extension flags 
            byte[] bytesFeatureExtensionData = ArrayPool<byte>.Shared.Rent(totalFeatureExtensionDataSize);

            int featureExtnIndex = 0;

            bytesFeatureExtensionData[featureExtnIndex++] = (byte)ColumnEncryptionData.FeatureExtensionFlag;
            featureExtensionData.colEncryptionData.FillData(bytesFeatureExtensionData.AsSpan().Slice(featureExtnIndex, 5));
            featureExtnIndex += 5;

            bytesFeatureExtensionData[featureExtnIndex++] = (byte)featureExtensionData.globalTransactionsFeature.FeatureExtensionFlag;
            featureExtensionData.globalTransactionsFeature.FillData(bytesFeatureExtensionData.AsSpan().Slice(featureExtnIndex, 4));
            featureExtnIndex += 4;

            bytesFeatureExtensionData[featureExtnIndex++] = (byte)featureExtensionData.dataClassificationFeature.FeatureExtensionFlag;
            featureExtensionData.dataClassificationFeature.FillData(bytesFeatureExtensionData.AsSpan().Slice(featureExtnIndex, 5));
            featureExtnIndex += 5;

            bytesFeatureExtensionData[featureExtnIndex++] = (byte)featureExtensionData.uTF8SupportFeature.FeatureExtensionFlag;
            featureExtensionData.uTF8SupportFeature.FillData(bytesFeatureExtensionData.AsSpan().Slice(featureExtnIndex, 4));
            featureExtnIndex += 4;

            bytesFeatureExtensionData[featureExtnIndex++] = (byte)featureExtensionData.sQLDNSCaching.FeatureExtensionFlag;
            featureExtensionData.sQLDNSCaching.FillData(bytesFeatureExtensionData.AsSpan().Slice(featureExtnIndex, 4));
            featureExtnIndex += 4;

            Debug.Assert(featureExtnIndex == totalFeatureExtensionDataSize, "The index of the feature extension data and size dont match");

            await _writeStream.WriteArrayAsync(isAsync, bytesFeatureExtensionData[..totalFeatureExtensionDataSize], ct).ConfigureAwait(false);

            ArrayPool<byte>.Shared.Return(bytesFeatureExtensionData);
            await _writeStream.WriteByteAsync(0xFF, isAsync, ct).ConfigureAwait(false);
            await _writeStream.FlushAsync(ct, isAsync, true).ConfigureAwait(false);

            return this;
        }

        private async Task<int> WriteOffSetAndLengthForString(bool isAsync, int offset, string applicationName, CancellationToken ct)
        {
            await _writeStream.WriteShortAsync((short)offset, isAsync, ct).ConfigureAwait(false);
            await _writeStream.WriteShortAsync((short)applicationName.Length, isAsync, ct).ConfigureAwait(false);
            offset += applicationName.Length * 2;
            return offset;
        }

        private async ValueTask<int> WriteAuthenticationOffsetLengthDetails(bool isAsync, int offset, CancellationToken ct)
        {
            // Support User name and password
            if (_authOptions.AuthenticationType == AuthenticationType.SQLAUTH)
            {
                await _writeStream.WriteShortAsync((short)offset, 
                    isAsync, 
                    ct).ConfigureAwait(false);

                await _writeStream.WriteShortAsync((short)_authOptions.AuthDetails.UserName.Length, 
                    isAsync, 
                    ct).ConfigureAwait(false);

                offset += _authOptions.AuthDetails.UserName.Length * 2;

                await _writeStream.WriteShortAsync((short)offset, 
                    isAsync, 
                    ct).ConfigureAwait(false);

                await _writeStream.WriteShortAsync(
                    (short)_authOptions.AuthDetails.EncryptedPassword.Length / 2, 
                    isAsync, 
                    ct).ConfigureAwait(false);
                offset += _authOptions.AuthDetails.EncryptedPassword.Length;
            }
            else
            {
                await _writeStream.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);  // userName offset
                await _writeStream.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);
                await _writeStream.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);  // password offset
                await _writeStream.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);
            }

            return offset;
        }

        private static int AssembleLogin7Flags(LoginPacket packet)
        {
            int log7Flags = 0;

            /*
             Current snapshot from TDS spec with the offsets added:
                0) fByteOrder:1,                // byte order of numeric data types on client
                1) fCharSet:1,                  // character set on client
                2) fFloat:2,                    // Type of floating point on client
                4) fDumpLoad:1,                 // Dump/Load and BCP enable
                5) fUseDb:1,                    // USE notification
                6) fDatabase:1,                 // Initial database fatal flag
                7) fSetLang:1,                  // SET LANGUAGE notification
                8) fLanguage:1,                 // Initial language fatal flag
                9) fODBC:1,                     // Set if client is ODBC driver
               10) fTranBoundary:1,             // Transaction boundary notification
               11) fDelegatedSec:1,             // Security with delegation is available
               12) fUserType:3,                 // Type of user
               15) fIntegratedSecurity:1,       // Set if client is using integrated security
               16) fSQLType:4,                  // Type of SQL sent from client
               20) fOLEDB:1,                    // Set if client is OLEDB driver
               21) fSpare1:3,                   // first bit used for read-only intent, rest unused
               24) fResetPassword:1,            // set if client wants to reset password
               25) fNoNBCAndSparse:1,           // set if client does not support NBC and Sparse column
               26) fUserInstance:1,             // This connection wants to connect to a SQL "user instance"
               27) fUnknownCollationHandling:1, // This connection can handle unknown collation correctly.
               28) fExtension:1                 // Extensions are used
               32 - total
            */

            // first byte
            log7Flags |= LucidTdsEnums.USE_DB_ON << 5;
            log7Flags |= LucidTdsEnums.INIT_DB_FATAL << 6;
            log7Flags |= LucidTdsEnums.SET_LANG_ON << 7;

            // second byte
            log7Flags |= LucidTdsEnums.INIT_LANG_FATAL << 8;
            log7Flags |= LucidTdsEnums.ODBC_ON << 9;

            // No SSPI usage
            if (packet.UseSSPI)
            {
                log7Flags |= LucidTdsEnums.SSPI_ON << 15;
            }

            // third byte
            if (packet.ReadOnlyIntent)
            {
                log7Flags |= LucidTdsEnums.READONLY_INTENT_ON << 21; // read-only intent flag is a first bit of fSpare1
            }

            // Always say that we are using Feature extensions
            log7Flags |= 1 << 28;
            return log7Flags;
        }

        //public async ValueTask Send(bool isAsync, CancellationToken ct)
        //{

        //}
    }
}
