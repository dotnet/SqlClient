// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Authentication;

namespace Microsoft.Data.SqlClient.Parser;

internal static class SslProtocolsHelper
{
    private static string ToFriendlyName(this SslProtocols protocol)
    {
        string name;

        /* The SslProtocols.Tls13 is supported by netcoreapp3.1 and later
         * This driver does not support this version yet!
        if ((protocol & SslProtocols.Tls13) == SslProtocols.Tls13)
        {
            name = "TLS 1.3";
        }*/

        #pragma warning disable CA5397 // Do not use deprecated SslProtocols values
        #pragma warning disable CA5398 // Avoid hardcoded SslProtocols values
        if ((protocol & SslProtocols.Tls12) != SslProtocols.None)
        {
            name = "TLS 1.2";
        }
        #if NET8_0_OR_GREATER
        #pragma warning disable SYSLIB0039 // Type or member is obsolete: TLS 1.0 & 1.1 are deprecated
        #endif
        else if ((protocol & SslProtocols.Tls11) != SslProtocols.None)
        {
            name = "TLS 1.1";
        }
        else if ((protocol & SslProtocols.Tls) != SslProtocols.None)
        {
            name = "TLS 1.0";
        }
        #if NET8_0_OR_GREATER
        #pragma warning restore SYSLIB0039 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
        #endif
        #pragma warning disable CS0618 // Type or member is obsolete: SSL is deprecated
        else if ((protocol & SslProtocols.Ssl3) != SslProtocols.None)
        {
            name = "SSL 3.0";
        }
        else if ((protocol & SslProtocols.Ssl2) != SslProtocols.None)
        #pragma warning restore CS0618 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
        {
            name = "SSL 2.0";
        }
        #pragma warning restore CA5397 // Do not use deprecated SslProtocols values
        #pragma warning restore CA5398 // Avoid hardcoded SslProtocols values
        else
        {
            #if !NETFRAMEWORK
            name = protocol.ToString();
            #else
            throw new ArgumentException(StringsHelper.GetString(StringsHelper.net_invalid_enum, "NativeProtocols"), "NativeProtocols");
            #endif
        }

        return name;
    }

    /// <summary>
    /// check the negotiated secure protocol if it's under TLS 1.2
    /// </summary>
    /// <param name="protocol"></param>
    /// <returns>Localized warning message</returns>
    public static string GetProtocolWarning(this SslProtocols protocol)
    {
        string message = string.Empty;

        #if NET8_0_OR_GREATER
        #pragma warning disable SYSLIB0039 // Type or member is obsolete: TLS 1.0 & 1.1 are deprecated
        #endif
        #pragma warning disable CS0618 // Type or member is obsolete : SSL is deprecated
        #pragma warning disable CA5397 // Do not use deprecated SslProtocols values
        #pragma warning disable CA5398 // Avoid hardcoded SslProtocols values
        if ((protocol & (SslProtocols.Ssl2 | SslProtocols.Ssl3 | SslProtocols.Tls | SslProtocols.Tls11)) != SslProtocols.None)
        #pragma warning restore CA5398 // Avoid hardcoded SslProtocols values
        #pragma warning restore CA5397 // Do not use deprecated SslProtocols values
        #pragma warning restore CS0618 // Type or member is obsolete : SSL is deprecated
        #if NET8_0_OR_GREATER
        #pragma warning restore SYSLIB0039 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
        #endif
        {
            #if !NETFRAMEWORK
            message = StringsHelper.Format(Strings.SEC_ProtocolWarning, protocol.ToFriendlyName());
            #else
            message = StringsHelper.GetString(Strings.SEC_ProtocolWarning, protocol.ToFriendlyName());
            #endif
        }
        return message;
    }
}
