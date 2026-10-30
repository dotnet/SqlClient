// LOCAL E2E ONLY: scenarios against a disposable SQL Server 2025 Linux container with a test PKI (#2353).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient;

namespace EncryptionInfoE2E
{
    internal static partial class Program
    {
        private static Server CtServer(string[] args)
        {
            string port = Arg(args, "--ct-port", null);
            if (port == null) return null;
            string host = Arg(args, "--ct-host", "localhost");
            return new Server("CT", $"tcp:{host},{port}", host, int.Parse(port));
        }

        private static string Auth(Server s)
        {
            if (s_ctIntegrated || s == null || !s.Key.StartsWith("CT", StringComparison.Ordinal)) return "Integrated Security=true";
            string pwd = Environment.GetEnvironmentVariable("E2E_SA_PWD");
            if (string.IsNullOrEmpty(pwd)) pwd = File.ReadAllText(Path.Combine(Path.GetTempPath(), "encinfo-ct", "sa.pwd")).Trim();
            return "Integrated Security=false;User ID=sa;Password=" + pwd;
        }

        private static bool IsTrustedByOs(string cerFile)
        {
            using (var cert = new X509Certificate2(cerFile))
            using (var chain = new X509Chain())
            {
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                return chain.Build(cert);
            }
        }

        private static void RunContainer(Server s, string profile, bool force, bool trusted, string pki)
        {
            string leaf = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(pki, profile + ".cer")));
            var pre = new List<string>();
            if (s_probeCertificates["CT"] != leaf) pre.Add($"probe DER differs from configured {profile} leaf");
            bool osTrusts = IsTrustedByOs(Path.Combine(pki, "good.cer"));
            if (osTrusts != trusted) pre.Add($"OS trust of test CA = {osTrusts}, but run declared trusted={trusted}");
            if (!BlockUnavailable("CT0 setup", "-", s))
                Check("CT0 setup", "CT", "-", pre, $"profile={profile} force={force} trusted={trusted}");

            string pin = Path.Combine(pki, profile + ".cer");
            string other = Path.Combine(pki, "other.cer");
            string untrustedCs = trusted ? null : "PartialChain"; // server sends only the leaf; CA unknown to client
            string Has(params string[] f) => "has:" + string.Join(",", f.Where(x => x != null));

            Exp Ok(SqlConnectionEncryptionMode m, string proto = "set", string ve = "eq:None", string cs = "eq:NoError") =>
                new Exp { Success = true, Mode = m, Proto = proto, Certificate = "match", VE = ve, CS = cs };
            Exp Fail(SqlConnectionEncryptionMode m, string ve, string cs, string proto = "set") =>
                new Exp { Success = false, Mode = m, Proto = proto, Certificate = "match", VE = ve, CS = cs };
            Exp Observe(string note) => new Exp { Observe = true, Certificate = "match", Note = note };

            const SqlConnectionEncryptionMode Full = SqlConnectionEncryptionMode.Full;
            const SqlConnectionEncryptionMode Strict = SqlConnectionEncryptionMode.Strict;
            const SqlConnectionEncryptionMode LoginOnly = SqlConnectionEncryptionMode.LoginOnly;

            // Pin mode never consults the chain, so it behaves the same whether or not the CA is trusted.
            // Pre-existing (stock SNI 7.1.0 too): Native checks the leaf validity period before the pin comparison,
            // so an expired leaf fails with NotTimeValid even when pinned; Managed accepts an exact pin match.
            Exp PinExp(Exp e, SqlConnectionEncryptionMode m)
            {
                if (!Managed && profile == "expired")
                {
                    e.Gap = Fail(m, "eq:ChainError", "has:NotTimeValid");
                    e.Gap.Note = "pre-existing Native SNI: expiry enforced before pin compare";
                }
                return e;
            }
            Both("C5a Strict+pin(correct)", s, Cs(s, $"Encrypt=Strict;ServerCertificate={pin}"), PinExp(Ok(Strict, "set", cs: "null"), Strict));
            Both("C5b Strict+pin(other)", s, Cs(s, $"Encrypt=Strict;ServerCertificate={other}"), PinExp(Fail(Strict, "eq:PinnedCertificateMismatch", "null"), Strict));
            Both("C5c Mandatory+pin(correct)", s, Cs(s, $"Encrypt=Mandatory;ServerCertificate={pin}"), PinExp(Ok(Full, cs: "null"), Full));
            Both("C5d Mandatory+pin(other)", s, Cs(s, $"Encrypt=Mandatory;ServerCertificate={other}"), PinExp(Fail(Full, "eq:PinnedCertificateMismatch", "null"), Full));
            Both("C8 MARS Strict+pin", s, Cs(s, $"Encrypt=Strict;MultipleActiveResultSets=true;ServerCertificate={pin}"), PinExp(Ok(Strict, "set", cs: "null"), Strict));
            Both("C2 Mandatory+TSC", s, Cs(s, "Encrypt=Mandatory;TrustServerCertificate=true"), Ok(Full, ve: "null", cs: "null"));
            Both("C8b MARS Mandatory+TSC", s, Cs(s, "Encrypt=Mandatory;TrustServerCertificate=true;MultipleActiveResultSets=true"), Ok(Full, ve: "null", cs: "null"));

