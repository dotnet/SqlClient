// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// TEMPORARY CI diagnostic instrumentation for investigating the intermittent
    /// "Connection Timeout Expired ... during the post-login phase" failures.
    ///
    /// This is intentionally NOT shipped behavior. Every emission is gated behind the
    /// <c>MDS_LOGIN_TIMEOUT_DIAG</c> environment variable (default OFF), so a normal
    /// build/test run is completely unaffected. When the variable is set to
    /// <c>1</c>/<c>true</c> the driver prints greppable lines prefixed with
    /// <c>[MDS-TIMEOUT-DIAG]</c> to stderr (captured by ADO/CI logs).
    ///
    /// The goal is to capture, at the moment a connection open times out, both the
    /// <em>configured</em> ConnectTimeout and the <em>actual</em> timer budget handed to
    /// the SNI read, so we can prove whether an ~89s post-login timeout comes from a large
    /// configured ConnectTimeout (environment) or from an inflated/mis-propagated budget
    /// (driver bug).
    /// </summary>
    internal static class LoginTimeoutDiagnostics
    {
        internal const string EnvVarName = "MDS_LOGIN_TIMEOUT_DIAG";

        private static readonly bool s_enabled = ComputeEnabled();

        internal static bool Enabled => s_enabled;

        private static bool ComputeEnabled()
        {
            try
            {
                string value = Environment.GetEnvironmentVariable(EnvVarName);
                if (string.IsNullOrEmpty(value))
                {
                    return false;
                }

                value = value.Trim();
                return value == "1"
                    || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Environment access can throw under partial trust; never let diagnostics
                // interfere with normal operation.
                return false;
            }
        }

        internal static void Log(string message)
        {
            if (!s_enabled)
            {
                return;
            }

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "[MDS-TIMEOUT-DIAG] {0:O} tid={1} {2}",
                DateTime.UtcNow,
                Environment.CurrentManagedThreadId,
                message);

            try
            {
                Console.Error.WriteLine(line);
                Console.Error.Flush();
            }
            catch
            {
                // Ignore - diagnostics must never affect connection behavior.
            }
        }
    }
}
