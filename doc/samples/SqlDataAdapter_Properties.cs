namespace SqlDataAdapter_Properties;

// <Snippet1>
using System;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        Settings settings = new Settings();

        // Copy the data from the database.  Get the table Department and Course from the database.
        String selectString = @"SELECT [DepartmentID],[Name],[Budget],[StartDate],[Administrator]
                                     FROM [MySchool].[dbo].[Department];

                                   SELECT [CourseID],@Year as [Year],Max([Title]) as [Title],
                                   Max([Credits]) as [Credits],Max([DepartmentID]) as [DepartmentID]
                                   FROM [MySchool].[dbo].[Course]
                                   Group by [CourseID]";

        DataSet mySchool = new DataSet();

        SqlCommand selectCommand = new SqlCommand(selectString);
        SqlParameter parameter = selectCommand.Parameters.Add("@Year", SqlDbType.SmallInt, 2);
        parameter.Value = new Random(DateTime.Now.Millisecond).Next(9999);

        // Use DataTableMapping to map the source tables and the destination tables.
        DataTableMapping[] tableMappings = { new DataTableMapping("Table", "Department"), new DataTableMapping("Table1", "Course") };
        CopyData(mySchool, settings.MySchoolConnectionString, selectCommand, tableMappings);

        Console.WriteLine("The following tables are from the database.");
        foreach (DataTable table in mySchool.Tables)
        {
            Console.WriteLine(table.TableName);
            ShowDataTable(table);
        }

        // Roll back the changes
        DataTable department = mySchool.Tables["Department"];
        DataTable course = mySchool.Tables["Course"];

        department.Rows[0]["Name"] = "New" + department.Rows[0][1];
        course.Rows[0]["Title"] = "New" + course.Rows[0]["Title"];
        course.Rows[0]["Credits"] = 10;

        Console.WriteLine("After we changed the tables:");
        foreach (DataTable table in mySchool.Tables)
        {
            Console.WriteLine(table.TableName);
            ShowDataTable(table);
        }

        department.RejectChanges();
        Console.WriteLine("After use the RejectChanges method in Department table to roll back the changes:");
        ShowDataTable(department);

        DataColumn[] primaryColumns = { course.Columns["CourseID"] };
        DataColumn[] resetColumns = { course.Columns["Title"] };
        ResetCourse(course, settings.MySchoolConnectionString, primaryColumns, resetColumns);
        Console.WriteLine("After use the ResetCourse method in Course table to roll back the changes:");
        ShowDataTable(course);

        // Batch update the table.
        String insertString = @"Insert into [MySchool].[dbo].[Course]([CourseID],[Year],[Title],
                                   [Credits],[DepartmentID])
             values (@CourseID,@Year,@Title,@Credits,@DepartmentID)";
        SqlCommand insertCommand = new SqlCommand(insertString);
        insertCommand.Parameters.Add("@CourseID", SqlDbType.NVarChar, 10, "CourseID");
        insertCommand.Parameters.Add("@Year", SqlDbType.SmallInt, 2, "Year");
        insertCommand.Parameters.Add("@Title", SqlDbType.NVarChar, 100, "Title");
        insertCommand.Parameters.Add("@Credits", SqlDbType.Int, 4, "Credits");
        insertCommand.Parameters.Add("@DepartmentID", SqlDbType.Int, 4, "DepartmentID");

        const Int32 batchSize = 10;
        BatchInsertUpdate(course, settings.MySchoolConnectionString, insertCommand, batchSize);
    }

    private static void CopyData(DataSet dataSet, String connectionString, SqlCommand selectCommand, DataTableMapping[] tableMappings)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            selectCommand.Connection = connection;

            connection.Open();

            using (SqlDataAdapter adapter = new SqlDataAdapter(selectCommand))
            {
                adapter.TableMappings.AddRange(tableMappings);
                // If set the AcceptChangesDuringFill as the false, AcceptChanges will not be called on a
                // DataRow after it is added to the DataTable during any of the Fill operations.
                adapter.AcceptChangesDuringFill = false;

                adapter.Fill(dataSet);
            }
        }
    }

    // Roll back only one column or several columns data of the Course table by call ResetDataTable method.
    private static void ResetCourse(DataTable table, String connectionString,
        DataColumn[] primaryColumns, DataColumn[] resetColumns)
    {
        table.PrimaryKey = primaryColumns;

        // Build the query string
        String primaryCols = String.Join(",", primaryColumns.Select(col => col.ColumnName));
        String resetCols = String.Join(",", resetColumns.Select(col => $"Max({col.ColumnName}) as {col.ColumnName}"));

        String selectString = $"Select {primaryCols},{resetCols} from Course Group by {primaryCols}";

        SqlCommand selectCommand = new SqlCommand(selectString);

        ResetDataTable(table, connectionString, selectCommand);
    }

    // RejectChanges will roll back all changes made to the table since it was loaded, or the last time AcceptChanges
    // was called. When you copy from the database, you can lose all the data after calling RejectChanges
    // The ResetDataTable method rolls back one or more columns of data.
    private static void ResetDataTable(DataTable table, String connectionString,
        SqlCommand selectCommand)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            selectCommand.Connection = connection;

            connection.Open();

            using (SqlDataAdapter adapter = new SqlDataAdapter(selectCommand))
            {
                // The incoming values for this row will be written to the current version of each
                // column. The original version of each column's data will not be changed.
                adapter.FillLoadOption = LoadOption.Upsert;

                adapter.Fill(table);
            }
        }
    }

    private static void BatchInsertUpdate(DataTable table, String connectionString,
        SqlCommand insertCommand, Int32 batchSize)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            insertCommand.Connection = connection;
            // When setting UpdateBatchSize to a value other than 1, all the commands
            // associated with the SqlDataAdapter have to have their UpdatedRowSource
            // property set to None or OutputParameters. An exception is thrown otherwise.
            insertCommand.UpdatedRowSource = UpdateRowSource.None;

            connection.Open();

            using (SqlDataAdapter adapter = new SqlDataAdapter())
            {
                adapter.InsertCommand = insertCommand;
                // Gets or sets the number of rows that are processed in each round-trip to the server.
                // Setting it to 1 disables batch updates, as rows are sent one at a time.
                adapter.UpdateBatchSize = batchSize;

                adapter.Update(table);

                Console.WriteLine("Successfully to update the table.");
            }
        }
    }

    private static void ShowDataTable(DataTable table)
    {
        foreach (DataColumn col in table.Columns)
        {
            Console.Write("{0,-14}", col.ColumnName);
        }
        Console.WriteLine("{0,-14}", "RowState");

        foreach (DataRow row in table.Rows)
        {
            foreach (DataColumn col in table.Columns)
            {
                if (col.DataType.Equals(typeof(DateTime)))
                    Console.Write("{0,-14:d}", row[col]);
                else if (col.DataType.Equals(typeof(Decimal)))
                    Console.Write("{0,-14:C}", row[col]);
                else
                    Console.Write("{0,-14}", row[col]);
            }
            Console.WriteLine("{0,-14}", row.RowState);
        }
    }
}

internal sealed partial class Settings : System.Configuration.ApplicationSettingsBase
{
    private static readonly Settings defaultInstance =
        ((Settings)(System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));

    public static Settings Default => defaultInstance;

    [System.Configuration.ApplicationScopedSetting()]
    [System.Configuration.DefaultSettingValue("Data Source=(local);Initial Catalog=MySchool;Integrated Security=True")]
    public string MySchoolConnectionString => ((string)(this["MySchoolConnectionString"]));
}
// </Snippet1>
