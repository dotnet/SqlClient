using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;

namespace EncryptionInfoE2E
{
    internal static partial class Program
    {
        private static void RunOldStrict(Server server, string pki)
        {
            if (!s_oldSni) throw new ArgumentException("old-strict requires --old-sni");
            string pin = System.IO.Path.Combine(pki, "revoked.cer");
            string other = System.IO.Path.Combine(pki, "other.cer");
            foreach (bool mars in new[] { false, true })
                Both("Old SNI Strict pin match MARS=" + mars, server,
                    Cs(server, $"Encrypt=Strict;ServerCertificate={pin};MultipleActiveResultSets={mars}"),
                    new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Strict, Proto = "set", Certificate = "null" });
            Both("Old SNI Strict pin mismatch", server,
                Cs(server, $"Encrypt=Strict;ServerCertificate={other}"),
                new Exp { Success = false, Mode = SqlConnectionEncryptionMode.Strict, Proto = "set", Certificate = "null" });
        }

        // A real SQL Server's Optional path still encrypts login. Use the repository's
        // TDS test endpoint to exercise successful ENCRYPT_NOT_SUP without server changes.
        private static void RunNoTls()
        {
            using var server = new TdsServer(new TdsServerArguments
            {
                Encryption = TDSPreLoginTokenEncryptionType.NotSupported
            });
            var endpoint = new TDSServerEndPoint(server)
            {
                ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
                EndpointName = "CipherNoTls"
            };
            try
            {
                endpoint.Start();
                int port = endpoint.ServerEndPoint.Port;
                var target = new Server("NO-TLS-SIMULATED", $"tcp:127.0.0.1,{port}", "127.0.0.1", port);
                Both("Successful mode=None (simulated TDS endpoint)", target,
                    $"Data Source={target.DataSource};Encrypt=Optional;Pooling=false;Connect Timeout=15",
                    new Exp
                    {
                        Success = true, Mode = SqlConnectionEncryptionMode.None,
                        Proto = "null", Certificate = "null", VE = "null", CS = "null"
                    });
            }
            finally { endpoint.Stop(); }
        }

        private static void RunLocalTls()
        {
#if NET
            // Supplemental runtime coverage only: this is not a real SQL Server,
            // a corporate-PKI endpoint, or an availability-group test.
            using var key = System.Security.Cryptography.RSA.Create(2048);
            var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
                "CN=localhost", key, System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            var names = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
            using var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(
                generated.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx), null);
            string correctPin = System.IO.Path.Combine(s_certDir, "local-tls.cer");
            System.IO.File.WriteAllBytes(correctPin, certificate.RawData);
            string otherPin = System.IO.Path.Combine(s_certDir, "CT.cer");
            foreach (string scenario in new[] { "TSC", "correct pin", "wrong pin" })
            {
                bool wrongPin = scenario == "wrong pin";
                using var server = new TdsServer(new TdsServerArguments
                {
                    Encryption = TDSPreLoginTokenEncryptionType.Required,
                    EncryptionCertificate = certificate,
                    EncryptionProtocols = System.Security.Authentication.SslProtocols.Tls12,
                    ExpectTlsHandshakeFailure = wrongPin
                });
                var endpoint = new TDSServerEndPoint(server)
                {
                    ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
                    EndpointName = "RuntimeTls",
                    ExpectTlsHandshakeFailure = wrongPin
                };
                try
                {
                    endpoint.Start();
                    int port = endpoint.ServerEndPoint.Port;
                    var target = new Server("LOCAL-TLS", $"tcp:127.0.0.1,{port}", "127.0.0.1", port);
                    s_skipStrict = true;
                    Probe(target).GetAwaiter().GetResult();
                    string data = Convert.ToBase64String(System.IO.File.ReadAllBytes(
                        System.IO.Path.Combine(s_certDir, target.Key + ".cer")));
                    if (data != Convert.ToBase64String(certificate.RawData))
                        throw new InvalidOperationException("Independent loopback probe did not observe the configured leaf.");
                    s_probeCertificates[target.Key] = data;
                    if (!s_allCertificates.Contains(data)) s_allCertificates.Add(data);
                    foreach (string line in System.IO.File.ReadAllLines(System.IO.Path.Combine(s_certDir, "ciphers.tsv")))
                    {
                        string[] fields = line.Split('\t');
                        s_probeCipher[fields[0]] = int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture);
                    }
                    string validation = scenario == "TSC" ? "TrustServerCertificate=true" :
                        "ServerCertificate=" + (wrongPin ? otherPin : correctPin);
                    Both("Supplemental simulated TLS " + scenario, target,
                        $"Data Source={target.DataSource};Encrypt=Mandatory;{validation};Pooling=false;Connect Timeout=15",
                        new Exp
                        {
                            Success = !wrongPin, Mode = SqlConnectionEncryptionMode.Full, Proto = "eq:Tls12",
                            Certificate = "match", VE = scenario == "TSC" ? "null" :
                                wrongPin ? "eq:PinnedCertificateMismatch" : "eq:None",
                            CS = "null"
                        });
                }
                finally { endpoint.Stop(); }
            }
#else
            throw new PlatformNotSupportedException("Supplemental local TLS requires the net10.0 harness.");
#endif
        }

        // Targeted OS coverage: require fail-closed certificate handling and validate
        // cipher availability without assuming newer Schannel certificate exports work.
        private static void RunPlatformCipher(Server server)
        {
            if (BlockUnavailable("Platform cipher checks", "sync+async", server)) return;
            foreach (string encryption in new[] { "Optional", "Mandatory" })
                Both("Platform " + encryption + "+TSC", server,
                    Cs(server, $"Encrypt={encryption};TrustServerCertificate=true"),
                    new Exp
                    {
                        Success = true,
                        Mode = encryption == "Optional" ? SqlConnectionEncryptionMode.LoginOnly : SqlConnectionEncryptionMode.Full,
                        Proto = "set", Certificate = "match", VE = "null", CS = "null"
                    });

            foreach (string extra in new[] { "", ";HostNameInCertificate=wrong-name" })
                foreach (bool async in new[] { false, true })
                {
                    string cs = Cs(server, "Encrypt=Mandatory;TrustServerCertificate=false" + extra);
                    Outcome outcome = async ? OpenAsync(cs).GetAwaiter().GetResult() : OpenSync(cs);
                    string scenario = "Platform certificate failure" + extra;
                    if (IsInfrastructureTimeout(outcome.Ex ?? outcome.Other))
                    {
                        EmitBlocked(scenario, server.Key, async ? "async" : "sync", (outcome.Ex ?? outcome.Other).ToString());
                        continue;
                    }
                    var failures = new List<string>();
                    if (outcome.Success || outcome.Ex == null) failures.Add("Expected certificate SqlException");
                    if (outcome.Other != null) failures.Add(outcome.Other.ToString());
                    if (outcome.Info == null) failures.Add("EncryptionInfo missing");
                    else
                    {
                        CheckCipher(server, outcome.Info, outcome.Success, failures);
                        string data = CertificateData(outcome.Info);
                        if (data != "null" && data != s_probeCertificates[server.Key]) failures.Add("Certificate mismatch");
                    }
                    if (outcome.Ex != null)
                    {
                        s_messagesAndToString.Add(outcome.Ex.Message);
                        s_messagesAndToString.Add(outcome.Ex.ToString());
                    }
                    Check(scenario, server.Key, async ? "async" : "sync", failures,
                        Desc(outcome.Info) + " exception=" + outcome.Ex?.Number + " " + outcome.Ex?.Message);
                }
        }
    }
}
