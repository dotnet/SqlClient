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

        private const string MakeReadAsyncBlockingString = @"Switch.Microsoft.Data.SqlClient.MakeReadAsyncBlocking";
        private const string LegacyRowVersionNullString = @"Switch.Microsoft.Data.SqlClient.LegacyRowVersionNullBehavior";
        private const string SuppressInsecureTlsWarningString = @"Switch.Microsoft.Data.SqlClient.SuppressInsecureTLSWarning";
        private const string UseMinimumLoginTimeoutString = @"Switch.Microsoft.Data.SqlClient.UseOneSecFloorInTimeoutCalculationDuringLogin";
        private const string LegacyVarTimeZeroScaleBehaviourString = @"Switch.Microsoft.Data.SqlClient.LegacyVarTimeZeroScaleBehaviour";
        private const string UseCompatibilityProcessSniString = @"Switch.Microsoft.Data.SqlClient.UseCompatibilityProcessSni";
        private const string UseCompatibilityAsyncBehaviourString = @"Switch.Microsoft.Data.SqlClient.UseCompatibilityAsyncBehaviour";
        private const string UseConnectionPoolV2String = @"Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2";
        private const string TruncateScaledDecimalString = @"Switch.Microsoft.Data.SqlClient.TruncateScaledDecimal";
        private const string IgnoreServerProvidedFailoverPartnerString = @"Switch.Microsoft.Data.SqlClient.IgnoreServerProvidedFailoverPartner";
        private const string EnableUserAgentString = @"Switch.Microsoft.Data.SqlClient.EnableUserAgent";
#if NET
        private const string GlobalizationInvariantModeString = @"System.Globalization.Invariant";
        private const string GlobalizationInvariantModeEnvironmentVariable = "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT";
        private const string UseManagedNetworkingOnWindowsString = "Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows";
#else
        private const string DisableTnirByDefaultString = @"Switch.Microsoft.Data.SqlClient.DisableTNIRByDefaultInConnectionString";
#endif

        // this field is accessed through reflection in tests and should not be renamed or have the type changed without refactoring NullRow related tests
        private static Tristate s_legacyRowVersionNullBehavior;
        private static Tristate s_suppressInsecureTlsWarning;
        private static Tristate s_makeReadAsyncBlocking;
        private static Tristate s_useMinimumLoginTimeout;
        // this field is accessed through reflection in Microsoft.Data.SqlClient.Tests.SqlParameterTests and should not be renamed or have the type changed without refactoring related tests
        private static Tristate s_legacyVarTimeZeroScaleBehaviour;
        private static Tristate s_useCompatibilityProcessSni;
        private static Tristate s_useCompatibilityAsyncBehaviour;
        private static Tristate s_useConnectionPoolV2;
        private static Tristate s_truncateScaledDecimal;
        private static Tristate s_ignoreServerProvidedFailoverPartner;
        private static Tristate s_enableUserAgent;
#if NET
        private static Tristate s_globalizationInvariantMode;
        private static Tristate s_useManagedNetworking;
#else
        private static Tristate s_disableTnirByDefault;
