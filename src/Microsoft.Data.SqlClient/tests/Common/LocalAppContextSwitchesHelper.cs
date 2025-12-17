using System;
using System.Threading;

namespace Microsoft.Data.SqlClient.Tests.Common;

/// <summary>
/// This class provides read/write access to LocalAppContextSwitches values
/// for the duration of a test.  It is intended to be constructed at the start
/// of a test and disposed of at the end.  It captures the original values of
/// the switches and restores them when disposed.
///
/// This follows the RAII pattern to ensure that the switches are always
/// restored, which is important for global state like LocalAppContextSwitches.
///
/// https://en.wikipedia.org/wiki/Resource_acquisition_is_initialization
/// 
/// Only one instance can exist at a time. Overlapping constructor calls will
/// wait until the previous instance is disposed.
/// </summary>
public sealed class LocalAppContextSwitchesHelper : IDisposable
{
    #region Private Fields

    // This semaphore ensures that only one instance of this class may exist at
    // a time.
    private static readonly SemaphoreSlim s_instanceLock = new(1, 1);

    // These fields are used to capture the original switch values.
    private readonly bool? _legacyRowVersionNullBehaviorOriginal;
    private readonly bool? _suppressInsecureTlsWarningOriginal;
    private readonly bool? _makeReadAsyncBlockingOriginal;
    private readonly bool? _useMinimumLoginTimeoutOriginal;
    private readonly bool? _legacyVarTimeZeroScaleBehaviourOriginal;
    private readonly bool? _useCompatibilityProcessSniOriginal;
    private readonly bool? _useCompatibilityAsyncBehaviourOriginal;
    private readonly bool? _useConnectionPoolV2Original;
    private readonly bool? _truncateScaledDecimalOriginal;
    private readonly bool? _ignoreServerProvidedFailoverPartnerOriginal;
    private readonly bool? _enableUserAgentOriginal;
    private readonly bool? _multiSubnetFailoverByDefaultOriginal;
    
    #if NET
    private readonly bool? _globalizationInvariantModeOriginal;
    #endif
    
    #if NET && _WINDOWS
    private readonly bool? _useManagedNetworkingOriginal;
    #endif    

    #if NETFRAMEWORK
    private readonly bool? _disableTnirByDefaultOriginal;
    #endif

    #endregion

    #region Construction

    /// <summary>
    /// Construct to capture all existing switch values.
    ///
    /// This call will block, waiting for any previous instance to be disposed
    /// before completing construction.
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

