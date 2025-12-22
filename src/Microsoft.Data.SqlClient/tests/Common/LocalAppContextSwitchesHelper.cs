// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;

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
/// This class is not thread-aware and should not be used concurrently.
/// </summary>
public sealed class LocalAppContextSwitchesHelper : IDisposable
{
    #region Private Fields

    // These fields are used to expose LocalAppContextSwitches's properties.
    private readonly PropertyInfo _legacyRowVersionNullBehaviorProperty;
    private readonly PropertyInfo _suppressInsecureTlsWarningProperty;
    private readonly PropertyInfo _makeReadAsyncBlockingProperty;
    private readonly PropertyInfo _useMinimumLoginTimeoutProperty;
    private readonly PropertyInfo _legacyVarTimeZeroScaleBehaviourProperty;
    private readonly PropertyInfo _useCompatibilityProcessSniProperty;
    private readonly PropertyInfo _useCompatibilityAsyncBehaviourProperty;
    private readonly PropertyInfo _useConnectionPoolV2Property;
    private readonly PropertyInfo _truncateScaledDecimalProperty;
    private readonly PropertyInfo _ignoreServerProvidedFailoverPartner;
    private readonly PropertyInfo _enableUserAgent;
    private readonly PropertyInfo _enableMultiSubnetFailoverByDefaultProperty;
#if NET
    private readonly PropertyInfo _globalizationInvariantModeProperty;
    #endif
    
    #if NET && _WINDOWS
    private readonly PropertyInfo _useManagedNetworkingProperty;
    #endif
    
    #if NETFRAMEWORK
    private readonly PropertyInfo _disableTnirByDefaultProperty;
    #endif

    // These fields are used to capture the original switch values.
    private readonly FieldInfo _legacyRowVersionNullBehaviorField;
    private readonly Tristate _legacyRowVersionNullBehaviorOriginal;
    private readonly FieldInfo _suppressInsecureTlsWarningField;
    private readonly Tristate _suppressInsecureTlsWarningOriginal;
    private readonly FieldInfo _makeReadAsyncBlockingField;
    private readonly Tristate _makeReadAsyncBlockingOriginal;
    private readonly FieldInfo _useMinimumLoginTimeoutField;
    private readonly Tristate _useMinimumLoginTimeoutOriginal;
    private readonly FieldInfo _legacyVarTimeZeroScaleBehaviourField;
    private readonly Tristate _legacyVarTimeZeroScaleBehaviourOriginal;
    private readonly FieldInfo _useCompatibilityProcessSniField;
    private readonly Tristate _useCompatibilityProcessSniOriginal;
    private readonly FieldInfo _useCompatibilityAsyncBehaviourField;
    private readonly Tristate _useCompatibilityAsyncBehaviourOriginal;
    private readonly FieldInfo _useConnectionPoolV2Field;
    private readonly Tristate _useConnectionPoolV2Original;
    private readonly FieldInfo _truncateScaledDecimalField;
    private readonly Tristate _truncateScaledDecimalOriginal;
    private readonly FieldInfo _ignoreServerProvidedFailoverPartnerField;
    private readonly Tristate _ignoreServerProvidedFailoverPartnerOriginal;
    private readonly FieldInfo _enableUserAgentField;
    private readonly Tristate _enableUserAgentOriginal;
    private readonly FieldInfo _multiSubnetFailoverByDefaultField;
    private readonly Tristate _multiSubnetFailoverByDefaultOriginal;
#if NET
    private readonly FieldInfo _globalizationInvariantModeField;
    private readonly Tristate _globalizationInvariantModeOriginal;
    #endif
    
    #if NET && _WINDOWS
    private readonly FieldInfo _useManagedNetworkingField;
    private readonly Tristate _useManagedNetworkingOriginal;
    #endif    

    #if NETFRAMEWORK
    private readonly FieldInfo _disableTnirByDefaultField;
    private readonly Tristate _disableTnirByDefaultOriginal;
    #endif

    #endregion

    #region Public Types

    /// <summary>
    /// This enum is used to represent the state of a switch.
    ///
    /// It is a copy of the Tristate enum from LocalAppContextSwitches.
    /// </summary>
    public enum Tristate : byte
    {
        NotInitialized = 0,
        False = 1,
        True = 2
    }

