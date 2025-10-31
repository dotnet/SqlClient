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
    private readonly PropertyInfo _useConnectionPoolV2Property;
    private readonly PropertyInfo _ignoreServerProvidedFailoverPartner;
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
    private readonly FieldInfo _useConnectionPoolV2Field;
    private readonly Tristate _useConnectionPoolV2Original;
    private readonly FieldInfo _ignoreServerProvidedFailoverPartnerField;
    private readonly Tristate _ignoreServerProvidedFailoverPartnerOriginal;
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
            "UseConnectionPoolV2",
            out _useConnectionPoolV2Property);

        InitProperty(
            "IgnoreServerProvidedFailoverPartner",
            out _ignoreServerProvidedFailoverPartner);

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
            "s_useConnectionPoolV2",
            out _useConnectionPoolV2Field,
            out _useConnectionPoolV2Original);

        InitField(
            "s_ignoreServerProvidedFailoverPartner",
            out _ignoreServerProvidedFailoverPartnerField,
            out _ignoreServerProvidedFailoverPartnerOriginal);
        
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
            _useConnectionPoolV2Field,
            _useConnectionPoolV2Original);

        RestoreField(
            _ignoreServerProvidedFailoverPartnerField,
            _ignoreServerProvidedFailoverPartnerOriginal);

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
    /// Access the LocalAppContextSwitches.UseConnectionPoolV2 property.
    /// </summary>
    public bool UseConnectionPoolV2
    {
        get => (bool)_useConnectionPoolV2Property.GetValue(null);
    }

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
    /// Get or set the LocalAppContextSwitches.UseConnectionPoolV2 switch value.
    /// </summary>
    public Tristate UseConnectionPoolV2Field
    {
        get => GetValue(_useConnectionPoolV2Field);
        set => SetValue(_useConnectionPoolV2Field, value);
    }

    public Tristate IgnoreServerProvidedFailoverPartnerField
    {
        get => GetValue(_ignoreServerProvidedFailoverPartnerField);
        set => SetValue(_ignoreServerProvidedFailoverPartnerField, value);
    }

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
