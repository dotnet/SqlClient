#if NET
namespace SqlClientFactory_Netcoreapp;

using System;
using System.Data.Common;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
    }

    // <Snippet1>
    private static DbProviderFactory GetFactory()
    {
        // register SqlClientFactory in provider factories
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);

        return DbProviderFactories.GetFactory("Microsoft.Data.SqlClient");
    }
    // </Snippet1>
}
#endif
