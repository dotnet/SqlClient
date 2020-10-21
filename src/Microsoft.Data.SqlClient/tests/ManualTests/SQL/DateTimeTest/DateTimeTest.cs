// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class DateTimeTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void SQLBU503165Test()
        {
            SqlParameter p = new SqlParameter();
            p.SqlValue = new DateTime(1200, 1, 1);
            p.SqlDbType = SqlDbType.DateTime2;

            DateTime expectedValue = new DateTime(1200, 1, 1);
            Assert.True(p.Value.Equals(expectedValue), "FAILED: Value did not match expected DateTime value");
            Assert.True(p.SqlValue.Equals(expectedValue), "FAILED: SqlValue did not match expected DateTime value");
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void SQLBU527900Test()
        {
            object chs = new char[] { 'a', 'b', 'c' };

            SqlCommand command = new SqlCommand();
            SqlParameter parameter = command.Parameters.AddWithValue("@o", chs);

            Assert.True(parameter.Value is char[], "FAILED: Expected parameter value to be char[]");
            Assert.True(parameter.SqlValue is SqlChars, "FAILED: Expected parameter value to be SqlChars");
            Assert.True(parameter.SqlValue is SqlChars, "FAILED: Expected parameter value to be SqlChars");
            Assert.True(parameter.Value is char[], "FAILED: Expected parameter value to be char[]");
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void SQLBU503290Test()
        {
            using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                conn.Open();
                SqlParameter p = new SqlParameter("@p", SqlDbType.DateTimeOffset);
                p.Value = DBNull.Value;
                p.Size = 27;
                SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT @p";
                cmd.Parameters.Add(p);

                Assert.True(cmd.ExecuteScalar() is DBNull, "FAILED: ExecuteScalar did not return a result of type DBNull");
            }
        }

        // Synapse: CREATE or ALTER PROCEDURE statement uses syntax or features that are not supported in SQL Server PDW.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void ReaderParameterTest()
        {
            string tableName = "#t_" + Guid.NewGuid().ToString().Replace('-', '_');
            string procName = "#p_" + Guid.NewGuid().ToString().Replace('-', '_');
            string procNullName = "#pn_" + Guid.NewGuid().ToString().Replace('-', '_');

            string tempTableCreate = "CREATE TABLE " + tableName + " (ci int, c0 dateTime, c1 date, c2 time(7), c3 datetime2(3), c4 datetimeoffset)";
            string tempTableInsert1 = "INSERT INTO " + tableName + " VALUES (0, " +
                "'1753-01-01 12:00AM', " +
                "'1753-01-01', " +
                "'20:12:13.36', " +
                "'2000-12-31 23:59:59.997', " +
                "'9999-12-31 15:59:59.997 -08:00')";
            string tempTableInsert2 = "INSERT INTO " + tableName + " VALUES (@pi, @p0, @p1, @p2, @p3, @p4)";

            string createProc = "CREATE PROCEDURE " + procName + " @p0 datetime OUTPUT, @p1 date OUTPUT, @p2 time(7) OUTPUT, @p3 datetime2(3) OUTPUT, @p4 datetimeoffset OUTPUT";
            createProc += " AS ";
            createProc += " SET @p0 = '1753-01-01 12:00AM'";
            createProc += " SET @p1 = '1753-01-01'";
            createProc += " SET @p2 = '20:12:13.36'";
            createProc += " SET @p3 = '2000-12-31 23:59:59.997'";
            createProc += "SET @p4 = '9999-12-31 15:59:59.997 -08:00'";

            string createProcN = "CREATE PROCEDURE " + procNullName + " @p0 datetime OUTPUT, @p1 date OUTPUT, @p2 time(7) OUTPUT, @p3 datetime2(3) OUTPUT, @p4 datetimeoffset OUTPUT";
            createProcN += " AS ";
            createProcN += " SET @p0 = NULL";
            createProcN += " SET @p1 = NULL";
            createProcN += " SET @p2 = NULL";
            createProcN += " SET @p3 = NULL";
            createProcN += " SET @p4 = NULL";

            using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                try
                {
                    // ReaderParameterTest Setup
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = tempTableCreate;
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = tempTableInsert1;
                        cmd.ExecuteNonQuery();

                        #region parameter
                        // Parameter Tests
                        // Test 1
                        using (SqlCommand cmd2 = conn.CreateCommand())
                        {
                            cmd2.CommandText = tempTableInsert2;
                            SqlParameter pi = cmd2.Parameters.Add("@pi", SqlDbType.Int);
                            SqlParameter p0 = cmd2.Parameters.Add("@p0", SqlDbType.DateTime);
                            SqlParameter p1 = cmd2.Parameters.Add("@p1", SqlDbType.Date);
                            SqlParameter p2 = cmd2.Parameters.Add("@p2", SqlDbType.Time);
                            SqlParameter p3 = cmd2.Parameters.Add("@p3", SqlDbType.DateTime2);
                            SqlParameter p4 = cmd2.Parameters.Add("@p4", SqlDbType.DateTimeOffset);
                            pi.Value = DBNull.Value;
                            p0.Value = DBNull.Value;
                            p1.Value = DBNull.Value;
                            p2.Value = DBNull.Value;
                            p3.Value = DBNull.Value;
                            p4.Value = DBNull.Value;
                            p3.Scale = 7;

                            cmd2.ExecuteNonQuery();
                            pi.Value = 1;
                            p0.Value = new DateTime(2000, 12, 31);
                            p1.Value = new DateTime(2000, 12, 31);
                            p2.Value = new TimeSpan(23, 59, 59);
                            p3.Value = new DateTime(2000, 12, 31);
                            p4.Value = new DateTimeOffset(2000, 12, 31, 23, 59, 59, new TimeSpan(-8, 0, 0));
                            p3.Scale = 3;
                            cmd2.ExecuteNonQuery();

                            // Test 2
                            cmd2.CommandText = "SELECT COUNT(*) FROM " + tableName + " WHERE @pi = ci AND @p0 = c0 AND @p1 = c1 AND @p2 = c2 AND @p3 = c3 AND @p4 = c4";
                            pi.Value = 0;
                            p0.Value = new DateTime(1753, 1, 1, 0, 0, 0);
                            p1.Value = new DateTime(1753, 1, 1, 0, 0, 0);
                            p2.Value = new TimeSpan(0, 20, 12, 13, 360);
                            p3.Value = new DateTime(2000, 12, 31, 23, 59, 59, 997);
                            p4.Value = new DateTimeOffset(9999, 12, 31, 23, 59, 59, 997, TimeSpan.Zero);
                            p4.Scale = 3;
                            object scalarResult = cmd2.ExecuteScalar();
                            Assert.True(scalarResult.Equals(1), string.Format("FAILED: Execute scalar returned unexpected result. Expected: {0}. Actual: {1}.", 1, scalarResult));

                            cmd2.Parameters.Clear();
                            pi = cmd2.Parameters.Add("@pi", SqlDbType.Int);
                            p0 = cmd2.Parameters.Add("@p0", SqlDbType.DateTime);
                            p1 = cmd2.Parameters.Add("@p1", SqlDbType.Date);
                            p2 = cmd2.Parameters.Add("@p2", SqlDbType.Time);
                            p3 = cmd2.Parameters.Add("@p3", SqlDbType.DateTime2);
                            p4 = cmd2.Parameters.Add("@p4", SqlDbType.DateTimeOffset);
                            pi.SqlValue = new SqlInt32(0);
                            p0.SqlValue = new SqlDateTime(1753, 1, 1, 0, 0, 0);
                            p1.SqlValue = new DateTime(1753, 1, 1, 0, 0, 0);
                            p2.SqlValue = new TimeSpan(0, 20, 12, 13, 360);
                            p3.SqlValue = new DateTime(2000, 12, 31, 23, 59, 59, 997);
                            p4.SqlValue = new DateTimeOffset(9999, 12, 31, 23, 59, 59, 997, TimeSpan.Zero);
                            p2.Scale = 3;
                            p3.Scale = 3;
                            p4.Scale = 3;
                            scalarResult = cmd2.ExecuteScalar();
                            Assert.True(scalarResult.Equals(1), string.Format("FAILED: ExecutScalar returned unexpected result. Expected: {0}. Actual: {1}.", 1, scalarResult));

                            // Test 3

                            cmd.CommandText = createProc;
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = createProcN;
                            cmd.ExecuteNonQuery();
                            using (SqlCommand cmd3 = conn.CreateCommand())
                            {
                                cmd3.CommandType = CommandType.StoredProcedure;
                                cmd3.CommandText = procName;
                                p0 = cmd3.Parameters.Add("@p0", SqlDbType.DateTime);
                                p1 = cmd3.Parameters.Add("@p1", SqlDbType.Date);
                                p2 = cmd3.Parameters.Add("@p2", SqlDbType.Time);
                                p3 = cmd3.Parameters.Add("@p3", SqlDbType.DateTime2);
                                p4 = cmd3.Parameters.Add("@p4", SqlDbType.DateTimeOffset);
                                p0.Direction = ParameterDirection.Output;
                                p1.Direction = ParameterDirection.Output;
                                p2.Direction = ParameterDirection.Output;
                                p3.Direction = ParameterDirection.Output;
                                p4.Direction = ParameterDirection.Output;
                                p2.Scale = 7;
                                cmd3.ExecuteNonQuery();

                                Assert.True(p0.Value.Equals((new SqlDateTime(1753, 1, 1, 0, 0, 0)).Value), "FAILED: SqlParameter p0 contained incorrect value");
                                Assert.True(p1.Value.Equals(new DateTime(1753, 1, 1, 0, 0, 0)), "FAILED: SqlParameter p1 contained incorrect value");
                                Assert.True(p2.Value.Equals(new TimeSpan(0, 20, 12, 13, 360)), "FAILED: SqlParameter p2 contained incorrect value");
                                Assert.True(p2.Scale.Equals(7), "FAILED: SqlParameter p0 contained incorrect scale");
                                Assert.True(p3.Value.Equals(new DateTime(2000, 12, 31, 23, 59, 59, 997)), "FAILED: SqlParameter p3 contained incorrect value");
                                Assert.True(p3.Scale.Equals(7), "FAILED: SqlParameter p3 contained incorrect scale");
                                Assert.True(p4.Value.Equals(new DateTimeOffset(9999, 12, 31, 23, 59, 59, 997, TimeSpan.Zero)), "FAILED: SqlParameter p4 contained incorrect value");
                                Assert.True(p4.Scale.Equals(7), "FAILED: SqlParameter p4 contained incorrect scale");

                                // Test 4
                                cmd3.CommandText = procNullName;
                                cmd3.ExecuteNonQuery();
                            }
                            Assert.True(p0.Value.Equals(DBNull.Value), "FAILED: SqlParameter p0 expected to be NULL");
                            Assert.True(p1.Value.Equals(DBNull.Value), "FAILED: SqlParameter p1 expected to be NULL");
                            Assert.True(p2.Value.Equals(DBNull.Value), "FAILED: SqlParameter p2 expected to be NULL");
                            Assert.True(p3.Value.Equals(DBNull.Value), "FAILED: SqlParameter p3 expected to be NULL");
                            Assert.True(p4.Value.Equals(DBNull.Value), "FAILED: SqlParameter p4 expected to be NULL");

                            // Date
                            Assert.False(IsValidParam(SqlDbType.Date, "c1", new DateTimeOffset(1753, 1, 1, 0, 0, 0, TimeSpan.Zero), conn, tableName), "FAILED: Invalid param for Date SqlDbType");
                            Assert.False(IsValidParam(SqlDbType.Date, "c1", new SqlDateTime(1753, 1, 1, 0, 0, 0), conn, tableName), "FAILED: Invalid param for Date SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.Date, "c1", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), conn, tableName), "FAILED: Invalid param for Date SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.Date, "c1", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc), conn, tableName), "FAILED: Invalid param for Date SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.Date, "c1", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Local), conn, tableName), "FAILED: Invalid param for Date SqlDbType");
                            Assert.False(IsValidParam(SqlDbType.Date, "c1", new TimeSpan(), conn, tableName), "FAILED: Invalid param for Date SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.Date, "c1", "1753-1-1", conn, tableName), "FAILED: Invalid param for Date SqlDbType");

                            // Time
                            Assert.False(IsValidParam(SqlDbType.Time, "c2", new DateTimeOffset(1753, 1, 1, 0, 0, 0, TimeSpan.Zero), conn, tableName), "FAILED: Invalid param for Time SqlDbType");
                            Assert.False(IsValidParam(SqlDbType.Time, "c2", new SqlDateTime(1753, 1, 1, 0, 0, 0), conn, tableName), "FAILED: Invalid param for Time SqlDbType");
                            Assert.False(IsValidParam(SqlDbType.Time, "c2", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), conn, tableName), "FAILED: Invalid param for Time SqlDbType");
                            Assert.False(IsValidParam(SqlDbType.Time, "c2", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc), conn, tableName), "FAILED: Invalid param for Time SqlDbType");
                            Assert.False(IsValidParam(SqlDbType.Time, "c2", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Local), conn, tableName), "FAILED: Invalid param for Time SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.Time, "c2", TimeSpan.Parse("20:12:13.36"), conn, tableName), "FAILED: Invalid param for Time SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.Time, "c2", "20:12:13.36", conn, tableName), "FAILED: Invalid param for Time SqlDbType");

                            // DateTime2
                            DateTime dt = DateTime.Parse("2000-12-31 23:59:59.997");
                            Assert.False(IsValidParam(SqlDbType.DateTime2, "c3", new DateTimeOffset(1753, 1, 1, 0, 0, 0, TimeSpan.Zero), conn, tableName), "FAILED: Invalid param for DateTime2 SqlDbType");
                            Assert.False(IsValidParam(SqlDbType.DateTime2, "c3", new SqlDateTime(2000, 12, 31, 23, 59, 59, 997), conn, tableName), "FAILED: Invalid param for DateTime2 SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.DateTime2, "c3", new DateTime(2000, 12, 31, 23, 59, 59, 997, DateTimeKind.Unspecified), conn, tableName), "FAILED: Invalid param for DateTime2 SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.DateTime2, "c3", new DateTime(2000, 12, 31, 23, 59, 59, 997, DateTimeKind.Utc), conn, tableName), "FAILED: Invalid param for DateTime2 SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.DateTime2, "c3", new DateTime(2000, 12, 31, 23, 59, 59, 997, DateTimeKind.Local), conn, tableName), "FAILED: Invalid param for DateTime2 SqlDbType");
                            Assert.False(IsValidParam(SqlDbType.DateTime2, "c3", new TimeSpan(), conn, tableName), "FAILED: Invalid param for DateTime2 SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.DateTime2, "c3", "2000-12-31 23:59:59.997", conn, tableName), "FAILED: Invalid param for DateTime2 SqlDbType");

                            // DateTimeOffset
                            DateTimeOffset dto = DateTimeOffset.Parse("9999-12-31 23:59:59.997 +00:00");
                            Assert.True(IsValidParam(SqlDbType.DateTimeOffset, "c4", dto, conn, tableName), "FAILED: Invalid param for DateTimeOffset SqlDbType");
                            Assert.False(IsValidParam(SqlDbType.DateTimeOffset, "c4", new SqlDateTime(1753, 1, 1, 0, 0, 0), conn, tableName), "FAILED: Invalid param for DateTimeOffset SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.DateTimeOffset, "c4", new DateTime(9999, 12, 31, 15, 59, 59, 997, DateTimeKind.Unspecified), conn, tableName), "FAILED: Invalid param for DateTimeOffset SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.DateTimeOffset, "c4", dto.UtcDateTime, conn, tableName), "FAILED: Invalid param for DateTimeOffset SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.DateTimeOffset, "c4", dto.LocalDateTime, conn, tableName), "FAILED: Invalid param for DateTimeOffset SqlDbType");
                            Assert.False(IsValidParam(SqlDbType.DateTimeOffset, "c4", new TimeSpan(), conn, tableName), "FAILED: Invalid param for DateTimeOffset SqlDbType");
                            Assert.True(IsValidParam(SqlDbType.DateTimeOffset, "c4", "9999-12-31 23:59:59.997 +00:00", conn, tableName), "FAILED: Invalid param for DateTimeOffset SqlDbType");
                        }

                        // Do the same thing as above except using DbType now
                        using (DbCommand cmd3 = conn.CreateCommand())
                        {
                            cmd3.CommandText = tempTableInsert2;
                            DbParameter pi = cmd3.CreateParameter();
                            pi.DbType = DbType.Int32;
                            pi.ParameterName = "@pi";
                            DbParameter p0 = cmd3.CreateParameter();
                            p0.DbType = DbType.DateTime;
                            p0.ParameterName = "@p0";
                            DbParameter p1 = cmd3.CreateParameter();
                            p1.DbType = DbType.Date;
                            p1.ParameterName = "@p1";
                            DbParameter p2 = cmd3.CreateParameter();
                            p2.DbType = DbType.Time;
                            p2.ParameterName = "@p2";
                            DbParameter p3 = cmd3.CreateParameter();
                            p3.DbType = DbType.DateTime2;
                            p3.ParameterName = "@p3";
                            DbParameter p4 = cmd3.CreateParameter();
                            p4.DbType = DbType.DateTimeOffset;
                            p4.ParameterName = "@p4";
                            pi.Value = DBNull.Value;
                            p0.Value = DBNull.Value;
                            p1.Value = DBNull.Value;
                            p2.Value = DBNull.Value;
                            p3.Value = DBNull.Value;
                            p4.Value = DBNull.Value;
                            p3.Scale = 7;

                            cmd3.Parameters.Add(pi);
                            cmd3.Parameters.Add(p0);
                            cmd3.Parameters.Add(p1);
                            cmd3.Parameters.Add(p2);
                            cmd3.Parameters.Add(p3);
                            cmd3.Parameters.Add(p4);
                            cmd3.ExecuteNonQuery();
                            pi.Value = 1;
                            p0.Value = new DateTime(2000, 12, 31);
                            p1.Value = new DateTime(2000, 12, 31);
                            p2.Value = new TimeSpan(23, 59, 59);
                            p3.Value = new DateTime(2000, 12, 31);
                            p4.Value = new DateTimeOffset(2000, 12, 31, 23, 59, 59, new TimeSpan(-8, 0, 0));
                            p3.Scale = 3;

                            // This used to be broken for p2/TimeSpan before removing back-compat code for DbType.Date and DbType.Time parameters
                            cmd3.ExecuteNonQuery();

                            // Test 2
                            cmd3.CommandText = "SELECT COUNT(*) FROM " + tableName + " WHERE @pi = ci AND @p0 = c0 AND @p1 = c1 AND @p2 = c2 AND @p3 = c3 AND @p4 = c4";
                            pi.Value = 0;
                            p0.Value = new DateTime(1753, 1, 1, 0, 0, 0);
                            p1.Value = new DateTime(1753, 1, 1, 0, 0, 0);
                            p2.Value = new TimeSpan(0, 20, 12, 13, 360);
                            p3.Value = new DateTime(2000, 12, 31, 23, 59, 59, 997);
                            p4.Value = new DateTimeOffset(9999, 12, 31, 23, 59, 59, 997, TimeSpan.Zero);
                            p4.Scale = 3;
                            cmd3.Parameters.Clear();
                            cmd3.Parameters.Add(pi);
                            cmd3.Parameters.Add(p0);
                            cmd3.Parameters.Add(p1);
                            cmd3.Parameters.Add(p2);
                            cmd3.Parameters.Add(p3);
                            cmd3.Parameters.Add(p4);

                            // This used to be broken for p2/TimeSpan before removing back-compat code for DbType.Date and DbType.Time parameters
                            object scalarResult = cmd3.ExecuteScalar();
                            Assert.True(scalarResult.Equals(1), string.Format("FAILED: Execute scalar returned unexpected result. Expected: {0}. Actual: {1}.", 1, scalarResult));

                            // Test 3

                            using (SqlCommand cmd4 = conn.CreateCommand())
                            {
                                cmd4.CommandType = CommandType.StoredProcedure;
                                cmd4.CommandText = procName;
                                p0 = cmd3.CreateParameter();
                                p0.DbType = DbType.DateTime;
                                p0.ParameterName = "@p0";
                                cmd4.Parameters.Add(p0);
                                p1 = cmd3.CreateParameter();
                                p1.DbType = DbType.Date;
                                p1.ParameterName = "@p1";
                                cmd4.Parameters.Add(p1);
                                p2 = cmd3.CreateParameter();
                                p2.DbType = DbType.Time;
                                p2.ParameterName = "@p2";
                                cmd4.Parameters.Add(p2);
                                p3 = cmd3.CreateParameter();
                                p3.DbType = DbType.DateTime2;
                                p3.ParameterName = "@p3";
                                cmd4.Parameters.Add(p3);
                                p4 = cmd3.CreateParameter();
                                p4.DbType = DbType.DateTimeOffset;
                                p4.ParameterName = "@p4";
                                cmd4.Parameters.Add(p4);
                                p0.Direction = ParameterDirection.Output;
                                p1.Direction = ParameterDirection.Output;
                                p2.Direction = ParameterDirection.Output;
                                p3.Direction = ParameterDirection.Output;
                                p4.Direction = ParameterDirection.Output;
                                p2.Scale = 7;
                                cmd4.ExecuteNonQuery();

                                Assert.True(p0.Value.Equals((new SqlDateTime(1753, 1, 1, 0, 0, 0)).Value), "FAILED: SqlParameter p0 contained incorrect value");
                                Assert.True(p1.Value.Equals(new DateTime(1753, 1, 1, 0, 0, 0)), "FAILED: SqlParameter p1 contained incorrect value");
                                // This used to be broken for p2/TimeSpan before removing back-compat code for DbType.Date and DbType.Time parameters
                                Assert.True(p2.Value.Equals(new TimeSpan(0, 20, 12, 13, 360)), "FAILED: SqlParameter p2 contained incorrect value");
                                Assert.True(p2.Scale.Equals(7), "FAILED: SqlParameter p0 contained incorrect scale");
                                Assert.True(p3.Value.Equals(new DateTime(2000, 12, 31, 23, 59, 59, 997)), "FAILED: SqlParameter p3 contained incorrect value");
                                Assert.True(p3.Scale.Equals(7), "FAILED: SqlParameter p3 contained incorrect scale");
                                Assert.True(p4.Value.Equals(new DateTimeOffset(9999, 12, 31, 23, 59, 59, 997, TimeSpan.Zero)), "FAILED: SqlParameter p4 contained incorrect value");
                                Assert.True(p4.Scale.Equals(7), "FAILED: SqlParameter p4 contained incorrect scale");

                                // Test 4
                                cmd4.CommandText = procNullName;
                                cmd4.ExecuteNonQuery();
                            }
                            Assert.True(p0.Value.Equals(DBNull.Value), "FAILED: SqlParameter p0 expected to be NULL");
                            Assert.True(p1.Value.Equals(DBNull.Value), "FAILED: SqlParameter p1 expected to be NULL");
                            Assert.True(p2.Value.Equals(DBNull.Value), "FAILED: SqlParameter p2 expected to be NULL");
                            Assert.True(p3.Value.Equals(DBNull.Value), "FAILED: SqlParameter p3 expected to be NULL");
                            Assert.True(p4.Value.Equals(DBNull.Value), "FAILED: SqlParameter p4 expected to be NULL");

                            // Date
                            Assert.False(IsValidParam(DbType.Date, "c1", new DateTimeOffset(1753, 1, 1, 0, 0, 0, TimeSpan.Zero), conn, tableName), "FAILED: Invalid param for Date DbType");
                            Assert.False(IsValidParam(DbType.Date, "c1", new SqlDateTime(1753, 1, 1, 0, 0, 0), conn, tableName), "FAILED: Invalid param for Date DbType");
                            Assert.True(IsValidParam(DbType.Date, "c1", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), conn, tableName), "FAILED: Invalid param for Date DbType");
                            Assert.True(IsValidParam(DbType.Date, "c1", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc), conn, tableName), "FAILED: Invalid param for Date DbType");
                            Assert.True(IsValidParam(DbType.Date, "c1", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Local), conn, tableName), "FAILED: Invalid param for Date DbType");
                            Assert.False(IsValidParam(DbType.Date, "c1", new TimeSpan(), conn, tableName), "FAILED: Invalid param for Date DbType");
                            Assert.True(IsValidParam(DbType.Date, "c1", "1753-1-1", conn, tableName), "FAILED: Invalid param for Date DbType");

                            // Time
                            // These 7 Asserts used to be broken for before removing back-compat code for DbType.Date and DbType.Time parameters
                            Assert.False(IsValidParam(DbType.Time, "c2", new DateTimeOffset(1753, 1, 1, 0, 0, 0, TimeSpan.Zero), conn, tableName), "FAILED: Invalid param for Time DbType");
                            Assert.False(IsValidParam(DbType.Time, "c2", new SqlDateTime(1753, 1, 1, 0, 0, 0), conn, tableName), "FAILED: Invalid param for Time DbType");
                            Assert.False(IsValidParam(DbType.Time, "c2", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), conn, tableName), "FAILED: Invalid param for Time DbType");
                            Assert.False(IsValidParam(DbType.Time, "c2", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc), conn, tableName), "FAILED: Invalid param for Time DbType");
                            Assert.False(IsValidParam(DbType.Time, "c2", new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Local), conn, tableName), "FAILED: Invalid param for Time DbType");
                            Assert.True(IsValidParam(DbType.Time, "c2", TimeSpan.Parse("20:12:13.36"), conn, tableName), "FAILED: Invalid param for Time DbType");
                            Assert.True(IsValidParam(DbType.Time, "c2", "20:12:13.36", conn, tableName), "FAILED: Invalid param for Time DbType");

                            // DateTime2
                            DateTime dt = DateTime.Parse("2000-12-31 23:59:59.997");
                            Assert.False(IsValidParam(DbType.DateTime2, "c3", new DateTimeOffset(1753, 1, 1, 0, 0, 0, TimeSpan.Zero), conn, tableName), "FAILED: Invalid param for DateTime2 DbType");
                            Assert.False(IsValidParam(DbType.DateTime2, "c3", new SqlDateTime(2000, 12, 31, 23, 59, 59, 997), conn, tableName), "FAILED: Invalid param for DateTime2 DbType");
                            Assert.True(IsValidParam(DbType.DateTime2, "c3", new DateTime(2000, 12, 31, 23, 59, 59, 997, DateTimeKind.Unspecified), conn, tableName), "FAILED: Invalid param for DateTime2 DbType");
                            Assert.True(IsValidParam(DbType.DateTime2, "c3", new DateTime(2000, 12, 31, 23, 59, 59, 997, DateTimeKind.Utc), conn, tableName), "FAILED: Invalid param for DateTime2 DbType");
                            Assert.True(IsValidParam(DbType.DateTime2, "c3", new DateTime(2000, 12, 31, 23, 59, 59, 997, DateTimeKind.Local), conn, tableName), "FAILED: Invalid param for DateTime2 DbType");
                            Assert.False(IsValidParam(DbType.DateTime2, "c3", new TimeSpan(), conn, tableName), "FAILED: Invalid param for DateTime2 DbType");
                            Assert.True(IsValidParam(DbType.DateTime2, "c3", "2000-12-31 23:59:59.997", conn, tableName), "FAILED: Invalid param for DateTime2 DbType");

                            // DateTimeOffset
                            DateTimeOffset dto = DateTimeOffset.Parse("9999-12-31 23:59:59.997 +00:00");
                            Assert.True(IsValidParam(DbType.DateTimeOffset, "c4", dto, conn, tableName), "FAILED: Invalid param for DateTimeOffset DbType");
                            Assert.False(IsValidParam(DbType.DateTimeOffset, "c4", new SqlDateTime(1753, 1, 1, 0, 0, 0), conn, tableName), "FAILED: Invalid param for DateTimeOffset DbType");
                            Assert.True(IsValidParam(DbType.DateTimeOffset, "c4", new DateTime(9999, 12, 31, 15, 59, 59, 997, DateTimeKind.Unspecified), conn, tableName), "FAILED: Invalid param for DateTimeOffset DbType");
                            Assert.True(IsValidParam(DbType.DateTimeOffset, "c4", dto.UtcDateTime, conn, tableName), "FAILED: Invalid param for DateTimeOffset DbType");
                            Assert.True(IsValidParam(DbType.DateTimeOffset, "c4", dto.LocalDateTime, conn, tableName), "FAILED: Invalid param for DateTimeOffset DbType");
                            Assert.False(IsValidParam(DbType.DateTimeOffset, "c4", new TimeSpan(), conn, tableName), "FAILED: Invalid param for DateTimeOffset DbType");
                            Assert.True(IsValidParam(DbType.DateTimeOffset, "c4", "9999-12-31 23:59:59.997 +00:00", conn, tableName), "FAILED: Invalid param for DateTimeOffset DbType");
                        }
                        #endregion

                        #region reader
                        // Reader Tests
                        cmd.CommandText = "SELECT * FROM " + tableName;
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            object[] values = new object[rdr.FieldCount];
                            object[] sqlValues = new object[rdr.FieldCount];
                            object[] psValues = new object[rdr.FieldCount];

                            while (rdr.Read())
                            {
                                rdr.GetValues(values);
                                rdr.GetSqlValues(sqlValues);
                                rdr.GetProviderSpecificValues(psValues);

                                for (int i = 0; i < rdr.FieldCount; ++i)
                                {
                                    if (!rdr.IsDBNull(i))
                                    {
                                        bool parsingSucceeded = true;
                                        try
                                        {
                                            switch (i)
                                            {
                                                case 0:
                                                    rdr.GetInt32(i);
                                                    break;
                                                case 1:
                                                    rdr.GetDateTime(i);
                                                    break;
                                                case 2:
                                                    rdr.GetDateTime(i);
                                                    break;
                                                case 3:
                                                    rdr.GetTimeSpan(i);
                                                    break;
                                                case 4:
                                                    rdr.GetDateTime(i);
                                                    break;
                                                case 5:
                                                    rdr.GetDateTimeOffset(i);
                                                    break;
                                                default:
                                                    Console.WriteLine("Received unknown column number {0} during ReaderParameterTest.", i);
                                                    parsingSucceeded = false;
                                                    break;
                                            }
                                        }
                                        catch (InvalidCastException)
                                        {
                                            parsingSucceeded = false;
                                        }
                                        Assert.True(parsingSucceeded, "FAILED: SqlDataReader parsing failed for column: " + i);

                                        // Check if each value cast is equivalent to the others
                                        // Using ToString() helps get around different representations (mainly for values[] and GetValue())
                                        string[] valueStrList =
                                            {
                                        sqlValues[i].ToString(), rdr.GetSqlValue(i).ToString(), psValues[i].ToString(),
                                        rdr.GetProviderSpecificValue(i).ToString(), values[i].ToString(), rdr.GetValue(i).ToString()
                                    };
                                        string[] valueNameList = { "sqlValues[]", "GetSqlValue", "psValues[]", "GetProviderSpecificValue", "values[]", "GetValue" };

                                        for (int valNum = 0; valNum < valueStrList.Length; valNum++)
                                        {
                                            string currValueStr = valueStrList[valNum].ToString();
                                            for (int otherValNum = 0; otherValNum < valueStrList.Length; otherValNum++)
                                            {
                                                if (valNum == otherValNum)
                                                    continue;

                                                Assert.True(currValueStr.Equals(valueStrList[otherValNum]),
                                                    string.Format("FAILED: Value from {0} not equivalent to {1} result", valueNameList[valNum], valueNameList[otherValNum]));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        #endregion
                    }
                }
                finally
                {
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "DROP TABLE " + tableName;
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "DROP PROCEDURE " + procName;
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "DROP PROCEDURE " + procNullName;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void TypeVersionKnobTest()
        {
            string tableName = "#t_" + Guid.NewGuid().ToString().Replace('-', '_');
            string tableCreate = "CREATE TABLE " + tableName + " (ci int, c0 dateTime, c1 date, c2 time(7), c3 datetime2(3), c4 datetimeoffset)";
            string tableInsert1 = "INSERT INTO " + tableName + " VALUES (0, " +
                "'1753-01-01 12:00AM', " +
                "'1753-01-01', " +
                "'20:12:13.36', " +
                "'2000-12-31 23:59:59.997', " +
                "'9999-12-31 15:59:59.997 -08:00')";
            string tableInsert2 = "INSERT INTO " + tableName + " VALUES (NULL, NULL, NULL, NULL, NULL, NULL)";

            using (SqlConnection conn = new SqlConnection(new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                TypeSystemVersion = "SQL Server 2008"
            }.ConnectionString))
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = tableCreate;
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = tableInsert1;
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = tableInsert2;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "SELECT * FROM " + tableName;
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            for (int i = 2; i < rdr.FieldCount; ++i)
                            {
                                Assert.True(IsNotString(rdr, i), string.Format("FAILED: IsNotString failed for column: {0}", i));
                            }
                            for (int i = 2; i < rdr.FieldCount; ++i)
                            {
                                ValidateReader(rdr, i);
                            }
                        }
                    }
                }
            }

            using (SqlConnection conn = new SqlConnection(new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                TypeSystemVersion = "SQL Server 2005"
            }.ConnectionString))
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    try
                    {
                        cmd.CommandText = tableCreate;
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = tableInsert1;
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = tableInsert2;
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = "SELECT * FROM " + tableName;
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                for (int i = 2; i < rdr.FieldCount; ++i)
                                {
                                    Assert.True(IsString(rdr, i), string.Format("FAILED: IsString failed for column: {0}", i));
                                }
                                for (int i = 2; i < rdr.FieldCount; ++i)
                                {
                                    ValidateReader(rdr, i);
                                }
                            }
                        }
                    }
                    finally
                    {
                        cmd.CommandText = "DROP TABLE " + tableName;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }


        private static bool IsValidParam(SqlDbType dbType, string col, object value, SqlConnection conn, string tempTable)
        {
            try
            {
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM " + tempTable + " WHERE " + col + " = @p", conn);
                cmd.Parameters.Add("@p", dbType).Value = value;
                cmd.ExecuteScalar();
            }
            catch (InvalidCastException)
            {
                return false;
            }
            return true;
        }

        private static bool IsValidParam(DbType dbType, string col, object value, SqlConnection conn, string tempTable)
        {
            try
            {
                DbCommand cmd = new SqlCommand("SELECT COUNT(*) FROM " + tempTable + " WHERE " + col + " = @p", conn);
                DbParameter p = cmd.CreateParameter();
                p.ParameterName = "@p";
                p.DbType = dbType;
                p.Value = value;
                cmd.Parameters.Add(p);
                cmd.ExecuteScalar();
            }
            catch (InvalidCastException)
            {
                return false;
            }
            return true;
        }

        private static bool IsString(SqlDataReader rdr, int column)
        {
            if (!rdr.IsDBNull(column))
            {
                try
                {
                    rdr.GetString(column);
                }
                catch (InvalidCastException)
                {
                    return false;
                }
            }

            try
            {
                rdr.GetSqlString(column);
            }
            catch (InvalidCastException)
            {
                return false;
            }

            try
            {
                rdr.GetSqlChars(column);
            }
            catch (InvalidCastException)
            {
                return false;
            }

            object o = rdr.GetValue(column);
            if (o != DBNull.Value && !(o is string))
            {
                return false;
            }

            o = rdr.GetSqlValue(column);
            if (!(o is SqlString))
            {
                return false;
            }

            return true;
        }

        private static bool IsNotString(SqlDataReader rdr, int column)
        {
            if (!rdr.IsDBNull(column))
            {
                try
                {
                    rdr.GetString(column);
                    return false;
                }
                catch (InvalidCastException)
                {
                }
            }

            try
            {
                rdr.GetSqlString(column);
                return false;
            }
            catch (InvalidCastException)
            {
            }

            try
            {
                rdr.GetSqlChars(column);
                return false;
            }
            catch (InvalidCastException)
            {
            }

            object o = rdr.GetValue(column);
            if (o is string)
            {
                return false;
            }

            o = rdr.GetSqlValue(column);
            if (o is SqlString)
            {
                return false;
            }

            return true;
        }

        private static void ValidateReader(SqlDataReader rdr, int column)
        {
            bool validateSucceeded = false;
            Action[] nonDbNullActions =
            {
                () => rdr.GetDateTime(column),
                () => rdr.GetTimeSpan(column),
                () => rdr.GetDateTimeOffset(column),
                () => rdr.GetString(column)
            };
            Action[] genericActions =
            {
                () => rdr.GetSqlString(column),
                () => rdr.GetSqlChars(column),
                () => rdr.GetSqlDateTime(column)
            };

            Action<Action[]> validateParsingActions =
                (testActions) =>
                {
                    foreach (Action testAction in testActions)
                    {
                        try
                        {
                            testAction();
                            validateSucceeded = true;
                        }
                        catch (InvalidCastException)
                        {
                        }
                    }
                };

            if (!rdr.IsDBNull(column))
            {
                validateParsingActions(nonDbNullActions);
            }
            validateParsingActions(genericActions);

            // Server 2008 & 2005 seem to represent DBNull slightly differently. Might be related to a Timestamp IsDBNull bug
            // in SqlDataReader, which requires different server versions to handle NULLs differently.
            // Empty string is expected for DBNull SqlValue (as per API), but SqlServer 2005 returns "Null" for it.
            // See GetSqlValue code path in SqlDataReader for more details
            if (!validateSucceeded && rdr.IsDBNull(column) && rdr.GetValue(column).ToString().Equals(""))
            {
                validateSucceeded = true;
            }

            Assert.True(validateSucceeded, string.Format("FAILED: SqlDataReader failed reader validation for column: {0}. Column literal value: {1}", column, rdr.GetSqlValue(column)));
        }
    }
}
