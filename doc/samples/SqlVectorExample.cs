// VectorConsoleApp: End-to-end demo of working with
// SQL Server vector datatype via Microsoft.Data.SqlClient
//
// Highlights:
// - Creates demo tables with a vector(3) column
// - Inserts vectors using SqlParameter (non-null and NULL)
// - Reads vectors back with GetSqlVector<float>()
// - Uses a prepared command for efficient repeated inserts
// - Demonstrates a stored procedure with vector IN and OUTPUT parameters
// - Performs SqlBulkCopy into a vector column
//   • From a SqlDataReader (source table → destination table)
//   • From a DataTable (in-memory → destination table)
//
// Requirements:
// - SQL Server 2025 and above
// - Microsoft.Data.SqlClient (6.1.2 and above)
//<Snippet1>
using Microsoft.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using System.Data;

class VectorConsoleApp
{
    // It is recommended to use a secure connection string in production code with valid cert.
    private const string ConnectionString =
        "Server=localhost;Database=Demo2;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;";

    private const string TableName = "[dbo].[VectorDocsDemo]";
    private const string ProcName = "[dbo].[VectorDocsEchoProc]";

    // Bulk copy demo tables
    private const string SrcTable = "[dbo].[VectorDocsSrc]";
    private const string DestTable = "[dbo].[VectorDocsBulkDest]";

    static async Task Main()
    {
        try
        {
            using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            // Create tables and stored procedures needed for the demo
            await DropObjectsIfExistAsync(conn);
            await CreateObjectsAsync(conn);

            // ===== Basic Demos for inserts, reads and bulk copy =====
            await InsertNonNullVectorValuesAsync(conn);
            await InsertNullVectorValuesAsync(conn);
            await ReadRowsAsync(conn);
            await PreparedInsertAsync(conn, rowCount: 5);
            await ReadRowsAsync(conn);
            await CallStoredProcedureAsync(conn);

            // ===== Bulk Copy Demos =====

            // Prepare source and destination tables
            await DropBulkObjectsIfExistAsync(conn);
            await CreateBulkObjectsAsync(conn);
            await SeedBulkSourceAsync(conn);

            // Bulk-copy from a SqlDataReader
            await BulkCopyFromReaderAsync(conn);
            await VerifyBulkDestinationAsync(conn, heading: "BulkCopy from SqlDataReader");

            // Delete all rows from destination table
            await TruncateBulkDestinationAsync(conn);

            // Bulk-copy from a DataTable
            await BulkCopyFromDataTableAsync(conn);
            await VerifyBulkDestinationAsync(conn, heading: "BulkCopy from DataTable");
        }
        catch (SqlException ex)
        {
            Console.Error.WriteLine($"SQL ERROR: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex}");
        }
    }

    #region CreateTableAndProc
    private static async Task CreateObjectsAsync(SqlConnection conn)
    {
        string createTable = $@"
IF OBJECT_ID(N'{TableName}', N'U') IS NULL
BEGIN
    CREATE TABLE {TableName}
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        VectorData vector(3) NULL
    );
END";
        using (var cmd = new SqlCommand(createTable, conn))
            await cmd.ExecuteNonQueryAsync();

