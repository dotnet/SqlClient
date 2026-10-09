// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETSTANDARD2_0

namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Provides the trimming annotation missing from .NET Standard 2.0 so shared code can mark
/// members that may fail when unreferenced code is removed.
/// </summary>
/// <remarks>
/// Stores the warning message and optional guidance URL for trimming tools.
/// Modern targets use the framework-provided attribute instead.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class,
    Inherited = false)]
internal sealed class RequiresUnreferencedCodeAttribute(string message) : Attribute
{
    public string Message { get; } = message;
    public string? Url { get; set; }
}

/// <summary>
/// Provides the Native AOT annotation missing from .NET Standard 2.0 so shared code can mark
/// members that require runtime code generation.
/// </summary>
/// <remarks>
/// Stores the warning message and optional guidance URL for AOT analysis tools.
/// Modern targets use the framework-provided attribute instead.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class,
    Inherited = false)]
internal sealed class RequiresDynamicCodeAttribute(string message) : Attribute
{
    public string Message { get; } = message;
    public string? Url { get; set; }
}

/// <summary>
/// Provides the feature-switch annotation missing from .NET Standard 2.0, associating a
/// Boolean property with a named switch that trimming tools can substitute at publish time.
/// </summary>
/// <remarks>
/// Stores the switch name as metadata; it does not read or set the switch at runtime.
/// Modern targets use the framework-provided attribute instead.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
internal sealed class FeatureSwitchDefinitionAttribute(string switchName) : Attribute
{
    public string SwitchName { get; } = switchName;
}

/// <summary>
/// Provides the feature-guard annotation missing from .NET Standard 2.0, identifying a
/// Boolean property whose true value indicates that an annotated capability is available.
/// </summary>
/// <remarks>
/// Stores the capability's attribute type so analysis tools can recognize guarded code paths.
/// Modern targets use the framework-provided attribute instead.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
internal sealed class FeatureGuardAttribute(Type featureType) : Attribute
{
    public Type FeatureType { get; } = featureType;
}

/// <summary>
/// Provides the diagnostic-suppression annotation missing from .NET Standard 2.0 so shared
/// code can record suppressions that remain in assembly metadata for trimming and AOT tools.
/// </summary>
/// <remarks>
/// Stores the diagnostic category, identifier, and optional justification without changing
/// runtime behavior. Modern targets use the framework-provided attribute instead.
/// </remarks>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class UnconditionalSuppressMessageAttribute(string category, string checkId) : Attribute
{
    public string Category { get; } = category;
    public string CheckId { get; } = checkId;
    public string? Justification { get; set; }
}

#endif
