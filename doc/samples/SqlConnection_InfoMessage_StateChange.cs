using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SqlConnection_InfoMessage_StateChange
{
    public class Class1
    {
        public SqlConnection InitiateConnection(string connectionString)
        {
            // Assumes that connectionString is a valid SQL Server connection string.
            var connection = new SqlConnection(connectionString);

            // <Snippet1>
            // Assumes that connection represents a SqlConnection object.
            connection.InfoMessage +=
                (object sender, SqlInfoMessageEventArgs args) =>
                {
                    foreach (SqlError err in args.Errors)
                    {
                        Console.WriteLine(
                      "The {0} has received a severity {1}, state {2} error number {3}\n" +
                      "on line {4} of procedure {5} on server {6}:\n{7}",
                       err.Source, err.Class, err.State, err.Number, err.LineNumber,
                       err.Procedure, err.Server, err.Message);
                    }
                };
            // </Snippet1>

            // <Snippet2>
            // Assumes that connection represents a SqlConnection object.
            connection.StateChange +=
                (object sender, StateChangeEventArgs args) =>
                {
                    Console.WriteLine(
                      "The current Connection state has changed from {0} to {1}.",
                        args.OriginalState, args.CurrentState);
                };
            // </Snippet2>

            return connection;
        }
    }
}
