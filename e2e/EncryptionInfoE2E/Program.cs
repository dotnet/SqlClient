// LOCAL E2E ONLY: end-to-end harness for connection encryption info (#2353).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

#if NET
using System.Net.Security;
#endif

namespace EncryptionInfoE2E
{
    internal sealed class Server
    {
        public string Key;
        public string DataSource;
        public string Host;
        public int Port;
        public Server(string key, string ds, string host, int port) { Key = key; DataSource = ds; Host = host; Port = port; }
    }

    internal sealed class Exp
    {
        public bool Success;
        public SqlConnectionEncryptionMode? Mode;
        public string Proto = "any";   // set | null | any
        public string Certificate = "match";  // match | null | any
        public string VE = "null";     // null | any | eq:<v> | has:<v>
        public string CS = "null";     // null | any | eq:<v> | has:<v>
        public string Note = "";
        public bool Blocked;
        public bool Observe;   // record behaviour only; checks certificate and no unexpected exception
        public int Number;     // expected SqlException.Number on failure (0 = don't check)
        public Exp Gap;        // documented known-gap expectation, used only when the primary fails
    }

    internal sealed class Outcome
    {
        public bool Success;
        public SqlConnectionEncryptionInfo Info;
        public SqlException Ex;
        public Exception Other;
    }

    internal static partial class Program
    {
        private static readonly Server S22 = new Server("SQL2022", "tcp:localhost,1433", "localhost", 1433);
        private static readonly Server S25 = new Server("SQL2025", "tcp:localhost,1440", "localhost", 1440);
        private static readonly Server S22b = new Server("SQL2022b", "tcp:localhost,50904", "localhost", 50904);
        private static readonly Server[] Servers = { S22, S25, S22b };

        private static string s_tfm;
        private static string s_sni;
        private static string s_certDir;
        private static bool s_oldSni;
        private static bool s_poolV2;
        private static bool s_skipStrict;
        private static bool s_ctIntegrated;
        private static readonly HashSet<string> s_unavailable = new HashSet<string>();
        private static readonly Dictionary<string, string> s_probeCertificates = new Dictionary<string, string>();
        private static readonly Dictionary<string, int> s_probeCipher = new Dictionary<string, int>();
        private static readonly List<string> s_allCertificates = new List<string>();
        private static StreamWriter s_out;
        private static int s_pass, s_fail, s_blocked, s_gap;
        private static readonly List<string> s_messagesAndToString = new List<string>();
        private static TraceListener s_trace;

        private static int Main(string[] args)
        {
            // Set switches before JIT/type initialisation of any SqlClient entry point.
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", Arg(args, "--sni", "native") == "managed");
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2", args.Contains("--pool-v2"));
            return Run(args);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int Run(string[] args)
        {
            string mode = Arg(args, "--mode", "run");
            s_certDir = Path.GetFullPath(Arg(args, "--certdir", "certs"));
            Directory.CreateDirectory(s_certDir);

            Server ct = CtServer(args);
            s_ctIntegrated = args.Contains("--ct-integrated");
            s_skipStrict = args.Contains("--skip-strict");
            s_sni = Arg(args, "--sni", "native");
            if (args.Contains("--default-stopped")) s_unavailable.Add(S22.Key);
            if (args.Contains("--health-gate"))
                foreach (Server server in ct != null ? new[] { ct } : Servers)
                    if (!s_unavailable.Contains(server.Key)) HealthGate(server);
            if (mode == "probe")
            {
#if NET
                File.WriteAllText(Path.Combine(s_certDir, "ciphers.tsv"), "");
                foreach (Server s in ct != null ? new[] { ct } : Servers)
                    if (!s_unavailable.Contains(s.Key)) Probe(s).GetAwaiter().GetResult();
                return 0;
#else
                Console.WriteLine("probe requires net10.0");
                return 2;
#endif
            }

            s_sni = Arg(args, "--sni", "native");
            s_oldSni = args.Contains("--old-sni");
#if NET
            s_tfm = "net10.0";
#else
            s_tfm = "net462";
#endif
            s_poolV2 = args.Contains("--pool-v2");
            bool extra = args.Contains("--extra");
            string label = (s_sni == "managed" ? "Managed" : "Native") + (s_oldSni ? "-oldSNI" : "") + (s_poolV2 ? "-PoolV2" : "") + (extra ? "-Extra" : "");
            if (mode == "ct") label += $"-CT-{Arg(args, "--ct-profile", "good")}-f{Arg(args, "--ct-force", "0")}" + (args.Contains("--ct-trusted") ? "-trusted" : "-untrusted");
            if (mode == "no-tls" || mode == "platform" || mode == "old-strict") label += "-" + mode;
            string outFile = Path.GetFullPath(Arg(args, "--out", $"results-{s_tfm}-{label}.tsv"));
            s_out = new StreamWriter(outFile, false, new UTF8Encoding(false)) { AutoFlush = true };

            foreach (string file in Directory.GetFiles(s_certDir, "*.cer"))
            {
                string data = Convert.ToBase64String(File.ReadAllBytes(file));
                s_probeCertificates[Path.GetFileNameWithoutExtension(file)] = data;
                s_allCertificates.Add(data);
            }
            foreach (string line in File.ReadAllLines(Path.Combine(s_certDir, "ciphers.tsv")))
            {
                string[] p = line.Split('\t');
                s_probeCipher.Add(p[0], int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture));
            }

            s_trace = new TraceListener();
            VerifyCipherApi();
            Console.WriteLine($"TFM={s_tfm} SNI={label} Runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} OSArchitecture={System.Runtime.InteropServices.RuntimeInformation.OSArchitecture} ProcessArchitecture={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} Is64BitProcess={Environment.Is64BitProcess}");

            if (mode == "no-tls")
            {
                RunNoTls();
            }
            else if (mode == "certificate")
            {
                foreach (Server server in new[] { S25, S22b })
                {
                    string certificate = s_oldSni ? "null" : "match";
                    Both("Certificate TSC", server, Cs(server, "Encrypt=Mandatory;TrustServerCertificate=true"),
                        new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = certificate });
                    foreach (bool wrong in new[] { false, true })
                        Both("Certificate pin " + (wrong ? "mismatch" : "match"), server,
                            Cs(server, "Encrypt=Mandatory;ServerCertificate=" + CertFile(wrong ? (server == S25 ? S22b : S25) : server)),
                            new Exp { Success = !wrong, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = certificate,
                                VE = s_oldSni ? "null" : wrong ? "eq:PinnedCertificateMismatch" : "eq:None" });
                    Both("Certificate untrusted", server, Cs(server, "Encrypt=Mandatory;TrustServerCertificate=false"),
                        new Exp { Success = false, Mode = s_oldSni ? null : SqlConnectionEncryptionMode.Full,
                            Proto = s_oldSni ? "null" : "set", Certificate = certificate,
                            VE = s_oldSni ? "null" : "has:ChainError", CS = s_oldSni ? "null" : "has:UntrustedRoot" });
                    Both("Certificate hostname mismatch", server,
                        Cs(server, "Encrypt=Mandatory;HostNameInCertificate=wrong-name"),
                        new Exp { Success = false, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = certificate,
                            VE = s_oldSni ? "null" : "has:HostNameMismatch,ChainError", CS = s_oldSni ? "null" : "has:UntrustedRoot" });
                }
            }
            else if (mode == "local-tls")
            {
                RunLocalTls();
            }
            else if (mode == "host-success")
            {
                var server = ct ?? throw new ArgumentException("host-success requires --ct-host and --ct-port");
                foreach (bool mars in new[] { false, true })
                    Both("Real SQL Mandatory+TSC MARS=" + mars, server,
                        Cs(server, $"Encrypt=Mandatory;TrustServerCertificate=true;MultipleActiveResultSets={mars}"),
                        new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match" });
            }
            else if (mode == "platform")
            {
                RunPlatformCipher(ct ?? throw new ArgumentException("platform mode requires --ct-host and --ct-port"));
            }
            else if (mode == "azure")
            {
                RunAzurePublicCa(ct ?? throw new ArgumentException("azure mode requires --ct-host and --ct-port"),
                    Arg(args, "--database", "Northwind"), Arg(args, "--other-cert", null), args.Contains("--tls-only"));
            }
            else if (mode == "old-strict")
            {
                RunOldStrict(ct ?? throw new ArgumentException("old-strict mode requires --ct-host and --ct-port"),
                    Arg(args, "--ct-pki", null));
            }
            else if (mode == "cipher-check")
            {
                foreach (Server s in Servers)
                    Both("Cipher probe comparison", s, Cs(s, "Encrypt=Mandatory;TrustServerCertificate=true"),
                        new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match" });
            }
            else if (mode == "ping")
            {
                foreach (string spec in Arg(args, "--ds", "").Split('|'))
                {
                    string ds = spec.Split('~')[0], enc = spec.Contains("~") ? spec.Split('~')[1] : "Encrypt=Mandatory;TrustServerCertificate=true";
                    foreach (bool isAsync in new[] { false, true })
                    {
                        string how = isAsync ? "async" : "sync";
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                            using var c = new SqlConnection($"Data Source={ds};{Auth(ct)};{enc};Pooling=false;Connect Timeout=10");
                            if (isAsync) c.OpenAsync().GetAwaiter().GetResult(); else c.Open();
                            Console.WriteLine($"PING {ds} [{enc}] {how} ok {sw.ElapsedMilliseconds}ms {Desc(c.EncryptionInfo)}");
                        }
                        catch (SqlException ex)
                        {
                            Console.WriteLine($"PING {ds} [{enc}] {how} fail #{ex.Number} {sw.ElapsedMilliseconds}ms {Desc(ex.EncryptionInfo)} {ex.Message.Replace("\r", " ").Replace("\n", " ")}");
                        }
                    }
                }
            }
            else if (mode == "ct")
            {
                RunContainer(ct, Arg(args, "--ct-profile", "good"), Arg(args, "--ct-force", "0") == "1", args.Contains("--ct-trusted"), Arg(args, "--ct-pki", Path.Combine(s_certDir, "pki")));
            }
            else if (args.Contains("--s12-only"))
            {
                PostTlsLoginFailures(s_oldSni ? "null" : "match");
            }
            else if (extra)
            {
                RunExtra();
            }
            else if (s_oldSni)
            {
                RunOldSni(label);
            }
            else
            {
                RunMatrix(label);
            }

            LogModules();
            CheckNoLeaks(label);
            Console.WriteLine($"SUMMARY {s_tfm} {label}: PASS={s_pass} FAIL={s_fail} BLOCKED={s_blocked} KNOWN-GAP={s_gap}");
            s_out.WriteLine($"#SUMMARY\t{s_tfm}\t{label}\tPASS={s_pass}\tFAIL={s_fail}\tBLOCKED={s_blocked}\tKNOWN-GAP={s_gap}");
            s_out.Dispose();
            return s_fail == 0 ? 0 : 1;
        }

