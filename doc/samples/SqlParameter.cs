using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;

class Program
{
    // using Microsoft.Data.SqlClient;
    static void CreateSqlParameterOutput()
    {
        SqlParameter parameter = new SqlParameter("Description", SqlDbType.VarChar, 88);
        parameter.Direction = ParameterDirection.Output;
    }
    // </Snippet1>

    // <Snippet2>
    // using Microsoft.Data.SqlClient;
    static void CreateSqlParameterNullable()
    {
        SqlParameter parameter = new SqlParameter("Description", SqlDbType.VarChar, 88);
        parameter.IsNullable = true;
        parameter.Direction = ParameterDirection.Output;
    }
    // </Snippet2>

    // <Snippet3>
    // using Microsoft.Data.SqlClient;
    static void CreateSqlParameterOffset()
    {
        SqlParameter parameter = new SqlParameter("pDName", SqlDbType.VarChar);
        parameter.IsNullable = true;
        parameter.Offset = 3;
    }
    // </Snippet3>

    // <Snippet4>
    // using Microsoft.Data.SqlClient;
    static void CreateSqlParameterPrecisionScale()
    {
        SqlParameter parameter = new SqlParameter("Price", SqlDbType.Decimal);
        parameter.Value = 3.1416;
        parameter.Precision = 8;
        parameter.Scale = 4;
    }
    // </Snippet4>

    // <Snippet5>
    // using Microsoft.Data.SqlClient;
    static void CreateSqlParameterSize()
    {
        string description = "12 foot scarf - multiple colors, one previous owner";
        SqlParameter parameter = new SqlParameter("Description", SqlDbType.VarChar);
        parameter.Direction = ParameterDirection.InputOutput;
        parameter.Size = description.Length;
        parameter.Value = description;
    }
    // </Snippet5>

    // <Snippet6>
    // using Microsoft.Data.SqlClient;
    static void CreateSqlParameterSourceColumn()
    {
        SqlParameter parameter = new SqlParameter("Description", SqlDbType.VarChar, 88);
        parameter.SourceColumn = "Description";
    }
    // </Snippet6>

    // <Snippet7>
    // using Microsoft.Data.SqlClient;
    static void CreateSqlParameterSourceVersion()
    {
        SqlParameter parameter = new SqlParameter("Description", SqlDbType.VarChar, 88);
        parameter.SourceColumn = "Description";
        parameter.SourceVersion = DataRowVersion.Current;
    }
    // </Snippet7>

    // <Snippet8>
    // using Microsoft.Data.SqlClient;
    static void CreateSqlParameterVersion()
    {
        SqlParameter parameter = new SqlParameter("Description", SqlDbType.VarChar, 88);
        parameter.Value = "garden hose";
    }
    // </Snippet8>
}