    #endregion

    #region Construction

    /// <summary>
    /// Construct to capture all existing switch values.
    /// </summary>
    /// 
    /// <exception cref="Exception">
    /// Throws if any values cannot be captured.
    /// </exception>
    public LocalAppContextSwitchesHelper()
    {
        // Acquire a handle to the LocalAppContextSwitches type.
        var assembly = typeof(SqlCommandBuilder).Assembly;
        var switchesType = assembly.GetType(
            "Microsoft.Data.SqlClient.LocalAppContextSwitches");
        if (switchesType == null)
        {
            throw new Exception("Unable to find LocalAppContextSwitches type.");
        }

        // A local helper to acquire a handle to a property.
        void InitProperty(string name, out PropertyInfo property)
        {
            var prop = switchesType.GetProperty(
                name, BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
            {
                throw new Exception($"Unable to find {name} property.");
            }
            property = prop;
        }

        // Acquire handles to all of the public properties of
        // LocalAppContextSwitches.
        InitProperty(
            "LegacyRowVersionNullBehavior",
            out _legacyRowVersionNullBehaviorProperty);

        InitProperty(
            "SuppressInsecureTlsWarning",
            out _suppressInsecureTlsWarningProperty);

        InitProperty(
            "MakeReadAsyncBlocking",
            out _makeReadAsyncBlockingProperty);

        InitProperty(
            "UseMinimumLoginTimeout",
            out _useMinimumLoginTimeoutProperty);

        InitProperty(
            "LegacyVarTimeZeroScaleBehaviour",
            out _legacyVarTimeZeroScaleBehaviourProperty);

        InitProperty(
            "UseCompatibilityProcessSni",
            out _useCompatibilityProcessSniProperty);

        InitProperty(
            "UseCompatibilityAsyncBehaviour",
            out _useCompatibilityAsyncBehaviourProperty);

        InitProperty(
            "UseConnectionPoolV2",
            out _useConnectionPoolV2Property);

        InitProperty(
            "TruncateScaledDecimal",
            out _truncateScaledDecimalProperty);

        InitProperty(
            "IgnoreServerProvidedFailoverPartner",
            out _ignoreServerProvidedFailoverPartner);

        InitProperty(
            "EnableUserAgent",
            out _enableUserAgent);

        InitProperty(
            "EnableMultiSubnetFailoverByDefault",
            out _enableMultiSubnetFailoverByDefaultProperty);

#if NET
        InitProperty(
            "GlobalizationInvariantMode",
            out _globalizationInvariantModeProperty);
        #endif
        
        #if NET && _WINDOWS
        InitProperty(
            "UseManagedNetworking",
            out _useManagedNetworkingProperty);
        #endif

        #if NETFRAMEWORK
        InitProperty(
            "DisableTnirByDefault",
            out _disableTnirByDefaultProperty);
        #endif

        // A local helper to capture the original value of a switch.
        void InitField(string name, out FieldInfo field, out Tristate value)
        {
            var fieldInfo =
                switchesType.GetField(
                    name, BindingFlags.NonPublic | BindingFlags.Static);
            if (fieldInfo == null)
            {
                throw new Exception($"Unable to find {name} field.");
            }
            field = fieldInfo;
            value = GetValue(field);
        }

        // Capture the original value of each switch.
        InitField(
            "s_legacyRowVersionNullBehavior",
            out _legacyRowVersionNullBehaviorField,
            out _legacyRowVersionNullBehaviorOriginal);

        InitField(
            "s_suppressInsecureTlsWarning",
            out _suppressInsecureTlsWarningField,
            out _suppressInsecureTlsWarningOriginal);

        InitField(
            "s_makeReadAsyncBlocking",
            out _makeReadAsyncBlockingField,
            out _makeReadAsyncBlockingOriginal);

        InitField(
            "s_useMinimumLoginTimeout",
            out _useMinimumLoginTimeoutField,
            out _useMinimumLoginTimeoutOriginal);

        InitField(
            "s_legacyVarTimeZeroScaleBehaviour",
            out _legacyVarTimeZeroScaleBehaviourField,
            out _legacyVarTimeZeroScaleBehaviourOriginal);

        InitField(
            "s_useCompatibilityProcessSni",
            out _useCompatibilityProcessSniField,
            out _useCompatibilityProcessSniOriginal);

        InitField(
            "s_useCompatibilityAsyncBehaviour",
            out _useCompatibilityAsyncBehaviourField,
            out _useCompatibilityAsyncBehaviourOriginal);

        InitField(
            "s_useConnectionPoolV2",
            out _useConnectionPoolV2Field,
            out _useConnectionPoolV2Original);

        InitField(
            "s_truncateScaledDecimal",
            out _truncateScaledDecimalField,
            out _truncateScaledDecimalOriginal);

        InitField(
            "s_ignoreServerProvidedFailoverPartner",
            out _ignoreServerProvidedFailoverPartnerField,
            out _ignoreServerProvidedFailoverPartnerOriginal);
        
        InitField(
            "s_enableUserAgent",
            out _enableUserAgentField,
            out _enableUserAgentOriginal);

        InitField(
            "s_multiSubnetFailoverByDefault",
            out _multiSubnetFailoverByDefaultField,
            out _multiSubnetFailoverByDefaultOriginal);

#if NET
        InitField(
            "s_globalizationInvariantMode",
            out _globalizationInvariantModeField,
            out _globalizationInvariantModeOriginal);
        #endif
        
        #if NET && _WINDOWS
        InitField(
            "s_useManagedNetworking",
            out _useManagedNetworkingField,
            out _useManagedNetworkingOriginal);
#endif

        #if NETFRAMEWORK
        InitField(
            "s_disableTnirByDefault",
            out _disableTnirByDefaultField,
            out _disableTnirByDefaultOriginal);
        #endif
    }

    /// <summary>
    /// Disposal restores all original switch values as a best effort.
    /// </summary>
    /// 
    /// <exception cref="Exception">
    /// Throws if any values could not be restored after trying to restore all
    /// values.
    /// </exception>
    public void Dispose()
    {
        List<string> failedFields = new();

        void RestoreField(FieldInfo field, Tristate value)
        {
            try
            {
                SetValue(field, value);
            }
            catch (Exception)
            {
                failedFields.Add(field.Name);
            }
        }

        RestoreField(
            _legacyRowVersionNullBehaviorField,
            _legacyRowVersionNullBehaviorOriginal);

        RestoreField(
            _suppressInsecureTlsWarningField,
            _suppressInsecureTlsWarningOriginal);

        RestoreField(
            _makeReadAsyncBlockingField,
            _makeReadAsyncBlockingOriginal);

        RestoreField(
            _useMinimumLoginTimeoutField,
            _useMinimumLoginTimeoutOriginal);

        RestoreField(
            _legacyVarTimeZeroScaleBehaviourField,
            _legacyVarTimeZeroScaleBehaviourOriginal);

        RestoreField(
            _useCompatibilityProcessSniField,
            _useCompatibilityProcessSniOriginal);

        RestoreField(
            _useCompatibilityAsyncBehaviourField,
            _useCompatibilityAsyncBehaviourOriginal);

        RestoreField(
            _useConnectionPoolV2Field,
            _useConnectionPoolV2Original);

        RestoreField(
            _truncateScaledDecimalField,
            _truncateScaledDecimalOriginal);

        RestoreField(
            _ignoreServerProvidedFailoverPartnerField,
            _ignoreServerProvidedFailoverPartnerOriginal);

        RestoreField(
            _enableUserAgentField,
            _enableUserAgentOriginal);

        RestoreField(
            _multiSubnetFailoverByDefaultField,
            _multiSubnetFailoverByDefaultOriginal);

        #if NET
        RestoreField(
            _globalizationInvariantModeField,
            _globalizationInvariantModeOriginal);
        #endif
        
        #if NET && _WINDOWS
        RestoreField(
            _useManagedNetworkingField,
            _useManagedNetworkingOriginal);
        #endif
        
        #if NETFRAMEWORK
        RestoreField(
            _disableTnirByDefaultField,
            _disableTnirByDefaultOriginal);
        #endif

        if (failedFields.Count > 0)
        {
            throw new Exception(
                "Failed to restore the following fields: " +
                string.Join(", ", failedFields));
        }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Access the LocalAppContextSwitches.LegacyRowVersionNullBehavior
    /// property.
    /// </summary>
    public bool LegacyRowVersionNullBehavior
    {
        get => (bool)_legacyRowVersionNullBehaviorProperty.GetValue(null);
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.SuppressInsecureTlsWarning property.
    /// </summary>
    public bool SuppressInsecureTlsWarning
    {
        get => (bool)_suppressInsecureTlsWarningProperty.GetValue(null);
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.MakeReadAsyncBlocking property.
    /// </summary>
    public bool MakeReadAsyncBlocking
    {
        get => (bool)_makeReadAsyncBlockingProperty.GetValue(null);
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.UseMinimumLoginTimeout property.
    /// </summary>
    public bool UseMinimumLoginTimeout
    {
        get => (bool)_useMinimumLoginTimeoutProperty.GetValue(null);
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.LegacyVarTimeZeroScaleBehaviour
    /// property.
    /// </summary>
    public bool LegacyVarTimeZeroScaleBehaviour
    {
        get => (bool)_legacyVarTimeZeroScaleBehaviourProperty.GetValue(null);
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.UseCompatibilityProcessSni property.
    /// </summary>
    public bool UseCompatibilityProcessSni
    {
        get => (bool)_useCompatibilityProcessSniProperty.GetValue(null);
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.UseCompatibilityAsyncBehaviour
    /// property.
    /// </summary>
    public bool UseCompatibilityAsyncBehaviour
    {
        get => (bool)_useCompatibilityAsyncBehaviourProperty.GetValue(null);
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.UseConnectionPoolV2 property.
    /// </summary>
    public bool UseConnectionPoolV2
    {
        get => (bool)_useConnectionPoolV2Property.GetValue(null);
    }

    /// <summary>
    /// Access the LocalAppContextSwitches.TruncateScaledDecimal property.
    /// </summary>
    public bool TruncateScaledDecimal
    {
        get => (bool)_truncateScaledDecimalProperty.GetValue(null);
    }

    public bool IgnoreServerProvidedFailoverPartner
    {
        get => (bool)_ignoreServerProvidedFailoverPartner.GetValue(null);
    }

    public bool EnableUserAgent
    {
        get => (bool)_enableUserAgent.GetValue(null);
    }

    public bool EnableMultiSubnetFailoverByDefault
    {
        get => (bool)_enableMultiSubnetFailoverByDefaultProperty.GetValue(null);
    }

    #if NET
    /// <summary>
    /// Access the LocalAppContextSwitches.GlobalizationInvariantMode property.
    /// </summary>
    public bool GlobalizationInvariantMode
    {
        get => (bool)_globalizationInvariantModeProperty.GetValue(null);
    }
    #endif

    #if NET && _WINDOWS
    /// <summary>
    /// Access the LocalAppContextSwitches.UseManagedNetworking property.
    /// </summary>
    public bool UseManagedNetworking
    {
        get => (bool)_useManagedNetworkingProperty.GetValue(null);
    }
    #endif
    
    #if NETFRAMEWORK
    /// <summary>
    /// Access the LocalAppContextSwitches.DisableTnirByDefault property.
    /// </summary>
    public bool DisableTnirByDefault
    {
        get => (bool)_disableTnirByDefaultProperty.GetValue(null);
    }
    #endif

    // These properties get or set the like-named underlying switch field value.
    //
    // They all fail the test if the value cannot be retrieved or set.

    /// <summary>
    /// Get or set the LocalAppContextSwitches.LegacyRowVersionNullBehavior
    /// switch value.
    /// </summary>
    public Tristate LegacyRowVersionNullBehaviorField
    {
        get => GetValue(_legacyRowVersionNullBehaviorField);
        set => SetValue(_legacyRowVersionNullBehaviorField, value);
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.SuppressInsecureTlsWarning
    /// switch value.
    /// </summary>
    public Tristate SuppressInsecureTlsWarningField
    {
        get => GetValue(_suppressInsecureTlsWarningField);
        set => SetValue(_suppressInsecureTlsWarningField, value);
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.MakeReadAsyncBlocking switch
    /// value.
    /// </summary>
    public Tristate MakeReadAsyncBlockingField
    {
        get => GetValue(_makeReadAsyncBlockingField);
        set => SetValue(_makeReadAsyncBlockingField, value);
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.UseMinimumLoginTimeout switch
    /// value.
    /// </summary>
    public Tristate UseMinimumLoginTimeoutField
    {
        get => GetValue(_useMinimumLoginTimeoutField);
        set => SetValue(_useMinimumLoginTimeoutField, value);
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.LegacyVarTimeZeroScaleBehaviour
    /// switch value.
    /// </summary>
    public Tristate LegacyVarTimeZeroScaleBehaviourField
    {
        get => GetValue(_legacyVarTimeZeroScaleBehaviourField);
        set => SetValue(_legacyVarTimeZeroScaleBehaviourField, value);
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.UseCompatibilityProcessSni switch
    /// value.
    /// </summary>
    public Tristate UseCompatibilityProcessSniField
    {
        get => GetValue(_useCompatibilityProcessSniField);
        set => SetValue(_useCompatibilityProcessSniField, value);
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.UseCompatibilityAsyncBehaviour
    /// switch value.
    /// </summary>
    public Tristate UseCompatibilityAsyncBehaviourField
    {
        get => GetValue(_useCompatibilityAsyncBehaviourField);
        set => SetValue(_useCompatibilityAsyncBehaviourField, value);
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.UseConnectionPoolV2 switch value.
    /// </summary>
    public Tristate UseConnectionPoolV2Field
    {
        get => GetValue(_useConnectionPoolV2Field);
        set => SetValue(_useConnectionPoolV2Field, value);
    }

    /// <summary>
    /// Get or set the LocalAppContextSwitches.TruncateScaledDecimal switch value.
    /// </summary>
    public Tristate TruncateScaledDecimalField
    {
        get => GetValue(_truncateScaledDecimalField);
        set => SetValue(_truncateScaledDecimalField, value);
    }

    public Tristate IgnoreServerProvidedFailoverPartnerField
    {
        get => GetValue(_ignoreServerProvidedFailoverPartnerField);
        set => SetValue(_ignoreServerProvidedFailoverPartnerField, value);
    }

    public Tristate EnableUserAgentField
    {
        get => GetValue(_enableUserAgentField);
        set => SetValue(_enableUserAgentField, value);
    }

    public Tristate EnableMultiSubnetFailoverByDefaultField
    {
        get => GetValue(_multiSubnetFailoverByDefaultField);
        set => SetValue(_multiSubnetFailoverByDefaultField, value);
    }

#if NET
    /// <summary>
    /// Get or set the LocalAppContextSwitches.GlobalizationInvariantMode switch value.
    /// </summary>
    public Tristate GlobalizationInvariantModeField
    {
        get => GetValue(_globalizationInvariantModeField);
        set => SetValue(_globalizationInvariantModeField, value);
    }
    #endif

    #if NET && _WINDOWS
    /// <summary>
    /// Get or set the LocalAppContextSwitches.UseManagedNetworking switch value.
    /// </summary>
    public Tristate UseManagedNetworkingField
    {
        get => GetValue(_useManagedNetworkingField);
        set => SetValue(_useManagedNetworkingField, value);
    }
    #endif
    
    #if NETFRAMEWORK
    /// <summary>
    /// Get or set the LocalAppContextSwitches.DisableTnirByDefault switch
    /// value.
    /// </summary>
    public Tristate DisableTnirByDefaultField
    {
        get => GetValue(_disableTnirByDefaultField);
        set => SetValue(_disableTnirByDefaultField, value);
    }
    #endif

    #endregion

    #region Private Helpers

    // Get the value of the given field, or throw if it is null.
    private static Tristate GetValue(FieldInfo field)
    {
        var value = field.GetValue(null);
        if (value is null)
        {
            throw new Exception($"Field {field.Name} has a null value.");
        }

        return (Tristate)value;
    }

    // Set the value of the given field.
    private static void SetValue(FieldInfo field, Tristate value)
    {
        field.SetValue(null, (byte)value);
    }

    #endregion
}
