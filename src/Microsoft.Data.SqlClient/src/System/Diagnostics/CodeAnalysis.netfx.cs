// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

namespace System.Diagnostics.CodeAnalysis
{
    // These classes are provided to provide compile-time support for Microsoft.CodeAnalysis
    // attributes. These attributes are native to netcore and available for netfx as a nuget
    // package - but only for net472. As such, until net462 support is dropped, these placeholder
    // classes will need to exist if we want to use them for static analysis.

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class NotNullAttribute : Attribute
    {
    }
}

#endif
