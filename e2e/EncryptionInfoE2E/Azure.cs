using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace EncryptionInfoE2E
{
    internal static partial class Program
    {
        private static void RunAzurePublicCa(Server server, string database, string otherCertificate, bool tlsOnly)
        {
            // The launcher supplies an existing Azure CLI token through stdin, never args or logs.
            string token = tlsOnly ? null : Console.ReadLine();
            if (!tlsOnly && string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Azure mode requires an existing access token on stdin.");
            if (tlsOnly) Console.WriteLine("TLS-ONLY: no login identity supplied; authenticated success remains BLOCKED.");

            string ConnectionString(string encryption, string pin = null)
            {
                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = server.DataSource,
                    InitialCatalog = database,
                    Encrypt = SqlConnectionEncryptOption.Parse(encryption),
                    TrustServerCertificate = false,
                    Pooling = false,
                    ConnectTimeout = 15,
                    ConnectRetryCount = 0,
                    ApplicationName = "EncryptionInfoSection6"
                };
                if (pin != null) builder.ServerCertificate = pin;
                return builder.ConnectionString;
            }

            string blocked = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var timer = Stopwatch.StartNew();
                Outcome health = OpenAzure(ConnectionString("Mandatory"), token, false).GetAwaiter().GetResult();
                Console.WriteLine($"AZURE-HEALTH attempt={attempt} seconds={timer.Elapsed.TotalSeconds:F2} success={health.Success} {Desc(health.Info)}");
                if (health.Success && timer.Elapsed <= TimeSpan.FromSeconds(15)) break;
                if (health.Ex != null) Console.WriteLine(health.Ex.ToString());
                if (health.Other != null) Console.WriteLine(health.Other.ToString());
                if (AzureAccessBlocked(health.Ex))
                {
                    blocked = "Existing identity/network authorization unavailable: " + health.Ex.Message;
                    break;
                }
                if (!IsInfrastructureTimeout(health.Ex ?? health.Other) && !health.Success)
                {
                    // A certificate/product error is evaluated below, not hidden by the health gate.
                    break;
                }
                if (attempt == 3) blocked = "Server unavailable after three bounded health attempts.";
                else Thread.Sleep(TimeSpan.FromMinutes(2));
            }

            string goodPin = Path.Combine(s_certDir, server.Key + ".cer");
            if (!File.Exists(otherCertificate)) throw new FileNotFoundException("Independent wrong-pin fixture is required.", otherCertificate);
            foreach (string scenario in new[] { "Mandatory public CA", "Strict public CA", "Strict correct pin", "Strict wrong pin" })
            {
                bool wrongPin = scenario == "Strict wrong pin";
                bool pin = wrongPin || scenario == "Strict correct pin";
                var expected = new Exp
                {
                    Success = !wrongPin,
                    Mode = scenario.StartsWith("Mandatory", StringComparison.Ordinal)
                        ? SqlConnectionEncryptionMode.Full : SqlConnectionEncryptionMode.Strict,
                    Proto = "set",
                    Certificate = "any",
                    VE = wrongPin ? "eq:PinnedCertificateMismatch" : "eq:None",
                    CS = pin ? "null" : "eq:NoError"
                };
                foreach (bool async in new[] { false, true })
                {
                    string how = async ? "async" : "sync";
                    if (blocked != null && blocked.StartsWith("Server unavailable", StringComparison.Ordinal))
                    {
                        EmitBlocked(scenario, server.Key, how, blocked);
                        continue;
                    }
                    string encryption = scenario.StartsWith("Mandatory", StringComparison.Ordinal) ? "Mandatory" : "Strict";
                    Outcome outcome = OpenAzure(ConnectionString(encryption, pin ? (wrongPin ? otherCertificate : goodPin) : null),
                        token, async).GetAwaiter().GetResult();
                    string data = CertificateData(outcome.Info);
                    Console.WriteLine($"AZURE-DETAIL {scenario} {how} {Desc(outcome.Info)}");
                    if (!wrongPin && AzureAccessBlocked(outcome.Ex))
                        EmitBlocked(scenario, server.Key, how, outcome.Ex.ToString());
                    else if (scenario == "Strict correct pin" &&
                        outcome.Info?.ValidationErrors == SqlServerCertificateValidationErrors.PinnedCertificateMismatch &&
                        data != s_probeCertificates[server.Key])
                        EmitBlocked(scenario, server.Key, how,
                            "Probe certificate is not the leaf presented by this Azure gateway connection; no stable correct-pin fixture.");
                    else
                    {
                        // Azure gateways can present different leaves on separate connections.
                        // Keep the pin verdict separate from a cross-connection DER comparison.
                        if (data == "null")
                            outcome.Other = new InvalidOperationException("Expected a presented leaf certificate.");
                        Record(scenario, server, how, expected, outcome);
                        if (data != s_probeCertificates[server.Key])
                            EmitBlocked(scenario + " independent DER comparison", server.Key, how,
                                "Azure gateway leaf differs from the independent probe; this is not a same-connection comparison.");
                    }
                }
            }
        }

        private static bool AzureAccessBlocked(SqlException exception) =>
            exception != null && (exception.Number == 18456 || exception.Number == 4060 ||
                exception.Number == 40615 || exception.Number == 40532);

        private static async Task<Outcome> OpenAzure(string connectionString, string token, bool async)
        {
            using var connection = new SqlConnection(connectionString) { AccessToken = token };
            try
            {
                if (async) await connection.OpenAsync().ConfigureAwait(false);
                else connection.Open();
                using var command = new SqlCommand("SELECT 1", connection) { CommandTimeout = 15 };
                object result = async ? await command.ExecuteScalarAsync().ConfigureAwait(false) : command.ExecuteScalar();
                if (!Equals(result, 1)) throw new InvalidOperationException("Read-only Azure SQL query did not return 1.");
                return new Outcome { Success = true, Info = connection.EncryptionInfo };
            }
            catch (SqlException exception) { return new Outcome { Ex = exception, Info = exception.EncryptionInfo }; }
            catch (Exception exception) { return new Outcome { Other = exception, Info = connection.EncryptionInfo }; }
        }
    }
}
