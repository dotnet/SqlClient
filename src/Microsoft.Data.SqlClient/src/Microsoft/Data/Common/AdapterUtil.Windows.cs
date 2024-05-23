// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using Microsoft.Win32;

namespace Microsoft.Data.Common
{
    /// <summary>
    /// The class ADP defines the exceptions that are specific to the Adapters.
    /// The class contains functions that take the proper informational variables and then construct
    /// the appropriate exception with an error string obtained from the resource framework.
    /// The exception is then returned to the caller, so that the caller may then throw from its
    /// location so that the catcher of the exception will have the appropriate call stack.
    /// This class is used so that there will be compile time checking of error messages.
    /// The resource Framework.txt will ensure proper string text based on the appropriate locale.
    /// </summary>
    internal static partial class ADP
    {
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal static object LocalMachineRegistryValue(string subkey, string queryvalue)
        { // MDAC 77697
#if NETFRAMEWORK
            (new RegistryPermission(RegistryPermissionAccess.Read, "HKEY_LOCAL_MACHINE\\" + subkey)).Assert(); // MDAC 62028
#endif
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subkey, false))
                {
                    return key?.GetValue(queryvalue);
                }
            }
            catch (SecurityException e)
            {
                // Even though we assert permission - it's possible there are
                // ACL's on registry that cause SecurityException to be thrown.
                ADP.TraceExceptionWithoutRethrow(e);
                return null;
            }
#if NETFRAMEWORK
            finally
            {
                RegistryPermission.RevertAssert();
            }
#endif
        }
    }
}
