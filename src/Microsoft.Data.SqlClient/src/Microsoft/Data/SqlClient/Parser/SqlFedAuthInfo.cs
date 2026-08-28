// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient.Parser;

internal sealed class SqlFedAuthInfo
{
    internal SqlFedAuthInfo(string spn, string stsurl)
    {
        Spn = spn;
        StsUrl = stsurl;
    }

    internal string Spn { get; }

    internal string StsUrl { get; }

    public override string ToString()
    {
        return $"SPN: {Spn}, STSURL: {StsUrl}";
    }
}
