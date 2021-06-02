using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    internal static class KerberosTicketManagemnt
    {
        private static readonly string s_cmdPrompt = "cmd.exe";

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
                System.Diagnostics.Process.Start(s_cmdPrompt, command);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
    }
}
