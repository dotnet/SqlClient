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
        internal const string UseSystemDefaultSecureProtocolsString = @"Switch.Microsoft.Data.SqlClient.UseSystemDefaultSecureProtocols";
        internal const string SuppressTLSWarningString = @"Switch.Microsoft.Data.SqlClient.SuppressTLSWarning";

        private static bool s_makeReadAsyncBlocking;
        private static bool? s_LegacyRowVersionNullBehavior;
        private static bool? s_UseSystemDefaultSecureProtocols;
        private static bool? s_SuppressTLSWarning;

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

        public static bool SuppressTLSWarning
        {
            get
            {
                if (s_SuppressTLSWarning is null)
                {
                    bool result;
                    result = AppContext.TryGetSwitch(SuppressTLSWarningString, out result) ? result : false;
                    s_SuppressTLSWarning = result;
                }
                return s_SuppressTLSWarning.Value;
            }
        }

        public static bool MakeReadAsyncBlocking
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return AppContext.TryGetSwitch(MakeReadAsyncBlockingString, out s_makeReadAsyncBlocking) ? s_makeReadAsyncBlocking : false;
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
        public static bool UseSystemDefaultSecureProtocols
        {
            get
            {
                if (s_UseSystemDefaultSecureProtocols is null)
                {
                    bool result;
                    result = AppContext.TryGetSwitch(UseSystemDefaultSecureProtocolsString, out result) ? result : false;
                    s_UseSystemDefaultSecureProtocols = result;
                }
                return s_UseSystemDefaultSecureProtocols.Value;
            }
        }
    }
}
