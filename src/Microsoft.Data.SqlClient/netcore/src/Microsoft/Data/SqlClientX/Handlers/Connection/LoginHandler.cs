// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Handlers.Connection.Login;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// A handler for Sql Server login. 
    /// </summary>
    internal class LoginHandler : ContextHandler<ConnectionHandlerContext>
    {

        // NIC address caching
        private static byte[] s_nicAddress;             // cache the NIC address from the registry
        private readonly GlobalTransactionsFeature _globalTransactionsFeature;
        private readonly DataClassificationFeature _dataClassificationFeature;
        private readonly Utf8Feature _utf8SupportFeature;
        private readonly SqlDnsCachingFeature _sqlDnsCachingFeature;
        private readonly SessionRecoveryFeature _sessionRecoveryFeature;
        private readonly FedAuthFeature _fedAuthFeature;
        private readonly TceFeature _tceFeature;

        /// <summary>
        /// Parameterless constructor for the login handler.
        /// </summary>
        public LoginHandler()
        {
            // Setup the feature extensions.
            _sessionRecoveryFeature = new SessionRecoveryFeature();
            _fedAuthFeature = new FedAuthFeature();
            _tceFeature = new TceFeature();
            _globalTransactionsFeature = new GlobalTransactionsFeature();
            _dataClassificationFeature = new DataClassificationFeature();
            _utf8SupportFeature = new Utf8Feature();
            _sqlDnsCachingFeature = new SqlDnsCachingFeature();
        }

        public override async ValueTask Handle(ConnectionHandlerContext context, bool isAsync, CancellationToken ct)
        {
            LoginHandlerContext loginHandlerContext = new LoginHandlerContext(context);
            await SendLogin(loginHandlerContext, isAsync, ct).ConfigureAwait(false);

            await context.TdsParser.RunAsync(RunBehavior.UntilDone, isAsync, ct);
        }

        private async ValueTask SendLogin(LoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            FeatureExtensions features = context.Features;

            features.AppendOptionalFeatures(context);

            await TdsLogin(context, isAsync, ct).ConfigureAwait(false);
        }

        private async ValueTask TdsLogin(LoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            // TODO: Password Change

            context.TdsStream.PacketHeaderType = TdsStreamPacketType.Login7;

            // Fixed length of the login record
            int length = TdsEnums.SQL2005_LOG_REC_FIXED_LEN;

            Debug.Assert(TdsEnums.MAXLEN_CLIENTINTERFACE >= context.ClientInterfaceName.Length, "cchCltIntName can specify at most 128 unicode characters. See Tds spec");

            // Calculate the fixed length

            length += context.CalculateLoginRecordLength();

            string userName = context.UserName;
            byte[] encryptedPassword = context.EncryptedPassword;
            int encryptedPasswordLengthInBytes = encryptedPassword != null ? encryptedPassword.Length : 0;

            PasswordChangeRequest passwordChangeRequest = context.PasswordChangeRequest;
            byte[] encryptedChangePassword = passwordChangeRequest?.NewSecurePassword == null ? null : TdsParserStaticMethods.ObfuscatePassword(passwordChangeRequest.NewPassword);
            int encryptedChangePasswordLength = encryptedChangePassword != null ? encryptedChangePassword.Length : 0;
            int encryptedChangePasswordLengthInBytes = passwordChangeRequest?.NewSecurePassword != null ? passwordChangeRequest.NewSecurePassword.Length : encryptedChangePasswordLength;

            byte[] rentedSSPIBuff = null;
            byte[] outSSPIBuff = null; // track the rented buffer as a separate variable in case it is updated via the ref parameter
            uint outSSPILength = 0;

            // only add lengths of password and username if not using SSPI or requesting federated authentication info
            if (!context.UseSspi && !(context.Features.FederatedAuthenticationInfoRequested || context.Features.FederatedAuthenticationRequested))
            {
                checked
                {
                    length += (userName.Length * 2) + encryptedPasswordLengthInBytes
                    + encryptedChangePasswordLengthInBytes;
                }
            }
            else
            {
                if (context.UseSspi)
                {
                    throw new NotImplementedException("SSPI is not implemented");
                }
            }


            int feOffset = length;
            // calculate and reserve the required bytes for the featureEx
            length = CalculateFeatureExtensionLength(context);

            length += feOffset;

            // TODO : Plumb Session recovery.
            SessionData recoverySessionData = null;

            await WriteLoginData(context,
                           recoverySessionData,
                           encryptedPassword,
                           encryptedChangePassword,
                           encryptedPasswordLengthInBytes,
                           encryptedChangePasswordLengthInBytes,
                           userName,
                           length,
                           feOffset,
                           context.ClientInterfaceName,
                           outSSPIBuff,
                           outSSPILength,
                           isAsync,
                           ct).ConfigureAwait(false);

            if (rentedSSPIBuff != null)
            {
                ArrayPool<byte>.Shared.Return(rentedSSPIBuff, clearArray: true);
            }

            TdsStream stream = context.TdsStream;
            ct.ThrowIfCancellationRequested();

            if (isAsync)
            {
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }
            else
            {
                stream.Flush();
            }
        }


        /// <summary>
        /// Calculates the length in bytes to send all the feature extensions.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private int CalculateFeatureExtensionLength(LoginHandlerContext context)
        {
            int feLength = 0;
            TdsEnums.FeatureExtension requestedFeatures = context.Features.RequestedFeatures;
            if (context.UseFeatureExt)
            {
                if (_sessionRecoveryFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _sessionRecoveryFeature.GetLengthInBytes(context);
                }
                if (_fedAuthFeature.ShouldUseFeature(requestedFeatures))
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.TdsLogin|SEC> Calculating Length for federated authentication feature request");
                    feLength += _fedAuthFeature.GetLengthInBytes(context);
                }
                if (_tceFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _tceFeature.GetLengthInBytes(context);
                }
                if (_globalTransactionsFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _globalTransactionsFeature.GetLengthInBytes(context);
                }
                if (_dataClassificationFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _dataClassificationFeature.GetLengthInBytes(context);
                }
                if (_utf8SupportFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _utf8SupportFeature.GetLengthInBytes(context);
                }
                if (_sqlDnsCachingFeature.ShouldUseFeature(requestedFeatures))
                {
                    feLength += _sqlDnsCachingFeature.GetLengthInBytes(context);
                }
                // terminator
                feLength++;
            }
            return feLength;
        }


        private async ValueTask WriteLoginData(LoginHandlerContext context,
                                     SessionData recoverySessionData,
                                     byte[] encryptedPassword,
                                     byte[] encryptedChangePassword,
                                     int encryptedPasswordLengthInBytes,
                                     int encryptedChangePasswordLengthInBytes,
                                     string userName,
                                     int length,
                                     int featureExOffset,
                                     string clientInterfaceName,
                                     byte[] outSSPIBuff,
                                     uint outSSPILength,
                                     bool isAsync,
                                     CancellationToken ct)
        {
            // TODO: Tackle small writes in a more effective way. 
            // Complete the discussion on https://github.com/dotnet/SqlClient/discussions/2689 and then refactor/change this code
            // to get to the optimization.
            try
            {
                TdsEnums.FeatureExtension requestedFeatures = context.Features.RequestedFeatures;
                TdsStream stream = context.TdsStream;
                SqlConnectionEncryptOption encrypt = context.ConnectionOptions.Encrypt;
                FederatedAuthenticationFeatureExtensionData fedAuthFeatureExtensionData = context.Features.FedAuthFeatureExtensionData;
                TdsWriter writer = stream.TdsWriter;
                await writer.WriteIntAsync(length, isAsync, ct).ConfigureAwait(false);

                if (recoverySessionData == null)
                {
                    int protocolVersion = encrypt == SqlConnectionEncryptOption.Strict
                        ? (TdsEnums.TDS8_MAJOR << 24) | (TdsEnums.TDS8_INCREMENT << 16) | TdsEnums.TDS8_MINOR
                        : (TdsEnums.SQL2012_MAJOR << 24) | (TdsEnums.SQL2012_INCREMENT << 16) | TdsEnums.SQL2012_MINOR;
                    await writer.WriteIntAsync(protocolVersion, isAsync, ct).ConfigureAwait(false);
                }
                else
                {
                    await writer.WriteUnsignedIntAsync(recoverySessionData._tdsVersion, isAsync, ct).ConfigureAwait(false);
                }

                await writer.WriteIntAsync(context.PacketSize, isAsync, ct).ConfigureAwait(false);
                await writer.WriteIntAsync(TdsEnums.CLIENT_PROG_VER, isAsync, ct).ConfigureAwait(false);
                await writer.WriteIntAsync(TdsParserStaticMethods.GetCurrentProcessIdForTdsLoginOnly(), isAsync, ct).ConfigureAwait(false);
                await writer.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false); // connectionID is unused

                // Log7Flags (DWORD)

                int log7Flags = CreateLogin7Flags(context);
                await writer.WriteIntAsync(log7Flags, isAsync, ct).ConfigureAwait(false);

                await writer.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);  // ClientTimeZone is not used
                await writer.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);  // LCID is unused by server

                // Start writing offset and length of variable length portions
                int offset = TdsEnums.SQL2005_LOG_REC_FIXED_LEN;

                // write offset/length pairs

                // note that you must always set ibHostName since it indicates the beginning of the variable length section of the login record
                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // host name offset
                await writer.WriteShortAsync(context.HostName.Length, isAsync, ct).ConfigureAwait(false);
                offset += context.HostName.Length * 2;

                // Only send user/password over if not fSSPI...  If both user/password and SSPI are in login
                // rec, only SSPI is used.  Confirmed same behavior as in luxor.
                if (!context.UseSspi && !(context.Features.FederatedAuthenticationInfoRequested || context.Features.FederatedAuthenticationRequested))
                {
                    await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false);  // userName offset
                    await writer.WriteShortAsync(userName.Length, isAsync, ct).ConfigureAwait(false);
                    offset += userName.Length * 2;

                    // the encrypted password is a byte array - so length computations different than strings
                    await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // password offset
                    await writer.WriteShortAsync(encryptedPasswordLengthInBytes / 2, isAsync, ct).ConfigureAwait(false);
                    offset += encryptedPasswordLengthInBytes;
                }
                else
                {
                    // case where user/password data is not used, send over zeros
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);  // userName offset
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);  // password offset
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);
                }

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // app name offset
                await writer.WriteShortAsync(context.ApplicationName.Length, isAsync, ct).ConfigureAwait(false);
                offset += context.ApplicationName.Length * 2;

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // server name offset
                await writer.WriteShortAsync(context.ServerName.Length, isAsync, ct).ConfigureAwait(false);
                offset += context.ServerName.Length * 2;

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false);
                if (context.UseFeatureExt)
                {
                    await writer.WriteShortAsync(4, isAsync, ct).ConfigureAwait(false); // length of ibFeatgureExtLong (which is a DWORD)
                    offset += 4;
                }
                else
                {
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false); // unused (was remote password ?)
                }

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // client interface name offset
                await writer.WriteShortAsync(clientInterfaceName.Length, isAsync, ct).ConfigureAwait(false);
                offset += clientInterfaceName.Length * 2;

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // language name offset
                await writer.WriteShortAsync(context.Language.Length, isAsync, ct).ConfigureAwait(false);
                offset += context.Language.Length * 2;

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // database name offset
                await writer.WriteShortAsync(context.Database.Length, isAsync, ct).ConfigureAwait(false);
                offset += context.Database.Length * 2;

                if (null == s_nicAddress)
                    s_nicAddress = TdsParserStaticMethods.GetNetworkPhysicalAddressForTdsLoginOnly();

                await stream.TdsWriter.WriteBytesAsync(s_nicAddress.AsMemory(), isAsync, ct).ConfigureAwait(false);

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // ibSSPI offset
                if (context.UseSspi)
                {
                    await writer.WriteShortAsync((int)outSSPILength, isAsync, ct).ConfigureAwait(false);
                    offset += (int)outSSPILength;
                }
                else
                {
                    await writer.WriteShortAsync(0, isAsync, ct).ConfigureAwait(false);
                }

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // DB filename offset
                await writer.WriteShortAsync(context.AttachedDbFileName.Length, isAsync, ct).ConfigureAwait(false);
                offset += context.AttachedDbFileName.Length * 2;

                await writer.WriteShortAsync(offset, isAsync, ct).ConfigureAwait(false); // reset password offset
                await writer.WriteShortAsync(encryptedChangePasswordLengthInBytes / 2, isAsync, ct).ConfigureAwait(false);

                await writer.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);        // reserved for chSSPI

                // write variable length portion
                await stream.WriteStringAsync(context.HostName, isAsync, ct).ConfigureAwait(false);

                // if we are using SSPI, do not send over username/password, since we will use SSPI instead
                // same behavior as Luxor
                if (!context.UseSspi && !(context.Features.FederatedAuthenticationInfoRequested || context.Features.FederatedAuthenticationRequested))
                {
                    await stream.WriteStringAsync(userName, isAsync, ct).ConfigureAwait(false);

                    if (context.Credential != null)
                    {
                        // TODO: Implement secure string save.
                        throw new NotImplementedException();
                        // _physicalStateObj.WriteSecureString(rec.credential.Password);
                    }
                    else
                    {
                        await stream.TdsWriter.WriteBytesAsync(encryptedPassword.AsMemory(0, encryptedPasswordLengthInBytes), isAsync, ct).ConfigureAwait(false);
                    }
                }

                await stream.WriteStringAsync(context.ApplicationName, isAsync, ct).ConfigureAwait(false);

                await stream.WriteStringAsync(context.ServerName, isAsync, ct).ConfigureAwait(false);

                // write ibFeatureExtLong
                if (context.UseFeatureExt)
                {
                    await writer.WriteIntAsync(featureExOffset, isAsync, ct).ConfigureAwait(false);
                }

                await stream.WriteStringAsync(clientInterfaceName, isAsync, ct).ConfigureAwait(false);
                await stream.WriteStringAsync(context.Language, isAsync, ct).ConfigureAwait(false);
                await stream.WriteStringAsync(context.Database, isAsync, ct).ConfigureAwait(false);

                // send over SSPI data if we are using SSPI
                if (context.UseSspi)
                {
                    await stream.TdsWriter.WriteBytesAsync(outSSPIBuff.AsMemory(0, (int)outSSPILength), isAsync, ct).ConfigureAwait(false);
                }

                await stream.WriteStringAsync(context.AttachedDbFileName, isAsync, ct).ConfigureAwait(false);
                if (!context.UseSspi && !(context.Features.FederatedAuthenticationInfoRequested || context.Features.FederatedAuthenticationRequested))
                {
                    if (context.PasswordChangeRequest?.NewSecurePassword != null)
                    {
                        // TODO : implement saving secure string.
                        throw new NotImplementedException();
                        // _physicalStateObj.WriteSecureString(rec.newSecurePassword);
                    }
                    else
                    {
                        await stream.TdsWriter.WriteBytesAsync(encryptedChangePassword.AsMemory(0, encryptedChangePasswordLengthInBytes), isAsync, ct).ConfigureAwait(false);
                    }
                }

                await SendFeatureExtensionData(context,
                    isAsync,
                    ct).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (ADP.IsCatchableExceptionType(e))
                {
                    // Reset the buffer if there was an exception.
                    context.TdsStream.Reset();
                }

                throw;
            }
        }

        private async ValueTask SendFeatureExtensionData(LoginHandlerContext context,
                                        bool isAsync,
                                        CancellationToken ct)
        {
            TdsEnums.FeatureExtension requestedFeatures = context.Features.RequestedFeatures;
            if (context.UseFeatureExt)
            {
                if (_sessionRecoveryFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _sessionRecoveryFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }
                if (_fedAuthFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _fedAuthFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }
                if (_tceFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _tceFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }
                if (_globalTransactionsFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _globalTransactionsFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }
                if (_dataClassificationFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _dataClassificationFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }
                if (_utf8SupportFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _utf8SupportFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }

                if (_sqlDnsCachingFeature.ShouldUseFeature(requestedFeatures))
                {
                    await _sqlDnsCachingFeature.WriteFeatureData(context, isAsync, ct).ConfigureAwait(false);
                }

                // terminator
                await context.TdsStream.WriteByteAsync(FeatureExtensions.FeatureTerminator, isAsync, ct).ConfigureAwait(false);

            }
        }

        private static int CreateLogin7Flags(LoginHandlerContext context)
        {
            bool useFeatureExt = context.UseFeatureExt;

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
            log7Flags |= TdsEnums.USE_DB_ON << 5;
            log7Flags |= TdsEnums.INIT_DB_FATAL << 6;
            log7Flags |= TdsEnums.SET_LANG_ON << 7;

            // second byte
            log7Flags |= TdsEnums.INIT_LANG_FATAL << 8;
            log7Flags |= TdsEnums.ODBC_ON << 9;
            if (context.UseReplication)
            {
                log7Flags |= TdsEnums.REPL_ON << 12;
            }
            if (context.UseSspi)
            {
                log7Flags |= TdsEnums.SSPI_ON << 15;
            }

            // third byte
            if (context.ReadOnlyIntent)
            {
                log7Flags |= TdsEnums.READONLY_INTENT_ON << 21; // read-only intent flag is a first bit of fSpare1
            }

            // 4th one
            if (!string.IsNullOrEmpty(context.NewPassword) || (context.NewSecurePassword != null && context.NewSecurePassword.Length != 0))
            {
                log7Flags |= 1 << 24;
            }
            if (context.ConnectionOptions.UserInstance)
            {
                log7Flags |= 1 << 26;
            }
            if (useFeatureExt)
            {
                log7Flags |= 1 << 28;
            }

            return log7Flags;
        }
    }
}
#endif