        string createProc = $@"
IF OBJECT_ID(N'{ProcName}', N'P') IS NOT NULL
    DROP PROCEDURE {ProcName};
GO
CREATE PROCEDURE {ProcName}
    @VectorData       vector(3),
    @OutputVectorData vector(3) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO {TableName}(VectorData) VALUES(@VectorData);
    SELECT TOP (1) @OutputVectorData = VectorData
    FROM {TableName}
    ORDER BY Id DESC;
END
";
        foreach (var batch in createProc.Split(new[] { "\r\nGO", "\nGO" }, StringSplitOptions.RemoveEmptyEntries))
        {
            using var cmd = new SqlCommand(batch, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
    #endregion

    private static async Task DropObjectsIfExistAsync(SqlConnection conn)
    {
        string dropProc = $@"IF OBJECT_ID(N'{ProcName}', N'P') IS NOT NULL DROP PROCEDURE {ProcName};";
        string dropTable = $@"IF OBJECT_ID(N'{TableName}', N'U') IS NOT NULL DROP TABLE {TableName};";
        using (var cmd = new SqlCommand(dropProc, conn))
            await cmd.ExecuteNonQueryAsync();
        using (var cmd = new SqlCommand(dropTable, conn))
            await cmd.ExecuteNonQueryAsync();
    }

    #region InsertWithSqlVectorParam
    private static async Task InsertNonNullVectorValuesAsync(SqlConnection conn)
    {
        float[] values = new float[] { 1.1f, 2.2f, 3.3f };
        var vector = new SqlVector<float>(values);

        string insertSql = $@"INSERT INTO {TableName}(VectorData) VALUES(@VectorData);";
        using var cmd = new SqlCommand(insertSql, conn);

        // Insert using SqlParameter with SqlDbTypeExtensions.Vector or SqlDbType.Vector
        var p = new SqlParameter
        {
            ParameterName = "@VectorData",
            SqlDbType = SqlDbTypeExtensions.Vector,
            Value = vector
        };
        cmd.Parameters.Add(p);
        int rows = await cmd.ExecuteNonQueryAsync();
        cmd.Parameters.Clear();

        // Insert using SqlParameter(ParamName, Value) (internally infers SqlDbTypeExtensions.Vector or SqlDbType.Vector)
        cmd.Parameters.Add(new SqlParameter("@VectorData", vector));
        rows += await cmd.ExecuteNonQueryAsync();
        cmd.Parameters.Clear();

        // Use AddWithValue (internally infers SqlDbTypeExtensions.Vector or SqlDbType.Vector)
        cmd.Parameters.AddWithValue("@VectorData", new SqlVector<float>(new float[] { 4.4f, 5.5f, 6.6f }));
        rows += await cmd.ExecuteNonQueryAsync();


        Console.WriteLine($"Inserted non-null vector rows: {rows}");
    }
    #endregion

    #region InsertNullVector
    private static async Task InsertNullVectorValuesAsync(SqlConnection conn)
    {
        string insertSql = $@"INSERT INTO {TableName}(VectorData) VALUES(@VectorData);";
        using var cmd = new SqlCommand(insertSql, conn);

        // Insert a NULL vector using SqlVector<T>.CreateNull(dimension)
        var p = new SqlParameter("@VectorData", SqlDbTypeExtensions.Vector) { Value = SqlVector<float>.CreateNull(3) };
        cmd.Parameters.Add(p);
        int rows = await cmd.ExecuteNonQueryAsync();
        cmd.Parameters.Clear();

        // Insert a NULL vector using DBNull.Value
        cmd.Parameters.AddWithValue("@VectorData", DBNull.Value);
        rows += await cmd.ExecuteNonQueryAsync();
        cmd.Parameters.Clear();

        Console.WriteLine($"Inserted NULL vector rows: {rows}");
    }
    #endregion

    #region ReadVectors
    private static async Task ReadRowsAsync(SqlConnection conn)
    {
        string selectSql = $@"SELECT Id, VectorData FROM {TableName} ORDER BY Id;";
        using var cmd = new SqlCommand(selectSql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        Console.WriteLine("\nReading rows...");
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);

            // Read vector column using GetSqlVector<T>()
            SqlVector<float> v = reader.GetSqlVector<float>(1);
            Console.WriteLine($"  Id:{id} Length:{v.Length} IsNull:{v.IsNull}, Values:[{string.Join(", ", v.Memory.ToArray())}]");

            // For backward compatibility purposes, you can also read a vector column as a string.
            // This is not efficient and not recommended for new applications.
            if (await reader.IsDBNullAsync(1))
            {
                Console.WriteLine($"  Id={id}: VectorData (string): NULL");
            }
            else
            {
                Console.WriteLine($"  Id={id}: VectorData (string): {reader.GetString(1)}");
            }
        }
    }
    #endregion

    #region PreparedInsert
    private static async Task PreparedInsertAsync(SqlConnection conn, int rowCount)
    {
        string insertSql = $@"INSERT INTO {TableName}(VectorData) VALUES(@VectorData);";
        using var cmd = new SqlCommand(insertSql, conn);

        // For SqlDbType.Vector or SqlDbTypeExtensions.Vector, it is not necessary to specify Size.
        // It is inferred from the Value.
        var p = new SqlParameter("@VectorData", SqlDbTypeExtensions.Vector);
        cmd.Parameters.Add(p);

        await cmd.PrepareAsync();

        for (int i = 0; i < rowCount; i++)
        {
            var vector = new SqlVector<float>(new float[] { i + 0.1f, i + 0.2f, i + 0.3f });
            p.Value = vector;
            await cmd.ExecuteNonQueryAsync();
        }

        Console.WriteLine($"\nPrepared insert: added {rowCount} rows.");
    }
    #endregion

    #region StoredProcedureInOut
    private static async Task CallStoredProcedureAsync(SqlConnection conn)
    {
        using var cmd = new SqlCommand(ProcName, conn) { CommandType = CommandType.StoredProcedure };

        var inVector = new SqlVector<float>(new float[] { 9.9f, 8.8f, 7.7f });
        cmd.Parameters.Add(new SqlParameter("@VectorData", SqlDbTypeExtensions.Vector) { Value = inVector });

        // Vector output parameter must be initialized to a null value with the correct dimension.
        var outParam = new SqlParameter
        {
            ParameterName = "@OutputVectorData",
            SqlDbType = SqlDbTypeExtensions.Vector,
            Direction = ParameterDirection.Output,
            Value = SqlVector<float>.CreateNull(3)
        };
        cmd.Parameters.Add(outParam);

        await cmd.ExecuteNonQueryAsync();

        var echoed = (SqlVector<float>)outParam.Value;
        Console.WriteLine($"\nStored proc echoed Length={echoed.Length}, IsNull: {echoed.IsNull}, Values=[{string.Join(", ", echoed.Memory.ToArray())}]");
    }
    #endregion

    // =========================
    // ===== Bulk Copy ========
    // =========================

    private static async Task DropBulkObjectsIfExistAsync(SqlConnection conn)
    {
        string dropSrc = $@"IF OBJECT_ID(N'{SrcTable}',  N'U') IS NOT NULL DROP TABLE {SrcTable};";
        string dropDest = $@"IF OBJECT_ID(N'{DestTable}', N'U') IS NOT NULL DROP TABLE {DestTable};";
        using (var cmd = new SqlCommand(dropSrc, conn))
            await cmd.ExecuteNonQueryAsync();
        using (var cmd = new SqlCommand(dropDest, conn))
            await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CreateBulkObjectsAsync(SqlConnection conn)
    {
        string createSrc = $@"
CREATE TABLE {SrcTable}
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VectorData vector(3) NULL
);";
        string createDest = $@"
CREATE TABLE {DestTable}
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VectorData vector(3) NULL
);";
        using (var cmd = new SqlCommand(createSrc, conn))
            await cmd.ExecuteNonQueryAsync();
        using (var cmd = new SqlCommand(createDest, conn))
            await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SeedBulkSourceAsync(SqlConnection conn)
    {
        // Insert one non-null and one null row into source
        string insert = $@"INSERT INTO {SrcTable}(VectorData) VALUES(@v);";
        using var cmd = new SqlCommand(insert, conn);
        var p = new SqlParameter("@v", SqlDbTypeExtensions.Vector);
        cmd.Parameters.Add(p);

        p.Value = new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f });
        await cmd.ExecuteNonQueryAsync();

