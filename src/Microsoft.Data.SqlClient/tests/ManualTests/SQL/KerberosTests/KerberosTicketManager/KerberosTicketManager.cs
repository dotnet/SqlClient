// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    // This class is not used much since we are using Intengrated Authentication.
    // When support per User is added will be used more frequently.
    internal static class KerberosTicketManagemnt
    {
        private static readonly string s_cmdPrompt = "/bin/bash";

        internal static void Init(string user, string password)
        {
            RunKerberosCommand($"kinit {user}", password);
        }

        internal static void Destroy()
        {
            RunKerberosCommand("kdestroy", null);
        }
        internal static void List()
        {
            RunKerberosCommand("klist", null);
        }

        public static void RunKerberosCommand(string command, string echoString)
        {
            StringBuilder output = new StringBuilder();
            var proc = new Process
            {
                StartInfo =
                {
                    FileName = s_cmdPrompt,
                    Arguments = "-c \"" + (!string.IsNullOrEmpty(echoString) ? $"echo {echoString} | " : "") + $"{command}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };
            // Use async event handlers to avoid deadlocks
            proc.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                output.AppendLine(e.Data);
            });

            proc.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                output.AppendLine(e.Data);
            });

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            if (!proc.WaitForExit(10000))
            {
                proc.Kill();
                // allow async output to process
                proc.WaitForExit(2000);
                throw new Exception($"Kerberos command `{command}` timed out. Output:{Environment.NewLine + output}");
            }
        }
    }
}
