// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.Data.SqlClient
{
    internal static partial class LocalAppContextSwitches
    {
        private const string TypeName = nameof(LocalAppContextSwitches);
        internal const string MakeReadAsyncBlockingString = @"Switch.Microsoft.Data.SqlClient.MakeReadAsyncBlocking";
        internal const string LegacyRowVersionNullString = @"Switch.Microsoft.Data.SqlClient.LegacyRowVersionNullBehavior";
        internal const string EnableSecureProtocolsByOSString = @"Switch.Microsoft.Data.SqlClient.EnableSecureProtocolsByOS";
        // safety switch
        internal const string EnableRetryLogicSwitch = "Switch.Microsoft.Data.SqlClient.EnableRetryLogic";

        private static bool _makeReadAsyncBlocking;
        private static bool? s_LegacyRowVersionNullBehavior;
        private static bool? s_EnableSecureProtocolsByOS;
        private static bool? s_isRetryEnabled = null;

#if !NETFRAMEWORK
        static LocalAppContextSwitches()
        {
            IAppContextSwitchOverridesSection appContextSwitch = AppConfigManager.FetchConfigurationSection<AppContextSwitchOverridesSection>(AppContextSwitchOverridesSection.Name);
            try
            {
                SqlAppContextSwitchManager.ApplyContextSwitches(appContextSwitch);
            }
            catch (Exception e)
            {
                // Don't throw an exception for an invalid config file
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO>: {2}", TypeName, MethodBase.GetCurrentMethod().Name, e);
            }
        }
#endif

        internal static bool IsRetryEnabled
        {
            get
            {
                if (s_isRetryEnabled is null)
                {
                    bool result;
                    result = AppContext.TryGetSwitch(EnableRetryLogicSwitch, out result) ? result : false;
                    s_isRetryEnabled = result;
                }
                return s_isRetryEnabled.Value;
            }
        }

        public static bool MakeReadAsyncBlocking
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return AppContext.TryGetSwitch(MakeReadAsyncBlockingString, out _makeReadAsyncBlocking) ? _makeReadAsyncBlocking : false;
            }
        }

        /// <summary>
        /// In System.Data.SqlClient and Microsoft.Data.SqlClient prior to 3.0.0 a field with type Timestamp/RowVersion
        /// would return an empty byte array. This switch contols whether to preserve that behaviour on newer versions
        /// of Microsoft.Data.SqlClient, if this switch returns false an appropriate null value will be returned
        /// </summary>
        public static bool LegacyRowVersionNullBehavior
        {
            get
            {
                if (s_LegacyRowVersionNullBehavior is null)
                {
                    bool result;
                    result = AppContext.TryGetSwitch(LegacyRowVersionNullString, out result) ? result : false;
                    s_LegacyRowVersionNullBehavior = result;
                }
                return s_LegacyRowVersionNullBehavior.Value;
            }
        }

        /// <summary>
        /// For backward compatibility, this switch can be on to jump back on OS preferences.
        /// </summary>
        public static bool EnableSecureProtocolsByOS
        {
            get
            {
                if (s_EnableSecureProtocolsByOS is null)
                {
                    bool result;
                    result = AppContext.TryGetSwitch(EnableSecureProtocolsByOSString, out result) ? result : false;
                    s_EnableSecureProtocolsByOS = result;
                }
                return s_EnableSecureProtocolsByOS.Value;
            }
        }
    }
}