#endif

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

        /// <summary>
        /// In TdsParser, the ProcessSni function changed significantly when the packet
        /// multiplexing code needed for high speed multi-packet column values was added.
        /// When this switch is set to true (the default), the old ProcessSni design is used.
        /// When this switch is set to false, the new experimental ProcessSni behavior using
        /// the packet multiplexer is enabled.
        /// </summary>
        public static bool UseCompatibilityProcessSni
        {
            get
            {
                if (s_useCompatibilityProcessSni == Tristate.NotInitialized)
                {
                    // Check if the switch has been set by the AppContext switch directly
                    // If it has not been set, we default to true.
                    if (!AppContext.TryGetSwitch(UseCompatibilityProcessSniString, out bool returnedValue) || returnedValue)
                    {
                        s_useCompatibilityProcessSni = Tristate.True;
                    }
                    else
                    {
                        s_useCompatibilityProcessSni = Tristate.False;
                    }
                }
                return s_useCompatibilityProcessSni == Tristate.True;
            }
        }

        /// <summary>
        /// In TdsParser, the async multi-packet column value fetch behavior can use a continue snapshot state
        /// for improved efficiency. When this switch is enabled (the default), the driver preserves the legacy
        /// compatibility behavior, which does not use the continue snapshot state. When disabled, the new behavior
        /// using the continue snapshot state is enabled. This switch will always return true if
        /// <see cref="UseCompatibilityProcessSni"/> is enabled, because the continue state is not stable without
        /// the multiplexer.
        /// </summary>
        public static bool UseCompatibilityAsyncBehaviour
        {
            get
            {
                if (UseCompatibilityProcessSni)
                {
                    // If ProcessSni compatibility mode has been enabled then the packet
                    // multiplexer has been disabled. The new async behaviour using continue
                    // point capture is only stable if the multiplexer is enabled so we must
                    // return true to enable compatibility async behaviour using only restarts.
                    return true;
                }

                if (s_useCompatibilityAsyncBehaviour == Tristate.NotInitialized)
                {
                    if (!AppContext.TryGetSwitch(UseCompatibilityAsyncBehaviourString, out bool returnedValue) || returnedValue)
                    {
                        s_useCompatibilityAsyncBehaviour = Tristate.True;
                    }
                    else
                    {
                        s_useCompatibilityAsyncBehaviour = Tristate.False;
                    }
                }
                return s_useCompatibilityAsyncBehaviour == Tristate.True;
            }
        }

        /// <summary>
        /// When using Encrypt=false in the connection string, a security warning is output to the console if the TLS version is 1.2 or lower.
        /// This warning can be suppressed by enabling this AppContext switch.
        /// This app context switch defaults to 'false'.
        /// </summary>
        public static bool SuppressInsecureTlsWarning
        {
            get
            {
                if (s_suppressInsecureTlsWarning == Tristate.NotInitialized)
                {
                    if (AppContext.TryGetSwitch(SuppressInsecureTlsWarningString, out bool returnedValue) && returnedValue)
                    {
                        s_suppressInsecureTlsWarning = Tristate.True;
                    }
                    else
                    {
                        s_suppressInsecureTlsWarning = Tristate.False;
                    }
                }
                return s_suppressInsecureTlsWarning == Tristate.True;
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
        /// When set to true, the connection pool will use the new V2 connection pool implementation.
        /// When set to false, the connection pool will use the legacy V1 implementation.
        /// This app context switch defaults to 'false'.
        /// </summary>
        public static bool UseConnectionPoolV2
        {
            get
            {
                if (s_useConnectionPoolV2 == Tristate.NotInitialized)
                {
                    if (AppContext.TryGetSwitch(UseConnectionPoolV2String, out bool returnedValue) && returnedValue)
                    {
                        s_useConnectionPoolV2 = Tristate.True;
                    }
                    else
                    {
                        s_useConnectionPoolV2 = Tristate.False;
                    }
                }
                return s_useConnectionPoolV2 == Tristate.True;
            }
        }

        /// <summary>
        /// When set to true, TdsParser will truncate (rather than round) decimal and SqlDecimal values when scaling them.
        /// </summary>
        public static bool TruncateScaledDecimal
        {
            get
            {
                if (s_truncateScaledDecimal == Tristate.NotInitialized)
                {
                    if (AppContext.TryGetSwitch(TruncateScaledDecimalString, out bool returnedValue) && returnedValue)
                    {
                        s_truncateScaledDecimal = Tristate.True;
                    }
                    else
                    {
                        s_truncateScaledDecimal = Tristate.False;
                    }
                }
                return s_truncateScaledDecimal == Tristate.True;
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
        /// When set to true, the user agent feature is enabled and the driver will send the user agent string to the server.
        /// </summary>
        public static bool EnableUserAgent
        {
            get
            {
                if (s_enableUserAgent == Tristate.NotInitialized)
                {
                    if (AppContext.TryGetSwitch(EnableUserAgentString, out bool returnedValue) && returnedValue)
                    {
                        s_enableUserAgent = Tristate.True;
                    }
                    else
                    {
                        s_enableUserAgent = Tristate.False;
                    }
                }
                return s_enableUserAgent == Tristate.True;
            }
        }
#if NET
        /// <summary>
        /// .NET Core 2.0 and up supports Globalization Invariant mode, which reduces the size of the required libraries for
        /// applications which don't need globalization support. SqlClient requires those libraries for core functionality,
        /// and will throw exceptions later if they are not present. This switch allows SqlClient to detect this mode early.
        /// </summary>
        public static bool GlobalizationInvariantMode
        {
            get
            {
                if (s_globalizationInvariantMode == Tristate.NotInitialized)
                {
                    // Check if invariant mode is has been set by the AppContext switch directly
                    if (AppContext.TryGetSwitch(GlobalizationInvariantModeString, out bool returnedValue) && returnedValue)
                    {
                        s_globalizationInvariantMode = Tristate.True;
                    }
                    else
                    {
                        // If the switch is not set, we check the environment variable as the first fallback
                        string envValue = Environment.GetEnvironmentVariable(GlobalizationInvariantModeEnvironmentVariable);

                        if (string.Equals(envValue, bool.TrueString, StringComparison.OrdinalIgnoreCase) || string.Equals(envValue, "1", StringComparison.OrdinalIgnoreCase))
                        {
                            s_globalizationInvariantMode = Tristate.True;
                        }
                        else
                        {
                            // If this hasn't been manually set, it could still apply if the OS doesn't have ICU libraries installed,
                            // or if the application is a native binary with ICU support trimmed away.
                            // .NET 3.1 to 5.0 do not throw in attempting to create en-US in invariant mode, but .NET 6+ does. In
                            // such cases, catch and infer invariant mode from the exception.
                            try
                            {
                                s_globalizationInvariantMode = System.Globalization.CultureInfo.GetCultureInfo("en-US").EnglishName.Contains("Invariant")
                                    ? Tristate.True
                                    : Tristate.False;
                            }
                            catch (System.Globalization.CultureNotFoundException)
                            {
                                // If the culture is not found, it means we are in invariant mode
                                s_globalizationInvariantMode = Tristate.True;
                            }
                        }
                    }
                }
                return s_globalizationInvariantMode == Tristate.True;
            }
        }

        /// <summary>
        /// When set to true, .NET Core will use the managed SNI implementation instead of the native SNI implementation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Non-Windows platforms will always use the managed networking implementation. Windows platforms will use the native SNI
        /// implementation by default, but this can be overridden by setting the AppContext switch.
        /// </para>
        /// <para>
        /// ILLink.Substitutions.xml allows the unused SNI implementation to be trimmed away when the corresponding AppContext
        /// switch is set at compile time. In such cases, this property will return a constant value, even if the AppContext switch is
        /// set or reset at runtime. See the ILLink.Substitutions.Windows.xml and ILLink.Substitutions.Unix.xml resource files for details.
        /// </para>
        /// </remarks>
        public static bool UseManagedNetworking
        {
            get
            {
                if (s_useManagedNetworking == Tristate.NotInitialized)
                {
                    if (!OperatingSystem.IsWindows())
                    {
                        s_useManagedNetworking = Tristate.True;
                    }
                    else if (AppContext.TryGetSwitch(UseManagedNetworkingOnWindowsString, out bool returnedValue) && returnedValue)
                    {
                        s_useManagedNetworking = Tristate.True;
                    }
                    else
                    {
                        s_useManagedNetworking = Tristate.False;
                    }
                }
                return s_useManagedNetworking == Tristate.True;
            }
        }
#else
        /// <summary>
        /// .NET Framework does not support Globalization Invariant mode, so this will always be false.
        /// </summary>
        public const bool GlobalizationInvariantMode = false;

        /// <summary>
        /// .NET Framework does not support the managed SNI, so this will always be false.
        /// </summary>
        public const bool UseManagedNetworking = false;

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
        public static bool DisableTnirByDefault
        {
            get
            {
                if (s_disableTnirByDefault == Tristate.NotInitialized)
                {
                    if (AppContext.TryGetSwitch(DisableTnirByDefaultString, out bool returnedValue) && returnedValue)
                    {
                        s_disableTnirByDefault = Tristate.True;
                    }
                    else
                    {
                        s_disableTnirByDefault = Tristate.False;
                    }
                }
                return s_disableTnirByDefault == Tristate.True;
            }
        }
#endif
    }
}
