// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Threading;

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
    private readonly bool? _enableMultiSubnetFailoverByDefaultOriginal;
    private readonly bool? _enableUserAgentOriginal;
    #if NET
    private readonly bool? _globalizationInvariantModeOriginal;
    #endif
    private readonly bool? _ignoreServerProvidedFailoverPartnerOriginal;
    private readonly bool? _legacyRowVersionNullBehaviorOriginal;
    private readonly bool? _legacyVarTimeZeroScaleBehaviourOriginal;
    private readonly bool? _makeReadAsyncBlockingOriginal;
    private readonly bool? _suppressInsecureTlsWarningOriginal;
    private readonly bool? _truncateScaledDecimalOriginal;
    private readonly bool? _useCompatibilityAsyncBehaviourOriginal;
    private readonly bool? _useCompatibilityProcessSniOriginal;
    private readonly bool? _useConnectionPoolV2Original;
    #if NET && _WINDOWS
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
                GetSwitchValue("s_disableTnirByDefault");
            #endif
            _enableMultiSubnetFailoverByDefaultOriginal =
                GetSwitchValue("s_enableMultiSubnetFailoverByDefault");
            _enableUserAgentOriginal =
                GetSwitchValue("s_enableUserAgent");
            #if NET
            _globalizationInvariantModeOriginal =
                GetSwitchValue("s_globalizationInvariantMode");
            #endif
            _ignoreServerProvidedFailoverPartnerOriginal =
                GetSwitchValue("s_ignoreServerProvidedFailoverPartner");
            _legacyRowVersionNullBehaviorOriginal =
                GetSwitchValue("s_legacyRowVersionNullBehavior");
            _legacyVarTimeZeroScaleBehaviourOriginal =
                GetSwitchValue("s_legacyVarTimeZeroScaleBehaviour");
            _makeReadAsyncBlockingOriginal =
                GetSwitchValue("s_makeReadAsyncBlocking");
            _suppressInsecureTlsWarningOriginal =
                GetSwitchValue("s_suppressInsecureTlsWarning");
            _truncateScaledDecimalOriginal =
                GetSwitchValue("s_truncateScaledDecimal");
            _useCompatibilityAsyncBehaviourOriginal =
                GetSwitchValue("s_useCompatibilityAsyncBehaviour");
            _useCompatibilityProcessSniOriginal =
                GetSwitchValue("s_useCompatibilityProcessSni");
            _useConnectionPoolV2Original =
                GetSwitchValue("s_useConnectionPoolV2");
            #if NET && _WINDOWS
            _useManagedNetworkingOriginal =
                GetSwitchValue("s_useManagedNetworking");
            #endif
            _useMinimumLoginTimeoutOriginal =
                GetSwitchValue("s_useMinimumLoginTimeout");
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
            SetSwitchValue(
                "s_disableTnirByDefault",
                _disableTnirByDefaultOriginal);
            #endif
            SetSwitchValue(
                "s_enableMultiSubnetFailoverByDefault",
                _enableMultiSubnetFailoverByDefaultOriginal);
            SetSwitchValue(
                "s_enableUserAgent",
                _enableUserAgentOriginal);
            #if NET
            SetSwitchValue(
                "s_globalizationInvariantMode",
                _globalizationInvariantModeOriginal);
            #endif
            SetSwitchValue(
                "s_ignoreServerProvidedFailoverPartner",
                _ignoreServerProvidedFailoverPartnerOriginal);
            SetSwitchValue(
                "s_legacyRowVersionNullBehavior", 
                _legacyRowVersionNullBehaviorOriginal);
            SetSwitchValue(
                "s_legacyVarTimeZeroScaleBehaviour",
                _legacyVarTimeZeroScaleBehaviourOriginal);
            SetSwitchValue(
                "s_makeReadAsyncBlocking",
                _makeReadAsyncBlockingOriginal);
            SetSwitchValue(
                "s_suppressInsecureTlsWarning",
                _suppressInsecureTlsWarningOriginal);
            SetSwitchValue(
                "s_truncateScaledDecimal",
                _truncateScaledDecimalOriginal);
            SetSwitchValue(
                "s_useCompatibilityAsyncBehaviour",
                _useCompatibilityAsyncBehaviourOriginal);
            SetSwitchValue(
                "s_useCompatibilityProcessSni",
                _useCompatibilityProcessSniOriginal);
            SetSwitchValue(
                "s_useConnectionPoolV2",
                _useConnectionPoolV2Original);
            #if NET && _WINDOWS
            SetSwitchValue(
                "s_useManagedNetworking",
                _useManagedNetworkingOriginal);
            #endif
            SetSwitchValue(
                "s_useMinimumLoginTimeout",
                _useMinimumLoginTimeoutOriginal);
        }
        finally
        {
            // Release the lock to allow another instance to be created.
            s_instanceLock.Release();
        }
    }

    #endregion

    #region Switch Value Getters and Setters

    // These properties get or set the like-named underlying switch field value.
    //
    // They all throw if the value cannot be retrieved or set.

    #if NETFRAMEWORK
    /// <summary>
    /// Get or set the DisableTnirByDefault switch value.
    /// </summary>
    public bool? DisableTnirByDefault
    {
        get => GetSwitchValue("s_disableTnirByDefault");
        set => SetSwitchValue("s_disableTnirByDefault", value);
    }
    #endif

    /// <summary>
    /// Get or set the EnableMultiSubnetFailoverByDefault switch value.
    /// </summary>
    public bool? EnableMultiSubnetFailoverByDefault
    {
        get => GetSwitchValue("s_enableMultiSubnetFailoverByDefault");
        set => SetSwitchValue("s_enableMultiSubnetFailoverByDefault", value);
    }

    /// <summary>
    /// Get or set the EnableUserAgent switch value.
    /// </summary>
    public bool? EnableUserAgent
    {
        get => GetSwitchValue("s_enableUserAgent");
        set => SetSwitchValue("s_enableUserAgent", value);
    }

    #if NET
    /// <summary>
    /// Get or set the GlobalizationInvariantMode switch value.
    /// </summary>
    public bool? GlobalizationInvariantMode
    {
        get => GetSwitchValue("s_globalizationInvariantMode");
        set => SetSwitchValue("s_globalizationInvariantMode", value);
    }
    #endif

    /// <summary>
    /// Get or set the IgnoreServerProvidedFailoverPartner switch value.
    /// </summary>
    public bool? IgnoreServerProvidedFailoverPartner
    {
        get => GetSwitchValue("s_ignoreServerProvidedFailoverPartner");
        set => SetSwitchValue("s_ignoreServerProvidedFailoverPartner", value);
    }

    /// <summary>
    /// Get or set the LegacyRowVersionNullBehavior switch value.
    /// </summary>
    public bool? LegacyRowVersionNullBehavior
    {
        get => GetSwitchValue("s_legacyRowVersionNullBehavior");
        set => SetSwitchValue("s_legacyRowVersionNullBehavior", value);
    }

    /// <summary>
    /// Get or set the LegacyVarTimeZeroScaleBehaviour switch value.
    /// </summary>
    public bool? LegacyVarTimeZeroScaleBehaviour
    {
        get => GetSwitchValue("s_legacyVarTimeZeroScaleBehaviour");
        set => SetSwitchValue("s_legacyVarTimeZeroScaleBehaviour", value);
    }

    /// <summary>
    /// Get or set the MakeReadAsyncBlocking switch value.
    /// </summary>
    public bool? MakeReadAsyncBlocking
    {
        get => GetSwitchValue("s_makeReadAsyncBlocking");
        set => SetSwitchValue("s_makeReadAsyncBlocking", value);
    }

    /// <summary>
    /// Get or set the SuppressInsecureTlsWarning switch value.
    /// </summary>
    public bool? SuppressInsecureTlsWarning
    {
        get => GetSwitchValue("s_suppressInsecureTlsWarning");
        set => SetSwitchValue("s_suppressInsecureTlsWarning", value);
    }

    /// <summary>
    /// Get or set the TruncateScaledDecimal switch value.
    /// </summary>
    public bool? TruncateScaledDecimal
    {
        get => GetSwitchValue("s_truncateScaledDecimal");
        set => SetSwitchValue("s_truncateScaledDecimal", value);
    }

    /// <summary>
    /// Get or set the UseCompatibilityAsyncBehaviour switch value.
    /// </summary>
    public bool? UseCompatibilityAsyncBehaviour
    {
        get => GetSwitchValue("s_useCompatibilityAsyncBehaviour");
        set => SetSwitchValue("s_useCompatibilityAsyncBehaviour", value);
    }

    /// <summary>
    /// Get or set the UseCompatibilityProcessSni switch value.
    /// </summary>
    public bool? UseCompatibilityProcessSni
    {
        get => GetSwitchValue("s_useCompatibilityProcessSni");
        set => SetSwitchValue("s_useCompatibilityProcessSni", value);
    }

    /// <summary>
    /// Get or set the UseConnectionPoolV2 switch value.
    /// </summary>
    public bool? UseConnectionPoolV2
    {
        get => GetSwitchValue("s_useConnectionPoolV2");
        set => SetSwitchValue("s_useConnectionPoolV2", value);
    }

    #if NET && _WINDOWS
    /// <summary>
    /// Get or set the UseManagedNetworking switch value.
    /// </summary>
    public bool? UseManagedNetworking
    {
        get => GetSwitchValue("s_useManagedNetworking");
        set => SetSwitchValue("s_useManagedNetworking", value);
    }
    #endif

    /// <summary>
    /// Get or set the UseMinimumLoginTimeout switch value.
    /// </summary>
    public bool? UseMinimumLoginTimeout
    {
        get => GetSwitchValue("s_useMinimumLoginTimeout");
        set => SetSwitchValue("s_useMinimumLoginTimeout", value);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Use reflection to get a switch field value from LocalAppContextSwitches.
    /// </summary>
    private static bool? GetSwitchValue(string fieldName)
    {
        var assembly = Assembly.GetAssembly(typeof(SqlConnection));
        if (assembly is null)
        {
            throw new InvalidOperationException(
                "Could not get assembly for Microsoft.Data.SqlClient");
        }
        
        var type = assembly.GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches");
        if (type is null)
        {
            throw new InvalidOperationException(
                "Could not get type LocalAppContextSwitches");
        }

        var field = type.GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException(
                $"Field '{fieldName}' not found in LocalAppContextSwitches");
        }

        var value = field.GetValue(null);
        if (value is not null)
        {
            // GOTCHA: This assumes that switch values map to bytes as:
            //
            //   None = 0
            //   True = 1
            //   False = 2
            //
            // See the LocalAppContextSwitches.SwitchValue enum definition.
            //
            byte underlyingValue = (byte)value;
            return underlyingValue == 0 ? null : underlyingValue == 1;
        }

        throw new InvalidOperationException(
            $"Field '{fieldName}' is not of type byte");
    }

    /// <summary>
    /// Use reflection to set a switch field value in LocalAppContextSwitches.
    /// </summary>
    private static void SetSwitchValue(string fieldName, bool? value)
    {
        var assembly = Assembly.GetAssembly(typeof(SqlConnection));
        if (assembly is null)
        {
            throw new InvalidOperationException(
                "Could not get assembly for Microsoft.Data.SqlClient");
        }
        
        var type = assembly.GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches");
        if (type is null)
        {
            throw new InvalidOperationException(
                "Could not get type LocalAppContextSwitches");
        }

        var field = type.GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException(
                $"Field '{fieldName}' not found in LocalAppContextSwitches");
        }

        // GOTCHA: This assumes that switch values map to bytes as:
        //
        //   None = 0
        //   True = 1
        //   False = 2
        //
        // See the LocalAppContextSwitches.SwitchValue enum definition.
        //
        byte byteValue =
            (byte)(!value.HasValue ? 0 : value.Value ? 1 : 2);

        field.SetValue(null, Enum.ToObject(field.FieldType, byteValue));
    }

    #endregion
}
