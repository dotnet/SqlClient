using System;
using System.Collections.Generic;
using System.Reflection;

using Xunit;

namespace Microsoft.Data.SqlClient.Tests.Common;

// This class provides read/write access to LocalAppContextSwitches values
// for the duration of a test.  It is intended to be constructed at the start
// of a test and disposed at the end.  It captures the original values of
// the switches and restores them when disposed.
//
// This follows the RAII pattern to ensure that the switches are always
// restored, which is important for global state like LocalAppContextSwitches.
//
// https://en.wikipedia.org/wiki/Resource_acquisition_is_initialization
//
public sealed class LocalAppContextSwitchesHelper : IDisposable
{
    #region Public Types

    // This enum is used to represent the state of a switch.
    //
    // It is a copy of the Tristate enum from LocalAppContextSwitches.
    //
    public enum Tristate : byte
    {
        NotInitialized = 0,
        False = 1,
        True = 2
    }

    #endregion

    #region Construction

    // Construct to capture all existing switch values.
    //
    // Fails the test if any values cannot be captured.
    //
    public LocalAppContextSwitchesHelper()
    {
        // Acquire a handle to the LocalAppContextSwitches type.
        var assembly = typeof(SqlCommandBuilder).Assembly;
        var switchesType = assembly.GetType(
            "Microsoft.Data.SqlClient.LocalAppContextSwitches");
        if (switchesType == null)
        {
            Assert.Fail("Unable to find LocalAppContextSwitches type.");
        }

        // A local helper to acquire a handle to a property.
        void InitProperty(string name, out PropertyInfo property)
        {
            var prop = switchesType.GetProperty(
                name, BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
            {
                Assert.Fail($"Unable to find {name} property.");
            }
            property = prop;
        }

        // Acquire handles to all of the public properties of
        // LocalAppContextSwitches.
        InitProperty(
            "LegacyRowVersionNullBehavior",
            out _legacyRowVersionNullBehaviorProperty);
        InitProperty(
            "SuppressInsecureTLSWarning",
            out _suppressInsecureTLSWarningProperty);
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
            out _useCompatProcessSniProperty);
        InitProperty(
            "UseCompatibilityAsyncBehaviour",
            out _useCompatAsyncBehaviourProperty);
#if NETFRAMEWORK
        InitProperty(
            "DisableTNIRByDefault",
            out _disableTNIRByDefaultProperty);
#endif

        // A local helper to capture the original value of a switch.
        void InitField(string name, out FieldInfo field, out Tristate value)
        {
            var fieldInfo =
                switchesType.GetField(
                    name, BindingFlags.NonPublic | BindingFlags.Static);
            if (fieldInfo == null)
            {
                Assert.Fail($"Unable to find {name} field.");
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
            "s_suppressInsecureTLSWarning",
            out _suppressInsecureTLSWarningField,
            out _suppressInsecureTLSWarningOriginal);

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
            "s_useCompatProcessSni",
            out _useCompatProcessSniField,
            out _useCompatProcessSniOriginal);

        InitField(
            "s_useCompatAsyncBehaviour",
            out _useCompatAsyncBehaviourField,
            out _useCompatAsyncBehaviourOriginal);

#if NETFRAMEWORK
        InitField(
            "s_disableTNIRByDefault",
            out _disableTNIRByDefaultField,
            out _disableTNIRByDefaultOriginal);
#endif
    }

    // Disposal restores all original switch values as a best effort.
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
            _suppressInsecureTLSWarningField,
            _suppressInsecureTLSWarningOriginal);
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
            _useCompatProcessSniField,
            _useCompatProcessSniOriginal);
        RestoreField(
            _useCompatAsyncBehaviourField,
            _useCompatAsyncBehaviourOriginal);
#if NETFRAMEWORK
        RestoreField(
            _disableTNIRByDefaultField,
            _disableTNIRByDefaultOriginal);
#endif
        if (failedFields.Count > 0)
        {
            Assert.Fail(
                $"Failed to restore the following fields: " +
                string.Join(", ", failedFields));
        }
    }

    #endregion

    #region Public Properties

    // These properties expose the like-named LocalAppContextSwitches
    // properties.
    public bool LegacyRowVersionNullBehavior
    {
        get => (bool)_legacyRowVersionNullBehaviorProperty.GetValue(null);
    }
    public bool SuppressInsecureTLSWarning
    {
        get => (bool)_suppressInsecureTLSWarningProperty.GetValue(null);
    }
    public bool MakeReadAsyncBlocking
    {
        get => (bool)_makeReadAsyncBlockingProperty.GetValue(null);
    }
    public bool UseMinimumLoginTimeout
    {
        get => (bool)_useMinimumLoginTimeoutProperty.GetValue(null);
    }
    public bool LegacyVarTimeZeroScaleBehaviour
    {
        get => (bool)_legacyVarTimeZeroScaleBehaviourProperty.GetValue(null);
    }
    public bool UseCompatibilityProcessSni
    {
        get => (bool)_useCompatProcessSniProperty.GetValue(null);
    }
    public bool UseCompatibilityAsyncBehaviour
    {
        get => (bool)_useCompatAsyncBehaviourProperty.GetValue(null);
    }
