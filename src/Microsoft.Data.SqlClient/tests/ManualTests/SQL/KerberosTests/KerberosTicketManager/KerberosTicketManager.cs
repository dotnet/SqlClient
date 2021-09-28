// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

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

        public static void RunKerberosCommand(string command, bool isInit)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo =
                    {
                        FileName = s_cmdPrompt,
                        Arguments = isInit? $"-c {command}" : $"-c {command} -p:{DataTestUtility.KerberosDomainPassword}"
                    }
                };
                proc.Start();
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
