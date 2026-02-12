// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.Data.SqlClient
{
    // these members were moved to a separate file in order
    // to be able to skip them on platforms where AssemblyLoadContext members are not supported
    // for example, netstandard
    partial class SqlDependencyPerAppDomainDispatcher
    {
        partial void SubscribeToAssemblyLoadContextUnload()
        {
            AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly()).Unloading += SqlDependencyPerAppDomainDispatcher_Unloading;
        }

        private void SqlDependencyPerAppDomainDispatcher_Unloading(AssemblyLoadContext obj)
        {
            UnloadEventHandler(null, EventArgs.Empty);
        }
    }
}

#endif
