// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    internal static partial class LocalAppContextSwitches
    {
        private enum Tristate : byte
        {
            NotInitialized = 0,
            False = 1,
            True = 2
        }

        internal const string MakeReadAsyncBlockingString = @"Switch.Microsoft.Data.SqlClient.MakeReadAsyncBlocking";
        internal const string LegacyRowVersionNullString = @"Switch.Microsoft.Data.SqlClient.LegacyRowVersionNullBehavior";
        internal const string SuppressInsecureTLSWarningString = @"Switch.Microsoft.Data.SqlClient.SuppressInsecureTLSWarning";
        internal const string UseMinimumLoginTimeoutString = @"Switch.Microsoft.Data.SqlClient.UseOneSecFloorInTimeoutCalculationDuringLogin";
        internal const string LegacyVarTimeZeroScaleBehaviourString = @"Switch.Microsoft.Data.SqlClient.LegacyVarTimeZeroScaleBehaviour";
        private const string IgnoreServerProvidedFailoverPartnerString = @"Switch.Microsoft.Data.SqlClient.IgnoreServerProvidedFailoverPartner";
        private const string EnableMultiSubnetFailoverByDefaultString = @"Switch.Microsoft.Data.SqlClient.EnableMultiSubnetFailoverByDefault";

        // this field is accessed through reflection in tests and should not be renamed or have the type changed without refactoring NullRow related tests
        private static Tristate s_legacyRowVersionNullBehavior;
        private static Tristate s_suppressInsecureTLSWarning;
        private static Tristate s_makeReadAsyncBlocking;
        private static Tristate s_useMinimumLoginTimeout;
        // this field is accessed through reflection in Microsoft.Data.SqlClient.Tests.SqlParameterTests and should not be renamed or have the type changed without refactoring related tests
        private static Tristate s_legacyVarTimeZeroScaleBehaviour;
        private static Tristate s_ignoreServerProvidedFailoverPartner;
        private static Tristate s_multiSubnetFailoverByDefault;

#if NET
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
        private static Tristate s_disableTNIRByDefault;

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
        {
            get
            {
                if (s_disableTNIRByDefault == Tristate.NotInitialized)
                {
                    if (AppContext.TryGetSwitch(DisableTNIRByDefaultString, out bool returnedValue) && returnedValue)
                    {
                        s_disableTNIRByDefault = Tristate.True;
                    }
                    else
                    {
                        s_disableTNIRByDefault = Tristate.False;
                    }
                }
                return s_disableTNIRByDefault == Tristate.True;
            }
        }
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
                if (s_suppressInsecureTLSWarning == Tristate.NotInitialized)
                {
                    if (AppContext.TryGetSwitch(SuppressInsecureTLSWarningString, out bool returnedValue) && returnedValue)
                    {
                        s_suppressInsecureTLSWarning = Tristate.True;
                    }
                    else
                    {
                        s_suppressInsecureTLSWarning = Tristate.False;
                    }
                }
                return s_suppressInsecureTLSWarning == Tristate.True;
            }
        }

        /// <summary>
        /// In System.Data.SqlClient and Microsoft.Data.SqlClient prior to 3.0.0 a field with type Timestamp/RowVersion
        /// would return an empty byte array. This switch controls whether to preserve that behaviour on newer versions
        /// of Microsoft.Data.SqlClient, if this switch returns false an appropriate null value will be returned.
        /// This app context switch defaults to 'false'.
        /// </summary>
        public static bool LegacyRowVersionNullBehavior
        {
            get
            {
                if (s_legacyRowVersionNullBehavior == Tristate.NotInitialized)
                {
                    if (AppContext.TryGetSwitch(LegacyRowVersionNullString, out bool returnedValue) && returnedValue)
                    {
                        s_legacyRowVersionNullBehavior = Tristate.True;
                    }
                    else
                    {
                        s_legacyRowVersionNullBehavior = Tristate.False;
                    }
                }
                return s_legacyRowVersionNullBehavior == Tristate.True;
            }
        }

        /// <summary>
        /// When enabled, ReadAsync runs asynchronously and does not block the calling thread.
        /// This app context switch defaults to 'false'.
        /// </summary>
        public static bool MakeReadAsyncBlocking
        {
            get
            {
                if (s_makeReadAsyncBlocking == Tristate.NotInitialized)
                {
                    if (AppContext.TryGetSwitch(MakeReadAsyncBlockingString, out bool returnedValue) && returnedValue)
                    {
                        s_makeReadAsyncBlocking = Tristate.True;
                    }
                    else
                    {
                        s_makeReadAsyncBlocking = Tristate.False;
                    }
                }
                return s_makeReadAsyncBlocking == Tristate.True;
            }
        }

        /// <summary>
        /// Specifies minimum login timeout to be set to 1 second instead of 0 seconds,
        /// to prevent a login attempt from waiting indefinitely.
        /// This app context switch defaults to 'true'.
        /// </summary>
        public static bool UseMinimumLoginTimeout
        {
            get
            {
                if (s_useMinimumLoginTimeout == Tristate.NotInitialized)
                {
                    if (!AppContext.TryGetSwitch(UseMinimumLoginTimeoutString, out bool returnedValue) || returnedValue)
                    {
                        s_useMinimumLoginTimeout = Tristate.True;
                    }
                    else
                    {
                        s_useMinimumLoginTimeout = Tristate.False;
                    }
                }
                return s_useMinimumLoginTimeout == Tristate.True;
            }
        }


        /// <summary>
        /// When set to 'true' this will output a scale value of 7 (DEFAULT_VARTIME_SCALE) when the scale 
        /// is explicitly set to zero for VarTime data types ('datetime2', 'datetimeoffset' and 'time')
        /// If no scale is set explicitly it will continue to output scale of 7 (DEFAULT_VARTIME_SCALE)
        /// regardless of switch value.
        /// This app context switch defaults to 'true'.
        /// </summary>
        public static bool LegacyVarTimeZeroScaleBehaviour
        {
            get
            {
                if (s_legacyVarTimeZeroScaleBehaviour == Tristate.NotInitialized)
                {
                    if (!AppContext.TryGetSwitch(LegacyVarTimeZeroScaleBehaviourString, out bool returnedValue))
                    {
                        s_legacyVarTimeZeroScaleBehaviour = Tristate.True;
                    }
                    else
                    {
                        s_legacyVarTimeZeroScaleBehaviour = returnedValue ? Tristate.True : Tristate.False;
                    }
                }
                return s_legacyVarTimeZeroScaleBehaviour == Tristate.True;
            }
        }

        /// <summary>
        /// When set to true, the failover partner provided by the server during connection
        /// will be ignored. This is useful in scenarios where the application wants to
        /// control the failover behavior explicitly (e.g. using a custom port). The application 
        /// must be kept up to date with the failover configuration of the server. 
        /// The application will not automatically discover a newly configured failover partner.
        /// 
        /// This app context switch defaults to 'false'.
        /// </summary>
        public static bool IgnoreServerProvidedFailoverPartner
        {
            get
            {
                if (s_ignoreServerProvidedFailoverPartner == Tristate.NotInitialized)
                {
                    if (AppContext.TryGetSwitch(IgnoreServerProvidedFailoverPartnerString, out bool returnedValue) && returnedValue)
                    {
                        s_ignoreServerProvidedFailoverPartner = Tristate.True;
                    }
                    else
                    {
                        s_ignoreServerProvidedFailoverPartner = Tristate.False;
                    }
                }
                return s_ignoreServerProvidedFailoverPartner == Tristate.True;
            }
        }

        /// <summary>
        /// When set to true, the default value for MultiSubnetFailover connection string property
        /// will be true instead of false. This enables parallel IP connection attempts for 
        /// improved connection times in multi-subnet environments.
        /// This app context switch defaults to 'false'.
        /// </summary>
        public static bool EnableMultiSubnetFailoverByDefault
        {
            get
            {
                if (s_multiSubnetFailoverByDefault == Tristate.NotInitialized)
                {
                    if (AppContext.TryGetSwitch(EnableMultiSubnetFailoverByDefaultString, out bool returnedValue) && returnedValue)
                    {
                        s_multiSubnetFailoverByDefault = Tristate.True;
                    }
                    else
                    {
                        s_multiSubnetFailoverByDefault = Tristate.False;
                    }
                }
                return s_multiSubnetFailoverByDefault == Tristate.True;
            }
        }
    }
}
