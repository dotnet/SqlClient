// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Interop.Windows.Kernel32;
using Microsoft.Data.SqlClient;
using Interop.Windows.Sni;

namespace Microsoft.Data
{
    internal static partial class LocalDBAPI
    {
        private static IntPtr LoadProcAddress() =>
            Kernel32.GetProcAddress(UserInstanceDLLHandle, "LocalDBFormatMessage");

        private static IntPtr UserInstanceDLLHandle
        {
            get
            {
                if (s_userInstanceDLLHandle == IntPtr.Zero)
                {
                    lock (s_dllLock)
                    {
                        if (s_userInstanceDLLHandle == IntPtr.Zero)
                        {
                            SniNativeWrapper.SNIQueryInfo(QueryType.SNI_QUERY_LOCALDB_HMODULE, ref s_userInstanceDLLHandle);
                            if (s_userInstanceDLLHandle != IntPtr.Zero)
                            {
                                SqlClientEventSource.Log.TryTraceEvent("LocalDBAPI.UserInstanceDLLHandle | LocalDB - handle obtained");
                            }
                            else
                            {
                                SniNativeWrapper.SNIGetLastError(out SniError sniError);
                                throw CreateLocalDBException(errorMessage: StringsHelper.GetString("LocalDB_FailedGetDLLHandle"), sniError: sniError.sniError);
                            }
                        }
                    }
                }
                return s_userInstanceDLLHandle;
            }
        }
    }
}
