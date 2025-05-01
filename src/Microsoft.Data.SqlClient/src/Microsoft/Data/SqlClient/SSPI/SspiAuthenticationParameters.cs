#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class SspiAuthenticationParameters
    {
        public SspiAuthenticationParameters(string serverName, string resource)
        {
            ServerName = serverName;
        }

        public string Resource { get; }

        public string ServerName { get; }

        public string? UserId { get; set; }

        public string? DatabaseName { get; set; }

        public string? Password { get; set; }
    }
}
