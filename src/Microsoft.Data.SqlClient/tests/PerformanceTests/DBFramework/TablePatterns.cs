// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static Microsoft.Data.SqlClient.PerformanceTests.Constants;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public static class TablePatterns
    {
        /// <summary>
        /// Generates a simple table with 7 columns with variety of column types.
        /// Column Names in order: c_int, c_char, c_nvarchar, c_decimal, c_uid, c_xml
        /// </summary>
        /// <param name="config">DataTypes Configuration</param>
        /// <returns></returns>
        internal static Table Table7Columns(DataTypes dt, string tableNamePrefix)
            => Table.Build(tableNamePrefix)
                .AddColumn(new Column(dt.Numerics[n_int]))
                .AddColumn(new Column(dt.Characters[c_char]))
                .AddColumn(new Column(dt.MaxTypes[m_nvarchar]))
                .AddColumn(new Column(dt.Decimals[d_decimal]))
                .AddColumn(new Column(dt.Others[o_uniqueidentifier]))
                .AddColumn(new Column(dt.Others[o_xml]))
                .SetIndexColumn(0);

        /// <summary>
        /// Generates a table with all 25 columns types.
        /// Column Names in order:
        /// c_bit, c_int, c_tinyint, c_smallint, c_bigint, c_money, c_smallmoney, c_decimal, c_numeric, 
        /// c_float, c_real, c_date, c_datetime, c_datetime2, c_time, c_smalldatetime, c_datetimeoffset, 
        /// c_char, c_nchar, c_binary, c_varchar, c_nvarchar, c_varbinary, c_uid, c_xml
        /// </summary>
        /// <param name="config">DataTypes Configuration</param>
        /// <returns></returns>
        internal static Table TableAll25Columns(DataTypes dt, string tableNamePrefix)
            => Table.Build(tableNamePrefix)
                .AddColumn(new Column(dt.Numerics[n_bit]))
                .AddColumn(new Column(dt.Numerics[n_int]))
                .AddColumn(new Column(dt.Numerics[n_tinyint]))
                .AddColumn(new Column(dt.Numerics[n_smallint]))
                .AddColumn(new Column(dt.Numerics[n_bigint]))
                .AddColumn(new Column(dt.Numerics[n_money]))
                .AddColumn(new Column(dt.Numerics[n_smallmoney]))
                .AddColumn(new Column(dt.Decimals[d_decimal]))
                .AddColumn(new Column(dt.Decimals[d_numeric]))
                .AddColumn(new Column(dt.Decimals[d_float]))
                .AddColumn(new Column(dt.Decimals[d_real]))
                .AddColumn(new Column(dt.DateTimes[t_date]))
                .AddColumn(new Column(dt.DateTimes[t_datetime]))
                .AddColumn(new Column(dt.DateTimes[t_datetime2]))
                .AddColumn(new Column(dt.DateTimes[t_time]))
                .AddColumn(new Column(dt.DateTimes[t_smalldatetime]))
                .AddColumn(new Column(dt.DateTimes[t_datetimeoffset]))
                .AddColumn(new Column(dt.Characters[c_char]))
                .AddColumn(new Column(dt.Characters[c_nchar]))
                .AddColumn(new Column(dt.Binary[b_binary]))
                .AddColumn(new Column(dt.MaxTypes[m_varchar]))
                .AddColumn(new Column(dt.MaxTypes[m_nvarchar]))
                .AddColumn(new Column(dt.MaxTypes[m_varbinary]))
                .AddColumn(new Column(dt.Others[o_uniqueidentifier]))
                .AddColumn(new Column(dt.Others[o_xml]))
                .SetIndexColumn(1);

        /// <summary>
        /// Generates a table with column count in multiples of 25.
        /// Column Names in order:
        /// c_bit, c_int, c_tinyint, c_smallint, c_bigint, c_money, c_smallmoney, c_decimal, c_numeric, 
        /// c_float, c_real, c_date, c_datetime, c_datetime2, c_time, c_smalldatetime, c_datetimeoffset, 
        /// c_char, c_nchar, c_binary, c_varchar, c_nvarchar, c_varbinary, c_uid, c_xml and so on...
        /// </summary>
        /// <param name="config">DataTypes Configuration</param>
        /// <returns></returns>
        internal static Table TableX25Columns(int count, DataTypes dt, string tableNamePrefix)
        {
            if (count % 25 != 0)
                throw new System.ArgumentException($"Count {count} not a multiple of 25.");

            Table t = Table.Build(tableNamePrefix);
            int sets = count / 25, i = 0;
            while (i < sets)
            {
                t.AddColumn(new Column(dt.Numerics[n_bit], $"c{i}_"))
                .AddColumn(new Column(dt.Numerics[n_int], $"c{i}_"))
                .AddColumn(new Column(dt.Numerics[n_tinyint], $"c{i}_"))
                .AddColumn(new Column(dt.Numerics[n_smallint], $"c{i}_"))
                .AddColumn(new Column(dt.Numerics[n_bigint], $"c{i}_"))
                .AddColumn(new Column(dt.Numerics[n_money], $"c{i}_"))
                .AddColumn(new Column(dt.Numerics[n_smallmoney], $"c{i}_"))
                .AddColumn(new Column(dt.Decimals[d_decimal], $"c{i}_"))
                .AddColumn(new Column(dt.Decimals[d_numeric], $"c{i}_"))
                .AddColumn(new Column(dt.Decimals[d_float], $"c{i}_"))
                .AddColumn(new Column(dt.Decimals[d_real], $"c{i}_"))
                .AddColumn(new Column(dt.DateTimes[t_date], $"c{i}_"))
                .AddColumn(new Column(dt.DateTimes[t_datetime], $"c{i}_"))
                .AddColumn(new Column(dt.DateTimes[t_datetime2], $"c{i}_"))
                .AddColumn(new Column(dt.DateTimes[t_time], $"c{i}_"))
                .AddColumn(new Column(dt.DateTimes[t_smalldatetime], $"c{i}_"))
                .AddColumn(new Column(dt.DateTimes[t_datetimeoffset], $"c{i}_"))
                .AddColumn(new Column(dt.Characters[c_char], $"c{i}_"))
                .AddColumn(new Column(dt.Characters[c_nchar], $"c{i}_"))
                .AddColumn(new Column(dt.Binary[b_binary], $"c{i}_"))
                .AddColumn(new Column(dt.MaxTypes[m_varchar], $"c{i}_"))
                .AddColumn(new Column(dt.MaxTypes[m_nvarchar], $"c{i}_"))
                .AddColumn(new Column(dt.MaxTypes[m_varbinary], $"c{i}_"))
                .AddColumn(new Column(dt.Others[o_uniqueidentifier], $"c{i}_"))
                .AddColumn(new Column(dt.Others[o_xml], $"c{i}_"));
                i++;
            }
            return t.SetIndexColumn(1);

        }
    }
}
