using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.Data.SqlClient
{
    sealed internal partial class SqlConnectionFactory
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
