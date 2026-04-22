namespace SqlConfigurableRetryLogic_StepByStep_OpenConnection;

using System;
using Microsoft.Data.SqlClient;

// The purpose of this sample is to illustrate how to use this feature and the example conditions might not be realistic.
class RetryLogicSample
{
    private const string CnnStringFormat = "Server=localhost; Initial Catalog=Northwind; Integrated Security=true; pooling=false;";

    static void Main(string[] args)
    {
        RetryConnection(CnnStringFormat);
    }
    private static void RetryConnection(string connectionString)
    {
        // <Snippet1>
        // Define the retry logic parameters
        var options = new SqlRetryLogicOption()
        {
            // Tries 5 times before throwing an exception
            NumberOfTries = 5,
            // Preferred gap time to delay before retry
            DeltaTime = TimeSpan.FromSeconds(1),
            // Maximum gap time for each delay time before retry
            MaxTimeInterval = TimeSpan.FromSeconds(20)
        };
        // </Snippet1>

        // <Snippet2>
        // Create a retry logic provider
        SqlRetryLogicBaseProvider provider = SqlConfigurableRetryFactory.CreateExponentialRetryProvider(options);
        // </Snippet2>

        using(SqlConnection connection = new SqlConnection(connectionString))
        {
            // <Snippet3>
            // Assumes that connection is a valid SqlConnection object 
            // Set the retry logic provider on the connection instance
            connection.RetryLogicProvider = provider;
            // Establishing the connection will retry if a transient failure occurs.
            connection.Open();
            // </Snippet3>
        }
    }
}
