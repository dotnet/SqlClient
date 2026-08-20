// The Samples project references the released Microsoft.Data.SqlClient package, which does not yet
// expose the client certificate properties. Remove this guard once a package containing them ships.
#if false

namespace SqlConnection_ClientCertificateAuthentication;

// <Snippet1>
using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

class Program
{
    static async Task Main()
    {
        await OpenLoopbackConnectionAsync();
    }

    private static async Task OpenLoopbackConnectionAsync()
    {
        // Read the certificate locations from the environment so that no path or password is
        // committed to source control.
        string dataSource = Environment.GetEnvironmentVariable("SQLCLIENT_LOOPBACK_DATA_SOURCE")
            ?? throw new InvalidOperationException("Set SQLCLIENT_LOOPBACK_DATA_SOURCE.");
        string clientCertificate = Environment.GetEnvironmentVariable("SQLCLIENT_CLIENT_CERTIFICATE")
            ?? throw new InvalidOperationException("Set SQLCLIENT_CLIENT_CERTIFICATE.");

        // Client Key is only needed when the certificate is PEM or DER encoded. A PKCS#12 (PFX or
        // P12) certificate already contains its private key.
        string clientKey = Environment.GetEnvironmentVariable("SQLCLIENT_CLIENT_KEY")
            ?? string.Empty;
        string clientKeyPassword = Environment.GetEnvironmentVariable("SQLCLIENT_CLIENT_KEY_PASSWORD")
            ?? string.Empty;

        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
        {
            DataSource = dataSource,
            InitialCatalog = "master",
            Encrypt = SqlConnectionEncryptOption.Mandatory,

            // A loopback instance presents a certificate for "localhost" that is normally not
            // chain-trusted. When connecting to anything other than the local machine, supply
            // ServerCertificate or HostNameInCertificate instead of trusting every certificate.
            TrustServerCertificate = true,

            ClientCertificate = clientCertificate,
            ClientKey = clientKey,
            ClientKeyPassword = clientKeyPassword,
        };

        using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            Console.WriteLine("State: {0}", connection.State);
        }
    }
}
// </Snippet1>

#endif
