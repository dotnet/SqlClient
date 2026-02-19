// <Snippet1>
using System;
using System.Buffers;
using System.Net.Security;
using Microsoft.Data.SqlClient;

// Custom SSPI context provider that uses NegotiateAuthentication
// to perform SSPI authentication with a SQL Server.
class CustomSspiContextProvider : SspiContextProvider
{
    private NegotiateAuthentication? _auth;

    protected override bool GenerateContext(
        ReadOnlySpan<byte> incomingBlob,
        IBufferWriter<byte> outgoingBlobWriter,
        SspiAuthenticationParameters authParams)
    {
        // Initialize the NegotiateAuthentication on first call
        _auth ??= new NegotiateAuthentication(
            new NegotiateAuthenticationClientOptions
            {
                Package = "Negotiate",
                TargetName = authParams.Resource,
            });

        // Generate the next authentication token
        NegotiateAuthenticationStatusCode statusCode;
        byte[]? blob = _auth.GetOutgoingBlob(incomingBlob, out statusCode);

        if (statusCode is not NegotiateAuthenticationStatusCode.Completed
            and not NegotiateAuthenticationStatusCode.ContinueNeeded)
        {
            return false;
        }

        if (blob is not null)
        {
            outgoingBlobWriter.Write(blob);
        }

        return true;
    }
}

class Program
{
    static void Main()
    {
        using var connection = new SqlConnection("Server=myServer;Integrated Security=true;");
        connection.SspiContextProvider = new CustomSspiContextProvider();
        connection.Open();
        Console.WriteLine("Connected successfully with custom SSPI provider.");
    }
}
// </Snippet1>
