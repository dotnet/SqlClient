// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <summary>
/// This class provides immutable access to the app context switches used
/// throughout the codebase.  Each switch value is read once (on first access)
/// and cached for future access.  Most switch values are obtained solely from
/// the AppContext, but some switches may use other mechanisms to determine
/// their values as well (e.g. environment variables).
/// </summary>
internal static class LocalAppContextSwitches
{
    #region Switch Names

    #if NETFRAMEWORK
    /// <summary>
    /// The name of the app context switch that controls whether TNIR is
    /// disabled by default in the connection string.
    /// </summary>
    private const string DisableTnirByDefaultString =
        "Switch.Microsoft.Data.SqlClient.DisableTNIRByDefaultInConnectionString";
    #endif

    /// <summary>
    /// The name of the app context switch that controls whether
    /// MultiSubnetFailover is enabled by default in the connection string.
    /// </summary>
    private const string EnableMultiSubnetFailoverByDefaultString =
        "Switch.Microsoft.Data.SqlClient.EnableMultiSubnetFailoverByDefault";

    /// <summary>
    /// The name of the app context switch that controls whether
    /// the user agent feature is enabled.
    /// </summary>
    private const string EnableUserAgentString =
        "Switch.Microsoft.Data.SqlClient.EnableUserAgent";

    #if NET
    /// <summary>
    /// The name of the app context switch that controls whether
    /// Globalization Invariant mode is enabled.
    /// </summary>
    private const string GlobalizationInvariantModeString =
        "System.Globalization.Invariant";
    /// <summary>
    /// The name of the environment variable that controls whether
    /// Globalization Invariant mode is enabled.
    /// </summary>
    private const string GlobalizationInvariantModeEnvironmentVariable =
        "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT";
    #endif

    /// <summary>
    /// The name of the app context switch that controls whether the failover
    /// partner provided by the server during connection will be ignored.
    /// </summary>
    private const string IgnoreServerProvidedFailoverPartnerString =
        "Switch.Microsoft.Data.SqlClient.IgnoreServerProvidedFailoverPartner";

    /// <summary>
    /// The name of the app context switch that controls whether to preserve
    /// legacy behavior where Timestamp/RowVersion fields return empty byte
    /// arrays instead of null.
    /// </summary>
    private const string LegacyRowVersionNullString =
        "Switch.Microsoft.Data.SqlClient.LegacyRowVersionNullBehavior";

    /// <summary>
    /// The name of the app context switch that controls whether to preserve
    /// legacy VarTime zero scale behavior for datetime2, datetimeoffset, and
    /// time data types.
    /// </summary>
    private const string LegacyVarTimeZeroScaleBehaviourString =
        "Switch.Microsoft.Data.SqlClient.LegacyVarTimeZeroScaleBehaviour";

    /// <summary>
    /// The name of the app context switch that controls whether
    /// ReadAsync operations run synchronously and block the calling thread.
    /// </summary>
    private const string MakeReadAsyncBlockingString =
        "Switch.Microsoft.Data.SqlClient.MakeReadAsyncBlocking";

    /// <summary>
    /// The name of the app context switch that controls whether to suppress
    /// the security warning when using Encrypt=false with TLS 1.2 or lower.
    /// </summary>
    private const string SuppressInsecureTlsWarningString =
        "Switch.Microsoft.Data.SqlClient.SuppressInsecureTLSWarning";

    /// <summary>
    /// The name of the app context switch that controls whether to suppress
    /// the creation of <see cref="System.Diagnostics.Activity"/> instances.
    /// </summary>
    private const string SuppressNativeActivityTelemetryString =
        "Switch.Microsoft.Data.SqlClient.SuppressNativeActivityTelemetry";

    /// <summary>
    /// The name of the app context switch that controls whether TdsParser
    /// truncates (rather than rounds) decimal and SqlDecimal values when
    /// scaling them.
    /// </summary>
    private const string TruncateScaledDecimalString =
        "Switch.Microsoft.Data.SqlClient.TruncateScaledDecimal";

