// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System.Runtime.InteropServices;
#endif
using SwitchValue = Microsoft.Data.SqlClient.LocalAppContextSwitches.SwitchValue;

namespace Microsoft.Data.SqlClient.Tests.Common;

/// <summary>
/// This class provides read/write access to LocalAppContextSwitches values for
/// the duration of a test.  It is intended to be constructed at the start of a
/// test and disposed of at the end.  It captures the original values of the
/// switches and restores them when disposed.
///
/// This follows the RAII pattern to ensure that the switches are always
/// restored, which is important for global state like LocalAppContextSwitches.
///
/// https://en.wikipedia.org/wiki/Resource_acquisition_is_initialization
///
/// As with all global state, care must be taken when using this class in tests
/// that may run in parallel.  This class enforces a single instance policy
/// using a semaphore.  Overlapping constructor calls will wait up to 5 seconds
/// for the previous instance to be disposed.  Any tests that use this class
/// should not keep an instance alive for longer than 5 seconds, or they risk
/// causing failures in other tests.
/// </summary>
public sealed class LocalAppContextSwitchesHelper : IDisposable
{
    #region Private Fields

    /// <summary>
    /// This semaphore ensures that only one instance of this class may exist at
    /// a time.
    /// </summary>
    private static readonly SemaphoreSlim s_instanceLock = new(1, 1);

    /// <summary>
    /// These fields are used to capture the original switch values.
    /// </summary>
    #if NETFRAMEWORK
    private readonly bool? _disableTnirByDefaultOriginal;
    #endif
    #if NET
    private readonly bool? _enableAppConfigOriginal;
    #endif
    private readonly bool? _enableMultiSubnetFailoverByDefaultOriginal;
    #if NET
    private readonly bool? _globalizationInvariantModeOriginal;
    #endif
    private readonly bool? _ignoreServerProvidedFailoverPartnerOriginal;
    private readonly bool? _useLegacyFailoverAlternationOnLoginSqlErrorsOriginal;
    private readonly bool? _enableTransactionIsolationLevelResetOriginal;
    private readonly bool? _legacyRowVersionNullBehaviorOriginal;
    private readonly bool? _legacyVarTimeZeroScaleBehaviourOriginal;
    private readonly bool? _makeReadAsyncBlockingOriginal;
    private readonly bool? _suppressInsecureTlsWarningOriginal;
    private readonly bool? _truncateScaledDecimalOriginal;
    private readonly bool? _useCompatibilityAsyncBehaviourOriginal;
    private readonly bool? _useCompatibilityProcessSniOriginal;
    private readonly bool? _useConnectionPoolV2Original;
    private readonly bool? _useLegacyIdleTimeoutBehaviorOriginal;
    private readonly bool? _useOverallConnectTimeoutForPoolWaitOriginal;
    #if NET
    // On non-Windows platforms the UseManagedNetworking switch is always true
    // and never consults its cached field, so the field is captured/restored
    // only when running on Windows.  See UseManagedNetworking below.
    private readonly bool? _useManagedNetworkingOriginal;
    #endif
    private readonly bool? _useMinimumLoginTimeoutOriginal;

    #endregion

    #region Construction

