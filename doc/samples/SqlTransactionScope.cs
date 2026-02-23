namespace SqlTransactionScope;

// <Snippet1>
using System;
using System.Transactions;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main(string[] args)
    {
        string connectionString = "Data Source = localhost; Integrated Security = true; Initial Catalog = AdventureWorks";

        string commandText1 = "INSERT INTO Production.ScrapReason(Name) VALUES('Wrong size')";
        string commandText2 = "INSERT INTO Production.ScrapReason(Name) VALUES('Wrong color')";

        int result = CreateTransactionScope(connectionString, connectionString, commandText1, commandText2);

        Console.WriteLine("result = " + result);
    }

    static public int CreateTransactionScope(string connectString1, string connectString2,
                                            string commandText1, string commandText2)
    {
        // Initialize the return value to zero and create a StringWriter to display results.  
        int returnValue = 0;
        System.IO.StringWriter writer = new System.IO.StringWriter();

        // Create the TransactionScope in which to execute the commands, guaranteeing  
        // that both commands will commit or roll back as a single unit of work.  
        using (TransactionScope scope = new TransactionScope())
        {
            using (SqlConnection connection1 = new SqlConnection(connectString1))
            {
                try
                {
                    // Opening the connection automatically enlists it in the
                    // TransactionScope as a lightweight transaction.  
                    connection1.Open();

                    // Create the SqlCommand object and execute the first command.  
                    SqlCommand command1 = new SqlCommand(commandText1, connection1);
                    returnValue = command1.ExecuteNonQuery();
                    writer.WriteLine("Rows to be affected by command1: {0}", returnValue);

                    // if you get here, this means that command1 succeeded. By nesting  
                    // the using block for connection2 inside that of connection1, you  
                    // conserve server and network resources by opening connection2
                    // only when there is a chance that the transaction can commit.
                    using (SqlConnection connection2 = new SqlConnection(connectString2))
                        try
                        {
                            // The transaction is promoted to a full distributed  
                            // transaction when connection2 is opened.  
                            connection2.Open();

                            // Execute the second command in the second database.  
                            returnValue = 0;
                            SqlCommand command2 = new SqlCommand(commandText2, connection2);
                            returnValue = command2.ExecuteNonQuery();
                            writer.WriteLine("Rows to be affected by command2: {0}", returnValue);
                        }
                        catch (Exception ex)
                        {
                            // Display information that command2 failed.  
                            writer.WriteLine("returnValue for command2: {0}", returnValue);
                            writer.WriteLine("Exception Message2: {0}", ex.Message);
                        }
                }
                catch (Exception ex)
                {
                    // Display information that command1 failed.  
                    writer.WriteLine("returnValue for command1: {0}", returnValue);
                    writer.WriteLine("Exception Message1: {0}", ex.Message);
                }
            }

            // If an exception has been thrown, Complete will not
            // be called and the transaction is rolled back.  
            scope.Complete();
        }

        // The returnValue is greater than 0 if the transaction committed.  
        if (returnValue > 0)
        {
            writer.WriteLine("Transaction was committed.");
        }
        else
        {
            // You could write additional business logic here, notify the caller by  
            // throwing a TransactionAbortedException, or log the failure.  
            writer.WriteLine("Transaction rolled back.");
        }

        // Display messages.  
        Console.WriteLine(writer.ToString());

        return returnValue;
    }
}
// </Snippet1>