        _legacyRowVersionNullBehaviorOriginal = LocalAppContextSwitches.s_legacyRowVersionNullBehavior;
        _suppressInsecureTlsWarningOriginal = LocalAppContextSwitches.s_suppressInsecureTlsWarning;
        _makeReadAsyncBlockingOriginal = LocalAppContextSwitches.s_makeReadAsyncBlocking;
        _useMinimumLoginTimeoutOriginal = LocalAppContextSwitches.s_useMinimumLoginTimeout;
        _legacyVarTimeZeroScaleBehaviourOriginal = LocalAppContextSwitches.s_legacyVarTimeZeroScaleBehaviour;
        _useCompatibilityProcessSniOriginal = LocalAppContextSwitches.s_useCompatibilityProcessSni;
        _useCompatibilityAsyncBehaviourOriginal = LocalAppContextSwitches.s_useCompatibilityAsyncBehaviour;
        _useConnectionPoolV2Original = LocalAppContextSwitches.s_useConnectionPoolV2;
        _truncateScaledDecimalOriginal = LocalAppContextSwitches.s_truncateScaledDecimal;
        _ignoreServerProvidedFailoverPartnerOriginal = LocalAppContextSwitches.s_ignoreServerProvidedFailoverPartner;
        _enableUserAgentOriginal = LocalAppContextSwitches.s_enableUserAgent;
        _multiSubnetFailoverByDefaultOriginal = LocalAppContextSwitches.s_multiSubnetFailoverByDefault;
        #if NET
        _globalizationInvariantModeOriginal = LocalAppContextSwitches.s_globalizationInvariantMode;
        #endif
        #if NET && _WINDOWS
        _useManagedNetworkingOriginal = LocalAppContextSwitches.s_useManagedNetworking;
        #endif
        #if NETFRAMEWORK
        _disableTnirByDefaultOriginal = LocalAppContextSwitches.s_disableTnirByDefault;
        #endif
    }

    /// <summary>
    /// Disposal restores all original switch values and releases the instance lock.
    /// </summary>
    public void Dispose()
    {
        LocalAppContextSwitches.s_legacyRowVersionNullBehavior = _legacyRowVersionNullBehaviorOriginal;
        LocalAppContextSwitches.s_suppressInsecureTlsWarning = _suppressInsecureTlsWarningOriginal;
        LocalAppContextSwitches.s_makeReadAsyncBlocking = _makeReadAsyncBlockingOriginal;
        LocalAppContextSwitches.s_useMinimumLoginTimeout = _useMinimumLoginTimeoutOriginal;
        LocalAppContextSwitches.s_legacyVarTimeZeroScaleBehaviour = _legacyVarTimeZeroScaleBehaviourOriginal;
        LocalAppContextSwitches.s_useCompatibilityProcessSni = _useCompatibilityProcessSniOriginal;
        LocalAppContextSwitches.s_useCompatibilityAsyncBehaviour = _useCompatibilityAsyncBehaviourOriginal;
        LocalAppContextSwitches.s_useConnectionPoolV2 = _useConnectionPoolV2Original;
        LocalAppContextSwitches.s_truncateScaledDecimal = _truncateScaledDecimalOriginal;
        LocalAppContextSwitches.s_ignoreServerProvidedFailoverPartner = _ignoreServerProvidedFailoverPartnerOriginal;
        LocalAppContextSwitches.s_enableUserAgent = _enableUserAgentOriginal;
        LocalAppContextSwitches.s_multiSubnetFailoverByDefault = _multiSubnetFailoverByDefaultOriginal;
        #if NET
        LocalAppContextSwitches.s_globalizationInvariantMode = _globalizationInvariantModeOriginal;
        #endif
        #if NET && _WINDOWS
        LocalAppContextSwitches.s_useManagedNetworking = _useManagedNetworkingOriginal;
        #endif
        #if NETFRAMEWORK
        LocalAppContextSwitches.s_disableTnirByDefault = _disableTnirByDefaultOriginal;
        #endif

        // Release the lock to allow another instance to be created.
        s_instanceLock.Release();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Access the LocalAppContextSwitches.LegacyRowVersionNullBehavior
    /// property.
    /// </summary>
    public bool LegacyRowVersionNullBehavior
    {
        get => LocalAppContextSwitches.LegacyRowVersionNullBehavior;
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.SuppressInsecureTlsWarning property.
    /// </summary>
    public bool SuppressInsecureTlsWarning
    {
        get => LocalAppContextSwitches.SuppressInsecureTlsWarning;
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.MakeReadAsyncBlocking property.
    /// </summary>
    public bool MakeReadAsyncBlocking
    {
        get => LocalAppContextSwitches.MakeReadAsyncBlocking;
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.UseMinimumLoginTimeout property.
    /// </summary>
    public bool UseMinimumLoginTimeout
    {
        get => LocalAppContextSwitches.UseMinimumLoginTimeout;
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.LegacyVarTimeZeroScaleBehaviour
    /// property.
    /// </summary>
    public bool LegacyVarTimeZeroScaleBehaviour
    {
        get => LocalAppContextSwitches.LegacyVarTimeZeroScaleBehaviour;
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.UseCompatibilityProcessSni property.
    /// </summary>
    public bool UseCompatibilityProcessSni
    {
        get => LocalAppContextSwitches.UseCompatibilityProcessSni;
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.UseCompatibilityAsyncBehaviour
    /// property.
    /// </summary>
    public bool UseCompatibilityAsyncBehaviour
    {
        get => LocalAppContextSwitches.UseCompatibilityAsyncBehaviour;
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.UseConnectionPoolV2 property.
    /// </summary>
    public bool UseConnectionPoolV2
    {
        get => LocalAppContextSwitches.UseConnectionPoolV2;
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.TruncateScaledDecimal property.
    /// </summary>
    public bool TruncateScaledDecimal
    {
        get => LocalAppContextSwitches.TruncateScaledDecimal;
    }

    public bool IgnoreServerProvidedFailoverPartner
    {
        get => LocalAppContextSwitches.IgnoreServerProvidedFailoverPartner;
    }

    public bool EnableUserAgent
    {
        get => LocalAppContextSwitches.EnableUserAgent;
    }

    public bool EnableMultiSubnetFailoverByDefault
    {
        get => LocalAppContextSwitches.EnableMultiSubnetFailoverByDefault;
    }

    #if NET
    /// <summary>
    /// Access the LocalAppContextSwitches.GlobalizationInvariantMode property.
    /// </summary>
    public bool GlobalizationInvariantMode
    {
        get => LocalAppContextSwitches.GlobalizationInvariantMode;
    }
    #endif

    #if NET && _WINDOWS
    /// <summary>
    /// Access the LocalAppContextSwitches.UseManagedNetworking property.
    /// </summary>
    public bool UseManagedNetworking
    {
        get => LocalAppContextSwitches.UseManagedNetworking;
    }
    #endif
    
    #if NETFRAMEWORK
    /// <summary>
    /// Access the LocalAppContextSwitches.DisableTnirByDefault property.
    /// </summary>
    public bool DisableTnirByDefault
    {
        get => LocalAppContextSwitches.DisableTnirByDefault;
    }
    #endif

    // These properties get or set the like-named underlying switch field value.
    //
    // They all fail the test if the value cannot be retrieved or set.

    /// <summary>
    /// Get or set the LocalAppContextSwitches.LegacyRowVersionNullBehavior
    /// switch value.
    /// </summary>
    public bool? LegacyRowVersionNullBehaviorValue
    {
        get => LocalAppContextSwitches.s_legacyRowVersionNullBehavior;
        set => LocalAppContextSwitches.s_legacyRowVersionNullBehavior = value;
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.SuppressInsecureTlsWarning
    /// switch value.
    /// </summary>
    public bool? SuppressInsecureTlsWarningValue
    {
        get => LocalAppContextSwitches.s_suppressInsecureTlsWarning;
        set => LocalAppContextSwitches.s_suppressInsecureTlsWarning = value;
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.MakeReadAsyncBlocking switch
    /// value.
    /// </summary>
    public bool? MakeReadAsyncBlockingValue
    {
        get => LocalAppContextSwitches.s_makeReadAsyncBlocking;
        set => LocalAppContextSwitches.s_makeReadAsyncBlocking = value;
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.UseMinimumLoginTimeout switch
    /// value.
    /// </summary>
    public bool? UseMinimumLoginTimeoutValue
    {
        get => LocalAppContextSwitches.s_useMinimumLoginTimeout;
        set => LocalAppContextSwitches.s_useMinimumLoginTimeout = value;
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.LegacyVarTimeZeroScaleBehaviour
    /// switch value.
    /// </summary>
    public bool? LegacyVarTimeZeroScaleBehaviourValue
    {
        get => LocalAppContextSwitches.s_legacyVarTimeZeroScaleBehaviour;
        set => LocalAppContextSwitches.s_legacyVarTimeZeroScaleBehaviour = value;
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.UseCompatibilityProcessSni switch
    /// value.
    /// </summary>
    public bool? UseCompatibilityProcessSniValue
    {
        get => LocalAppContextSwitches.s_useCompatibilityProcessSni;
        set => LocalAppContextSwitches.s_useCompatibilityProcessSni = value;
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.UseCompatibilityAsyncBehaviour
    /// switch value.
    /// </summary>
    public bool? UseCompatibilityAsyncBehaviourValue
    {
        get => LocalAppContextSwitches.s_useCompatibilityAsyncBehaviour;
        set => LocalAppContextSwitches.s_useCompatibilityAsyncBehaviour = value;
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.UseConnectionPoolV2 switch value.
    /// </summary>
    public bool? UseConnectionPoolV2Value
    {
        get => LocalAppContextSwitches.s_useConnectionPoolV2;
        set => LocalAppContextSwitches.s_useConnectionPoolV2 = value;
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.TruncateScaledDecimal switch value.
    /// </summary>
    public bool? TruncateScaledDecimalValue
    {
        get => LocalAppContextSwitches.s_truncateScaledDecimal;
        set => LocalAppContextSwitches.s_truncateScaledDecimal = value;
    }

    public bool? IgnoreServerProvidedFailoverPartnerValue
    {
        get => LocalAppContextSwitches.s_ignoreServerProvidedFailoverPartner;
        set => LocalAppContextSwitches.s_ignoreServerProvidedFailoverPartner = value;
    }

    public bool? EnableUserAgentValue
    {
        get => LocalAppContextSwitches.s_enableUserAgent;
        set => LocalAppContextSwitches.s_enableUserAgent = value;
    }

    public bool? EnableMultiSubnetFailoverByDefaultValue
    {
        get => LocalAppContextSwitches.s_multiSubnetFailoverByDefault;
        set => LocalAppContextSwitches.s_multiSubnetFailoverByDefault = value;
    }

#if NET
    /// <summary>
    /// Get or set the LocalAppContextSwitches.GlobalizationInvariantMode switch value.
    /// </summary>
    public bool? GlobalizationInvariantModeValue
    {
        get => LocalAppContextSwitches.s_globalizationInvariantMode;
        set => LocalAppContextSwitches.s_globalizationInvariantMode = value;
    }
    #endif

    #if NET && _WINDOWS
    /// <summary>
    /// Get or set the LocalAppContextSwitches.UseManagedNetworking switch value.
    /// </summary>
    public bool? UseManagedNetworkingValue
    {
        get => LocalAppContextSwitches.s_useManagedNetworking;
        set => LocalAppContextSwitches.s_useManagedNetworking = value;
    }
    #endif
    
    #if NETFRAMEWORK
    /// <summary>
    /// Get or set the LocalAppContextSwitches.DisableTnirByDefault switch
    /// value.
    /// </summary>
    public bool? DisableTnirByDefaultValue
    {
        get => LocalAppContextSwitches.s_disableTnirByDefault;
        set => LocalAppContextSwitches.s_disableTnirByDefault = value;
    }
    #endif

    #endregion
}
