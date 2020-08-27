// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class NetSecurityNative
    {
        internal sealed class GssApiException : Exception
        {
            internal Status MinorStatus { get; private set; }

            internal GssApiException() { }

            internal GssApiException(string message) : base(message) { }

            internal GssApiException(string message, Exception innerException) : base(message, innerException) { }

            internal GssApiException(Status majorStatus, Status minorStatus)
                : base(GetGssApiDisplayStatus(majorStatus, minorStatus))
            {
                HResult = (int)majorStatus;
                MinorStatus = minorStatus;
            }

            private static string GetGssApiDisplayStatus(Status majorStatus, Status minorStatus)
            {
                string majorError = GetGssApiDisplayStatus(majorStatus, isMinor: false);
                string minorError = GetGssApiDisplayStatus(minorStatus, isMinor: true);

                return (majorError != null && minorError != null) ?
                    StringsHelper.Format(Strings.net_gssapi_operation_failed_detailed, majorError, minorError) :
                    StringsHelper.Format(Strings.net_gssapi_operation_failed, majorStatus.ToString("x"), minorStatus.ToString("x"));
            }

            private static string GetGssApiDisplayStatus(Status status, bool isMinor)
            {
                GssBuffer displayBuffer = default;

                try
                {
                    Status displayCallStatus = isMinor ?
                        DisplayMinorStatus(out Status minStat, status, ref displayBuffer) :
                        DisplayMajorStatus(out minStat, status, ref displayBuffer);
                    return (Status.GSS_S_COMPLETE != displayCallStatus) ? null : Marshal.PtrToStringAnsi(displayBuffer._data);
                }
                finally
                {
                    displayBuffer.Dispose();
                }
            }
        }
    }
}
