using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SqlCommand_ExecuteNonQuery_SP_DML
{
    class Program
    {
        static void Main()
        {
            string str = "Data Source=(local);Initial Catalog=Northwind;"
                + "Integrated Security=SSPI";

            CreateStoredProcedure(str);
            CreateCommand(str);
        }

        private static void CreateStoredProcedure(string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // <Snippet3>
                // Assumes connection is a valid SqlConnection.
                string queryString = "CREATE PROCEDURE InsertCategory  " + 
                    "@CategoryName nchar(15), " + 
                    "@Identity int OUT " + 
                    "AS " + 
                    "INSERT INTO Categories (CategoryName) VALUES(@CategoryName) " + 
                    "SET @Identity = @@Identity " + 
                    "RETURN @@ROWCOUNT";

                SqlCommand command = new SqlCommand(queryString, connection);
                command.ExecuteNonQuery();
                // </Snippet3>
            }
        }

        private static void CreateCommand(string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // <Snippet1>
                // Assumes connection is a valid SqlConnection.
                connection.Open();

                string queryString = "INSERT INTO Customers " +
                "(CustomerID, CompanyName) Values('NWIND', 'Northwind Traders')";

                SqlCommand command = new SqlCommand(queryString, connection);
                Int32 recordsAffected = command.ExecuteNonQuery();
                // </Snippet1>

                // <Snippet2>
                // Assumes command is a valid SqlCommand with an open connection.
                command.CommandText = "InsertCategory";
                command.CommandType = CommandType.StoredProcedure;

                SqlParameter parameter = command.Parameters.Add("@RowCount", SqlDbType.Int);
                parameter.Direction = ParameterDirection.ReturnValue;

                parameter = command.Parameters.Add("@CategoryName", SqlDbType.NChar, 15);

                parameter = command.Parameters.Add("@Identity", SqlDbType.Int);
                parameter.Direction = ParameterDirection.Output;

                command.Parameters["@CategoryName"].Value = "New Category";
                command.ExecuteNonQuery();

                Int32 categoryID = (Int32) command.Parameters["@Identity"].Value;
                Int32 rowCount = (Int32) command.Parameters["@RowCount"].Value;
                // </Snippet2>
            }
        }
    }
}