#if NETFRAMEWORK
    public bool DisableTNIRByDefault
    {
        get => (bool)_disableTNIRByDefaultProperty.GetValue(null);
    }
#endif

    // These properties get or set the like-named underlying switch field value.
    //
    // They all fail the test if the value cannot be retrieved or set.

    public Tristate LegacyRowVersionNullBehaviorField
    {
        get => GetValue(_legacyRowVersionNullBehaviorField);
        set => SetValue(_legacyRowVersionNullBehaviorField, value);
    }

    public Tristate SuppressInsecureTLSWarningField
    {
        get => GetValue(_suppressInsecureTLSWarningField);
        set => SetValue(_suppressInsecureTLSWarningField, value);
    }

    public Tristate MakeReadAsyncBlockingField
    {
        get => GetValue(_makeReadAsyncBlockingField);
        set => SetValue(_makeReadAsyncBlockingField, value);
    }

    public Tristate UseMinimumLoginTimeoutField
    {
        get => GetValue(_useMinimumLoginTimeoutField);
        set => SetValue(_useMinimumLoginTimeoutField, value);
    }

    public Tristate LegacyVarTimeZeroScaleBehaviourField
    {
        get => GetValue(_legacyVarTimeZeroScaleBehaviourField);
        set => SetValue(_legacyVarTimeZeroScaleBehaviourField, value);
    }

    public Tristate UseCompatProcessSniField
    {
        get => GetValue(_useCompatProcessSniField);
        set => SetValue(_useCompatProcessSniField, value);
    }

    public Tristate UseCompatAsyncBehaviourField
    {
        get => GetValue(_useCompatAsyncBehaviourField);
        set => SetValue(_useCompatAsyncBehaviourField, value);
    }

#if NETFRAMEWORK
    public Tristate DisableTNIRByDefaultField
    {
        get => GetValue(_disableTNIRByDefaultField);
        set => SetValue(_disableTNIRByDefaultField, value);
    }
#endif

    #endregion

    #region Private Helpers

    private static Tristate GetValue(FieldInfo field)
    {
        var value = field.GetValue(null);
        if (value is null)
        {
            Assert.Fail($"Field {field.Name} has a null value.");
        }

        return (Tristate)value;
    }

    private static void SetValue(FieldInfo field, Tristate value)
    {
        field.SetValue(null, (byte)value);
    }

    #endregion

    #region Private Members

    // These fields are used to expose LocalAppContextSwitches's properties.
    private readonly PropertyInfo _legacyRowVersionNullBehaviorProperty;
    private readonly PropertyInfo _suppressInsecureTLSWarningProperty;
    private readonly PropertyInfo _makeReadAsyncBlockingProperty;
    private readonly PropertyInfo _useMinimumLoginTimeoutProperty;
    private readonly PropertyInfo _legacyVarTimeZeroScaleBehaviourProperty;
    private readonly PropertyInfo _useCompatProcessSniProperty;
    private readonly PropertyInfo _useCompatAsyncBehaviourProperty;
#if NETFRAMEWORK
    private readonly PropertyInfo _disableTNIRByDefaultProperty;
#endif

    // These fields are used to capture the original switch values.
    private readonly FieldInfo _legacyRowVersionNullBehaviorField;
    private readonly Tristate _legacyRowVersionNullBehaviorOriginal;
    private readonly FieldInfo _suppressInsecureTLSWarningField;
    private readonly Tristate _suppressInsecureTLSWarningOriginal;
    private readonly FieldInfo _makeReadAsyncBlockingField;
    private readonly Tristate _makeReadAsyncBlockingOriginal;
    private readonly FieldInfo _useMinimumLoginTimeoutField;
    private readonly Tristate _useMinimumLoginTimeoutOriginal;
    private readonly FieldInfo _legacyVarTimeZeroScaleBehaviourField;
    private readonly Tristate _legacyVarTimeZeroScaleBehaviourOriginal;
    private readonly FieldInfo _useCompatProcessSniField;
    private readonly Tristate _useCompatProcessSniOriginal;
    private readonly FieldInfo _useCompatAsyncBehaviourField;
    private readonly Tristate _useCompatAsyncBehaviourOriginal;
#if NETFRAMEWORK
    private readonly FieldInfo _disableTNIRByDefaultField;
    private readonly Tristate _disableTNIRByDefaultOriginal;
#endif

    #endregion
}
