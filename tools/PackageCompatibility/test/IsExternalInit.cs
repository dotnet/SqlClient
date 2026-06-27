// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Polyfill required for 'record' types and 'init' setters on .NET Framework.
// The compiler emits a reference to this type when using these C# features; .NET 5+
// includes it in the BCL, but .NET Framework does not.
#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
