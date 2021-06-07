using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    internal static class KerberosTicketManagemnt
    {
        private static readonly string s_cmdPrompt = "/bin/bash";

        internal static void Init(string domain)
        {
            RunKerberosCommand($"kinit {domain}", true);
        }

        internal static void Destroy()
        {
            RunKerberosCommand("kdestroy", false);
        }
        internal static void List()
        {
            RunKerberosCommand("klist", false);
        }

        public static void RunKerberosCommand(string command, bool isIniti)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo =
                    {
                        FileName = s_cmdPrompt,
                        Arguments = isIniti? $"-c {command}" : $"-c {command} -p:{DataTestUtility.DomainPass}"
                    }
                };
                proc.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
    }
}