            if (profile == "revoked")
            {
                Both("R1 Mandatory (revoked leaf)", s, Cs(s, "Encrypt=Mandatory"), Observe("revocation: does any path check the CRL?"));
                Both("R2 Strict (revoked leaf)", s, Cs(s, "Encrypt=Strict"), Observe("revocation: does any path check the CRL?"));
                return;
            }

            // Chain-validated paths. good: clean chain except trust; expired/wrongeku add their own chain flag.
            string extraFlag = profile == "expired" ? "NotTimeValid" : profile == "wrongeku" ? "NotValidForUsage" : null;
            bool chainOk = trusted && extraFlag == null;
            Exp Validated(SqlConnectionEncryptionMode m, string proto = "set") => chainOk
                ? Ok(m, proto)
                : Fail(m, "eq:ChainError", Has(untrustedCs, extraFlag), proto);

            Both("C3 Mandatory", s, Cs(s, "Encrypt=Mandatory"), Validated(Full));
            if (s.Host == "localhost" || s.Host == "127.0.0.1")
                Both("C3b Mandatory via 127.0.0.1 (IP SAN)", s, Cs(s, "Encrypt=Mandatory").Replace($"tcp:{s.Host},", "tcp:127.0.0.1,"), Validated(Full));
            Both("C4b Mandatory HNIC=sqlct (in SAN)", s, Cs(s, "Encrypt=Mandatory;HostNameInCertificate=sqlct"), Validated(Full));
            Both("C6 Strict", s, Cs(s, "Encrypt=Strict"), Validated(Strict, "set"));
            Both("C6b Strict+TSC (TSC ignored)", s, Cs(s, "Encrypt=Strict;TrustServerCertificate=true"), Validated(Strict, "set"));

            // Pre-existing (SNI 6.0.2, 7.1.0, 182406, 183446 all identical): with a trusted chain and a cert valid
            // for the target host, Native SNI also connects despite a wrong HostNameInCertificate.
            Exp NativeHnicGap(SqlConnectionEncryptionMode m)
            {
                Exp g = Ok(m);
                g.Note = "pre-existing Native SNI: HNIC ignored when target host matches trusted cert";
                return g;
            }

            Exp wrongName = chainOk
                ? Fail(Full, "eq:HostNameMismatch", "eq:NoError")
                : Fail(Full, Has("HostNameMismatch", "ChainError"), Has(untrustedCs, extraFlag));
            if (!Managed && !chainOk)
            {
                wrongName.Gap = Fail(Full, "eq:ChainError", Has(untrustedCs, extraFlag));
                wrongName.Gap.Note = "historical Native SNI: HNIC ignored when target host matches cert";
            }
            if (!Managed && chainOk) wrongName.Gap = NativeHnicGap(Full);
            Both("C4 Mandatory HNIC=wrong-name", s, Cs(s, "Encrypt=Mandatory;HostNameInCertificate=wrong-name"), wrongName);

            Exp wrongNameStrict = chainOk
                ? Fail(Strict, "eq:HostNameMismatch", "eq:NoError", "set")
                : Fail(Strict, Has("HostNameMismatch", "ChainError"), Has(untrustedCs, extraFlag), "set");
            if (!Managed && !chainOk)
            {
                wrongNameStrict.Gap = Fail(Strict, "eq:ChainError", Has(untrustedCs, extraFlag), "set");
                wrongNameStrict.Gap.Note = "historical Native SNI: HNIC ignored when target host matches cert";
            }
            if (!Managed && chainOk) wrongNameStrict.Gap = NativeHnicGap(Strict);
            Both("C6c Strict HNIC=wrong-name", s, Cs(s, "Encrypt=Strict;HostNameInCertificate=wrong-name"), wrongNameStrict);

            if (profile == "expired")
            {
                Both("E3 Strict+pin(expired leaf)", s, Cs(s, $"Encrypt=Strict;ServerCertificate={pin}"), Observe("pin compares raw bytes; is expiry enforced?"));
            }

            if (force)
            {
                // Server forces encryption: Optional still validates the certificate unless TSC=true (TdsParser shouldValidateServerCert).
                Both("F1 Optional (server forces)", s, Cs(s, "Encrypt=Optional"), Validated(Full));
                Both("F2 Optional+TSC (server forces)", s, Cs(s, "Encrypt=Optional;TrustServerCertificate=true"), Ok(Full, ve: "null", cs: "null"));
            }
            else
            {
                Both("C1 Optional", s, Cs(s, "Encrypt=Optional"), new Exp { Success = true, Mode = LoginOnly, Proto = "set", Certificate = "match" });
            }
        }
    }
}
