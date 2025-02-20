// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class BatchTests
    {

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MissingCommandTextThrows()
        {
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (var batch = new SqlBatch { Connection = connection, BatchCommands = { new SqlBatchCommand() } })
            {
                connection.Open();
                Assert.Throws<InvalidOperationException>(() => batch.ExecuteNonQuery());
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MissingConnectionThrows()
        {
            using (var batch = new SqlBatch { BatchCommands = { new SqlBatchCommand("SELECT @@SPID") } })
            {
                Assert.Throws<InvalidOperationException>(() => batch.ExecuteNonQuery());
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ConnectionCanCreateBatch()
        {
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                Assert.True(connection.CanCreateBatch);
                using (var batch = connection.CreateBatch())
                {
                    Assert.NotNull(batch);
                    Assert.Equal(connection, batch.Connection);

                    batch.BatchCommands.Add(new SqlBatchCommand("SELECT @@SPID"));
                    connection.Open();
                    batch.ExecuteNonQuery();
                }
            }
        }

#if NET8_0_OR_GREATER
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void SqlBatchCanCreateParameter()
        {
            using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();
            using DbBatch batch = connection.CreateBatch();
            SqlBatchCommand batchCommand = new SqlBatchCommand("SELECT @p");

            Assert.True(batchCommand.CanCreateParameter);
            SqlParameter parameter = (SqlParameter)batchCommand.CreateParameter();
            Assert.NotNull(parameter);
            parameter.ParameterName = "@p";
            parameter.Value = 1;
            batchCommand.Parameters.Add(parameter);
            batch.BatchCommands.Add(batchCommand);
            batch.ExecuteNonQuery();
        }
#endif 

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void StoredProcedureBatchSupported()
        {
            SqlRetryLogicOption rto = new() { NumberOfTries = 3, DeltaTime = TimeSpan.FromMilliseconds(100), TransientErrors = new[] { 1205 } }; // Retry on 1205 / Deadlock
            SqlRetryLogicBaseProvider prov = SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(rto);

            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (var batch = new SqlBatch { Connection = connection, BatchCommands = { new SqlBatchCommand("sp_help", CommandType.StoredProcedure, new List<SqlParameter> { new("@objname", "sys.indexes") }) } })
            {
                connection.RetryLogicProvider = prov;
                connection.Open();
                batch.ExecuteNonQuery();
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void CommandTextBatchSupported()
        {
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (var batch = new SqlBatch { Connection = connection, BatchCommands = { new SqlBatchCommand("select @@SPID", CommandType.Text) } })
            {
                connection.Open();
                batch.ExecuteNonQuery();
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void TableDirectBatchNotSupported()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlBatchCommand("Categories", CommandType.TableDirect));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void MixedBatchSupported()
        {
            SqlRetryLogicOption rto = new() { NumberOfTries = 3, DeltaTime = TimeSpan.FromMilliseconds(100), TransientErrors = new[] { 1205 } }; // Retry on 1205 / Deadlock
            SqlRetryLogicBaseProvider prov = SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(rto);

            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (var batch = new SqlBatch
                   {
                       Connection = connection,
                       BatchCommands =
                       {
                           new SqlBatchCommand("select @@SPID", CommandType.Text),
                           new SqlBatchCommand("sp_help", CommandType.StoredProcedure, new List<SqlParameter> { new("@objname", "sys.indexes") })
                       }
                   })
            {
                connection.RetryLogicProvider = prov;
                connection.Open();
                batch.ExecuteNonQuery();
                return;
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void DisposedThrows()
        {
            var batch = new SqlBatch { BatchCommands = { new SqlBatchCommand("SELECT @@SPID") } };
            batch.Dispose();
            Assert.Throws<ObjectDisposedException>(() => batch.ExecuteNonQuery());
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ProviderApi()
        {
            decimal foundFreight = 0.0m;
            int resultCount = 0;
            int rowCount = 0;
            var dbProviderFactory = SqlClientFactory.Instance;
            DbBatch batch = dbProviderFactory.CreateBatch();
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    {
                        DbParameter p1 = dbProviderFactory.CreateParameter();
                        DbParameter p2 = dbProviderFactory.CreateParameter();
                        p1.ParameterName = "@p1";
                        p2.ParameterName = "@p2";
                        p1.Value = 50.0f;
                        p2.Value = 10248;
                        DbBatchCommand command = dbProviderFactory.CreateBatchCommand();
                        command.CommandText = "UPDATE Orders SET Freight=@p1 WHERE OrderID=@p2";
                        command.Parameters.Add(p1);
                        command.Parameters.Add(p2);
                        batch.BatchCommands.Add(command);
                    }

                    {
                        DbParameter parameter = dbProviderFactory.CreateParameter();
                        parameter.ParameterName = "@p4";
                        parameter.Value = 10248;
                        DbBatchCommand command = dbProviderFactory.CreateBatchCommand();
                        command.CommandText = $"SELECT Freight FROM Orders WHERE OrderID={parameter.ParameterName}";
                        command.Parameters.Add(parameter);
                        batch.BatchCommands.Add(command);
                    }

                    batch.Connection = connection;
                    batch.Transaction = transaction;

                    try
                    {
                        using (var reader = batch.ExecuteReader())
                        {
                            do
                            {
                                resultCount += 1;
                                while (reader.Read())
                                {
                                    foundFreight = reader.GetDecimal(0);
                                    rowCount += 1;
                                }
                            }
                            while (reader.NextResult());
                        }
                    }
                    finally
                    {
                        transaction.Rollback();
                    }
                }
            }

            Assert.Equal(1, resultCount);
            Assert.Equal(1, rowCount);
            Assert.Equal(50.0m, foundFreight);

            Assert.Equal(1, batch.BatchCommands[0].RecordsAffected);
            Assert.Equal(0, batch.BatchCommands[1].RecordsAffected);

        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void DirectApi()
        {
            decimal foundFreight = 0.0m;
            int resultCount = 0;
            int rowCount = 0;

            SqlException exception = null;
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var batch = new SqlBatch
                    {
                        Connection = connection,
                        Transaction = transaction,
                        BatchCommands =
                        {
                            new SqlBatchCommand("UPDATE table SET f1=@p1 WHERE f2=@p2")
                            {
                                Parameters =
                                {
                                    new SqlParameter("p1", 8),
                                    new SqlParameter("p2", 9),
                                }
                            },
                            new SqlBatchCommand("SELECT * FROM [does not exist] WHERE f2=@p1")
                            {
                                Parameters =
                                {
                                    new SqlParameter("p1", 8),
                                }
                            }
                        }
                    };

                    using (batch)
                    {
                        try
                        {
                            using (var reader = batch.ExecuteReader())
                            {
                                do
                                {
                                    resultCount += 1;
                                    while (reader.Read())
                                    {
                                        foundFreight = reader.GetDecimal(0);
                                        rowCount += 1;
                                    }
                                }
                                while (reader.NextResult());
                            }
                        }
                        catch (SqlException sqlex)
                        {
                            exception = sqlex;
                        }
                        finally
                        {
                            transaction.Rollback();
                        }
                    }
                }
            }

            Assert.NotNull(exception);
            Assert.NotNull(exception.BatchCommand);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExceptionInBatchContainsBatch()
        {
            SqlException exception = null;
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var batch = new SqlBatch
                    {
                        Connection = connection,
                        Transaction = transaction,
                        BatchCommands = { new SqlBatchCommand("RAISERROR ( 'an intentional error occured.', 15, 1)") }
                    }
                    )
                    {
                        try
                        {
                            batch.ExecuteNonQuery();
                        }
                        catch (SqlException sqlex)
                        {
                            exception = sqlex;
                        }
                        finally
                        {
                            transaction.Rollback();
                        }
                    }
                }
            }

            Assert.NotNull(exception);
            Assert.NotNull(exception.BatchCommand);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExceptionWithoutBatchContainsNoBatch()
        {
            SqlException exception = null;
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                using (var command = new SqlCommand("RAISERROR ( 'an intentional error occured.', 15, 1)", connection, transaction))
                {
                    try
                    {
                        command.ExecuteNonQuery();
                    }
                    catch (SqlException sqlex)
                    {
                        exception = sqlex;
                    }
                    finally
                    {
                        transaction.Rollback();
                    }
                }
            }

            Assert.NotNull(exception);
            Assert.Null(exception.BatchCommand);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ParameterInOutAndReturn()
        {
            string create =
                @"
CREATE PROCEDURE TestInAndOutParams 
	@Input int, 
	@InOut int OUTPUT,
	@Output int = default OUTPUT
AS
BEGIN
	SET NOCOUNT ON;
	SELECT @InOut = 2 * @InOut, @Output = 2 * @Input
	RETURN @Input
END";
            string drop = "DROP PROCEDURE TestInAndOutParams";

            SqlParameter input = CreateParameter("@Input", SqlDbType.Int, 2);
            SqlParameter inputOutput = CreateParameter("@InOut", SqlDbType.Int, 4, ParameterDirection.InputOutput);
            SqlParameter output = CreateParameter("@Output", SqlDbType.Int, DBNull.Value, ParameterDirection.Output);
            SqlParameter returned = CreateParameter("@RETURN_VALUE", SqlDbType.Int, DBNull.Value, ParameterDirection.ReturnValue);
            try
            {
                TryExecuteNonQueryCommand(drop);
                ExecuteNonQueryCommand(create);

                using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
                using (SqlBatch batch = new SqlBatch(conn))
                {
                    conn.Open();
                    batch.Commands.Add(new SqlBatchCommand("SELECT @@VERSION"));
                    batch.Commands.Add(
                        new SqlBatchCommand(
                            "TestInAndOutParams",
                            CommandType.StoredProcedure,
                            new[] { input, inputOutput, output, returned }
                        )
                    );
                    batch.Commands.Add(new SqlBatchCommand("SELECT @@SPID"));
                    batch.ExecuteNonQuery();
                }
            }
            finally
            {
                TryExecuteNonQueryCommand(drop);
            }

            Assert.Equal(8, Convert.ToInt32(inputOutput.Value));
            Assert.Equal(4, Convert.ToInt32(output.Value));
            Assert.Equal(2, Convert.ToInt32(returned.Value));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExecuteNonQuery()
        {
            int count = 0;

            var batch = new SqlBatch
            {
                BatchCommands =
                {
                    new SqlBatchCommand("UPDATE Orders SET Freight=@p1 WHERE OrderID=@p2")
                    {
                        Parameters =
                        {
                            new SqlParameter("@p1", 50.0f),
                            new SqlParameter("@p2", 10248),
                        }
                    },
                    new SqlBatchCommand($"UPDATE Orders SET Freight=@p1 WHERE OrderID=@p2")
                    {
                        Parameters =
                        {
                            new SqlParameter("@p1", 36.0f),
                            new SqlParameter("@p2", -10248),
                        }
                    },
                    new SqlBatchCommand($"UPDATE Orders SET Freight=@p1 WHERE OrderID=@p2")
                    {
                        Parameters =
                        {
                            new SqlParameter("@p1", 90.0f),
                            new SqlParameter("@p2", 10248),
                        }
                    }
                }
            };
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {

                    batch.Connection = connection;
                    batch.Transaction = transaction;

                    try
                    {
                        count = batch.ExecuteNonQuery();
                    }
                    finally
                    {
                        transaction.Rollback();
                    }
                }
            }
            Assert.Equal(3, batch.Commands.Count);
            Assert.Equal(2, count);
            Assert.Equal(1, batch.Commands[0].RecordsAffected);
            Assert.Equal(0, batch.Commands[1].RecordsAffected);
            Assert.Equal(1, batch.Commands[2].RecordsAffected);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task ExecuteNonQueryAsync()
        {
            int count = 0;

            var batch = new SqlBatch
            {
                BatchCommands =
                {
                    new SqlBatchCommand("UPDATE Orders SET Freight=@p1 WHERE OrderID=@p2")
                    {
                        Parameters =
                        {
                            new SqlParameter("@p1", 50.0f),
                            new SqlParameter("@p2", 10248),
                        }
                    },
                    new SqlBatchCommand($"UPDATE Orders SET Freight=@p1 WHERE OrderID=@p2")
                    {
                        Parameters =
                        {
                            new SqlParameter("@p1", 36.0f),
                            new SqlParameter("@p2", -10248),
                        }
                    },
                    new SqlBatchCommand($"UPDATE Orders SET Freight=@p1 WHERE OrderID=@p2")
                    {
                        Parameters =
                        {
                            new SqlParameter("@p1", 90.0f),
                            new SqlParameter("@p2", 10248),
                        }
                    }
                }
            };
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {

                    batch.Connection = connection;
                    batch.Transaction = (SqlTransaction)transaction;

                    try
                    {
                        count = await batch.ExecuteNonQueryAsync();
                    }
                    finally
                    {
                        transaction.Rollback();
                    }
                }
            }

            Assert.Equal(3, batch.Commands.Count);
            Assert.Equal(2, count);
            Assert.Equal(1, batch.Commands[0].RecordsAffected);
            Assert.Equal(0, batch.Commands[1].RecordsAffected);
            Assert.Equal(1, batch.Commands[2].RecordsAffected);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExecuteScalarMultiple()
        {
            int value = 0;

            using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlBatch batch = new SqlBatch(conn))
            {
                conn.Open();
                for (int index = 0; index < 10; index++)
                {
                    batch.Commands.Add(new SqlBatchCommand($"SELECT {index}"));
                }
                value = Convert.ToInt32(batch.ExecuteScalar());
            }

            Assert.Equal(9, value);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task ExecuteScalarAsyncMultiple()
        {
            int value = 0;

            using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlBatch batch = new SqlBatch(conn))
            {
                await conn.OpenAsync();
                for (int index = 0; index < 10; index++)
                {
                    batch.Commands.Add(new SqlBatchCommand($"SELECT {index}"));
                }
                value = Convert.ToInt32(await batch.ExecuteScalarAsync());
            }

            Assert.Equal(9, value);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExecuteReaderMultiple()
        {
            int resultSetCount = 0;
            int resultRowCount = 0;

            using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlBatch batch = new SqlBatch(conn))
            {
                conn.Open();
                for (int index = 0; index < 10; index++)
                {
                    batch.Commands.Add(new SqlBatchCommand($"SELECT {index}"));
                }
                using (var reader = batch.ExecuteReader())
                {
                    do
                    {
                        resultSetCount += 1;
                        while (reader.Read())
                        {
                            resultRowCount += 1;
                        }
                    } while (reader.NextResult());
                }
            }

            Assert.Equal(10, resultSetCount);
            Assert.Equal(10, resultRowCount);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task ExecuteReaderAsyncMultiple()
        {
            int resultSetCount = 0;
            int resultRowCount = 0;

            using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlBatch batch = new SqlBatch(conn))
            {
                await conn.OpenAsync();
                for (int index = 0; index < 10; index++)
                {
                    batch.Commands.Add(new SqlBatchCommand($"SELECT {index}"));
                }
                using (var reader = await batch.ExecuteReaderAsync())
                {
                    do
                    {
                        resultSetCount += 1;
                        while (await reader.ReadAsync())
                        {
                            resultRowCount += 1;
                        }
                    } while (await reader.NextResultAsync());
                }
            }

            Assert.Equal(10, resultSetCount);
            Assert.Equal(10, resultRowCount);
        }

        private static SqlParameter CreateParameter<T>(string name, SqlDbType type, T value, ParameterDirection direction = ParameterDirection.Input)
        {
            var parameter = new SqlParameter(name, type);
            parameter.Direction = direction;
            parameter.Value = value;
            return parameter;
        }

        private static void ExecuteNonQueryCommand(string command)
        {
            using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlCommand cmd = conn.CreateCommand())
            {
                conn.Open();
                cmd.CommandText = command;
                cmd.ExecuteNonQuery();
            }
        }
        private static bool TryExecuteNonQueryCommand(string command)
        {
            try
            {
                ExecuteNonQueryCommand(command);
                return true;
            }
            catch
            {
            }
            return false;
        }
    }
}