    /// <summary>
    /// Construct to capture all existing switch values.
    ///
    /// This call will block for at most 5 seconds, waiting for any previous
    /// instance to be disposed before completing construction.  Failure to
    /// acquire the lock in that time will result in an exception being thrown.
    /// </summary>
    public LocalAppContextSwitchesHelper()
    {
        // Wait for any previous instance to be disposed.
        //
        // We are only willing to wait a short time to avoid deadlocks.
        //
        if (! s_instanceLock.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new InvalidOperationException(
                "Timeout waiting for previous LocalAppContextSwitchesHelper " +
                "instance to be disposed.");
        }

        try
        {
            #if NETFRAMEWORK
            _disableTnirByDefaultOriginal =
                ToBool(LocalAppContextSwitches.s_disableTnirByDefault);
            #endif
            #if NET
            _enableAppConfigOriginal =
                ToBool(LocalAppContextSwitches.s_enableAppConfig);
            #endif
            _enableMultiSubnetFailoverByDefaultOriginal =
                ToBool(LocalAppContextSwitches.s_enableMultiSubnetFailoverByDefault);
            #if NET
            _globalizationInvariantModeOriginal =
                ToBool(LocalAppContextSwitches.s_globalizationInvariantMode);
            #endif
            _ignoreServerProvidedFailoverPartnerOriginal =
                ToBool(LocalAppContextSwitches.s_ignoreServerProvidedFailoverPartner);
            _useLegacyFailoverAlternationOnLoginSqlErrorsOriginal =
                ToBool(LocalAppContextSwitches.s_useLegacyFailoverAlternationOnLoginSqlErrors);
            _enableTransactionIsolationLevelResetOriginal =
                ToBool(LocalAppContextSwitches.s_enableTransactionIsolationLevelReset);
            _legacyRowVersionNullBehaviorOriginal =
                ToBool(LocalAppContextSwitches.s_legacyRowVersionNullBehavior);
            _legacyVarTimeZeroScaleBehaviourOriginal =
                ToBool(LocalAppContextSwitches.s_legacyVarTimeZeroScaleBehaviour);
            _makeReadAsyncBlockingOriginal =
                ToBool(LocalAppContextSwitches.s_makeReadAsyncBlocking);
            _suppressInsecureTlsWarningOriginal =
                ToBool(LocalAppContextSwitches.s_suppressInsecureTlsWarning);
            _truncateScaledDecimalOriginal =
                ToBool(LocalAppContextSwitches.s_truncateScaledDecimal);
            _useCompatibilityAsyncBehaviourOriginal =
                ToBool(LocalAppContextSwitches.s_useCompatibilityAsyncBehaviour);
            _useCompatibilityProcessSniOriginal =
                ToBool(LocalAppContextSwitches.s_useCompatibilityProcessSni);
            _useConnectionPoolV2Original =
                ToBool(LocalAppContextSwitches.s_useConnectionPoolV2);
            _useLegacyIdleTimeoutBehaviorOriginal =
                ToBool(LocalAppContextSwitches.s_useLegacyIdleTimeoutBehavior);
            _useOverallConnectTimeoutForPoolWaitOriginal =
                ToBool(LocalAppContextSwitches.s_useOverallConnectTimeoutForPoolWait);
            #if NET
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _useManagedNetworkingOriginal =
                    ToBool(LocalAppContextSwitches.s_useManagedNetworking);
            }
            #endif
            _useMinimumLoginTimeoutOriginal =
                ToBool(LocalAppContextSwitches.s_useMinimumLoginTimeout);
        }
        catch
        {
            // If we fail to capture the original values, release the lock
            // immediately to avoid deadlocks.
            s_instanceLock.Release();
            throw;
        }
    }

    /// <summary>
    /// Disposal restores all original switch values and releases the instance
    /// lock.
    /// </summary>
    public void Dispose()
    {
        try
        {
            #if NETFRAMEWORK
            LocalAppContextSwitches.s_disableTnirByDefault =
                ToSwitchValue(_disableTnirByDefaultOriginal);
            #endif
            #if NET
            LocalAppContextSwitches.s_enableAppConfig =
                ToSwitchValue(_enableAppConfigOriginal);
            #endif
            LocalAppContextSwitches.s_enableMultiSubnetFailoverByDefault =
                ToSwitchValue(_enableMultiSubnetFailoverByDefaultOriginal);
            #if NET
            LocalAppContextSwitches.s_globalizationInvariantMode =
                ToSwitchValue(_globalizationInvariantModeOriginal);
            #endif
            LocalAppContextSwitches.s_ignoreServerProvidedFailoverPartner =
                ToSwitchValue(_ignoreServerProvidedFailoverPartnerOriginal);
            LocalAppContextSwitches.s_useLegacyFailoverAlternationOnLoginSqlErrors =
                ToSwitchValue(_useLegacyFailoverAlternationOnLoginSqlErrorsOriginal);
            LocalAppContextSwitches.s_enableTransactionIsolationLevelReset =
                ToSwitchValue(_enableTransactionIsolationLevelResetOriginal);
            LocalAppContextSwitches.s_legacyRowVersionNullBehavior =
                ToSwitchValue(_legacyRowVersionNullBehaviorOriginal);
            LocalAppContextSwitches.s_legacyVarTimeZeroScaleBehaviour =
                ToSwitchValue(_legacyVarTimeZeroScaleBehaviourOriginal);
            LocalAppContextSwitches.s_makeReadAsyncBlocking =
                ToSwitchValue(_makeReadAsyncBlockingOriginal);
            LocalAppContextSwitches.s_suppressInsecureTlsWarning =
                ToSwitchValue(_suppressInsecureTlsWarningOriginal);
            LocalAppContextSwitches.s_truncateScaledDecimal =
                ToSwitchValue(_truncateScaledDecimalOriginal);
            LocalAppContextSwitches.s_useCompatibilityAsyncBehaviour =
                ToSwitchValue(_useCompatibilityAsyncBehaviourOriginal);
            LocalAppContextSwitches.s_useCompatibilityProcessSni =
                ToSwitchValue(_useCompatibilityProcessSniOriginal);
            LocalAppContextSwitches.s_useConnectionPoolV2 =
                ToSwitchValue(_useConnectionPoolV2Original);
            LocalAppContextSwitches.s_useLegacyIdleTimeoutBehavior =
                ToSwitchValue(_useLegacyIdleTimeoutBehaviorOriginal);
            LocalAppContextSwitches.s_useOverallConnectTimeoutForPoolWait =
                ToSwitchValue(_useOverallConnectTimeoutForPoolWaitOriginal);
            #if NET
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LocalAppContextSwitches.s_useManagedNetworking =
                    ToSwitchValue(_useManagedNetworkingOriginal);
            }
            #endif
            LocalAppContextSwitches.s_useMinimumLoginTimeout =
                ToSwitchValue(_useMinimumLoginTimeoutOriginal);
        }
        finally
        {
            // Release the lock to allow another instance to be created.
            s_instanceLock.Release();
        }
    }

    #endregion

    #region Switch Value Getters and Setters

    // These properties get the like-named underlying switch *property* value and set the underlying
    // switch *field* value. This allows tests to verify the default switch values.

    #if NETFRAMEWORK
    /// <summary>
    /// Get or set the DisableTnirByDefault switch value.
    /// </summary>
    public bool? DisableTnirByDefault
    {
        get => LocalAppContextSwitches.DisableTnirByDefault;
        set => LocalAppContextSwitches.s_disableTnirByDefault = ToSwitchValue(value);
    }
    #endif

    #if NET
    /// <summary>
    /// Get or set the EnableAppConfig switch value.
    /// </summary>
    public bool? EnableAppConfig
    {
        get => LocalAppContextSwitches.EnableAppConfig;
        set => LocalAppContextSwitches.s_enableAppConfig = ToSwitchValue(value);
    }
    #endif

    /// <summary>
    /// Get or set the EnableMultiSubnetFailoverByDefault switch value.
    /// </summary>
    public bool? EnableMultiSubnetFailoverByDefault
    {
        get => LocalAppContextSwitches.EnableMultiSubnetFailoverByDefault;
        set => LocalAppContextSwitches.s_enableMultiSubnetFailoverByDefault = ToSwitchValue(value);
    }

    #if NET
    /// <summary>
    /// Get or set the GlobalizationInvariantMode switch value.
    /// </summary>
    public bool? GlobalizationInvariantMode
    {
        get => LocalAppContextSwitches.GlobalizationInvariantMode;
        set => LocalAppContextSwitches.s_globalizationInvariantMode = ToSwitchValue(value);
    }
    #endif

    /// <summary>
    /// Get or set the IgnoreServerProvidedFailoverPartner switch value.
    /// </summary>
    public bool? IgnoreServerProvidedFailoverPartner
    {
        get => LocalAppContextSwitches.IgnoreServerProvidedFailoverPartner;
        set => LocalAppContextSwitches.s_ignoreServerProvidedFailoverPartner = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the UseLegacyFailoverAlternationOnLoginSqlErrors switch value.
    /// </summary>
    public bool? UseLegacyFailoverAlternationOnLoginSqlErrors
    {
        get => LocalAppContextSwitches.UseLegacyFailoverAlternationOnLoginSqlErrors;
        set => LocalAppContextSwitches.s_useLegacyFailoverAlternationOnLoginSqlErrors = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the EnableTransactionIsolationLevelReset switch value.
    /// </summary>
    public bool? EnableTransactionIsolationLevelReset
    {
        get => LocalAppContextSwitches.EnableTransactionIsolationLevelReset;
        set => LocalAppContextSwitches.s_enableTransactionIsolationLevelReset = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the LegacyRowVersionNullBehavior switch value.
    /// </summary>
    public bool? LegacyRowVersionNullBehavior
    {
        get => LocalAppContextSwitches.LegacyRowVersionNullBehavior;
        set => LocalAppContextSwitches.s_legacyRowVersionNullBehavior = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the LegacyVarTimeZeroScaleBehaviour switch value.
    /// </summary>
    public bool? LegacyVarTimeZeroScaleBehaviour
    {
        get => LocalAppContextSwitches.LegacyVarTimeZeroScaleBehaviour;
        set => LocalAppContextSwitches.s_legacyVarTimeZeroScaleBehaviour = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the MakeReadAsyncBlocking switch value.
    /// </summary>
    public bool? MakeReadAsyncBlocking
    {
        get => LocalAppContextSwitches.MakeReadAsyncBlocking;
        set => LocalAppContextSwitches.s_makeReadAsyncBlocking = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the SuppressInsecureTlsWarning switch value.
    /// </summary>
    public bool? SuppressInsecureTlsWarning
    {
        get => LocalAppContextSwitches.SuppressInsecureTlsWarning;
        set => LocalAppContextSwitches.s_suppressInsecureTlsWarning = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the TruncateScaledDecimal switch value.
    /// </summary>
    public bool? TruncateScaledDecimal
    {
        get => LocalAppContextSwitches.TruncateScaledDecimal;
        set => LocalAppContextSwitches.s_truncateScaledDecimal = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the UseCompatibilityAsyncBehaviour switch value.
    /// </summary>
    public bool? UseCompatibilityAsyncBehaviour
    {
        get => LocalAppContextSwitches.UseCompatibilityAsyncBehaviour;
        set => LocalAppContextSwitches.s_useCompatibilityAsyncBehaviour = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the UseCompatibilityProcessSni switch value.
    /// </summary>
    public bool? UseCompatibilityProcessSni
    {
        get => LocalAppContextSwitches.UseCompatibilityProcessSni;
        set => LocalAppContextSwitches.s_useCompatibilityProcessSni = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the UseConnectionPoolV2 switch value.
    /// </summary>
    public bool? UseConnectionPoolV2
    {
        get => LocalAppContextSwitches.UseConnectionPoolV2;
        set => LocalAppContextSwitches.s_useConnectionPoolV2 = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the UseLegacyIdleTimeoutBehavior switch value.
    /// </summary>
    public bool? UseLegacyIdleTimeoutBehavior
    {
        get => LocalAppContextSwitches.UseLegacyIdleTimeoutBehavior;
        set => LocalAppContextSwitches.s_useLegacyIdleTimeoutBehavior = ToSwitchValue(value);
    }

    /// <summary>
    /// Get or set the UseOverallConnectTimeoutForPoolWait switch value.
    /// </summary>
    public bool? UseOverallConnectTimeoutForPoolWait
    {
        get => LocalAppContextSwitches.UseOverallConnectTimeoutForPoolWait;
        set => LocalAppContextSwitches.s_useOverallConnectTimeoutForPoolWait = ToSwitchValue(value);
    }

    #if NET
    /// <summary>
    /// Get or set the UseManagedNetworking switch value.
    /// </summary>
    /// <remarks>
    /// On non-Windows platforms LocalAppContextSwitches.UseManagedNetworking is
    /// always true and never consults the cached field, so setting this property
    /// only has an effect when running on Windows.
    /// </remarks>
    public bool? UseManagedNetworking
    {
        get => LocalAppContextSwitches.UseManagedNetworking;
        set
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LocalAppContextSwitches.s_useManagedNetworking = ToSwitchValue(value);
            }
        }
    }
    #endif

    /// <summary>
    /// Get or set the UseMinimumLoginTimeout switch value.
    /// </summary>
    public bool? UseMinimumLoginTimeout
    {
        get => LocalAppContextSwitches.UseMinimumLoginTimeout;
        set => LocalAppContextSwitches.s_useMinimumLoginTimeout = ToSwitchValue(value);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Converts a cached switch value to its nullable bool equivalent.
    /// </summary>
    private static bool? ToBool(SwitchValue value) =>
        value switch
        {
            SwitchValue.None => null,
            SwitchValue.True => true,
            SwitchValue.False => false,
            _ => throw new InvalidOperationException(
                $"Unexpected cached switch value: {value}.")
        };

    /// <summary>
    /// Converts a nullable bool to its cached switch value equivalent.
    /// </summary>
    private static SwitchValue ToSwitchValue(bool? value) =>
        value switch
        {
            null => SwitchValue.None,
            true => SwitchValue.True,
            false => SwitchValue.False
        };

    #endregion
}
