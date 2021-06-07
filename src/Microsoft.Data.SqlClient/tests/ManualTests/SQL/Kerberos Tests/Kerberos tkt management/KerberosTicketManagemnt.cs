using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    internal static class KerberosTicketManagemnt
    {
        private static readonly string s_cmdPrompt = "/bin/bash";
        private static readonly string s_domainPass = "";

        internal static void Init(string domain)
        {
            RunKerberosCommand($"kinit {domain}");
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
                var proc = new Process
                {
                    StartInfo =
                    {
                        FileName = s_cmdPrompt,
                        Arguments = string.IsNullOrEmpty(s_domainPass)? $"-c {command}" : $"-c {command} -p:{s_domainPass}"
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
