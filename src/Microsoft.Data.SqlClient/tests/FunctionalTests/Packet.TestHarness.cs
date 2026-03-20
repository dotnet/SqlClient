// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Namespace-level stubs for StringsHelper and Strings.
// These are needed because Packet.cs is compiled directly into the test project
// and references StringsHelper (in Microsoft.Data) and Strings (in System).
// The stubs inside TdsParserStateObject are nested and not visible to Packet.

namespace Microsoft.Data
{
    internal sealed class StringsHelper
    {
        internal static string GetString(string value) => value;
    }
}

namespace System
{
    internal class Strings
    {
        internal static string SQL_Packet_RequiredLengthUnavailable = nameof(SQL_Packet_RequiredLengthUnavailable);
    }
}
