// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    internal static partial class LocalAppContextSwitches
    {
        internal const string MakeReadAsyncBlockingString = @"Switch.Microsoft.Data.SqlClient.MakeReadAsyncBlocking";
        internal const string LegacyRowVersionNullString = @"Switch.Microsoft.Data.SqlClient.LegacyRowVersionNullBehavior";
        internal const string SuppressInsecureTLSWarningString = @"Switch.Microsoft.Data.SqlClient.SuppressInsecureTLSWarning";
        internal const string UseMinimumLoginTimeoutString = @"Switch.Microsoft.Data.SqlClient.UseOneSecFloorInTimeoutCalculationDuringLogin";

        private static bool? s_legacyRowVersionNullBehavior;
        private static bool? s_suppressInsecureTLSWarning;
        private static bool s_makeReadAsyncBlocking;
        private static bool s_useMinimumLoginTimeout;

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
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.ctor|INFO>: {1}", nameof(LocalAppContextSwitches), e);
            }
        }
#endif

#if NETFRAMEWORK
        internal const string DisableTNIRByDefaultString = @"Switch.Microsoft.Data.SqlClient.DisableTNIRByDefaultInConnectionString";
        private static bool s_disableTNIRByDefault;

        /// <summary>
        /// Transparent Network IP Resolution (TNIR) is a revision of the existing MultiSubnetFailover feature.
        /// TNIR affects the connection sequence of the driver in the case where the first resolved IP of the hostname
        /// doesn't respond and there are multiple IPs associated with the hostname.
        /// 
        /// TNIR interacts with MultiSubnetFailover to provide the following three connection sequences:
        /// 0: One IP is attempted, followed by all IPs in parallel
        /// 1: All IPs are attempted in parallel
        /// 2: All IPs are attempted one after another
        /// 
        /// TransparentNetworkIPResolution is enabled by default. MultiSubnetFailover is disabled by default.
        /// To disable TNIR, you can enable the app context switch.
        /// 
        /// This app context switch defaults to 'false'.
        /// </summary>
        public static bool DisableTNIRByDefault
            => AppContext.TryGetSwitch(DisableTNIRByDefaultString, out s_disableTNIRByDefault) && s_disableTNIRByDefault;
#endif

        /// <summary>
        /// When using Encrypt=false in the connection string, a security warning is output to the console if the TLS version is 1.2 or lower.
        /// This warning can be suppressed by enabling this AppContext switch.
        /// This app context switch defaults to 'false'.
        /// </summary>
        public static bool SuppressInsecureTLSWarning
        {
            get
            {
                if (s_suppressInsecureTLSWarning is null)
                {
                    bool result;
                    result = AppContext.TryGetSwitch(SuppressInsecureTLSWarningString, out result) && result;
                    s_suppressInsecureTLSWarning = result;
                }
                return s_suppressInsecureTLSWarning.Value;
            }
        }

        /// <summary>
        /// In System.Data.SqlClient and Microsoft.Data.SqlClient prior to 3.0.0 a field with type Timestamp/RowVersion
        /// would return an empty byte array. This switch contols whether to preserve that behaviour on newer versions
        /// of Microsoft.Data.SqlClient, if this switch returns false an appropriate null value will be returned.
        /// This app context switch defaults to 'false'.
        /// </summary>
        public static bool LegacyRowVersionNullBehavior
        {
            get
            {
                if (s_legacyRowVersionNullBehavior is null)
                {
                    bool result;
                    result = AppContext.TryGetSwitch(LegacyRowVersionNullString, out result) && result;
                    s_legacyRowVersionNullBehavior = result;
                }
                return s_legacyRowVersionNullBehavior.Value;
            }
        }

        /// <summary>
        /// When enabled, ReadAsync runs asynchronously and does not block the calling thread.
        /// This app context switch defaults to 'false'.
        /// </summary>
        public static bool MakeReadAsyncBlocking
            => AppContext.TryGetSwitch(MakeReadAsyncBlockingString, out s_makeReadAsyncBlocking) && s_makeReadAsyncBlocking;

        /// <summary>
        /// Specifies minimum login timeout to be set to 1 second instead of 0 seconds,
        /// to prevent a login attempt from waiting indefinitely.
        /// This app context switch defaults to 'true'.
        /// </summary>
        public static bool UseMinimumLoginTimeout
            => !AppContext.TryGetSwitch(UseMinimumLoginTimeoutString, out s_useMinimumLoginTimeout) || s_useMinimumLoginTimeout;
    }
}
