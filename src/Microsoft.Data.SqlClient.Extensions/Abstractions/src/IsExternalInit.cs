// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if !NET

namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill for the marker type the C# compiler requires to emit <c>init</c>-only setters
/// (used by records and init-only properties).  It is provided by the BCL on .NET, but not on
/// the netstandard2.0 / .NET Framework targets this assembly supports, so we define it here.
/// </summary>
internal static class IsExternalInit
{
}

#endif
