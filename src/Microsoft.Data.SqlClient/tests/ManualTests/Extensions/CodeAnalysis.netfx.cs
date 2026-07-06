// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

namespace System.Diagnostics.CodeAnalysis;

// These classes are provided to provide compile-time support for System.Diagnostics.CodeAnalysis
// attributes. These attributes are native to netcore and available for netfx as a nuget
// package - but only for net472. As such, until net462 support is dropped, these placeholder
// classes will need to exist if we want to use them for static analysis.

#nullable enable

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
internal sealed class MemberNotNullAttribute : Attribute
{
    public MemberNotNullAttribute(string member) => Members = [member];

    public MemberNotNullAttribute(params string[] members) => Members = members;

    public string[] Members { get; }
}
#endif
