// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        internal static object LocalMachineRegistryValue(string subkey, string queryvalue)
        {
            // No registry in non-Windows environments
            return null;
        }
    }
}
