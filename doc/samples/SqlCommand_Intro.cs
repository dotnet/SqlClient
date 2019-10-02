// <Snippet1>
using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

class Program
{

    static class SqlHelper
    {
        // Set the connection, command, and then execute the command with non query.
        public static Int32 ExecuteNonQuery(String connectionString, String commandText,
        CommandType commandType, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(commandText, conn))
                {
                    // There're three command types: StoredProcedure, Text, TableDirect. The TableDirect
                    // type is only for OLE DB.
                    cmd.CommandType = commandType;
                    cmd.Parameters.AddRange(parameters);

                    conn.Open();
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        // Set the connection, command, and then execute the command and only return one value.
        public static Object ExecuteScalar(String connectionString, String commandText,
        CommandType commandType, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(commandText, conn))
                {
                    cmd.CommandType = commandType;
                    cmd.Parameters.AddRange(parameters);

                    conn.Open();
                    return cmd.ExecuteScalar();
                }
            }
        }

        // Set the connection, command, and then execute the command with query and return the reader.
        public static SqlDataReader ExecuteReader(String connectionString, String commandText,
        CommandType commandType, params SqlParameter[] parameters)
        {
            SqlConnection conn = new SqlConnection(connectionString);

            using (SqlCommand cmd = new SqlCommand(commandText, conn))
            {
                cmd.CommandType = commandType;
                cmd.Parameters.AddRange(parameters);

                conn.Open();
                // When using CommandBehavior.CloseConnection, the connection will be closed when the
                // IDataReader is closed.
                SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.CloseConnection);

                return reader;
            }
        }
    }

    static void Main(string[] args)
    {
        String connectionString = "Data Source=(local);Initial Catalog=MySchool;Integrated Security=True;Asynchronous Processing=true;";

        CountCourses(connectionString, 2012);
        Console.WriteLine();

        Console.WriteLine("Following result is the departments that started from 2007:");
        GetDepartments(connectionString, 2007);
        Console.WriteLine();

        Console.WriteLine("Add the credits when the credits of course is lower than 4.");
        AddCredits(connectionString, 4);
        Console.WriteLine();

        Console.WriteLine("Please press any key to exit...");
        Console.ReadKey();
    }

    static void CountCourses(String connectionString, Int32 year)
    {
        String commandText = "Select Count([CourseID]) FROM [MySchool].[dbo].[Course] Where Year=@Year";
        SqlParameter parameterYear = new SqlParameter("@Year", SqlDbType.Int);
        parameterYear.Value = year;

        Object oValue = SqlHelper.ExecuteScalar(connectionString, commandText, CommandType.Text, parameterYear);
        Int32 count;
        if (Int32.TryParse(oValue.ToString(), out count))
            Console.WriteLine("There {0} {1} course{2} in {3}.", count > 1 ? "are" : "is", count, count > 1 ? "s" : null, year);
    }

    // Display the Departments that start from the specified year.
    static void GetDepartments(String connectionString, Int32 year)
    {
        String commandText = "dbo.GetDepartmentsOfSpecifiedYear";

        // Specify the year of StartDate
        SqlParameter parameterYear = new SqlParameter("@Year", SqlDbType.Int);
        parameterYear.Value = year;

        // When the direction of parameter is set as Output, you can get the value after
        // executing the command.
        SqlParameter parameterBudget = new SqlParameter("@BudgetSum", SqlDbType.Money);
        parameterBudget.Direction = ParameterDirection.Output;

        using (SqlDataReader reader = SqlHelper.ExecuteReader(connectionString, commandText,
        CommandType.StoredProcedure, parameterYear, parameterBudget))
        {
            Console.WriteLine("{0,-20}{1,-20}{2,-20}{3,-20}", "Name", "Budget", "StartDate",
            "Administrator");
            while (reader.Read())
            {
                Console.WriteLine("{0,-20}{1,-20:C}{2,-20:d}{3,-20}", reader["Name"],
                reader["Budget"], reader["StartDate"], reader["Administrator"]);
            }
        }
        Console.WriteLine("{0,-20}{1,-20:C}", "Sum:", parameterBudget.Value);
    }

    // If credits of course is lower than the certain value, the method will add the credits.
    static void AddCredits(String connectionString, Int32 creditsLow)
    {
        String commandText = "Update [MySchool].[dbo].[Course] Set Credits=Credits+1 Where Credits<@Credits";

        SqlParameter parameterCredits = new SqlParameter("@Credits", creditsLow);

        Int32 rows = SqlHelper.ExecuteNonQuery(connectionString, commandText, CommandType.Text, parameterCredits);

        Console.WriteLine("{0} row{1} {2} updated.", rows, rows > 1 ? "s" : null, rows > 1 ? "are" : "is");
    }
}
// </Snippet1>