        private static string Arg(string[] args, string name, string def)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : def;
        }

        private static bool Managed => s_sni == "managed";

        private static void HealthGate(Server server)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var timer = Stopwatch.StartNew();
                try
                {
                    using var connection = new SqlConnection(Cs(server, "Encrypt=Mandatory;TrustServerCertificate=true;ConnectRetryCount=0"));
                    connection.Open();
                    Console.WriteLine($"HEALTH {server.Key} attempt={attempt} connected seconds={timer.Elapsed.TotalSeconds:F2}");
                    if (timer.Elapsed <= TimeSpan.FromSeconds(15)) return;
                    Console.WriteLine($"HEALTH {server.Key} exceeded 15 seconds");
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"HEALTH {server.Key} attempt={attempt} failed #{ex.Number} seconds={timer.Elapsed.TotalSeconds:F2} {ex.Message.Replace("\r", " ").Replace("\n", " ")}");
                }
                if (attempt < 3) System.Threading.Thread.Sleep(TimeSpan.FromMinutes(2));
            }
            s_unavailable.Add(server.Key);
            Console.WriteLine($"HEALTH {server.Key} BLOCKED: server unavailable after three attempts");
        }

        private static void EmitBlocked(string scenario, string server, string how, string reason)
        {
            s_blocked++;
            Console.WriteLine($"BLOCKED {scenario} [{server} {how}] {reason}");
            s_out.WriteLine(string.Join("\t", s_tfm, (Managed ? "Managed" : "Native") + (s_oldSni ? "-oldSNI" : ""),
                scenario, server, how, "BLOCKED", "-", "-", "-", "-", "-", "-", "-", "", reason, "", "null"));
        }

        private static bool IsInfrastructureTimeout(Exception exception) =>
            exception is SqlException sql && sql.Number == -2 ||
            exception is InvalidOperationException &&
            exception.Message.StartsWith("Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.", StringComparison.Ordinal);

        private static bool BlockUnavailable(string scenario, string how, params Server[] servers)
        {
            string[] missing = servers.Where(s => s_unavailable.Contains(s.Key)).Select(s => s.Key).ToArray();
            if (missing.Length == 0) return false;
            EmitBlocked(scenario, string.Join("+", servers.Select(s => s.Key)), how,
                "Server unavailable at health gate: " + string.Join(", ", missing));
            return true;
        }

        private static string Cs(Server s, string extra, bool pooling = false, string app = null)
            => $"Data Source={s.DataSource};Initial Catalog=master;{Auth(s)};Connect Timeout=15;Pooling={pooling};Application Name={app ?? "e2e"};{extra}";

        private static string CertFile(Server s) => Path.Combine(s_certDir, s.Key + ".cer");

        // ------------------------------------------------------------------ matrix

        private static void RunMatrix(string label)
        {
            // S1 Encrypt=Optional
            foreach (Server s in Servers)
                Both("S1 Optional", s, Cs(s, "Encrypt=Optional"), new Exp { Success = true, Mode = SqlConnectionEncryptionMode.LoginOnly, Proto = "set", Certificate = "match" });

            // S2 Mandatory + TSC
            foreach (Server s in Servers)
                Both("S2 Mandatory+TSC", s, Cs(s, "Encrypt=Mandatory;TrustServerCertificate=true"), new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match" });

            // S3 Mandatory, no trust (self-signed). Native SchannelAutomatic captures cert info since SNI b9d842c (deferred validation).
            foreach (Server s in new[] { S25, S22 })
                Both("S3 Mandatory no-trust", s, Cs(s, "Encrypt=Mandatory;TrustServerCertificate=false"),
                    new Exp { Success = false, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match", VE = "has:ChainError", CS = "has:UntrustedRoot" });

            // S4 HostNameInCertificate wrong
            Both("S4 HNIC=wrong-name", S25, Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=false;HostNameInCertificate=wrong-name"),
                new Exp { Success = false, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match", VE = "has:HostNameMismatch,ChainError", CS = "has:UntrustedRoot", Gap = Managed ? null : new Exp { Success = false, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match", VE = "eq:ChainError", CS = "has:UntrustedRoot", Note = "native ChainPolicy reports a single error (ChainError only)" } });

            // S4b HostNameInCertificate = actual CN -> only chain error
            Both("S4b HNIC=SSL_Self_Signed_Fallback", S25, Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=false;HostNameInCertificate=SSL_Self_Signed_Fallback"),
                new Exp { Success = false, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match", VE = "eq:ChainError", CS = "has:UntrustedRoot" });

            // S5 Strict + pin
            foreach (Server s in new[] { S25, S22 })
                Both("S5a Strict+pin(correct)", s, Cs(s, $"Encrypt=Strict;ServerCertificate={CertFile(s)}"),
                new Exp { Success = false, Proto = "null", Certificate = "null", VE = "null", CS = "null", Blocked = true, Note = "ENV: server rejects TDS 8 (auto-generated fallback cert); real Strict needs server cert provisioning" });

            Both("S5b Strict+pin(other cert)", S25, Cs(S25, $"Encrypt=Strict;ServerCertificate={CertFile(S22)}"),
                new Exp { Success = false, Proto = "null", Certificate = "null", VE = "null", CS = "null", Blocked = true, Note = "ENV: server rejects TDS 8 (auto-generated fallback cert); real Strict needs server cert provisioning" });

            Both("S5c Mandatory+pin(correct)", S25, Cs(S25, $"Encrypt=Mandatory;ServerCertificate={CertFile(S25)}"),
                new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match", VE = "eq:None", CS = "null" });
            Both("S5d Strict+pin(missing file)", S25, Cs(S25, $"Encrypt=Strict;ServerCertificate={Path.Combine(s_certDir, "does-not-exist.cer")}"),
                new Exp { Success = false, Proto = "null", Certificate = "any", VE = "null", CS = "null", Note = "unreadable pin file is not a mismatch" });

            // S6 Strict without trust
            Both("S6 Strict no-trust", S25, Cs(S25, "Encrypt=Strict"),
                new Exp { Success = false, Proto = "null", Certificate = "null", VE = "null", CS = "null", Blocked = true, Note = "ENV: server rejects TDS 8 (auto-generated fallback cert); real Strict needs server cert provisioning" });


            Both("S6b Strict+TSC(ignored)", S25, Cs(S25, "Encrypt=Strict;TrustServerCertificate=true"),
                new Exp { Success = false, Proto = "null", Certificate = "null", VE = "null", CS = "null", Blocked = true, Note = "ENV: server rejects TDS 8 (auto-generated fallback cert); real Strict needs server cert provisioning" });


            Both("S6c Strict+HNIC=SSL_Self_Signed_Fallback", S25, Cs(S25, "Encrypt=Strict;HostNameInCertificate=SSL_Self_Signed_Fallback"),
                new Exp { Success = false, Proto = "null", Certificate = "null", VE = "null", CS = "null", Blocked = true, Note = "ENV: server rejects TDS 8 (auto-generated fallback cert); real Strict needs server cert provisioning" });


            // S8 MARS
            foreach (Server s in new[] { S25, S22 })
                Both("S8 MARS Mandatory+TSC", s, Cs(s, "Encrypt=Mandatory;TrustServerCertificate=true;MultipleActiveResultSets=true"),
                    new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match" });
            Both("S8b MARS Strict+pin", S25, Cs(S25, $"Encrypt=Strict;MultipleActiveResultSets=true;ServerCertificate={CertFile(S25)}"),
                new Exp { Success = false, Proto = "null", Certificate = "null", VE = "null", CS = "null", Blocked = true, Note = "ENV: server rejects TDS 8 (auto-generated fallback cert); real Strict needs server cert provisioning" });

            Both("S8c MARS Mandatory no-trust", S25, Cs(S25, "Encrypt=Mandatory;MultipleActiveResultSets=true"),
                new Exp { Success = false, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match", VE = "has:ChainError", CS = "has:UntrustedRoot" });

            // Named pipes (default instance)
            Both("S11 NamedPipe Mandatory+TSC", S22, Cs(S22, "Encrypt=Mandatory;TrustServerCertificate=true").Replace("tcp:localhost,1433", @"np:\\.\pipe\sql\query"),
                new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match", Note = "requires named pipes enabled on server" });

            PostTlsLoginFailures(certificateExpected: "match");

            // S7 pooling / lifetime / reset
            Pooling(false);
            Pooling(true);
            Reconnect(false);
            Reconnect(true);
            RunLifecycle("S7d Snapshot retained after dispose", "SQL2025", "sync", Retained);
        }

        private static void RunOldSni(string label)
        {
            foreach (Server s in new[] { S25, S22 })
            {
                Both("S9 old-SNI Optional", s, Cs(s, "Encrypt=Optional"), new Exp { Success = true, Mode = SqlConnectionEncryptionMode.LoginOnly, Proto = "set", Certificate = "null" });
                Both("S9 old-SNI Mandatory+TSC", s, Cs(s, "Encrypt=Mandatory;TrustServerCertificate=true"), new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "null" });
                Both("S9 old-SNI Mandatory no-trust", s, Cs(s, "Encrypt=Mandatory"), new Exp { Success = false, Proto = "null", Certificate = "null", VE = "null", CS = "null" });
                Both("S9 old-SNI HNIC=wrong", s, Cs(s, "Encrypt=Mandatory;HostNameInCertificate=wrong-name"), new Exp { Success = false, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "null", VE = "null", CS = "null" });
            }
            Both("S9 old-SNI Strict+pin", S25, Cs(S25, $"Encrypt=Strict;ServerCertificate={CertFile(S25)}"),
                new Exp { Success = false, Proto = "null", Certificate = "null", VE = "null", CS = "null", Blocked = true, Note = "ENV: server rejects TDS 8 (auto-generated fallback cert); real Strict needs server cert provisioning" });
            Both("S9 old-SNI MARS", S25, Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=true;MultipleActiveResultSets=true"), new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "null" });
            PostTlsLoginFailures(certificateExpected: "null");
        }

        // S12: TLS completes, then login fails at the server (unknown database 4060, unknown login 18456).
        // Design: mode and protocol are kept because TLS really was negotiated; cert fields as for success.
        private static void PostTlsLoginFailures(string certificateExpected)
        {
            // Throwaway credential generated at runtime; the login does not exist on the server.
            string sqlLogin = "Integrated Security=false;User ID=e2e_no_such_login;Password=" + Guid.NewGuid().ToString("N");
            foreach (Server s in new[] { S25, S22 })
            {
                Both("S12 Mandatory+TSC bad database", s, Cs(s, "Encrypt=Mandatory;TrustServerCertificate=true;Initial Catalog=e2e_no_such_db"),
                    new Exp { Success = false, Number = 4060, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = certificateExpected });
                Both("S12 Mandatory+TSC bad login", s, Cs(s, "Encrypt=Mandatory;TrustServerCertificate=true;" + sqlLogin),
                    new Exp { Success = false, Number = 18456, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = certificateExpected });
                Both("S12 Optional bad login", s, Cs(s, "Encrypt=Optional;" + sqlLogin),
                    new Exp { Success = false, Number = 18456, Mode = SqlConnectionEncryptionMode.LoginOnly, Proto = "set", Certificate = certificateExpected });
            }
        }

        // ------------------------------------------------------------------ execution

        private static void Both(string scenario, Server s, string cs, Exp e)
        {
            if (s_unavailable.Contains(s.Key) || (s_skipStrict && cs.IndexOf("Encrypt=Strict", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                string reason = s_unavailable.Contains(s.Key) ? "Server unavailable at health gate"
                    : "Strict/TLS 1.3 excluded pending user decision; no server changes authorised";
                foreach (string how in new[] { "sync", "async" }) EmitBlocked(scenario, s.Key, how, reason);
                return;
            }
            Record(scenario, s, "sync", e, OpenSync(cs));
            Record(scenario, s, "async", e, OpenAsync(cs).GetAwaiter().GetResult());
        }

        private static Outcome OpenSync(string cs)
        {
            using (var c = new SqlConnection(cs))
            {
                try
                {
                    c.Open();
                    using (var cmd = new SqlCommand("select 1", c)) cmd.ExecuteScalar();
                    return new Outcome { Success = true, Info = c.EncryptionInfo };
                }
                catch (SqlException ex) { return new Outcome { Ex = ex, Info = ex.EncryptionInfo }; }
                catch (Exception ex) { return new Outcome { Other = ex }; }
            }
        }

        private static async Task<Outcome> OpenAsync(string cs)
        {
            using (var c = new SqlConnection(cs))
            {
                try
                {
                    await c.OpenAsync().ConfigureAwait(false);
                    using (var cmd = new SqlCommand("select 1", c)) await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                    return new Outcome { Success = true, Info = c.EncryptionInfo };
                }
                catch (SqlException ex) { return new Outcome { Ex = ex, Info = ex.EncryptionInfo }; }
                catch (Exception ex) { return new Outcome { Other = ex }; }
            }
        }

        private static string CertificateData(SqlConnectionEncryptionInfo info)
        {
            if (info == null) return "null";
            using var first = info.GetServerCertificate();
            using var second = info.GetServerCertificate();
            if (first == null || second == null)
            {
                if (first != null || second != null)
                    throw new InvalidOperationException("Certificate capture is inconsistent between calls.");
                return "null";
            }
            if (ReferenceEquals(first, second) || first.HasPrivateKey || second.HasPrivateKey)
                throw new InvalidOperationException("Certificate ownership/private-key contract violated.");
            byte[] expected = first.RawData;
            if (!expected.SequenceEqual(second.RawData))
                throw new InvalidOperationException("Certificate DER changed between calls.");
            first.Dispose();
            if (!expected.SequenceEqual(second.RawData))
                throw new InvalidOperationException("Disposing one certificate changed another instance.");
            using var third = info.GetServerCertificate();
            if (third == null || third.HasPrivateKey || ReferenceEquals(second, third) ||
                !expected.SequenceEqual(third.RawData))
                throw new InvalidOperationException("Certificate snapshot changed after caller disposal.");
            string data = Convert.ToBase64String(expected);
            lock (s_allCertificates)
                if (!s_allCertificates.Contains(data)) s_allCertificates.Add(data);
            return data;
        }

        private static void Record(string scenario, Server s, string how, Exp e, Outcome o)
        {
            if (IsInfrastructureTimeout(o.Ex ?? o.Other))
            {
                EmitBlocked(scenario, s.Key, how, "Server unavailable/connection timeout after health gate; cause unresolved: " +
                    (o.Ex ?? o.Other).ToString().Replace("\r", " ").Replace("\n", " "));
                return;
            }
            List<string> fails = Evaluate(s, e, o);
            if (e.Observe)
            {
                fails = fails.Where(f => f.StartsWith("unexpected") || f.StartsWith("certificate") || f.StartsWith("cipher")).ToList();
            }
            string verdict = fails.Count == 0 ? (e.Blocked ? "BLOCKED" : e.Observe ? "OBSERVE" : "PASS") : "FAIL";
            if (fails.Count > 0 && e.Gap != null && Evaluate(s, e.Gap, o).Count == 0)
            {
                verdict = "KNOWN-GAP";
                fails = new List<string> { "gap: " + e.Gap.Note + " [" + string.Join("; ", fails) + "]" };
            }
            Emit(scenario, s, how, e, o, fails, verdict);
        }

        private static List<string> Evaluate(Server s, Exp e, Outcome o)
        {
            var fails = new List<string>();
            SqlConnectionEncryptionInfo i = o.Info;
            if (o.Other != null)
            {
                fails.Add("unexpected " + o.Other.GetType().Name + ": " + o.Other.Message);
            }
            else
            {
                if (i == null)
                {
                    fails.Add("unexpected null EncryptionInfo");
                    return fails;
                }
                CheckCipher(s, i, o.Success, fails);
                if (o.Success != e.Success) fails.Add($"success={o.Success}");
                if (e.Success && i.NegotiatedEncryptionMode != e.Mode) fails.Add($"mode={Str(i.NegotiatedEncryptionMode)}");
                if (!e.Success && i.NegotiatedEncryptionMode != e.Mode) fails.Add($"mode-on-failure={Str(i.NegotiatedEncryptionMode)} (expected {Str(e.Mode)})");
                if (!e.Success && e.Number != 0 && o.Ex?.Number != e.Number) fails.Add($"number={o.Ex?.Number} (expected {e.Number})");
                if (e.Proto == "set" && i.NegotiatedEncryptionProtocol == null) fails.Add("protocol null");
                if (e.Proto.StartsWith("eq:") && Str(i.NegotiatedEncryptionProtocol) != e.Proto.Substring(3)) fails.Add($"protocol={Str(i.NegotiatedEncryptionProtocol)} (expected {e.Proto.Substring(3)})");
                if (e.Proto == "null" && i.NegotiatedEncryptionProtocol != null) fails.Add("protocol=" + i.NegotiatedEncryptionProtocol);
                // Review-doc failure rule: a mode is published only together with an observed protocol.
                if (!o.Success && i.NegotiatedEncryptionMode != null && i.NegotiatedEncryptionMode != SqlConnectionEncryptionMode.None && i.NegotiatedEncryptionProtocol == null) fails.Add("mode without observed protocol");
                string hex = CertificateData(i);
                if (e.Certificate == "match" && hex != s_probeCertificates[s.Key]) fails.Add(hex == "null" ? "certificate null" : "certificate DER MISMATCH vs probe");
                if (e.Certificate == "null" && hex != "null") fails.Add("certificate present");
                CheckFlag(fails, "VE", e.VE, i.ValidationErrors.HasValue ? (long?)(long)i.ValidationErrors.Value : null, typeof(SqlServerCertificateValidationErrors));
                CheckFlag(fails, "CS", e.CS, i.ChainStatus.HasValue ? (long?)(long)i.ChainStatus.Value : null, typeof(X509ChainStatusFlags));
            }
            return fails;
        }

        private static void CheckCipher(Server s, SqlConnectionEncryptionInfo info, bool success, List<string> fails)
        {
            if (info == null) { fails.Add("cipher: missing EncryptionInfo"); return; }
            int? suite = info.NegotiatedCipherSuiteId;
#if NET
            if ((int?)info.NegotiatedCipherSuite != suite)
                fails.Add("cipher: typed NegotiatedCipherSuite differs from NegotiatedCipherSuiteId");
#endif
            if (s_oldSni || info.NegotiatedEncryptionProtocol == null || info.NegotiatedEncryptionMode == SqlConnectionEncryptionMode.None)
            {
                if (suite != null) fails.Add($"cipher: expected null, got {Cipher(suite)}");
                return;
            }
            if (suite == null || suite <= 0 || suite > ushort.MaxValue)
            {
                fails.Add($"cipher: missing/invalid suite with observed protocol ({Cipher(suite)})");
                return;
            }
            if (success)
            {
                string key = $"{s.Key}/{(info.NegotiatedEncryptionMode == SqlConnectionEncryptionMode.Strict ? "strict" : "tds7")}/{info.NegotiatedEncryptionProtocol}";
                if (!s_probeCipher.TryGetValue(key, out int expected))
                    fails.Add($"cipher: no independent probe for {key}");
                else if (suite != expected)
                    fails.Add($"cipher: {Cipher(suite)} != independent probe {Cipher(expected)} for {key}");
            }
        }

        private static string Cipher(int? value) => value.HasValue ? $"0x{value.Value:X4}" : "null";

        private static void VerifyCipherApi()
        {
            Type type = typeof(SqlConnectionEncryptionInfo);
            if (type.GetProperty("PresentedServerCertificateHash256") != null ||
                type.GetMethod("GetServerCertificate")?.ReturnType != typeof(X509Certificate2))
                throw new InvalidOperationException("Unexpected full-certificate API surface.");
            if (type.GetProperty("NegotiatedCipherSuiteId")?.PropertyType != typeof(int?) ||
                type.GetProperty("NegotiatedCipherSuiteValue") != null)
                throw new InvalidOperationException("Unexpected numeric cipher API surface.");
#if NET
            if (type.GetProperty("NegotiatedCipherSuite")?.PropertyType != typeof(System.Net.Security.TlsCipherSuite?))
                throw new InvalidOperationException("Expected nullable typed cipher API on NET.");
#else
            if (type.GetProperty("NegotiatedCipherSuite") != null)
                throw new InvalidOperationException("Typed cipher API must not be present on net462.");
#endif
            Console.WriteLine("API-SHAPE PASS: NegotiatedCipherSuiteId=int?; typed cipher NET-only; obsolete Value property absent.");
        }

        private static string TypedCipher(SqlConnectionEncryptionInfo info)
        {
#if NET
            return Cipher((int?)info?.NegotiatedCipherSuite);
#else
            return "not-applicable";
#endif
        }

        private static void Emit(string scenario, Server s, string how, Exp e, Outcome o, List<string> fails, string verdict)
        {
            SqlConnectionEncryptionInfo i = o.Info;
            if (o.Ex != null)
            {
                s_messagesAndToString.Add(o.Ex.Message);
                s_messagesAndToString.Add(o.Ex.ToString());
                if (Environment.GetEnvironmentVariable("E2E_STACK") == "1") s_out.WriteLine("#STACK " + o.Ex.ToString().Replace("\r", "").Replace("\n", " | "));
            }

            bool pass = verdict != "FAIL";
            if (verdict == "BLOCKED") s_blocked++; else if (verdict == "KNOWN-GAP") s_gap++; else if (pass) s_pass++; else s_fail++;
            string certificateData = i == null ? "-" : CertificateData(i);
            string certificateMatch = i == null || certificateData == "null" ? "-" : (certificateData == s_probeCertificates[s.Key] ? "match" : (s_allCertificates.Contains(certificateData) ? "OTHER-SERVER" : "MISMATCH"));
            string msg = (o.Ex?.Message ?? o.Other?.Message ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            int prov = msg.IndexOf("(provider:", StringComparison.Ordinal);
            if (prov > 0) msg = msg.Substring(prov);
            if (msg.Length > 220) msg = msg.Substring(0, 220);
            string line = string.Join("\t", s_tfm, (s_sni == "managed" ? "Managed" : "Native") + (s_oldSni ? "-oldSNI" : ""), scenario, s.Key, how, verdict,
                o.Success ? "ok" : "fail", o.Ex?.Number.ToString() ?? "-",
                Str(i?.NegotiatedEncryptionMode), Str(i?.NegotiatedEncryptionProtocol), certificateMatch, Str(i?.ValidationErrors), Str(i?.ChainStatus),
                string.Join("; ", fails), e.Note, msg, Cipher(i?.NegotiatedCipherSuiteId), TypedCipher(i));
            s_out.WriteLine(line);
            Console.WriteLine($"{verdict} {scenario} [{s.Key} {how}] ok={o.Success} #{o.Ex?.Number} mode={Str(i?.NegotiatedEncryptionMode)} proto={Str(i?.NegotiatedEncryptionProtocol)} cipher={Cipher(i?.NegotiatedCipherSuiteId)} typedCipher={TypedCipher(i)} certificate={certificateMatch} VE={Str(i?.ValidationErrors)} CS={Str(i?.ChainStatus)} {string.Join("; ", fails)}");
            if (verdict != "PASS" || !o.Success) Console.WriteLine("    msg: " + msg);
        }

        private static string Str<T>(T? v) where T : struct => v.HasValue ? v.Value.ToString().Replace(", ", "|") : "null";

        private static void CheckFlag(List<string> fails, string name, string spec, long? actual, Type enumType)
        {
            if (spec == "any") return;
            if (spec == "null") { if (actual != null) fails.Add($"{name}={Enum.ToObject(enumType, actual.Value)} (expected null)"); return; }
            string[] parts = spec.Split(':');
            long want = 0;
            foreach (string f in parts[1].Split(',')) want |= Convert.ToInt64(Enum.Parse(enumType, f));
            if (actual == null) { fails.Add($"{name} null (expected {parts[1]})"); return; }
            if (parts[0] == "eq" && actual.Value != want) fails.Add($"{name}={Enum.ToObject(enumType, actual.Value)} (expected {parts[1]})");
            if (parts[0] == "has" && (actual.Value & want) != want) fails.Add($"{name}={Enum.ToObject(enumType, actual.Value)} (expected to contain {parts[1]})");
        }

        // ------------------------------------------------------------------ pooling, reconnect, failover

        private static void Check(string scenario, string server, string how, List<string> fails, string detail, List<string> gaps = null)
        {
            bool pass = fails.Count == 0;
            string verdict = !pass ? "FAIL" : (gaps != null && gaps.Count > 0 ? "KNOWN-GAP" : "PASS");
            if (verdict == "PASS") s_pass++; else if (verdict == "KNOWN-GAP") s_gap++; else s_fail++;
            string notes = string.Join("; ", gaps != null && pass ? gaps : fails);
            string line = string.Join("\t", s_tfm, Managed ? "Managed" : "Native", scenario, server, how, verdict, "", "", "", "", "", "", "", notes, "", detail);
            s_out.WriteLine(line);
            Console.WriteLine($"{verdict} {scenario} [{server} {how}] {notes} :: {detail}");
        }

        private static string Desc(SqlConnectionEncryptionInfo i) =>
            $"mode={Str(i.NegotiatedEncryptionMode)} proto={Str(i.NegotiatedEncryptionProtocol)} cipher={Cipher(i.NegotiatedCipherSuiteId)} typedCipher={TypedCipher(i)} certificate={Short(i)} VE={Str(i.ValidationErrors)} CS={Str(i.ChainStatus)}";

        private static string Short(SqlConnectionEncryptionInfo i)
        {
            string h = CertificateData(i);
            foreach (var kv in s_probeCertificates) if (kv.Value == h) return kv.Key;
            return h == "null" ? "null" : "UNKNOWN";
        }

        private static bool IsEmpty(SqlConnectionEncryptionInfo i) =>
            i.NegotiatedEncryptionMode == null && i.NegotiatedEncryptionProtocol == null && i.NegotiatedCipherSuiteId == null &&
#if NET
            i.NegotiatedCipherSuite == null &&
#endif
            CertificateData(i) == "null" && i.ValidationErrors == null && i.ChainStatus == null;

        private static async Task Open(SqlConnection c, bool async)
        {
            if (async) await c.OpenAsync(); else c.Open();
        }

        private static void RunLifecycle(string scenario, string server, string how, Action action)
        {
            try { action(); }
            catch (Exception ex) when (IsInfrastructureTimeout(ex))
            {
                EmitBlocked(scenario, server, how, "Server unavailable/connection timeout after health gate; cause unresolved: " +
                    ex.ToString().Replace("\r", " ").Replace("\n", " "));
            }
            catch (SqlException ex)
            {
                Check(scenario, server, how, new List<string> { $"SqlException #{ex.Number}: {ex.Message.Replace("\r", " ").Replace("\n", " ")}" },
                    Desc(ex.EncryptionInfo));
            }
        }

        private static void Pooling(bool async) =>
            RunLifecycle("S7 Pooling/lifetime/reset", "SQL2025+SQL2022", async ? "async" : "sync",
                () => PoolingAsync(async).GetAwaiter().GetResult());

        private static async Task PoolingAsync(bool async)
        {
            string how = async ? "async" : "sync";
            if (BlockUnavailable("S7 Pooling/lifetime/reset", how, S25, S22)) return;
            string tag = Guid.NewGuid().ToString("N").Substring(0, 8);
            string ok25 = Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=true", pooling: true, app: "e2e-pool-ok-" + tag);
            string bad25 = Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=false", pooling: true, app: "e2e-pool-bad-" + tag);
            string ok22 = Cs(S22, "Encrypt=Optional", pooling: true, app: "e2e-pool-ok22-" + tag);
            var fails = new List<string>();
            var gaps = new List<string>();
            var detail = new StringBuilder();

            using (var c = new SqlConnection(ok25))
            {
                if (!IsEmpty(c.EncryptionInfo)) fails.Add("not empty before open");
                await Open(c, async);
                SqlConnectionEncryptionInfo a = c.EncryptionInfo;
                CheckCipher(S25, a, true, fails);
                object spid1 = new SqlCommand("select @@SPID", c).ExecuteScalar();
                detail.Append($"open1 {Desc(a)} spid={spid1}; ");
                if (a.NegotiatedEncryptionMode != SqlConnectionEncryptionMode.Full || Short(a) != "SQL2025") fails.Add("open1 unexpected");
                c.Close();
                if (!IsEmpty(c.EncryptionInfo)) fails.Add("not empty after close");
                if (a.NegotiatedEncryptionMode != SqlConnectionEncryptionMode.Full) fails.Add("retained snapshot changed after close");
                await Open(c, async);
                object spid2 = new SqlCommand("select @@SPID", c).ExecuteScalar();
                SqlConnectionEncryptionInfo b = c.EncryptionInfo;
                CheckCipher(S25, b, true, fails);
                detail.Append($"reopen {Desc(b)} spid={spid2} pooled={Equals(spid1, spid2)}; ");
                if (Desc(a) != Desc(b)) fails.Add("pooled reopen differs");

                // Same SqlConnection object switched to a failing connection string.
                c.Close();
                c.ConnectionString = bad25;
                SqlConnectionEncryptionInfo exInfo1 = null, exInfo2 = null;
                try { await Open(c, async); fails.Add("bad open succeeded"); }
                catch (SqlException ex) { exInfo1 = ex.EncryptionInfo; detail.Append($"bad1 #{ex.Number} {Desc(ex.EncryptionInfo)}; "); }
                if (!IsEmpty(c.EncryptionInfo)) fails.Add("connection info not empty after failed open: " + Desc(c.EncryptionInfo));
                // Second failing attempt in the same pool (pool blocking period may return the cached error).
                try { await Open(c, async); fails.Add("bad open2 succeeded"); }
                catch (SqlException ex) { exInfo2 = ex.EncryptionInfo; detail.Append($"bad2 #{ex.Number} {Desc(ex.EncryptionInfo)} sameObj={ReferenceEquals(exInfo1, exInfo2)}; "); }
                if (exInfo1 != null && (exInfo1.ValidationErrors == null || (exInfo1.ValidationErrors.Value & SqlServerCertificateValidationErrors.ChainError) == 0)) fails.Add("bad1 missing ChainError");
                if (exInfo1 != null && (exInfo1.NegotiatedEncryptionMode != SqlConnectionEncryptionMode.Full || exInfo1.NegotiatedEncryptionProtocol == null)) fails.Add("bad1 observed mode/protocol missing");
                if (exInfo1 != null && exInfo1.NegotiatedEncryptionProtocol == null && exInfo1.NegotiatedEncryptionMode != null) fails.Add("bad1 mode without observed protocol");
                if (exInfo1 != null && Short(exInfo1) != "SQL2025") fails.Add("bad1 certificate " + Short(exInfo1));
                CheckCipher(S25, exInfo1, false, fails);
                CheckCipher(S25, exInfo2, false, fails);

                // Switch to a different server; must not leak validation errors or the 2025 certificate.
                c.ConnectionString = ok22;
                await Open(c, async);
                SqlConnectionEncryptionInfo d = c.EncryptionInfo;
                CheckCipher(S22, d, true, fails);
                detail.Append($"ok22 {Desc(d)}; ");
                if (d.NegotiatedEncryptionMode != SqlConnectionEncryptionMode.LoginOnly) fails.Add("ok22 mode");
                if (Short(d) != "SQL2022") fails.Add("ok22 certificate " + Short(d));
                if (d.ValidationErrors != null || d.ChainStatus != null) fails.Add("ok22 leaked VE/CS");
                if (exInfo1 != null && Short(exInfo1) != "SQL2025") fails.Add("exception snapshot changed");
                if (Desc(a) != Desc(b)) fails.Add("earlier snapshot changed");
                c.Close();
            }
            // A fresh connection from the original pool still reports the original physical connection info.
            using (var c2 = new SqlConnection(ok25))
            {
                await Open(c2, async);
                CheckCipher(S25, c2.EncryptionInfo, true, fails);
                detail.Append($"fresh-from-pool {Desc(c2.EncryptionInfo)}");
                if (Short(c2.EncryptionInfo) != "SQL2025" || c2.EncryptionInfo.ValidationErrors != null) fails.Add("fresh pooled info wrong");
            }
            SqlConnection.ClearAllPools();
            Check("S7 Pooling/lifetime/reset", "SQL2025+SQL2022", how, fails, detail.ToString(), gaps);
        }

        private static void Reconnect(bool async) => ReconnectAsync(async).GetAwaiter().GetResult();

        private static async Task ReconnectAsync(bool async)
        {
            string how = async ? "async" : "sync";
            if (BlockUnavailable("S7b Idle reconnect (ConnectRetryCount)", how, S25)) return;
            var fails = new List<string>();
            var detail = new StringBuilder();
            string cs = Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=true;ConnectRetryCount=2;ConnectRetryInterval=1");
            try
            {
                using (var c = new SqlConnection(cs))
                using (var killer = new SqlConnection(Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=true")))
                {
                    await Open(c, async);
                    killer.Open();
                    string before = Desc(c.EncryptionInfo);
                    CheckCipher(S25, c.EncryptionInfo, true, fails);
                    object spid = new SqlCommand("select @@SPID", c).ExecuteScalar();
                    object login1 = new SqlCommand("select convert(varchar(30), login_time, 121) from sys.dm_exec_sessions where session_id=@@SPID", c).ExecuteScalar();
                    new SqlCommand($"KILL {spid}", killer).ExecuteNonQuery();
                    await Task.Delay(500);
                    var cmd = new SqlCommand("select convert(varchar(30), login_time, 121) from sys.dm_exec_sessions where session_id=@@SPID", c);
                    object login2 = async ? await cmd.ExecuteScalarAsync() : cmd.ExecuteScalar();
                    string after = Desc(c.EncryptionInfo);
                    CheckCipher(S25, c.EncryptionInfo, true, fails);
                    detail.Append($"before spid={spid} {before}; login={login1}; after reconnect login={login2} {after}");
                    if (Equals(login1, login2)) fails.Add("no reconnect happened");
                    if (c.EncryptionInfo.NegotiatedEncryptionMode != SqlConnectionEncryptionMode.Full || Short(c.EncryptionInfo) != "SQL2025") fails.Add("info after reconnect wrong");
                }
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                EmitBlocked("S7b Idle reconnect (ConnectRetryCount)", "SQL2025", how, "Server timed out after health gate: " + ex.Message.Replace("\r", " ").Replace("\n", " "));
                return;
            }
            catch (Exception ex) { fails.Add(ex.GetType().Name + ": " + ex.Message); }
            Check("S7b Idle reconnect (ConnectRetryCount)", "SQL2025", how, fails, detail.ToString());
        }

        private static void Failover(bool async) => FailoverAsync(async).GetAwaiter().GetResult();

        private static async Task FailoverAsync(bool async)
        {
            string how = async ? "async" : "sync";
            var fails = new List<string>();
            string detail = "";
            string cs = $"Data Source=tcp:localhost,1499;Failover Partner=tcp:localhost,1440;Initial Catalog=master;Integrated Security=true;Connect Timeout=30;Pooling=false;Encrypt=Mandatory;TrustServerCertificate=true";
            try
            {
                using (var c = new SqlConnection(cs))
                {
                    await Open(c, async);
                    detail = $"connected to {c.DataSource}: {Desc(c.EncryptionInfo)}";
                    if (c.EncryptionInfo.NegotiatedEncryptionMode != SqlConnectionEncryptionMode.Full || Short(c.EncryptionInfo) != "SQL2025") fails.Add("partner info wrong");
                }
            }
            catch (SqlException ex) { fails.Add($"#{ex.Number} {ex.Message}"); detail = Desc(ex.EncryptionInfo); }
            Check("S7c Failover partner (primary closed port)", "SQL2025", how, fails, detail);
        }

        private static void Retained()
        {
            if (BlockUnavailable("S7d Snapshot retained after dispose", "sync", S25)) return;
            var fails = new List<string>();
            SqlConnectionEncryptionInfo snap;
            using (var c = new SqlConnection(Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=true")))
            {
                c.Open();
                snap = c.EncryptionInfo;
                if (CertificateData(snap) != s_probeCertificates[S25.Key])
                    fails.Add("certificate DER differs from independent probe");
            }
            if (Short(snap) != "SQL2025") fails.Add("snapshot changed after dispose");
            CheckCipher(S25, snap, true, fails);
            Check("S7d Snapshot retained after dispose", "SQL2025", "sync", fails, Desc(snap));
        }

        // ------------------------------------------------------------------ extra driver paths (S13-S15)

        private static string PoolType(SqlConnection c)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            object inner = typeof(SqlConnection).GetProperty("InnerConnection", F)?.GetValue(c);
            object pool = inner?.GetType().GetProperty("Pool", F)?.GetValue(inner);
            return pool?.GetType().Name ?? "none";
        }

        private static void CheckPoolImplementation()
        {
            // Pool type sanity: proves whether the v2 switch took effect in this process.
            if (!BlockUnavailable("S13a Pool implementation", "sync", S25))
            using (var c = new SqlConnection(Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=true", pooling: true, app: "e2e-pooltype")))
            {
                c.Open();
                string pt = PoolType(c);
                var f = new List<string>();
                CheckCipher(S25, c.EncryptionInfo, true, f);
                string want = s_poolV2 ? "ChannelDbConnectionPool" : "WaitHandleDbConnectionPool";
                if (pt != want) f.Add($"pool={pt} expected {want}");
                Check("S13a Pool implementation", "SQL2025", "sync", f, "pool=" + pt + " " + Desc(c.EncryptionInfo));
            }
            SqlConnection.ClearAllPools();
        }

        private static void RunExtra()
        {
            RunLifecycle("S13a Pool implementation", "SQL2025", "sync", CheckPoolImplementation);
            Concurrency(false);
            Concurrency(true);
            Broken(false);
            Broken(true);
            RunLifecycle("S14b EncryptionInfo after Dispose", "SQL2025", "sync", Disposed);

            // S15 Shared memory (native only; managed SNI has no shared-memory provider).
            if (Managed)
            {
                Both("S15 SharedMemory Mandatory+TSC", S22, Cs(S22, "Encrypt=Mandatory;TrustServerCertificate=true").Replace("tcp:localhost,1433", "lpc:localhost"),
                    new Exp { Success = false, Mode = null, Proto = "null", Certificate = "null", Note = "managed SNI does not support shared memory (lpc:) - expected error, empty info" });
            }
            else
            {
                Both("S15 SharedMemory Mandatory+TSC", S22, Cs(S22, "Encrypt=Mandatory;TrustServerCertificate=true").Replace("tcp:localhost,1433", "lpc:localhost"),
                    new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match" });
                Both("S15 SharedMemory Optional", S22, Cs(S22, "Encrypt=Optional").Replace("tcp:localhost,1433", "lpc:localhost"),
                    new Exp { Success = true, Mode = SqlConnectionEncryptionMode.LoginOnly, Proto = "set", Certificate = "match" });
                Both("S15 SharedMemory Mandatory no-trust", S22, Cs(S22, "Encrypt=Mandatory").Replace("tcp:localhost,1433", "lpc:localhost"),
                    new Exp { Success = false, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match", VE = "has:ChainError", CS = "has:UntrustedRoot" });
            }
            // Named pipes to the named 2025 instance.
            string np25 = @"np:\\.\pipe\MSSQL$MSSQLSERVER_25_1\sql\query";
            Both("S11b NamedPipe(2025) Mandatory+TSC", S25, Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=true").Replace("tcp:localhost,1440", np25),
                new Exp { Success = true, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match", Note = "requires named pipes enabled on instance" });
            Both("S11b NamedPipe(2025) Mandatory no-trust", S25, Cs(S25, "Encrypt=Mandatory").Replace("tcp:localhost,1440", np25),
                new Exp { Success = false, Mode = SqlConnectionEncryptionMode.Full, Proto = "set", Certificate = "match", VE = "has:ChainError", CS = "has:UntrustedRoot" });
        }

        // S13: many concurrent opens (pooled and non-pooled, good and bad, 3 servers); each result must describe its own target only.
        private static void Concurrency(bool async)
        {
            string how = async ? "async" : "sync";
            if (BlockUnavailable("S13 Concurrency (60 mixed opens, 3 servers)", how, Servers)) return;
            string tag = Guid.NewGuid().ToString("N").Substring(0, 6);
            var fails = new ConcurrentBag<string>();
            var timeouts = new ConcurrentBag<string>();
            var counts = new ConcurrentDictionary<string, int>();
            const int N = 60;
            Func<int, Task> work = async idx =>
            {
                Server s = Servers[idx % 3];
                int kind = (idx / 3) % 4; // 0 good pooled, 1 bad pooled, 2 optional non-pooled, 3 bad non-pooled
                string cs = kind switch
                {
                    0 => Cs(s, "Encrypt=Mandatory;TrustServerCertificate=true", pooling: true, app: "e2e-cc-ok-" + tag),
                    1 => Cs(s, "Encrypt=Mandatory", pooling: true, app: "e2e-cc-bad-" + tag),
                    2 => Cs(s, "Encrypt=Optional"),
                    _ => Cs(s, "Encrypt=Mandatory"),
                };
                Outcome o = async ? await OpenAsync(cs).ConfigureAwait(false) : OpenSync(cs);
                string key = $"{s.Key}/k{kind}";
                if (IsInfrastructureTimeout(o.Ex ?? o.Other))
                {
                    timeouts.Add($"{key}: {(o.Ex ?? o.Other).ToString().Replace("\r", " ").Replace("\n", " ")}");
                    return;
                }
                counts.AddOrUpdate(key + (o.Success ? "/ok" : "/fail"), 1, (_, v) => v + 1);
                if (o.Other != null) { fails.Add($"{key}: {o.Other.GetType().Name} {o.Other.Message}"); return; }
                bool expectOk = kind == 0 || kind == 2;
                if (o.Success != expectOk) { fails.Add($"{key}: success={o.Success}"); return; }
                SqlConnectionEncryptionInfo i = o.Info;
                var cipherFails = new List<string>();
                CheckCipher(s, i, o.Success, cipherFails);
                foreach (string failure in cipherFails) fails.Add($"{key}: {failure}");
                if (CertificateData(i) != s_probeCertificates[s.Key]) fails.Add($"{key}: certificate {Short(i)}");
                var wantMode = kind == 2 ? SqlConnectionEncryptionMode.LoginOnly : SqlConnectionEncryptionMode.Full;
                if (i.NegotiatedEncryptionMode != wantMode) fails.Add($"{key}: mode {Str(i.NegotiatedEncryptionMode)}");
                if (expectOk && (i.ValidationErrors != null || i.ChainStatus != null)) fails.Add($"{key}: leaked VE/CS {Str(i.ValidationErrors)}/{Str(i.ChainStatus)}");
                if (!expectOk && (i.ValidationErrors == null || (i.ValidationErrors.Value & SqlServerCertificateValidationErrors.ChainError) == 0)) fails.Add($"{key}: VE {Str(i.ValidationErrors)}");
            };
            var sw = Stopwatch.StartNew();
            if (async)
                Task.WhenAll(Enumerable.Range(0, N).Select(work)).GetAwaiter().GetResult();
            else
                System.Threading.Tasks.Parallel.For(0, N, new ParallelOptions { MaxDegreeOfParallelism = 16 },
                    i => work(i).GetAwaiter().GetResult());
            SqlConnection.ClearAllPools();
            var fl = fails.Distinct().Take(10).ToList();
            if (timeouts.Count > 0)
            {
                string reason = "Server unavailable/connection timeouts after health gate; cause unresolved: " + string.Join("; ", timeouts.Distinct());
                Console.WriteLine(reason);
                if (fl.Count == 0)
                {
                    EmitBlocked("S13 Concurrency (60 mixed opens, 3 servers)", "all", how, reason);
                    return;
                }
            }
            Check("S13 Concurrency (60 mixed opens, 3 servers)", "all", how, fl,
                $"elapsed={sw.ElapsedMilliseconds}ms " + string.Join(" ", counts.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")) + (fails.Count > 10 ? $" (+{fails.Count - 10} more)" : ""));
        }

        // S14: server kills the session (no retry) -> connection breaks; info must reset with the connection, earlier snapshot unchanged.
        private static void Broken(bool async) => BrokenAsync(async).GetAwaiter().GetResult();

        private static async Task BrokenAsync(bool async)
        {
            string how = async ? "async" : "sync";
            if (BlockUnavailable("S14 Broken connection (KILL, no retry)", how, S25)) return;
            var fails = new List<string>();
            var detail = new StringBuilder();
            string cs = Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=true;ConnectRetryCount=0", pooling: true, app: "e2e-broken-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            try
            {
                using (var c = new SqlConnection(cs))
                using (var killer = new SqlConnection(Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=true")))
                {
                    await Open(c, async);
                    killer.Open();
                    SqlConnectionEncryptionInfo before = c.EncryptionInfo;
                    CheckCipher(S25, before, true, fails);
                    object spid = new SqlCommand("select @@SPID", c).ExecuteScalar();
                    new SqlCommand($"KILL {spid}", killer).ExecuteNonQuery();
                    await Task.Delay(500);
                    try
                    {
                        var cmd = new SqlCommand("select 1", c);
                        if (async) await cmd.ExecuteScalarAsync(); else cmd.ExecuteScalar();
                        fails.Add("command after KILL succeeded");
                    }
                    catch (SqlException ex)
                    {
                        if (ex.Number == -2)
                        {
                            EmitBlocked("S14 Broken connection (KILL, no retry)", "SQL2025", how, "Server timed out after health gate: " + ex.Message.Replace("\r", " ").Replace("\n", " "));
                            return;
                        }
                        detail.Append($"cmd-ex #{ex.Number} exInfo[{Desc(ex.EncryptionInfo)}]; ");
                    }
                    catch (InvalidOperationException ex) { detail.Append($"cmd-ex IOE {ex.Message}; "); }
                    detail.Append($"state={c.State} connInfo[{Desc(c.EncryptionInfo)}]; ");
                    if (c.State != System.Data.ConnectionState.Open && !IsEmpty(c.EncryptionInfo)) fails.Add("info not empty on broken/closed connection");
                    if (Short(before) != "SQL2025" || before.NegotiatedEncryptionMode != SqlConnectionEncryptionMode.Full) fails.Add("earlier snapshot changed");
                    // Reopen: must get a fresh, correct snapshot.
                    if (c.State != System.Data.ConnectionState.Closed) c.Close();
                    await Open(c, async);
                    detail.Append($"reopen[{Desc(c.EncryptionInfo)}]");
                    CheckCipher(S25, c.EncryptionInfo, true, fails);
                    if (Short(c.EncryptionInfo) != "SQL2025" || c.EncryptionInfo.NegotiatedEncryptionMode != SqlConnectionEncryptionMode.Full || c.EncryptionInfo.ValidationErrors != null) fails.Add("reopen info wrong");
                }
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                EmitBlocked("S14 Broken connection (KILL, no retry)", "SQL2025", how, "Server timed out after health gate: " + ex.Message.Replace("\r", " ").Replace("\n", " "));
                return;
            }
            catch (Exception ex) { fails.Add(ex.GetType().Name + ": " + ex.Message); }
            SqlConnection.ClearAllPools();
            Check("S14 Broken connection (KILL, no retry)", "SQL2025", how, fails, detail.ToString());
        }

        private static void Disposed()
        {
            if (BlockUnavailable("S14b EncryptionInfo after Dispose", "sync", S25)) return;
            var fails = new List<string>();
            var c = new SqlConnection(Cs(S25, "Encrypt=Mandatory;TrustServerCertificate=true"));
            c.Open();
            SqlConnectionEncryptionInfo snap = c.EncryptionInfo;
            CheckCipher(S25, snap, true, fails);
            c.Dispose();
            string d = "";
            try
            {
                SqlConnectionEncryptionInfo after = c.EncryptionInfo;
                d = Desc(after);
                if (!IsEmpty(after)) fails.Add("not empty after Dispose");
            }
            catch (Exception ex) { fails.Add("EncryptionInfo threw after Dispose: " + ex.GetType().Name); }
            if (Short(snap) != "SQL2025") fails.Add("retained snapshot changed");
            CheckCipher(S25, snap, true, fails);
            Check("S14b EncryptionInfo after Dispose", "SQL2025", "sync", fails, "after=" + d);
        }

        // ------------------------------------------------------------------ leak checks / modules

        private static void CheckNoLeaks(string label)
        {
            var fails = new List<string>();
            var needles = new List<string>();
            foreach (string h in s_allCertificates)
            {
                byte[] bytes = Convert.FromBase64String(h);
                needles.Add(Convert.ToBase64String(bytes));
                needles.Add(BitConverter.ToString(bytes));
                needles.Add(BitConverter.ToString(bytes).Replace("-", ""));
                needles.Add(BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant());
                using var cert = new X509Certificate2(bytes);
                needles.Add(cert.Thumbprint);
                needles.Add(cert.Thumbprint.ToLowerInvariant());
            }
            int msgHits = s_messagesAndToString.Count(m => needles.Any(n => m.Contains(n)));
            if (msgHits > 0) fails.Add($"{msgHits} exception Message/ToString contain certificate DER/base64/thumbprint");
            string[] events = s_trace.Messages.ToArray();
            int evHits = events.Count(m => needles.Any(n => m.Contains(n)));
            if (evHits > 0) fails.Add($"{evHits} trace events contain certificate DER/base64/thumbprint");
            if (events.Length == 0 && s_pass == 0 && s_fail == 0 && s_blocked > 0)
            {
                EmitBlocked("S10 No certificate in Message/ToString/EventSource", "all", "-", "No scenarios executed after health gate/exclusions");
                return;
            }
            if (events.Length == 0) fails.Add("no trace events captured (listener not working?)");
            Check("S10 No certificate in Message/ToString/EventSource", "all", "-", fails, $"exceptions={s_messagesAndToString.Count / 2} traceEvents={events.Length}");
        }

        private static void LogModules()
        {
            bool nativeLoaded = false;
            foreach (ProcessModule m in Process.GetCurrentProcess().Modules)
            {
                if (m.ModuleName.StartsWith("Microsoft.Data.SqlClient.SNI", StringComparison.OrdinalIgnoreCase))
                {
                    nativeLoaded = true;
                    string line = $"MODULE {m.FileName} FileVersion={m.FileVersionInfo.FileVersion} ProductVersion={m.FileVersionInfo.ProductVersion}";
                    Console.WriteLine(line);
                    s_out.WriteLine("#" + line);
                }
            }
            if (Managed && nativeLoaded)
                throw new InvalidOperationException("Managed run loaded native SNI; networking selection cannot be trusted.");
        }

        // ------------------------------------------------------------------ independent cert probe

#if NET
        // Independent probe: TDS 8.0 strict starts TLS directly on the TCP stream, so a plain SslStream with
        // ALPN "tds/8.0" receives exactly the certificate the server presents to SqlClient.
        private static async Task Probe(Server s)
        {
            string strictHex = null, tds7Hex = null;
            X509Certificate2 cert = null;
            if (!s_skipStrict)
            foreach (SslProtocols protocol in new[] { SslProtocols.Tls12, SslProtocols.Tls13 })
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(s.Host, s.Port).WaitAsync(TimeSpan.FromSeconds(15));
                X509Certificate2 presented = null;
                using var ssl = new SslStream(tcp.GetStream(), false, (o, c, chain, err) => { presented = new X509Certificate2(c); return true; });
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = s.Host,
                    ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("tds/8.0") },
                    EnabledSslProtocols = protocol,
                }).WaitAsync(TimeSpan.FromSeconds(15));
                cert = presented;
                strictHex = Convert.ToBase64String(presented.RawData);
                SaveCipherProbe(s, "strict", ssl);
                Console.WriteLine($"PROBE-STRICT {s.Key} proto={ssl.SslProtocol} alpn={ssl.NegotiatedApplicationProtocol} certificateBytes={presented.RawData.Length}");
            }
            catch (Exception ex) { Console.WriteLine($"PROBE-STRICT {s.Key} requested={protocol} failed: {ex.Message}"); }

            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(s.Host, s.Port);
                NetworkStream ns = tcp.GetStream();
                // PRELOGIN: VERSION + ENCRYPTION(ON) + TERMINATOR
                byte[] payload = { 0x00, 0x00, 0x0B, 0x00, 0x06, 0x01, 0x00, 0x11, 0x00, 0x01, 0xFF, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 };
                byte[] pkt = new byte[8 + payload.Length];
                pkt[0] = 0x12; pkt[1] = 0x01; pkt[2] = (byte)(pkt.Length >> 8); pkt[3] = (byte)pkt.Length; pkt[6] = 1;
                Buffer.BlockCopy(payload, 0, pkt, 8, payload.Length);
                await ns.WriteAsync(pkt);
                byte[] hdr = new byte[8];
                await ns.ReadExactlyAsync(hdr);
                byte[] body = new byte[((hdr[2] << 8) | hdr[3]) - 8];
                await ns.ReadExactlyAsync(body);
                X509Certificate2 presented = null;
                using var ssl = new SslStream(new TdsWrapStream(ns), false, (o, c, chain, err) => { presented = new X509Certificate2(c); return true; });
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = s.Host, EnabledSslProtocols = SslProtocols.Tls12 });
                cert ??= presented;
                tds7Hex = Convert.ToBase64String(presented.RawData);
                SaveCipherProbe(s, "tds7", ssl);
                Console.WriteLine($"PROBE-TDS7 {s.Key} proto={ssl.SslProtocol} certificateBytes={presented.RawData.Length}");
            }
            catch (Exception ex) { Console.WriteLine($"PROBE-TDS7 {s.Key} failed: {ex.Message}"); }

            if (strictHex != null && tds7Hex != null && strictHex != tds7Hex) Console.WriteLine($"PROBE {s.Key} WARNING strict and TDS7 certs differ");
            string hex = tds7Hex ?? strictHex;
            if (cert == null || hex == null) throw new InvalidOperationException($"No independent certificate probe succeeded for {s.Key}");
            File.WriteAllBytes(Path.Combine(s_certDir, s.Key + ".cer"), Convert.FromBase64String(hex));
            cert.Dispose();
        }

        private static void SaveCipherProbe(Server server, string transport, SslStream ssl)
        {
            int suite = (int)ssl.NegotiatedCipherSuite;
            string key = $"{server.Key}/{transport}/{ssl.SslProtocol}";
            Console.WriteLine($"PROBE-CIPHER {key} {Cipher(suite)} ({ssl.NegotiatedCipherSuite})");
            File.AppendAllText(Path.Combine(s_certDir, "ciphers.tsv"), $"{key}\t{suite}{Environment.NewLine}");
        }

        // Wraps TLS handshake records in TDS PRELOGIN (0x12) packets, as TDS 7.x requires.
        private sealed class TdsWrapStream : Stream
        {
            private readonly Stream _inner;
            private int _remaining;
            public TdsWrapStream(Stream inner) { _inner = inner; }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => _inner.Flush();
            public override long Seek(long o, SeekOrigin so) => throw new NotSupportedException();
            public override void SetLength(long v) => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_remaining == 0)
                {
                    byte[] h = new byte[8];
                    _inner.ReadExactly(h);
                    _remaining = ((h[2] << 8) | h[3]) - 8;
                }
                int n = _inner.Read(buffer, offset, Math.Min(count, _remaining));
                _remaining -= n;
                return n;
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                byte[] p = new byte[count + 8];
                p[0] = 0x12; p[1] = 0x01; p[2] = (byte)(p.Length >> 8); p[3] = (byte)p.Length; p[6] = 1;
                Buffer.BlockCopy(buffer, offset, p, 8, count);
                _inner.Write(p, 0, p.Length);
            }
        }
#endif
    }

    internal sealed class TraceListener : EventListener
    {
        public readonly ConcurrentQueue<string> Messages = new ConcurrentQueue<string>();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
            {
                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (e.Payload == null) return;
            Messages.Enqueue(string.Join("|", e.Payload.Select(p =>
                p is byte[] bytes ? Convert.ToBase64String(bytes) : p?.ToString())));
        }
    }
}
