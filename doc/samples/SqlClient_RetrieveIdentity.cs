using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SqlClient_RetrieveIdentity
{
    class Program
    {
        // <Snippet1>
        static void Main(string[] args)
        {
            String SqlDbConnectionString = "Data Source=(local);Initial Catalog=MySchool;Integrated Security=True;";

            InsertPersonInCommand(SqlDbConnectionString, "Janice", "Galvin");
            Console.WriteLine();

            InsertPersonInAdapter(SqlDbConnectionString, "Peter", "Krebs");
            Console.WriteLine();

            Console.WriteLine("Please press any key to exit.....");
            Console.ReadKey();
        }

        // Using stored procedure to insert a new row and retrieve the identity value
        static void InsertPersonInCommand(String connectionString, String firstName, String lastName)
        {
            String commandText = "dbo.InsertPerson";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(commandText, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add(new SqlParameter("@FirstName", firstName));
                    cmd.Parameters.Add(new SqlParameter("@LastName", lastName));
                    SqlParameter personId = new SqlParameter("@PersonID", SqlDbType.Int);
                    personId.Direction = ParameterDirection.Output;
                    cmd.Parameters.Add(personId);

                    conn.Open();
                    cmd.ExecuteNonQuery();

                    Console.WriteLine("Person Id of new person:{0}", personId.Value);
                }
            }
        }

        // Using stored procedure in adapter to insert new rows and update the identity value.
        static void InsertPersonInAdapter(String connectionString, String firstName, String lastName)
        {
            String commandText = "dbo.InsertPerson";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlDataAdapter mySchool = new SqlDataAdapter("Select PersonID,FirstName,LastName from [dbo].[Person]", conn);

                mySchool.InsertCommand = new SqlCommand(commandText, conn);
                mySchool.InsertCommand.CommandType = CommandType.StoredProcedure;

                mySchool.InsertCommand.Parameters.Add(
                    new SqlParameter("@FirstName", SqlDbType.NVarChar, 50, "FirstName"));
                mySchool.InsertCommand.Parameters.Add(
                    new SqlParameter("@LastName", SqlDbType.NVarChar, 50, "LastName"));

                SqlParameter personId = mySchool.InsertCommand.Parameters.Add(new SqlParameter("@PersonID", SqlDbType.Int, 0, "PersonID"));
                personId.Direction = ParameterDirection.Output;

                DataTable persons = new DataTable();
                mySchool.Fill(persons);

                DataRow newPerson = persons.NewRow();
                newPerson["FirstName"] = firstName;
                newPerson["LastName"] = lastName;
                persons.Rows.Add(newPerson);

                mySchool.Update(persons);
                Console.WriteLine("Show all persons:");
                ShowDataTable(persons, 14);
            }
        }

        private static void ShowDataTable(DataTable table, Int32 length)
        {
            foreach (DataColumn col in table.Columns)
            {
                Console.Write("{0,-" + length + "}", col.ColumnName);
            }
            Console.WriteLine();

            foreach (DataRow row in table.Rows)
            {
                foreach (DataColumn col in table.Columns)
                {
                    if (col.DataType.Equals(typeof(DateTime)))
                        Console.Write("{0,-" + length + ":d}", row[col]);
                    else if (col.DataType.Equals(typeof(Decimal)))
                        Console.Write("{0,-" + length + ":C}", row[col]);
                    else
                        Console.Write("{0,-" + length + "}", row[col]);
                }

                Console.WriteLine();
            }
        }
        // </Snippet1>
    }
}