    /// <summary>
    /// The name of the app context switch that controls whether to use legacy
    /// async multi-packet column value fetch behavior without continue snapshot
    /// state.
    /// </summary>
    private const string UseCompatibilityAsyncBehaviourString =
        "Switch.Microsoft.Data.SqlClient.UseCompatibilityAsyncBehaviour";

    /// <summary>
    /// The name of the app context switch that controls whether to use the old
    /// ProcessSni design instead of the packet multiplexing implementation.
    /// </summary>
    private const string UseCompatibilityProcessSniString =
        "Switch.Microsoft.Data.SqlClient.UseCompatibilityProcessSni";

    /// <summary>
    /// The name of the app context switch that controls whether to use the new
    /// V2 connection pool implementation or the legacy V1 implementation.
    /// </summary>
    private const string UseConnectionPoolV2String =
        "Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2";

    #if NET && _WINDOWS
    /// <summary>
    /// The name of the app context switch that controls whether to use the
    /// managed SNI implementation instead of the native SNI implementation on
    /// Windows.
    /// </summary>
    private const string UseManagedNetworkingOnWindowsString =
        "Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows";
    #endif

    /// <summary>
    /// The name of the app context switch that controls whether to enforce
    /// a minimum login timeout of 1 second instead of 0 seconds.
    /// </summary>
    private const string UseMinimumLoginTimeoutString =
        "Switch.Microsoft.Data.SqlClient.UseOneSecFloorInTimeoutCalculationDuringLogin";
    
    #endregion

    #region Switch Values

    /// <summary>
    /// We use a byte-based enum to track the value of each switch.  This plays
    /// nicely with threaded access.  A nullable bool would seem to be the
    /// obvious choice, but the way nullable bools are implemented in the CLR
    /// makes them not thread-safe without using locks (the HasValue and Value
    /// properties can get out of sync if one thread is writing while another is
    /// reading).
    /// </summary>
    private enum SwitchValue : byte
    {
        None = 0,
        True = 1,
        False = 2
    }

    // GOTCHA: These fields are accessed via reflection by the
    // LocalAppContextSwitchesHelper test helper class.  If you rename them, be
    // sure to update the test helper as well.
    
    #if NETFRAMEWORK
    /// <summary>
    /// The cached value of the DisableTnirByDefault switch.
    /// </summary>
    private static SwitchValue s_disableTnirByDefault = SwitchValue.None;
    #endif

    /// <summary>
    /// The cached value of the EnableMultiSubnetFailoverByDefault switch.
    /// </summary>
    private static SwitchValue s_enableMultiSubnetFailoverByDefault = SwitchValue.None;

    /// <summary>
    /// The cached value of the EnableUserAgent switch.
    /// </summary>
    private static SwitchValue s_enableUserAgent = SwitchValue.None;

    #if NET
    /// <summary>
    /// The cached value of the GlobalizationInvariantMode switch.
    /// </summary>
    private static SwitchValue s_globalizationInvariantMode = SwitchValue.None;
    #endif

    /// <summary>
    /// The cached value of the IgnoreServerProvidedFailoverPartner switch.
    /// </summary>
    private static SwitchValue s_ignoreServerProvidedFailoverPartner = SwitchValue.None;

    /// <summary>
    /// The cached value of the LegacyRowVersionNullBehavior switch.
    /// </summary>
    private static SwitchValue s_legacyRowVersionNullBehavior = SwitchValue.None;

    /// <summary>
    /// The cached value of the LegacyVarTimeZeroScaleBehaviour switch.
    /// </summary>
    private static SwitchValue s_legacyVarTimeZeroScaleBehaviour = SwitchValue.None;

    /// <summary>
    /// The cached value of the MakeReadAsyncBlocking switch.
    /// </summary>
    private static SwitchValue s_makeReadAsyncBlocking = SwitchValue.None;

    /// <summary>
    /// The cached value of the SuppressInsecureTlsWarning switch.
    /// </summary>
    private static SwitchValue s_suppressInsecureTlsWarning = SwitchValue.None;

