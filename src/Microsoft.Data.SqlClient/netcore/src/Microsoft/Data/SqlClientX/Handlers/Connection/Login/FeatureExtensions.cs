// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.Login
{
    internal class FeatureExtensions
    {
        public bool SessionRecoveryRequested { get; internal set; }
        public bool FederatedAuthenticationInfoRequested { get; internal set; }
        public FederatedAuthenticationFeatureExtensionData FedAuthFeatureExtensionData { get; internal set; }
        public bool FederatedAuthenticationRequested { get; internal set; }
        public TdsEnums.FeatureExtension RequestedFeatures { get; internal set; }
        public SessionData ReconnectData { get; internal set; }
    }

    internal abstract class FeatureExtensionFeatures
    {
        public TdsEnums.FeatureExtension FeatureExtensionFlag { get; private set; }

        public FeatureExtensionFeatures(TdsEnums.FeatureExtension featureExtensionFlags)
        {
            FeatureExtensionFlag = featureExtensionFlags;
        }

        public virtual bool ShouldUseFeature(TdsEnums.FeatureExtension requestedFeatures)
        {
            return (requestedFeatures & FeatureExtensionFlag) != 0;
        }

        public abstract int GetLengthInBytes(LoginHandlerContext context);

        public abstract ValueTask WriteFeatureData(LoginHandlerContext context, bool isAsync, CancellationToken ct);
    }

    internal abstract class VersionConfigurableFeature : FeatureExtensionFeatures
    {
        private readonly byte _tdsFeatureIdentifier;
        private readonly byte _maxSupportedFeatureVersion = 0x0;

        /// <summary>
        /// Constructor for features which are not versioned.
        /// </summary>
        /// <param name="featureExtension"></param>
        /// <param name="featureIdentifier"></param>
        protected VersionConfigurableFeature(TdsEnums.FeatureExtension featureExtension, byte featureIdentifier) : base(featureExtension)
        {
            Debug.Assert(_tdsFeatureIdentifier != 0x0, "Feature identifier should not be 0x0");
            _tdsFeatureIdentifier = featureIdentifier;
        }

        /// <summary>
        /// Constructor for features which have a maximum supported version.
        /// </summary>
        /// <param name="featureExtension"></param>
        /// <param name="tdsFeatureIdentifier"></param>
        /// <param name="maxSupportedVersion"></param>
        public VersionConfigurableFeature(TdsEnums.FeatureExtension featureExtension, byte tdsFeatureIdentifier, byte maxSupportedVersion) : base(featureExtension)
        {
            _tdsFeatureIdentifier = tdsFeatureIdentifier;
            _maxSupportedFeatureVersion = maxSupportedVersion;
        }

        public override int GetLengthInBytes(LoginHandlerContext context)
        {
            // 1byte = featureID, 4bytes = featureData length
            int featureDataLengthInBytes = 5;
            if (_maxSupportedFeatureVersion != 0x0)
            {
                // 1 byte for version
                featureDataLengthInBytes += 1;
            }
            return featureDataLengthInBytes;
        }

        public override async ValueTask WriteFeatureData(LoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            TdsStream stream = context.ConnectionContext.TdsStream;
            await stream.WriteByteAsync(_tdsFeatureIdentifier, isAsync, ct).ConfigureAwait(false);
            if (_maxSupportedFeatureVersion != 0x0)
            {
                await stream.TdsWriter.WriteIntAsync(1, isAsync, ct).ConfigureAwait(false);
                await stream.WriteByteAsync(_maxSupportedFeatureVersion, isAsync, ct).ConfigureAwait(false);
            }
            else
            { 
                await stream.TdsWriter.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);
            }
        }
    }

    internal class Utf8Feature : VersionConfigurableFeature
    {
        public Utf8Feature() : base(TdsEnums.FeatureExtension.UTF8Support, TdsEnums.FEATUREEXT_UTF8SUPPORT)
        {
        }
    }

    internal class GlobalTransactionsFeature : VersionConfigurableFeature
    {
        public GlobalTransactionsFeature() : base(TdsEnums.FeatureExtension.GlobalTransactions, TdsEnums.FEATUREEXT_GLOBALTRANSACTIONS)
        {
        }
    }

    internal class DataClassificationFeature : VersionConfigurableFeature
    {
        public DataClassificationFeature() : base(TdsEnums.FeatureExtension.DataClassification, TdsEnums.FEATUREEXT_DATACLASSIFICATION, TdsEnums.DATA_CLASSIFICATION_VERSION_MAX_SUPPORTED)
        {
        }
    }

    internal class TceFeature : VersionConfigurableFeature
    {
        public TceFeature() : base(TdsEnums.FeatureExtension.Tce, TdsEnums.FEATUREEXT_TCE, TdsEnums.MAX_SUPPORTED_TCE_VERSION)
        {
        }
    }

    internal class SqlDnsCachingFeature : VersionConfigurableFeature
    {
        public SqlDnsCachingFeature() : base(TdsEnums.FeatureExtension.SQLDNSCaching, TdsEnums.FEATUREEXT_SQLDNSCACHING)
        {
        }
    }

    internal class SessionRecoveryFeature : FeatureExtensionFeatures
    {
        public SessionRecoveryFeature() : base(TdsEnums.FeatureExtension.SessionRecovery)
        {
        }

        public override int GetLengthInBytes(LoginHandlerContext context)
        {
            int len = 1;
            SessionData reconnectData = context.Features.ReconnectData;
            if (context.Features.ReconnectData == null)
            {
                // Length of the data size int.
                len += 4;
            }
            else
            {
                Debug.Assert(reconnectData._unrecoverableStatesCount == 0, "Unrecoverable state count should be 0");
                int initialLength = 0; // sizeof(DWORD) - length itself
                initialLength += 1 + 2 * TdsParserStaticMethods.NullAwareStringLength(reconnectData._initialDatabase);
                initialLength += 1 + 2 * TdsParserStaticMethods.NullAwareStringLength(reconnectData._initialLanguage);
                initialLength += (reconnectData._initialCollation == null) ? 1 : 6;
                for (int i = 0; i < SessionData._maxNumberOfSessionStates; i++)
                {
                    if (reconnectData._initialState[i] != null)
                    {
                        initialLength += 1 /* StateId*/ + StateValueLength(reconnectData._initialState[i].Length);
                    }
                }
                int currentLength = 0; // sizeof(DWORD) - length itself
                currentLength += 1 + 2 * (reconnectData._initialDatabase == reconnectData._database ? 0 : TdsParserStaticMethods.NullAwareStringLength(reconnectData._database));
                currentLength += 1 + 2 * (reconnectData._initialLanguage == reconnectData._language ? 0 : TdsParserStaticMethods.NullAwareStringLength(reconnectData._language));
                currentLength += (reconnectData._collation != null && !SqlCollation.Equals(reconnectData._collation, reconnectData._initialCollation)) ? 6 : 1;
                bool[] writeState = new bool[SessionData._maxNumberOfSessionStates];
                for (int i = 0; i < SessionData._maxNumberOfSessionStates; i++)
                {
                    if (reconnectData._delta[i] != null)
                    {
                        Debug.Assert(reconnectData._delta[i]._recoverable, "State should be recoverable");
                        writeState[i] = true;
                        if (reconnectData._initialState[i] != null && reconnectData._initialState[i].Length == reconnectData._delta[i]._dataLength)
                        {
                            writeState[i] = false;
                            for (int j = 0; j < reconnectData._delta[i]._dataLength; j++)
                            {
                                if (reconnectData._initialState[i][j] != reconnectData._delta[i]._data[j])
                                {
                                    writeState[i] = true;
                                    break;
                                }
                            }
                        }
                        if (writeState[i])
                        {
                            currentLength += 1 /* StateId*/ + StateValueLength(reconnectData._delta[i]._dataLength);
                        }
                    }
                }
                len += initialLength + currentLength + 12 /* length fields (initial, current, total) */;
            }

            return len;
        }

        public override async ValueTask WriteFeatureData(LoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(context.Features.SessionRecoveryRequested == true, "SessionRecoveryRequested should be true");

            TdsStream stream = context.ConnectionContext.TdsStream;

            await stream.WriteByteAsync(TdsEnums.FEATUREEXT_SRECOVERY, isAsync, ct).ConfigureAwait(false);
            SessionData reconnectData = context.Features.ReconnectData;
            if (reconnectData == null)
            {
                await stream.TdsWriter.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);
            }
            else
            {
                Debug.Assert(reconnectData._unrecoverableStatesCount == 0, "Unrecoverable state count should be 0");
                int initialLength = 0; // sizeof(DWORD) - length itself
                initialLength += 1 + 2 * TdsParserStaticMethods.NullAwareStringLength(reconnectData._initialDatabase);
                initialLength += 1 + 2 * TdsParserStaticMethods.NullAwareStringLength(reconnectData._initialLanguage);
                initialLength += (reconnectData._initialCollation == null) ? 1 : 6;
                for (int i = 0; i < SessionData._maxNumberOfSessionStates; i++)
                {
                    if (reconnectData._initialState[i] != null)
                    {
                        initialLength += 1 /* StateId*/ + StateValueLength(reconnectData._initialState[i].Length);
                    }
                }
                int currentLength = 0; // sizeof(DWORD) - length itself
                currentLength += 1 + 2 * (reconnectData._initialDatabase == reconnectData._database ? 0 : TdsParserStaticMethods.NullAwareStringLength(reconnectData._database));
                currentLength += 1 + 2 * (reconnectData._initialLanguage == reconnectData._language ? 0 : TdsParserStaticMethods.NullAwareStringLength(reconnectData._language));
                currentLength += (reconnectData._collation != null && !SqlCollation.Equals(reconnectData._collation, reconnectData._initialCollation)) ? 6 : 1;
                bool[] writeState = new bool[SessionData._maxNumberOfSessionStates];
                for (int i = 0; i < SessionData._maxNumberOfSessionStates; i++)
                {
                    if (reconnectData._delta[i] != null)
                    {
                        Debug.Assert(reconnectData._delta[i]._recoverable, "State should be recoverable");
                        writeState[i] = true;
                        if (reconnectData._initialState[i] != null && reconnectData._initialState[i].Length == reconnectData._delta[i]._dataLength)
                        {
                            writeState[i] = false;
                            for (int j = 0; j < reconnectData._delta[i]._dataLength; j++)
                            {
                                if (reconnectData._initialState[i][j] != reconnectData._delta[i]._data[j])
                                {
                                    writeState[i] = true;
                                    break;
                                }
                            }
                        }
                        if (writeState[i])
                        {
                            currentLength += 1 /* StateId*/ + StateValueLength(reconnectData._delta[i]._dataLength);
                        }
                    }
                }
                TdsWriter writer = stream.TdsWriter;
                // length of data w/o total length (initial + current + 2 * sizeof(DWORD))
                await writer.WriteIntAsync(8 + initialLength + currentLength, isAsync, ct).ConfigureAwait(false);
                await writer.WriteIntAsync(initialLength, isAsync, ct);
                await WriteIdentifierAsync(context, reconnectData._initialDatabase, isAsync, ct).ConfigureAwait(false);
                await WriteCollationAsync(context, reconnectData._initialCollation, isAsync, ct).ConfigureAwait(false);
                await WriteIdentifierAsync(context, reconnectData._initialLanguage, isAsync, ct).ConfigureAwait(false);
                for (int i = 0; i < SessionData._maxNumberOfSessionStates; i++)
                {
                    if (reconnectData._initialState[i] != null)
                    {
                        await stream.WriteByteAsync((byte)i, isAsync, ct).ConfigureAwait(false);
                        if (reconnectData._initialState[i].Length < 0xFF)
                        {
                            await stream.WriteByteAsync((byte)reconnectData._initialState[i].Length, isAsync, ct).ConfigureAwait(false);
                        }
                        else
                        {
                            await stream.WriteByteAsync(0xFF, isAsync, ct).ConfigureAwait(false);
                            await writer.WriteIntAsync(reconnectData._initialState[i].Length, isAsync, ct).ConfigureAwait(false);
                        }
                        if (isAsync)
                        { 
                            await stream.WriteAsync(reconnectData._initialState[i].AsMemory(0, reconnectData._initialState[i].Length), ct).ConfigureAwait(false);
                        }
                        else
                        {
                            stream.Write(reconnectData._initialState[i].AsSpan(0, reconnectData._initialState[i].Length));
                        }
                    }
                }
                await writer.WriteIntAsync(currentLength, isAsync, ct);
                await WriteIdentifierAsync(context, reconnectData._database != reconnectData._initialDatabase ? reconnectData._database : null, isAsync, ct).ConfigureAwait(false);
                await WriteCollationAsync(context, SqlCollation.Equals(reconnectData._initialCollation, reconnectData._collation) ? null : reconnectData._collation, isAsync, ct).ConfigureAwait(false);
                await WriteIdentifierAsync(context, reconnectData._language != reconnectData._initialLanguage ? reconnectData._language : null, isAsync, ct).ConfigureAwait(false);
                for (int i = 0; i < SessionData._maxNumberOfSessionStates; i++)
                {
                    if (writeState[i])
                    {
                        await stream.WriteByteAsync((byte)i, isAsync, ct).ConfigureAwait(false);
                        if (reconnectData._delta[i]._dataLength < 0xFF)
                        {
                            await stream.WriteByteAsync((byte)reconnectData._delta[i]._dataLength, isAsync, ct).ConfigureAwait(false);
                        }
                        else
                        {
                            await stream.WriteByteAsync(0xFF, isAsync, ct).ConfigureAwait(false);
                            await writer.WriteIntAsync(reconnectData._delta[i]._dataLength, isAsync, ct).ConfigureAwait(false);
                        }

                        if (isAsync)
                        {
                            await stream.WriteAsync(reconnectData._delta[i]._data.AsMemory(0, reconnectData._delta[i]._dataLength), ct).ConfigureAwait(false);
                        }
                        else
                        {
                            stream.Write(reconnectData._delta[i]._data.AsSpan(0, reconnectData._delta[i]._dataLength));
                        }
                    }
                }
                

            }
        }

        private async ValueTask WriteCollationAsync(LoginHandlerContext context, SqlCollation collation, bool isAsync, CancellationToken ct)
        {
            TdsStream stream = context.ConnectionContext.TdsStream;

            if (collation == null)
            {
                await stream.WriteByteAsync(0, isAsync, ct).ConfigureAwait(false);
            }
            else
            {
                await stream.WriteByteAsync(sizeof(uint) + sizeof(byte), isAsync, ct).ConfigureAwait(false);
                await stream.TdsWriter.WriteUnsignedIntAsync(collation._info, isAsync, ct).ConfigureAwait(false);
                await stream.WriteByteAsync(collation._sortId, isAsync, ct).ConfigureAwait(false);
            }
        }

        private async ValueTask WriteIdentifierAsync(LoginHandlerContext context, string s, bool isAsync, CancellationToken ct)
        {
            TdsStream stream = context.ConnectionContext.TdsStream;
            if (null != s)
            {
                await stream.WriteByteAsync(checked((byte)s.Length), isAsync, ct).ConfigureAwait(false);
                await stream.WriteStringAsync(s, isAsync, ct).ConfigureAwait(false);
            }
            else
            {
                await stream.WriteByteAsync((byte)0, isAsync, ct).ConfigureAwait(false);
            }
        }

        private static int StateValueLength(int dataLen)
        {
            return dataLen < 0xFF ? (dataLen + 1) : (dataLen + 5);
        }
    }

    internal class FedAuthFeature : FeatureExtensionFeatures
    {
        public FedAuthFeature() : base(TdsEnums.FeatureExtension.FedAuth)
        {
        }

        public override int GetLengthInBytes(LoginHandlerContext context)
        {
            FederatedAuthenticationFeatureExtensionData fedAuthFeatureData = context.Features.FedAuthFeatureExtensionData;

            int dataLen = 0;

            switch (fedAuthFeatureData.libraryType)
            {
                case TdsEnums.FedAuthLibrary.MSAL:
                    dataLen = 2;  // length of feature data = 1 byte for library and echo + 1 byte for workflow
                    break;
                case TdsEnums.FedAuthLibrary.SecurityToken:
                    Debug.Assert(fedAuthFeatureData.accessToken != null, "AccessToken should not be null.");
                    dataLen = 1 + sizeof(int) + fedAuthFeatureData.accessToken.Length; // length of feature data = 1 byte for library and echo, security token length and sizeof(int) for token length itself
                    break;
                default:
                    Debug.Fail("Unrecognized library type for fedauth feature extension request");
                    break;
            }

            // length of feature id (1 byte), data length field (4 bytes), and feature data (dataLen)
            int totalLen = dataLen + 5;
            return totalLen;
        }

        public override async ValueTask WriteFeatureData(LoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            FederatedAuthenticationFeatureExtensionData fedAuthFeatureData = context.Features.FedAuthFeatureExtensionData;
            Debug.Assert(fedAuthFeatureData.libraryType == TdsEnums.FedAuthLibrary.MSAL || fedAuthFeatureData.libraryType == TdsEnums.FedAuthLibrary.SecurityToken,
                "only fed auth library type MSAL and Security Token are supported in writing feature request");

            int dataLen = 0;

            // set dataLen and totalLen
            switch (fedAuthFeatureData.libraryType)
            {
                case TdsEnums.FedAuthLibrary.MSAL:
                    dataLen = 2;  // length of feature data = 1 byte for library and echo + 1 byte for workflow
                    break;
                case TdsEnums.FedAuthLibrary.SecurityToken:
                    Debug.Assert(fedAuthFeatureData.accessToken != null, "AccessToken should not be null.");
                    dataLen = 1 + sizeof(int) + fedAuthFeatureData.accessToken.Length; // length of feature data = 1 byte for library and echo, security token length and sizeof(int) for token length itself
                    break;
                default:
                    Debug.Fail("Unrecognized library type for fedauth feature extension request");
                    break;
            }

            TdsStream stream = context.ConnectionContext.TdsStream;
            
            await stream.WriteByteAsync(TdsEnums.FEATUREEXT_FEDAUTH, isAsync, ct).ConfigureAwait(false);

            // set options
            byte options = 0x00;

            // set upper 7 bits of options to indicate fed auth library type
            switch (fedAuthFeatureData.libraryType)
            {
                case TdsEnums.FedAuthLibrary.MSAL:
                    Debug.Assert(context.Features.FederatedAuthenticationInfoRequested == true, "Features.FederatedAuthenticationInfoRequested field should be true");
                    options |= TdsEnums.FEDAUTHLIB_MSAL << 1;
                    break;
                case TdsEnums.FedAuthLibrary.SecurityToken:
                    Debug.Assert(context.Features.FederatedAuthenticationRequested == true, "_federatedAuthenticationRequested field should be true");
                    options |= TdsEnums.FEDAUTHLIB_SECURITYTOKEN << 1;
                    break;
                default:
                    Debug.Fail("Unrecognized FedAuthLibrary type for feature extension request");
                    break;
            }

            options |= (byte)(fedAuthFeatureData.fedAuthRequiredPreLoginResponse == true ? 0x01 : 0x00);

            // write dataLen and options
            await stream.TdsWriter.WriteIntAsync(dataLen, isAsync, ct).ConfigureAwait(false);
            await stream.WriteByteAsync(options, isAsync, ct).ConfigureAwait(false);

            // write accessToken for FedAuthLibrary.SecurityToken
            switch (fedAuthFeatureData.libraryType)
            {
                case TdsEnums.FedAuthLibrary.MSAL:
                    byte workflow = 0x00;
                    switch (fedAuthFeatureData.authentication)
                    {
                        case SqlAuthenticationMethod.ActiveDirectoryPassword:
                            workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYPASSWORD;
                            break;
                        case SqlAuthenticationMethod.ActiveDirectoryIntegrated:
                            workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYINTEGRATED;
                            break;
                        case SqlAuthenticationMethod.ActiveDirectoryInteractive:
                            workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYINTERACTIVE;
                            break;
                        case SqlAuthenticationMethod.ActiveDirectoryServicePrincipal:
                            workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYSERVICEPRINCIPAL;
                            break;
                        case SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow:
                            workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYDEVICECODEFLOW;
                            break;
                        case SqlAuthenticationMethod.ActiveDirectoryManagedIdentity:
                        case SqlAuthenticationMethod.ActiveDirectoryMSI:
                            workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYMANAGEDIDENTITY;
                            break;
                        case SqlAuthenticationMethod.ActiveDirectoryDefault:
                            workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYDEFAULT;
                            break;
                        case SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity:
                            workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYWORKLOADIDENTITY;
                            break;
                        default:
                            if (context.ConnectionContext.AccessTokenCallback != null)
                            {
                                workflow = TdsEnums.MSALWORKFLOW_ACTIVEDIRECTORYTOKENCREDENTIAL;
                            }
                            else
                            {
                                Debug.Assert(false, "Unrecognized Authentication type for fedauth MSAL request");
                            }
                            break;
                    }

                    await stream.WriteByteAsync(workflow, isAsync, ct);
                    break;
                case TdsEnums.FedAuthLibrary.SecurityToken:
                    await stream.TdsWriter.WriteIntAsync(fedAuthFeatureData.accessToken.Length, isAsync, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    if (isAsync)
                    {
                        await stream.WriteAsync(fedAuthFeatureData.accessToken.AsMemory(), ct).ConfigureAwait(false);
                    }
                    else
                    {
                        stream.Write(fedAuthFeatureData.accessToken);
                    }
                    break;
                default:
                    Debug.Fail("Unrecognized FedAuthLibrary type for feature extension request");
                    break;
            }
            
        }
    }
}
