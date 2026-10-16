// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Data.SqlClient.Tests.Common;

/// <summary>
/// Creates Windows network-only logon tokens without storing credentials in test configuration.
/// </summary>
public static class WindowsImpersonationHelper
{
    private const int Logon32LogonNewCredentials = 9;
    private const int Logon32ProviderWinnt50 = 3;

    /// <summary>
    /// Creates a new logon session with alternate outbound credentials. Windows does not validate
    /// these credentials until network authentication, so unit tests can use placeholders.
    /// </summary>
    /// <param name="userName">The outbound account name.</param>
    /// <param name="domain">The outbound account's domain.</param>
    /// <param name="password">The outbound password.</param>
    /// <returns>A token owned by the caller, which must be disposed.</returns>
    public static SafeAccessTokenHandle LogonNetOnly(string userName, string domain, string password)
    {
        if (!LogonUser(userName, domain, password, Logon32LogonNewCredentials, Logon32ProviderWinnt50, out SafeAccessTokenHandle token))
        {
            int error = Marshal.GetLastWin32Error();
            token?.Dispose();
            throw new Win32Exception(error);
        }

        return token;
    }

    [DllImport("advapi32.dll", EntryPoint = "LogonUserW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LogonUser(
        string userName,
        string domain,
        string password,
        int logonType,
        int logonProvider,
        out SafeAccessTokenHandle token);
}
