// <Snippet1>
using System;
using System.Data;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main(string[] args)
    {
        using (SqlConnection conn = new SqlConnection("Data Source=(local);Initial Catalog=MySchool;Integrated Security=True;Asynchronous Processing=true;"))
        {
            conn.Open();

            // Get the Meta Data for Supported Schema Collections  
            DataTable metaDataTable = conn.GetSchema("MetaDataCollections");

            Console.WriteLine("Meta Data for Supported Schema Collections:");
            ShowDataTable(metaDataTable, 25);
            Console.WriteLine();

            // Get the schema information of Databases in your instance  
            DataTable databasesSchemaTable = conn.GetSchema("Databases");

            Console.WriteLine("Schema Information of Databases:");
            ShowDataTable(databasesSchemaTable, 25);
            Console.WriteLine();

            // First, get schema information of all the tables in current database;  
            DataTable allTablesSchemaTable = conn.GetSchema("Tables");

            Console.WriteLine("Schema Information of All Tables:");
            ShowDataTable(allTablesSchemaTable, 20);
            Console.WriteLine();

            // You can specify the Catalog, Schema, Table Name, Table Type to get   
            // the specified table(s).  
            // You can use four restrictions for Table, so you should create a 4 members array.  
            String[] tableRestrictions = new String[4];

            // For the array, 0-member represents Catalog; 1-member represents Schema;   
            // 2-member represents Table Name; 3-member represents Table Type.   
            // Now we specify the Table Name of the table what we want to get schema information.  
            tableRestrictions[2] = "Course";

            DataTable courseTableSchemaTable = conn.GetSchema("Tables", tableRestrictions);

            Console.WriteLine("Schema Information of Course Tables:");
            ShowDataTable(courseTableSchemaTable, 20);
            Console.WriteLine();

            // First, get schema information of all the columns in current database.  
            DataTable allColumnsSchemaTable = conn.GetSchema("Columns");

            Console.WriteLine("Schema Information of All Columns:");
            ShowColumns(allColumnsSchemaTable);
            Console.WriteLine();

            // You can specify the Catalog, Schema, Table Name, Column Name to get the specified column(s).  
            // You can use four restrictions for Column, so you should create a 4 members array.  
            String[] columnRestrictions = new String[4];

            // For the array, 0-member represents Catalog; 1-member represents Schema;   
            // 2-member represents Table Name; 3-member represents Column Name.   
            // Now we specify the Table_Name and Column_Name of the columns what we want to get schema information.  
            columnRestrictions[2] = "Course";
            columnRestrictions[3] = "DepartmentID";

            DataTable departmentIDSchemaTable = conn.GetSchema("Columns", columnRestrictions);

            Console.WriteLine("Schema Information of DepartmentID Column in Course Table:");
            ShowColumns(departmentIDSchemaTable);
            Console.WriteLine();

            // First, get schema information of all the IndexColumns in current database  
            DataTable allIndexColumnsSchemaTable = conn.GetSchema("IndexColumns");

            Console.WriteLine("Schema Information of All IndexColumns:");
            ShowIndexColumns(allIndexColumnsSchemaTable);
            Console.WriteLine();

            // You can specify the Catalog, Schema, Table Name, Constraint Name, Column Name to   
            // get the specified column(s).  
            // You can use five restrictions for Column, so you should create a 5 members array.  
            String[] indexColumnsRestrictions = new String[5];

            // For the array, 0-member represents Catalog; 1-member represents Schema;   
            // 2-member represents Table Name; 3-member represents Constraint Name;4-member represents Column Name.   
            // Now we specify the Table_Name and Column_Name of the columns what we want to get schema information.  
            indexColumnsRestrictions[2] = "Course";
            indexColumnsRestrictions[4] = "CourseID";

            DataTable courseIdIndexSchemaTable = conn.GetSchema("IndexColumns", indexColumnsRestrictions);

            Console.WriteLine("Index Schema Information of CourseID Column in Course Table:");
            ShowIndexColumns(courseIdIndexSchemaTable);
            Console.WriteLine();
        }

        Console.WriteLine("Please press any key to exit...");
        Console.ReadKey();
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

    private static void ShowDataTable(DataTable table)
    {
        ShowDataTable(table, 14);
    }

    private static void ShowColumns(DataTable columnsTable)
    {
        var selectedRows = from info in columnsTable.AsEnumerable()
                           select new
                           {
                               TableCatalog = info["TABLE_CATALOG"],
                               TableSchema = info["TABLE_SCHEMA"],
                               TableName = info["TABLE_NAME"],
                               ColumnName = info["COLUMN_NAME"],
                               DataType = info["DATA_TYPE"]
                           };

        Console.WriteLine("{0,-15}{1,-15}{2,-15}{3,-15}{4,-15}", "TableCatalog", "TABLE_SCHEMA",
            "TABLE_NAME", "COLUMN_NAME", "DATA_TYPE");
        foreach (var row in selectedRows)
        {
            Console.WriteLine("{0,-15}{1,-15}{2,-15}{3,-15}{4,-15}", row.TableCatalog,
                row.TableSchema, row.TableName, row.ColumnName, row.DataType);
        }
    }

    private static void ShowIndexColumns(DataTable indexColumnsTable)
    {
        var selectedRows = from info in indexColumnsTable.AsEnumerable()
                           select new
                           {
                               TableSchema = info["table_schema"],
                               TableName = info["table_name"],
                               ColumnName = info["column_name"],
                               ConstraintSchema = info["constraint_schema"],
                               ConstraintName = info["constraint_name"],
                               KeyType = info["KeyType"]
                           };

        Console.WriteLine("{0,-14}{1,-11}{2,-14}{3,-18}{4,-16}{5,-8}", "table_schema", "table_name", "column_name", "constraint_schema", "constraint_name", "KeyType");
        foreach (var row in selectedRows)
        {
            Console.WriteLine("{0,-14}{1,-11}{2,-14}{3,-18}{4,-16}{5,-8}", row.TableSchema,
                row.TableName, row.ColumnName, row.ConstraintSchema, row.ConstraintName, row.KeyType);
        }
    }
}
// </Snippet1>
