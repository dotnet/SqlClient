using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    private enum kCommands
    {

    }
    internal static class KerberosTicketManagemnt
    {
        internal static void Init()
        {
            RunKerberosCommand("kinit");
        }

        internal static void Destroy()
        {
            RunKerberosCommand("kdestroy");
        }
        internal static void List()
        {
            RunKerberosCommand("klist");
        }

        public static void RunKerberosCommand(string command)
        {
            try
            {
                System.Diagnostics.Process.Start("cmd.exe", command);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
    }
}