        p.Value = DBNull.Value;
        await cmd.ExecuteNonQueryAsync();

        Console.WriteLine("\nSeeded bulk-copy source table with 2 rows (one vector, one NULL).");
    }

    #region BulkCopy_FromReader
    private static async Task BulkCopyFromReaderAsync(SqlConnection conn)
    {
        Console.WriteLine("\nBulkCopy: from SqlDataReader → destination table");

        // Read only the VectorData column (let destination IDENTITY generate Id)
        using var srcCmd = new SqlCommand($@"SELECT VectorData FROM {SrcTable} ORDER BY Id;", conn);
        using var reader = await srcCmd.ExecuteReaderAsync();

        using var destConn = new SqlConnection(conn.ConnectionString);
        await destConn.OpenAsync();

        using var bulkCopy = new SqlBulkCopy(destConn)
        {
            DestinationTableName = DestTable
        };
        bulkCopy.ColumnMappings.Add("VectorData", "VectorData");

        await bulkCopy.WriteToServerAsync(reader);
    }
    #endregion

    private static async Task TruncateBulkDestinationAsync(SqlConnection conn)
    {
        using var cmd = new SqlCommand($@"TRUNCATE TABLE {DestTable};", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    #region BulkCopy_FromDataTable
    private static async Task BulkCopyFromDataTableAsync(SqlConnection conn)
    {
        Console.WriteLine("\nBulkCopy: from DataTable → destination table");

        // Build an in-memory table with a VectorData column only
        var table = new DataTable();
        table.Columns.Add("VectorData", typeof(object)); // store SqlVector<float> or DBNull
        table.Rows.Add(SqlVector<float>.CreateNull(3));
        table.Rows.Add(new SqlVector<float>(new float[] { 7.1f, 8.2f, 9.3f }));
        table.Rows.Add(DBNull.Value);

        // Use a SEPARATE connection for the destination bulk copy
        using var destConn = new SqlConnection(conn.ConnectionString);
        await destConn.OpenAsync();

        using var bulkCopy = new SqlBulkCopy(destConn)
        {
            DestinationTableName = DestTable
        };
        bulkCopy.ColumnMappings.Add("VectorData", "VectorData");

        await bulkCopy.WriteToServerAsync(table);
    }
    #endregion

    private static async Task VerifyBulkDestinationAsync(SqlConnection conn, string heading)
    {
        Console.WriteLine($"\nVerifying destination rows ({heading})...");
        using var cmd = new SqlCommand($@"SELECT Id, VectorData FROM {DestTable} ORDER BY Id;", conn);
        using var reader = await cmd.ExecuteReaderAsync();

        int count = 0;
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            if (await reader.IsDBNullAsync(1))
            {
                Console.WriteLine($"  Id={id}: VectorData=NULL");
            }
            else
            {
                var vec = reader.GetSqlVector<float>(1);
                Console.WriteLine($"  Id={id}: Length={vec.Length}, Values=[{string.Join(", ", vec.Memory.ToArray())}]");
            }
            count++;
        }
        Console.WriteLine($"Destination row count: {count}");
    }
}
//</Snippet1>
