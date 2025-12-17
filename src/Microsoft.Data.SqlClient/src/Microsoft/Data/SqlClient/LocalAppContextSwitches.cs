// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#nullable enable

namespace Microsoft.Data.SqlClient;

internal static class LocalAppContextSwitches
{
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
    private const string EnableMultiSubnetFailoverByDefaultString = @"Switch.Microsoft.Data.SqlClient.EnableMultiSubnetFailoverByDefault";

    #if NET
    private const string GlobalizationInvariantModeString = @"System.Globalization.Invariant";
    private const string GlobalizationInvariantModeEnvironmentVariable = "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT";

    #if _WINDOWS
    private const string UseManagedNetworkingOnWindowsString = "Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows";
    #endif
    #else
    private const string DisableTnirByDefaultString = @"Switch.Microsoft.Data.SqlClient.DisableTNIRByDefaultInConnectionString";
    #endif

    // this field is accessed through reflection in tests and should not be renamed or have the type changed without refactoring NullRow related tests
    internal static bool? s_legacyRowVersionNullBehavior;
    internal static bool? s_suppressInsecureTlsWarning;
    internal static bool? s_makeReadAsyncBlocking;
    internal static bool? s_useMinimumLoginTimeout;
    // this field is accessed through reflection in Microsoft.Data.SqlClient.Tests.SqlParameterTests and should not be renamed or have the type changed without refactoring related tests
    internal static bool? s_legacyVarTimeZeroScaleBehaviour;
    internal static bool? s_useCompatibilityProcessSni;
    internal static bool? s_useCompatibilityAsyncBehaviour;
    internal static bool? s_useConnectionPoolV2;
    internal static bool? s_truncateScaledDecimal;
    internal static bool? s_ignoreServerProvidedFailoverPartner;
    internal static bool? s_enableUserAgent;
    internal static bool? s_multiSubnetFailoverByDefault;

