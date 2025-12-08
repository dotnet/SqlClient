// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

using System;
using System.Buffers;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class NativeSspiContextProvider : SspiContextProvider
    {
        private static readonly object s_tdsParserLock = new();

        // bool to indicate whether library has been loaded
        private static bool s_fSSPILoaded;

        // variable to hold max SSPI data size, keep for token from server
        private static volatile uint s_maxSSPILength;

        private protected override void Initialize()
        {
            LoadSSPILibrary();
        }

        private void LoadSSPILibrary()
        {
            // Outer check so we don't acquire lock once it's loaded.
            if (!s_fSSPILoaded)
            {
                lock (s_tdsParserLock)
                {
                    // re-check inside lock
                    if (!s_fSSPILoaded)
                    {
                        // use local for ref param to defer setting s_maxSSPILength until we know the call succeeded.
                        uint maxLength = 0;

                        if (0 != SniNativeWrapper.SniSecInitPackage(ref maxLength))
                        {
                            SSPIError(SQLMessage.SSPIInitializeError(), TdsEnums.INIT_SSPI_PACKAGE);
                        }

                        s_maxSSPILength = maxLength;
                        s_fSSPILoaded = true;
                    }
                }
            }

            if (s_maxSSPILength > int.MaxValue)
            {
                throw SQL.InvalidSSPIPacketSize();   // SqlBu 332503
            }
        }

        protected override bool GenerateContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams)
        {
            #if NET
            Debug.Assert(_physicalStateObj.SessionHandle.Type == SessionHandle.NativeHandleType);
            #endif

            SNIHandle handle = _physicalStateObj.SessionHandle.NativeHandle;

            // This must start as the length of the input, but will be updated by the call to SNISecGenClientContext to the written length
            var sendLength = s_maxSSPILength;
            var outBuff = outgoingBlobWriter.GetSpan((int)sendLength);

            if (SniNativeWrapper.SniSecGenClientContext(handle, incomingBlob, outBuff, ref sendLength, authParams.Resource) != 0)
            {
                return false;
            }

            if (sendLength > int.MaxValue)
            {
                throw SQL.InvalidSSPIPacketSize();  // SqlBu 332503
            }

            outgoingBlobWriter.Advance((int)sendLength);

            return true;
        }
    }
}

#endif
