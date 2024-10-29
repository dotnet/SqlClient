// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SqlConnectionFactory
    { 
        partial void SubscribeToAssemblyLoadContextUnload()
        {
            AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly()).Unloading += SqlConnectionFactoryAssemblyLoadContext_Unloading;
        }

        private void SqlConnectionFactoryAssemblyLoadContext_Unloading(AssemblyLoadContext obj)
        {
            Unload(obj, EventArgs.Empty);
        }
    }
}