    #if NET
    internal static bool? s_globalizationInvariantMode;
    #endif
    #if NET && _WINDOWS
    internal static bool? s_useManagedNetworking;
    #endif
    #if NETFRAMEWORK
    internal static bool? s_disableTnirByDefault;
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
            // @TODO: Adopt netcore style of trace logs
            // Don't throw an exception for an invalid config file
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.ctor|INFO>: {1}", nameof(LocalAppContextSwitches), e);
        }
    }
    #endif

    // @TODO: Sort by name

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
            if (!s_useCompatibilityProcessSni.HasValue)

            {
                // Check if the switch has been set by the AppContext switch directly
                // If it has not been set, we default to true.
                if (!AppContext.TryGetSwitch(UseCompatibilityProcessSniString, out bool returnedValue) || returnedValue)
                {
                    s_useCompatibilityProcessSni = true;
                }
                else
                {
                    s_useCompatibilityProcessSni = false;
                }
            }
            return s_useCompatibilityProcessSni.Value;
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

            if (!s_useCompatibilityAsyncBehaviour.HasValue)
            {
                if (!AppContext.TryGetSwitch(UseCompatibilityAsyncBehaviourString, out bool returnedValue) || returnedValue)
                {
                    s_useCompatibilityAsyncBehaviour = true;
                }
                else
                {
                    s_useCompatibilityAsyncBehaviour = false;
                }
            }
            return s_useCompatibilityAsyncBehaviour.Value;
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
            if (!s_suppressInsecureTlsWarning.HasValue)
            {
                if (AppContext.TryGetSwitch(SuppressInsecureTlsWarningString, out bool returnedValue) && returnedValue)
                {
                    s_suppressInsecureTlsWarning = true;
                }
                else
                {
                    s_suppressInsecureTlsWarning = false;
                }
            }
            return s_suppressInsecureTlsWarning.Value;
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
            if (!s_legacyRowVersionNullBehavior.HasValue)
            {
                if (AppContext.TryGetSwitch(LegacyRowVersionNullString, out bool returnedValue) && returnedValue)
                {
                    s_legacyRowVersionNullBehavior = true;
                }
                else
                {
                    s_legacyRowVersionNullBehavior = false;
                }
            }
            return s_legacyRowVersionNullBehavior.Value;
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
            if (!s_makeReadAsyncBlocking.HasValue)
            {
                if (AppContext.TryGetSwitch(MakeReadAsyncBlockingString, out bool returnedValue) && returnedValue)
                {
                    s_makeReadAsyncBlocking = true;
                }
                else
                {
                    s_makeReadAsyncBlocking = false;
                }
            }
            return s_makeReadAsyncBlocking.Value;
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
            if (!s_useMinimumLoginTimeout.HasValue)
            {
                if (!AppContext.TryGetSwitch(UseMinimumLoginTimeoutString, out bool returnedValue) || returnedValue)
                {
                    s_useMinimumLoginTimeout = true;
                }
                else
                {
                    s_useMinimumLoginTimeout = false;
                }
            }
            return s_useMinimumLoginTimeout.Value;
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
            if (!s_legacyVarTimeZeroScaleBehaviour.HasValue)
            {
                if (!AppContext.TryGetSwitch(LegacyVarTimeZeroScaleBehaviourString, out bool returnedValue))
                {
                    s_legacyVarTimeZeroScaleBehaviour = true;
                }
                else
                {
                    s_legacyVarTimeZeroScaleBehaviour = returnedValue ? true : false;
                }
            }
            return s_legacyVarTimeZeroScaleBehaviour.Value;
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
            if (!s_useConnectionPoolV2.HasValue)
            {
                if (AppContext.TryGetSwitch(UseConnectionPoolV2String, out bool returnedValue) && returnedValue)
                {
                    s_useConnectionPoolV2 = true;
                }
                else
                {
                    s_useConnectionPoolV2 = false;
                }
            }
            return s_useConnectionPoolV2.Value;
        }
    }

    /// <summary>
    /// When set to true, TdsParser will truncate (rather than round) decimal and SqlDecimal values when scaling them.
    /// </summary>
    public static bool TruncateScaledDecimal
    {
        get
        {
            if (!s_truncateScaledDecimal.HasValue)
            {
                if (AppContext.TryGetSwitch(TruncateScaledDecimalString, out bool returnedValue) && returnedValue)
                {
                    s_truncateScaledDecimal = true;
                }
                else
                {
                    s_truncateScaledDecimal = false;
                }
            }
            return s_truncateScaledDecimal.Value;
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
            if (!s_ignoreServerProvidedFailoverPartner.HasValue)
            {
                if (AppContext.TryGetSwitch(IgnoreServerProvidedFailoverPartnerString, out bool returnedValue) && returnedValue)
                {
                    s_ignoreServerProvidedFailoverPartner = true;
                }
                else
                {
                    s_ignoreServerProvidedFailoverPartner = false;
                }
            }
            return s_ignoreServerProvidedFailoverPartner.Value;
        }
    }
    /// <summary>
    /// When set to true, the user agent feature is enabled and the driver will send the user agent string to the server.
    /// </summary>
    public static bool EnableUserAgent
    {
        get
        {
            if (!s_enableUserAgent.HasValue)
            {
                if (AppContext.TryGetSwitch(EnableUserAgentString, out bool returnedValue) && returnedValue)
                {
                    s_enableUserAgent = true;
                }
                else
                {
                    s_enableUserAgent = false;
                }
            }
            return s_enableUserAgent.Value;
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
            if (!s_globalizationInvariantMode.HasValue)
            {
                // Check if invariant mode has been set by the AppContext switch directly
                if (AppContext.TryGetSwitch(GlobalizationInvariantModeString, out bool returnedValue) && returnedValue)
                {
                    s_globalizationInvariantMode = true;
                }
                else
                {
                    // If the switch is not set, we check the environment variable as the first fallback
                    string? envValue = Environment.GetEnvironmentVariable(GlobalizationInvariantModeEnvironmentVariable);

                    if (string.Equals(envValue, bool.TrueString, StringComparison.OrdinalIgnoreCase) || string.Equals(envValue, "1", StringComparison.OrdinalIgnoreCase))
                    {
                        s_globalizationInvariantMode = true;
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
                                ? true
                                : false;
                        }
                        catch (System.Globalization.CultureNotFoundException)
                        {
                            // If the culture is not found, it means we are in invariant mode
                            s_globalizationInvariantMode = true;
                        }
                    }
                }
            }
            return s_globalizationInvariantMode.Value;
        }
    }
    #else
    /// <summary>
    /// .NET Framework does not support Globalization Invariant mode, so this will always be false.
    /// </summary>
    public static bool GlobalizationInvariantMode
    {
        get => false;
    }
    #endif

    #if NET

    #if _WINDOWS
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
            if (!s_useManagedNetworking.HasValue)
            {
                if (!OperatingSystem.IsWindows())
                {
                    s_useManagedNetworking = true;
                }
                else if (AppContext.TryGetSwitch(UseManagedNetworkingOnWindowsString, out bool returnedValue) && returnedValue)
                {
                    s_useManagedNetworking = true;
                }
                else
                {
                    s_useManagedNetworking = false;
                }
            }
            return s_useManagedNetworking.Value;
        }
    }
    #else
    /// <summary>
    /// .NET Core on Unix does not support the native SNI, so this will always be true.
    /// </summary>
    public static bool UseManagedNetworking => true;
    #endif

    #else
    /// <summary>
    /// .NET Framework does not support the managed SNI, so this will always be false.
    /// </summary>
    public static bool UseManagedNetworking => false;
    #endif

    #if NETFRAMEWORK
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
            if (!s_disableTnirByDefault.HasValue)
            {
                if (AppContext.TryGetSwitch(DisableTnirByDefaultString, out bool returnedValue) && returnedValue)
                {
                    s_disableTnirByDefault = true;
                }
                else
                {
                    s_disableTnirByDefault = false;
                }
            }
            return s_disableTnirByDefault.Value;
        }
    }
#endif

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
            if (!s_multiSubnetFailoverByDefault.HasValue)
            {
                if (AppContext.TryGetSwitch(EnableMultiSubnetFailoverByDefaultString, out bool returnedValue) && returnedValue)
                {
                    s_multiSubnetFailoverByDefault = true;
                }
                else
                {
                    s_multiSubnetFailoverByDefault = false;
                }
            }
            return s_multiSubnetFailoverByDefault.Value;
        }
    }
}
