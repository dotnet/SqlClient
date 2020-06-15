// <Snippet1>
using System;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.DataClassification;

class Program
{
    // Name of the temporary table created for this sample program.
    static string tableName = "SQLCLIENT_DATA_DISCOVERY_CLASSIFICATION";

    public static void Main()
    {
        // To avoid storing the connection string in your code, you can retrieve it from a configuration file. 
        string connectionString = "Data Source=localhost; Integrated Security=true; Initial Catalog=AdventureWorks;";

        // Open a connection to the AdventureWorks database.
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            try
            {
                // Check if the target SQL Server supports Data Discovery and Classification.
                if (DataClassificationSupported(connection))
                {
                    // Create the temporary table and retrieve its Data Discovery and Classification information.
                    CreateTable(connection);
                    RunTests(connection);
                }
            }
            finally
            {
                // Drop the temporary table.
                DropTable(connection);
            }
        }
    }

    /// <summary>
    /// Verifies if SQL Data Discovery and Classification feature is available on the target server.
    /// </summary>
    /// <param name="connection">The SqlConnection to work with.</param>
    /// <returns>True if the target SQL Server supports the feature and false otherwise.</returns>
    public static bool DataClassificationSupported(SqlConnection connection)
    {
        try
        {
            SqlCommand command = new SqlCommand(null, connection);
            command.CommandText = "SELECT * FROM SYS.SENSITIVITY_CLASSIFICATIONS";
            command.ExecuteNonQuery();
        }
        catch (SqlException e)
        {
            // Error 208: Object Not Found
            if (e.Errors != null && e.Errors[0].Number == 208)
            {
                Console.WriteLine("This feature is not supported on the target SQL Server.");
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Creates a temporary table for this sample program and sets tags for Sensitivity Classification.
    /// </summary>
    /// <param name="connection">The SqlConnection to work with.</param>
    private static void CreateTable(SqlConnection connection)
    {
        SqlCommand command = new SqlCommand(null, connection);

        // Creates table for storing Supplier data.
        command.CommandText = $"CREATE TABLE {tableName} ("
            + "[Id] [int] IDENTITY(1,1) NOT NULL,"
            + "[CompanyName] [nvarchar](40) NOT NULL,"
            + "[ContactName] [nvarchar](50) NULL,"
            + "[ContactTitle] [nvarchar](40) NULL,"
            + "[City] [nvarchar](40) NULL,"
            + "[CountryName] [nvarchar](40) NULL,"
            + "[Phone] [nvarchar](30) MASKED WITH (FUNCTION = 'default()') NULL,"
            + "[Fax] [nvarchar](30) MASKED WITH (FUNCTION = 'default()') NULL)";
        command.ExecuteNonQuery();

        // Set Sensitivity Classification tags for table columns.
        command.CommandText = $"ADD SENSITIVITY CLASSIFICATION TO {tableName}"
                + ".CompanyName WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Company Name', INFORMATION_TYPE_ID='COMPANY')";
        command.ExecuteNonQuery();

        command.CommandText = $"ADD SENSITIVITY CLASSIFICATION TO {tableName}"
                + ".ContactName WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Person Name', INFORMATION_TYPE_ID='NAME')";
        command.ExecuteNonQuery();

        command.CommandText = $"ADD SENSITIVITY CLASSIFICATION TO {tableName}"
                + ".Phone WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Contact Information', INFORMATION_TYPE_ID='CONTACT')";
        command.ExecuteNonQuery();

        command.CommandText = $"ADD SENSITIVITY CLASSIFICATION TO {tableName}"
                + ".Fax WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Contact Information', INFORMATION_TYPE_ID='CONTACT')";
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Run query to fetch result set from target table.
    /// </summary>
    /// <param name="connection">The SqlConnection to work with.</param>
    private static void RunTests(SqlConnection connection)
    {
        SqlCommand command = new SqlCommand(null, connection);
        command.CommandText = $"SELECT * FROM {tableName}";
        using (SqlDataReader reader = command.ExecuteReader())
        {
            PrintSensitivityClassification(reader);
        }
    }

    /// <summary>
    /// Prints Sensitivity Classification data as received in the result set.
    /// </summary>
    /// <param name="reader">The SqlDataReader to work with.</param>
    private static void PrintSensitivityClassification(SqlDataReader reader)
    {
        if (reader.SensitivityClassification != null)
        {
            for (int columnPos = 0; columnPos < reader.SensitivityClassification.ColumnSensitivities.Count; columnPos++)
            {
                foreach (SensitivityProperty sp in reader.SensitivityClassification.ColumnSensitivities[columnPos].SensitivityProperties)
                {
                    if (sp.Label != null)
                    {
                        Console.WriteLine($"Labels received for Column : {columnPos}");
                        Console.WriteLine($"Label ID: {sp.Label.Id}");
                        Console.WriteLine($"Label Name: {sp.Label.Name}");
                        Console.WriteLine();
                    }

                    if (sp.InformationType != null)
                    {
                        Console.WriteLine($"Information Types received for Column : {columnPos}");
                        Console.WriteLine($"Information Type ID: {sp.InformationType.Id}");
                        Console.WriteLine($"Information Type: {sp.InformationType.Name}");
                        Console.WriteLine();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Deletes the table created for this sample program.
    /// </summary>
    /// <param name="connection">The SqlConnection to work with.</param>
    private static void DropTable(SqlConnection connection)
    {
        SqlCommand command = new SqlCommand(null, connection);
        command.CommandText = $"DROP TABLE {tableName}";
        command.ExecuteNonQuery();
    }
}
// </Snippet1>