    /// <summary>
    /// The cached value of the SuppressNativeActivityTelemetry switch.
    /// </summary>
    private static SwitchValue s_suppressNativeActivityTelemetry = SwitchValue.None;

    /// <summary>
    /// The cached value of the TruncateScaledDecimal switch.
    /// </summary>
    private static SwitchValue s_truncateScaledDecimal = SwitchValue.None;

    /// <summary>
    /// The cached value of the UseCompatibilityAsyncBehaviour switch.
    /// </summary>
    private static SwitchValue s_useCompatibilityAsyncBehaviour = SwitchValue.None;

    /// <summary>
    /// The cached value of the UseCompatibilityProcessSni switch.
    /// </summary>
    private static SwitchValue s_useCompatibilityProcessSni = SwitchValue.None;

    /// <summary>
    /// The cached value of the UseConnectionPoolV2 switch.
    /// </summary>
    private static SwitchValue s_useConnectionPoolV2 = SwitchValue.None;

    #if NET && _WINDOWS
    /// <summary>
    /// The cached value of the UseManagedNetworking switch.
    /// </summary>
    private static SwitchValue s_useManagedNetworking = SwitchValue.None;
    #endif

    /// <summary>
    /// The cached value of the UseMinimumLoginTimeout switch.
    /// </summary>
    private static SwitchValue s_useMinimumLoginTimeout = SwitchValue.None;

    #endregion

    #region Static Initialization

    #if NET
    /// <summary>
    /// Static construction for .NET reads the AppContextSwitchOverridesSection
    /// from the default app config file and applies any switch values found
    /// there to the AppContext.  These values are then later read and cached by
    /// the individual switch properties on first access.
    /// </summary>
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

    #endregion

    #region Switch Properties

    #if NETFRAMEWORK
    /// <summary>
    /// Transparent Network IP Resolution (TNIR) is a revision of the existing
    /// MultiSubnetFailover feature.  TNIR affects the connection sequence of
    /// the driver in the case where the first resolved IP of the hostname
    /// doesn't respond and there are multiple IPs associated with the hostname.
    ///
    /// TNIR interacts with MultiSubnetFailover to provide the following three
    /// connection sequences:
    ///
    ///   0: One IP is attempted, followed by all IPs in parallel
    ///   1: All IPs are attempted in parallel
    ///   2: All IPs are attempted one after another
    ///
    /// TransparentNetworkIPResolution is enabled by default.
    /// MultiSubnetFailover is disabled by default.  To disable TNIR, you can
    /// enable the app context switch.
    ///
    /// The default value of this switch is false.
    /// </summary>
    public static bool DisableTnirByDefault =>
        AcquireAndReturn(
            DisableTnirByDefaultString,
            defaultValue: false,
            ref s_disableTnirByDefault);
    #endif

    /// <summary>
    /// When set to true, the default value for MultiSubnetFailover connection
    /// string property will be true instead of false. This enables parallel IP
    /// connection attempts for improved connection times in multi-subnet
    /// environments.
    ///
    /// The default value of this switch is false.
    /// </summary>
    public static bool EnableMultiSubnetFailoverByDefault =>
        AcquireAndReturn(
            EnableMultiSubnetFailoverByDefaultString,
            defaultValue: false,
            ref s_enableMultiSubnetFailoverByDefault);

    /// <summary>
    /// When set to true, the user agent feature is enabled and the driver will
    /// send the user agent string to the server.
    ///
    /// The default value of this switch is false.
    /// </summary>
    public static bool EnableUserAgent =>
        AcquireAndReturn(
            EnableUserAgentString,
            defaultValue: false,
            ref s_enableUserAgent);

