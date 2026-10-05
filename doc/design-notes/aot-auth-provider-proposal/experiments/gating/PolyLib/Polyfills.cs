namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
    internal sealed class RequiresUnreferencedCodeAttribute : Attribute
    { public RequiresUnreferencedCodeAttribute(string message) { Message = message; } public string Message { get; } public string? Url { get; set; } }
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
    internal sealed class RequiresDynamicCodeAttribute : Attribute
    { public RequiresDynamicCodeAttribute(string message) { Message = message; } public string Message { get; } public string? Url { get; set; } }
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    internal sealed class FeatureSwitchDefinitionAttribute : Attribute
    { public FeatureSwitchDefinitionAttribute(string switchName) { SwitchName = switchName; } public string SwitchName { get; } }
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class FeatureGuardAttribute : Attribute
    { public FeatureGuardAttribute(Type featureType) { FeatureType = featureType; } public Type FeatureType { get; } }
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    { public UnconditionalSuppressMessageAttribute(string category, string checkId) { Category = category; CheckId = checkId; } public string Category { get; } public string CheckId { get; } public string? Justification { get; set; } }
}
