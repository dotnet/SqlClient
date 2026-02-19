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

#if _WINDOWS
using Interop.Windows.Kernel32;
#endif

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

        // @TODO: Use naming rules
        // @TODO: All values but Unix and Windows32NT are used today, for netfx this should always be Win32NT. We can likely hard code this to true for netfx.
        internal static readonly bool s_isWindowsNT = Environment.OSVersion.Platform == PlatformID.Win32NT;

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

        // SNI
        internal static PlatformNotSupportedException SNIPlatformNotSupported(string platform) =>
            new(StringsHelper.GetString(Strings.SNI_PlatformNotSupportedNetFx, platform));

        #endregion
    }
}

#endif
