// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Versioning;

namespace Microsoft.Data.ProviderBase
{
    internal sealed partial class DbConnectionPoolIdentity
    {
        [ResourceExposure(ResourceScope.None)] // SxS: this method does not create named objects
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        internal static DbConnectionPoolIdentity GetCurrent()
        {
            return GetCurrentManaged();
        }
    }
}
