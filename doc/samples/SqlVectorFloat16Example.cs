namespace SqlVectorFloat16Example;

// VectorFloat16ConsoleApp: Demonstrates working with the float16 base type of the
// SQL Server vector datatype via Microsoft.Data.SqlClient
//
// Highlights:
// - Creates a table with a vector(3, float16) column
// - Inserts vectors using SqlVector<Half> on .NET
// - Inserts vectors from .NET Framework, where System.Half is unavailable
// - Reads float16 vectors as SqlVector<Half>, as widened SqlVector<float>, and as JSON
// - Inspects a column's base type and number of dimensions
// - Converts between the float16 and float32 base types
//
// Requirements:
// - SQL Server 2025 and above, with PREVIEW_FEATURES enabled for the database
// - Microsoft.Data.SqlClient (7.1.0 and above)
//<Snippet1>
using Microsoft.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

class VectorFloat16ConsoleApp
{
    // It is recommended to use a secure connection string in production code with valid cert.
    private const string ConnectionString =
        "Server=localhost;Database=Demo2;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;";

    private const string TableName = "[dbo].[VectorFloat16Demo]";

    static async Task Main()
    {
        try
        {
            using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            await CreateObjectsAsync(conn);

            await InsertVectorsAsync(conn);
            await ReadVectorsAsync(conn);
            await ReadColumnMetadataAsync(conn);
            await ConvertBetweenBaseTypesAsync(conn);
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

    private static async Task CreateObjectsAsync(SqlConnection conn)
    {
        // The float16 base type is in preview, so it has to be enabled for the database.
        string setup = $@"
ALTER DATABASE SCOPED CONFIGURATION SET PREVIEW_FEATURES = ON;
IF OBJECT_ID(N'{TableName}', N'U') IS NOT NULL DROP TABLE {TableName};
CREATE TABLE {TableName}
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VectorData vector(3, float16) NULL
);";
        using var cmd = new SqlCommand(setup, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    #region InsertFloat16Vectors
    private static async Task InsertVectorsAsync(SqlConnection conn)
    {
        string insertSql = $@"INSERT INTO {TableName}(VectorData) VALUES(@VectorData);";
        using var cmd = new SqlCommand(insertSql, conn);
        var p = new SqlParameter("@VectorData", SqlDbTypeExtensions.Vector);
        cmd.Parameters.Add(p);

#if NET
        // On .NET, a float16 vector is represented by SqlVector<Half>.
        p.Value = new SqlVector<Half>(new Half[] { (Half)1.5f, (Half)2.5f, (Half)3.5f });
        await cmd.ExecuteNonQueryAsync();
#endif

        // System.Half is unavailable on .NET Framework, so a float16 vector cannot be
        // represented directly there. A vector of single precision values can be used
        // instead: SQL Server converts it to the column's base type. The conversion loses
        // precision for values which float16 cannot represent exactly, and fails for values
        // outside its range, in the same way as inserting a JSON literal does.
        p.Value = new SqlVector<float>(new float[] { 4.5f, 5.5f, 6.5f });
        await cmd.ExecuteNonQueryAsync();

        // A JSON array can also be used, which SQL Server parses directly into the column's
        // base type.
        cmd.Parameters.Clear();
        cmd.Parameters.Add(new SqlParameter("@VectorData", SqlDbType.VarChar, -1) { Value = "[7.5,8.5,9.5]" });
        await cmd.ExecuteNonQueryAsync();

        Console.WriteLine("Inserted float16 vectors.");
    }
    #endregion

    #region ReadFloat16Vectors
    private static async Task ReadVectorsAsync(SqlConnection conn)
    {
        string selectSql = $@"SELECT Id, VectorData FROM {TableName} ORDER BY Id;";
        using var cmd = new SqlCommand(selectSql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        Console.WriteLine("\nReading rows...");
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);

#if NET
            // On .NET, the column's own base type is available directly.
            SqlVector<Half> exact = reader.GetSqlVector<Half>(1);
            Console.WriteLine($"  Id={id} as Half:  [{string.Join(", ", exact.Memory.ToArray())}]");
#endif

            // On any framework, the elements can be widened to single precision. Widening
            // from float16 is exact, so no information is lost.
            SqlVector<float> widened = reader.GetSqlVector<float>(1);
            Console.WriteLine($"  Id={id} as float: [{string.Join(", ", widened.Memory.ToArray())}]");

            // The value can also be read as a JSON array.
            Console.WriteLine($"  Id={id} as JSON:  {reader.GetString(1)}");
        }
    }
    #endregion

    #region ReadVectorColumnMetadata
    private static async Task ReadColumnMetadataAsync(SqlConnection conn)
    {
        using var cmd = new SqlCommand($@"SELECT VectorData FROM {TableName};", conn);
        using var reader = await cmd.ExecuteReaderAsync();

        DbColumn column = reader.GetColumnSchema()[0];

        // A vector column reports its base type and number of dimensions, which is how an
        // application can discover them without querying the server's catalog views. Both
        // are null for columns which are not vectors.
        Console.WriteLine($"\nColumn base type:  {column["VectorBaseType"]}");
        Console.WriteLine($"Column dimensions: {column["VectorDimensions"]}");
    }
    #endregion

    #region ConvertBetweenBaseTypes
    private static async Task ConvertBetweenBaseTypesAsync(SqlConnection conn)
    {
        // SQL Server converts between the two base types, so a vector read from a column of
        // one base type can be written to a column of the other.
        using var cmd = new SqlCommand(
            "SELECT CAST(CAST('[1.5,2.5,3.5]' AS vector(3, float16)) AS vector(3, float32));", conn);
        using var reader = await cmd.ExecuteReaderAsync();

        await reader.ReadAsync();
        Console.WriteLine($"\nfloat16 converted to float32: {reader.GetString(0)}");
    }
    #endregion
}
//</Snippet1>
