using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    internal static class KerberosTicketManagemnt
    {
        internal static void Init()
        {
            try
            {
                System.Diagnostics.Process.Start("kinit");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        internal static void Destroy()
        {
            try
            {
                System.Diagnostics.Process.Start("kdestroy");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        internal static void List()
        {
            try
            {
                System.Diagnostics.Process.Start("klist");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
