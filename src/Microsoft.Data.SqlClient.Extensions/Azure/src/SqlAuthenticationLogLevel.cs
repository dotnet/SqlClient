// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationLogLevel.xml' path='docs/members[@name="SqlAuthenticationLogLevel"]/SqlAuthenticationLogLevel/*'/>
public enum SqlAuthenticationLogLevel
{
    /// <summary>Important health diagnostics independent of severity.</summary>
    Always = 0,
    /// <summary>Unrecoverable authentication failures.</summary>
    Critical = 1,
    /// <summary>Authentication errors.</summary>
    Error = 2,
    /// <summary>Warnings about authentication behavior.</summary>
    Warning = 3,
    /// <summary>General authentication diagnostics.</summary>
    Informational = 4,
    /// <summary>Detailed authentication diagnostics.</summary>
    Verbose = 5
}
