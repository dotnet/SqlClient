// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;

namespace Microsoft.Data.SqlClient
{
    // these members were moved to a separate file in order
    // to be able to skip them on platforms where AppDomain members are not supported
    // for example, some mobile profiles on mono
    partial class SqlDependencyPerAppDomainDispatcher
    {
        partial void SubscribeToAppDomainUnload()
        {
            // If rude abort - we'll leak.  This is acceptable for now.
            AppDomain.CurrentDomain.DomainUnload += new EventHandler(UnloadEventHandler);
        }
    }
}

#endif