    #if NET
    /// <summary>
    /// .NET Core 2.0 and up supports Globalization Invariant mode, which
    /// reduces the size of the required libraries for applications which don't
    /// need globalization support. SqlClient requires those libraries for core
    /// functionality, and will throw exceptions later if they are not present.
    /// This switch allows SqlClient to detect this mode early.
    ///
    /// The value of this switch is determined first by checking the AppContext.
    /// If not set there, it falls back to checking the environment variable
    /// DOTNET_SYSTEM_GLOBALIZATION_INVARIANT.  If neither is set, it attempts to
    /// create the "en-US" culture and infers invariant mode from whether that
    /// operation throws an exception.
    ///
    /// The default value of this switch is false.
    /// </summary>
    public static bool GlobalizationInvariantMode
    {
        get
        {
            if (s_globalizationInvariantMode != SwitchValue.None)
            {
                return s_globalizationInvariantMode == SwitchValue.True;
            }

            // Check if invariant mode has been set by the AppContext switch directly
            if (AppContext.TryGetSwitch(GlobalizationInvariantModeString, out bool returnedValue) && returnedValue)
            {
                s_globalizationInvariantMode = SwitchValue.True;
                return true;
            }

            // TODO(https://github.com/dotnet/SqlClient/pull/3853):
            //
            // The intention of the comment below doesn't match the code.
            //
            // The comment claims to fallback to the environment variable if the
            // switch is not set.  However, it actually falls-back if the switch
            // is not set _OR_ it is set to false.
            //
            // Should we update the comment or fix the code to match?

            // If the switch is not set, we check the environment variable as the first fallback
            string? envValue = Environment.GetEnvironmentVariable(GlobalizationInvariantModeEnvironmentVariable);

            if (string.Equals(envValue, bool.TrueString, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(envValue, "1", StringComparison.OrdinalIgnoreCase))
            {
                s_globalizationInvariantMode = SwitchValue.True;
                return true;
            }

            // TODO(https://github.com/dotnet/SqlClient/pull/3853):
            //
            // What if the environment variable is set to false?  Why are we
            // ignoring that case?

            // If this hasn't been manually set, it could still apply if the OS
            // doesn't have ICU libraries installed, or if the application is a
            // native binary with ICU support trimmed away.  .NET 3.1 to 5.0 do
            // not throw in attempting to create en-US in invariant mode, but
            // .NET 6+ does. In such cases, catch and infer invariant mode from
            // the exception.
            try
            {
                s_globalizationInvariantMode = System.Globalization.CultureInfo.GetCultureInfo("en-US").EnglishName.Contains("Invariant")
                    ? SwitchValue.True
                    : SwitchValue.False;
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                // If the culture is not found, it means we are in invariant mode
                s_globalizationInvariantMode = SwitchValue.True;
            }

            return s_globalizationInvariantMode == SwitchValue.True;
        }
    }
    #else
    /// <summary>
    /// .NET Framework does not support Globalization Invariant mode, so this
    /// will always be false.
    /// </summary>
    public static bool GlobalizationInvariantMode => false;
    #endif

    /// <summary>
    /// When set to true, the failover partner provided by the server during
    /// connection will be ignored. This is useful in scenarios where the
    /// application wants to control the failover behavior explicitly (e.g.
    /// using a custom port). The application must be kept up to date with the
    /// failover configuration of the server.  The application will not
    /// automatically discover a newly configured failover partner.
    ///
    /// The default value of this switch is false.
    /// </summary>
    public static bool IgnoreServerProvidedFailoverPartner =>
        AcquireAndReturn(
            IgnoreServerProvidedFailoverPartnerString,
            defaultValue: false,
            ref s_ignoreServerProvidedFailoverPartner);

    /// <summary>
    /// In System.Data.SqlClient and Microsoft.Data.SqlClient prior to 3.0.0 a
    /// field with type Timestamp/RowVersion would return an empty byte array.
    /// This switch controls whether to preserve that behaviour on newer
    /// versions of Microsoft.Data.SqlClient, if this switch returns false an
    /// appropriate null value will be returned.
    ///
    /// The default value of this switch is false.
    /// </summary>
    public static bool LegacyRowVersionNullBehavior =>
        AcquireAndReturn(
            LegacyRowVersionNullString,
            defaultValue: false,
            ref s_legacyRowVersionNullBehavior);

    /// <summary>
    /// When set to 'true' this will output a scale value of 7
    /// (DEFAULT_VARTIME_SCALE) when the scale is explicitly set to zero for
    /// VarTime data types ('datetime2', 'datetimeoffset' and 'time') If no
    /// scale is set explicitly it will continue to output scale of 7
    /// (DEFAULT_VARTIME_SCALE) regardless of switch value.
    ///
    /// The default value of this switch is true.
    /// </summary>
    public static bool LegacyVarTimeZeroScaleBehaviour =>
        AcquireAndReturn(
            LegacyVarTimeZeroScaleBehaviourString,
            defaultValue: true,
            ref s_legacyVarTimeZeroScaleBehaviour);

    /// <summary>
    /// When enabled, ReadAsync runs asynchronously and does not block the
    /// calling thread.
    ///
    /// The default value of this switch is false.
    /// </summary>
    public static bool MakeReadAsyncBlocking =>
        AcquireAndReturn(
            MakeReadAsyncBlockingString,
            defaultValue: false,
            ref s_makeReadAsyncBlocking);

    /// <summary>
    /// When using Encrypt=false in the connection string, a security warning is
    /// output to the console if the TLS version is 1.2 or lower.  This warning
    /// can be suppressed by enabling this AppContext switch.
    ///
    /// The default value of this switch is false.
    /// </summary>
    public static bool SuppressInsecureTlsWarning =>
        AcquireAndReturn(
            SuppressInsecureTlsWarningString,
            defaultValue: false,
            ref s_suppressInsecureTlsWarning);

    /// <summary>
    /// Suppresses the creation of <see cref="System.Diagnostics.Activity"/> instances
    /// within SqlClient. This prevents duplicate activities from being recorded if an
    /// existing library already hooks other diagnostic pathways and creates them.
    /// </summary>
    public static bool SuppressNativeActivityTelemetry =>
        AcquireAndReturn(
            SuppressNativeActivityTelemetryString,
            defaultValue: false,
            ref s_suppressNativeActivityTelemetry);

    /// <summary>
    /// When set to true, TdsParser will truncate (rather than round) decimal
    /// and SqlDecimal values when scaling them.
    ///
    /// The default value of this switch is false.
    /// </summary>
    public static bool TruncateScaledDecimal =>
        AcquireAndReturn(
            TruncateScaledDecimalString,
            defaultValue: false,
            ref s_truncateScaledDecimal);

    /// <summary>
    /// In TdsParser, the async multi-packet column value fetch behavior can use
    /// a continue snapshot state for improved efficiency. When this switch is
    /// enabled (the default), the driver preserves the legacy compatibility
    /// behavior, which does not use the continue snapshot state. When disabled,
    /// the new behavior using the continue snapshot state is enabled.
    ///
    /// This switch will always return true if
    /// <see cref="UseCompatibilityProcessSni"/> is enabled, because the
    /// continue state is not stable without the multiplexer.
    ///
    /// The default value of this switch is true.
    /// </summary>
    public static bool UseCompatibilityAsyncBehaviour
    {
        get
        {
            if (UseCompatibilityProcessSni)
            {
                // If ProcessSni compatibility mode has been enabled then the
                // packet multiplexer has been disabled. The new async behaviour
                // using continue point capture is only stable if the
                // multiplexer is enabled so we must return true to enable
                // compatibility async behaviour using only restarts.
                return true;
            }

            return AcquireAndReturn(
                UseCompatibilityAsyncBehaviourString,
                defaultValue: true,
                ref s_useCompatibilityAsyncBehaviour);
        }
    }

    /// <summary>
    /// In TdsParser, the ProcessSni function changed significantly when the
    /// packet multiplexing code needed for high speed multi-packet column
    /// values was added.  When this switch is set to true (the default), the
    /// old ProcessSni design is used.  When this switch is set to false, the
    /// new experimental ProcessSni behavior using the packet multiplexer is
    /// enabled.
    ///
    /// The default value of this switch is true.
    /// </summary>
    public static bool UseCompatibilityProcessSni =>
        AcquireAndReturn(
            UseCompatibilityProcessSniString,
            defaultValue: true,
            ref s_useCompatibilityProcessSni);

    /// <summary>
    /// When set to true, the connection pool will use the new V2 connection
    /// pool implementation.  When set to false, the connection pool will use
    /// the legacy V1 implementation.
    ///
    /// The default value of this switch is false.
    /// </summary>
    public static bool UseConnectionPoolV2 =>
        AcquireAndReturn(
            UseConnectionPoolV2String,
            defaultValue: false,
            ref s_useConnectionPoolV2);

    #if NET && _WINDOWS
    /// <summary>
    /// When set to true, .NET on Windows will use the managed SNI
    /// implementation instead of the native SNI implementation.
    ///
    /// ILLink.Substitutions.xml allows the unused SNI implementation to be
    /// trimmed away when the corresponding AppContext switch is set at compile
    /// time. In such cases, this property will return a constant value, even if
    /// the AppContext switch is set or reset at runtime. See the
    /// ILLink.Substitutions.Windows.xml and ILLink.Substitutions.Unix.xml
    /// resource files for details.
    ///
    /// The default value of this switch is false.
    /// </summary>
    public static bool UseManagedNetworking
    {
        get
        {
            if (s_useManagedNetworking != SwitchValue.None)
            {
                return s_useManagedNetworking == SwitchValue.True;
            }

            if (!OperatingSystem.IsWindows())
            {
                s_useManagedNetworking = SwitchValue.True;
                return true;
            }

            if (AppContext.TryGetSwitch(UseManagedNetworkingOnWindowsString, out bool returnedValue) && returnedValue)
            {
                s_useManagedNetworking = SwitchValue.True;
                return true;
            }

            s_useManagedNetworking = SwitchValue.False;
            return false;
        }
    }
    #elif NET
    /// <summary>
    /// .NET Core on Unix does not support native SNI, so this will always be
    /// true.
    /// </summary>
    public static bool UseManagedNetworking => true;
    #else
    /// <summary>
    /// .NET Framework does not support the managed SNI, so this will always be
    /// false.
    /// </summary>
    public static bool UseManagedNetworking => false;
    #endif

    /// <summary>
    /// Specifies minimum login timeout to be set to 1 second instead of 0
    /// seconds, to prevent a login attempt from waiting indefinitely.
    ///
    /// The default value of this switch is true.
    /// </summary>
    public static bool UseMinimumLoginTimeout =>
        AcquireAndReturn(
            UseMinimumLoginTimeoutString,
            defaultValue: true,
            ref s_useMinimumLoginTimeout);

    #endregion

    #region Helpers

    /// <summary>
    /// Acquires the value of the specified app context switch and stores it
    /// in the given reference.  Applies the default value if the switch isn't
    /// set.
    /// 
    /// If the cached value is already set, it is returned immediately without
    /// attempting to re-acquire it.
    /// 
    /// No attempt is made to prevent multiple threads from acquiring the same
    /// switch value simultaneously.  The worst that can happen is that the
    /// switch is acquired more than once, and the last acquired value wins.
    /// </summary>
    /// <param name="switchName">The name of the app context switch.</param>
    /// <param name="defaultValue">The default value to use if the switch is not set.</param>
    /// <param name="switchValue">A reference to variable to store the switch value in.</param>
    /// <returns>Returns the acquired value as a bool.</returns>
    private static bool AcquireAndReturn(
        string switchName,
        bool defaultValue,
        ref SwitchValue switchValue)
    {
        // Refuse to re-acquire a switch value.  Simply return whatever value
        // was previously acquired.
        if (switchValue != SwitchValue.None)
        {
            return switchValue == SwitchValue.True;
        }

        // Attempt to acquire the switch value from AppContext.
        if (! AppContext.TryGetSwitch(switchName, out bool acquiredValue))
        {
            // The switch has no value, so use the given default.
            switchValue = defaultValue ? SwitchValue.True : SwitchValue.False;
            return defaultValue;
        }

        // Assign the appropriate SwitchValue based on the acquired value.
        switchValue = acquiredValue ? SwitchValue.True : SwitchValue.False;
        return acquiredValue;
    }
    
    #endregion
}
