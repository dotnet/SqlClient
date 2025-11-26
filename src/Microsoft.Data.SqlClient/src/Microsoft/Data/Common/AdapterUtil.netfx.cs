// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using Interop.Windows.Kernel32;

namespace Microsoft.Data.Common
{
    internal static partial class ADP
    {
        #region Constants

        /// <summary>
        /// Fraction of timeout to use in case of Transparent Network IP resolution.
        /// </summary>
        internal const float FailoverTimeoutStepForTnir = 0.125F;

        /// <summary>
        /// The first login attempt in Transparent network IP Resolution.
        /// </summary>
        internal const int MinimumTimeoutForTnirMs = 500;

        /// <remarks>
        /// Use for INVALID_HANDLE
        /// </remarks>
        // @TODO: Use naming rules
        internal static readonly IntPtr s_invalidPtr = new(-1);

        // @TODO: Use naming rules
        // @TODO: All values but Unix and Windows32NT are used today, for netfx this should always be Win32NT. We can likely hard code this to true for netfx.
        internal static readonly bool s_isWindowsNT = Environment.OSVersion.Platform == PlatformID.Win32NT;

        // @TODO: Use naming rules
        // @TODO: We don't support anything other than this, so I don't know why we need this anymore.
        internal static readonly bool s_isPlatformNT5 = s_isWindowsNT && Environment.OSVersion.Version.Major >= 5;

        // @TODO: Get rid of this and use IntPtr.Size;
        // @TODO: Use naming rules
        internal static readonly int s_ptrSize = IntPtr.Size;

        #endregion

        #region Utility Methods

        internal static void CheckArgumentLength(string value, string parameterName)
        {
            CheckArgumentNull(value, parameterName);
            if (value.Length == 0)
            {
                throw Argument(StringsHelper.GetString(Strings.ADP_EmptyString, parameterName));
            }
        }

        // @TODO: Replace usages with Task.FromException.
        internal static Task<T> CreatedTaskWithException<T>(Exception ex)
        {
            TaskCompletionSource<T> completion = new();
            completion.SetException(ex);
            return completion.Task;
        }

        // TODO: cache machine name and listen to longhorn event to reset it
        internal static string GetComputerNameDnsFullyQualified()
        {
            // winbase.h, enum COMPUTER_NAME_FORMAT
            const int ComputerNameDnsFullyQualified = 3;

            // winerror.h
            // @TODO: Use naming rules
            const int ERROR_MORE_DATA = 234;

            string value;
            if (s_isPlatformNT5)
            {
                // Length parameter must be zero if buffer is null
                int length = 0;

                // Query for the required length
                int getComputerNameExError = 0;
                if (Kernel32Safe.GetComputerNameEx(ComputerNameDnsFullyQualified, null, ref length) == 0)
                {
                    getComputerNameExError = Marshal.GetLastWin32Error();
                }

                // Ensure that GetComputerNameEx does not fail with unexpected values and that the
                // length is positive
                if ((getComputerNameExError != 0 && getComputerNameExError != ERROR_MORE_DATA) || length <= 0)
                {
                    throw ComputerNameEx(getComputerNameExError);
                }

                StringBuilder buffer = new(length);
                length = buffer.Capacity;
                if (Kernel32Safe.GetComputerNameEx(ComputerNameDnsFullyQualified, buffer, ref length) == 0)
                {
                    throw ComputerNameEx(Marshal.GetLastWin32Error());
                }

                // Note: In Longhorn you'll be able to rename a machine without
                // rebooting.  Therefore, don't cache this machine name.
                value = buffer.ToString();
            }
            else
            {
                value = MachineName();
            }
            return value;
        }

        // @TODO: ........ why do we need this?
        [FileIOPermission(SecurityAction.Assert, AllFiles = FileIOPermissionAccess.PathDiscovery)]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal static string GetFullPath(string filename) =>
            Path.GetFullPath(filename);

        internal static IntPtr IntPtrOffset(IntPtr pbase, int offset)
        {
            if (s_ptrSize == 4)
            {
                return (IntPtr)checked(pbase.ToInt32() + offset);
            }

            Debug.Assert(s_ptrSize == 8, "8 != IntPtr.Size");

            return (IntPtr)checked(pbase.ToInt64() + offset);
        }

        #endregion

        #region Exception Throwing Methods (sorted by users)

        #region DbConnectionOptions, DataAccess

        internal static ArgumentException InvalidKeyname(string parameterName) =>
            Argument(StringsHelper.GetString(Strings.ADP_InvalidKey), parameterName);

        internal static Exception InvalidMixedUsageOfAccessTokenAndCredential() =>
            InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfAccessTokenAndCredential));

        internal static ArgumentException InvalidValue(string parameterName) =>
            Argument(StringsHelper.GetString(Strings.ADP_InvalidValue), parameterName);

        #endregion

        // DbDataAdapter
        internal static InvalidOperationException ComputerNameEx(int lastError) =>
            InvalidOperation(StringsHelper.GetString(Strings.ADP_ComputerNameEx, lastError));

        // DBDataPermissionAttribute.KeyRestrictionBehavior
        internal static ArgumentOutOfRangeException InvalidKeyRestrictionBehavior(KeyRestrictionBehavior value)
        {
            // @TODO: Use a one line debug assert?
            #if DEBUG
            switch (value)
            {
                case KeyRestrictionBehavior.PreventUsage:
                case KeyRestrictionBehavior.AllowOnly:
                    Debug.Assert(false, "valid KeyRestrictionBehavior " + value.ToString());
                    break;
            }
            #endif

            return InvalidEnumerationValue(typeof(KeyRestrictionBehavior), (int)value);
        }

        #region DbDataPermission, DataAccess

        internal static Exception InvalidXMLBadVersion() =>
            Argument(StringsHelper.GetString(Strings.ADP_InvalidXMLBadVersion));

        internal static Exception NotAPermissionElement() =>
            Argument(StringsHelper.GetString(Strings.ADP_NotAPermissionElement));

        internal static Exception PermissionTypeMismatch() =>
            Argument(StringsHelper.GetString(Strings.ADP_PermissionTypeMismatch));

        #endregion

        // DbDataReader
        internal static Exception NumericToDecimalOverflow() =>
            InvalidCast(StringsHelper.GetString(Strings.ADP_NumericToDecimalOverflow));

        // SNI
        internal static PlatformNotSupportedException SNIPlatformNotSupported(string platform) =>
            new(StringsHelper.GetString(Strings.SNI_PlatformNotSupportedNetFx, platform));

        #endregion
    }
}

#endif
