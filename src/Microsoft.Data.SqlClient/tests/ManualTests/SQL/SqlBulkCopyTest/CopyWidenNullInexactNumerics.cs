// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class CopyWidenNullInexactNumerics
    {
        public static void Test(string sourceDatabaseConnectionString, string destinationDatabaseConnectionString)
        {
            string sourceTableName = DataTestUtility.GetUniqueNameForSqlServer("BCP_SRC");
            string destTableName = DataTestUtility.GetUniqueNameForSqlServer("BCP_DST");

            // this test copies float and real inexact numeric types into decimal targets using bulk copy to check that the widening of the type succeeds.
            using (var sourceConnection = new SqlConnection(sourceDatabaseConnectionString))
            using (var destinationConnection = new SqlConnection(destinationDatabaseConnectionString))
            {
                try
                {
                    sourceConnection.Open();
                    destinationConnection.Open();

                    RunCommands(sourceConnection,
                        new[]
                        {
                            $"drop table if exists {sourceTableName}",
                            $"create table {sourceTableName} (floatVal float null, realVal real null)",
                            $"insert {sourceTableName}(floatVal,realVal) values(1,1),(2,2),(null,null),(0.00000000000001,0.00000000000001)"
                        }
                    );

                    RunCommands(destinationConnection,
                        new[]
                        {
                            $"drop table if exists {destTableName}",
                            $"create table {destTableName} (floatVal decimal(18,10) null,realVal decimal(18,10) null)"
                        }
                    );

                    Exception error = null;
                    try
                    {
                        using (var bulkCopy = new SqlBulkCopy(destinationConnection, SqlBulkCopyOptions.Default, null)
                        {
                            DestinationTableName = destTableName
                        })
                        using (var sourceCommand = new SqlCommand($"select * from {sourceTableName}", sourceConnection, null))
                        using (var sourceReader = sourceCommand.ExecuteReader())
                        {
                            bulkCopy.WriteToServer(sourceReader);
                        }
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        RunCommands(sourceConnection,
                            new[]
                            {
                                $"drop table if exists {sourceTableName}",
                                $"drop table if exists {destTableName}",
                            }
                        );
                    }
                    Assert.Null(error);
                }
                catch
                {
                    throw;
                }
            }
        }

        public static void RunCommands(SqlConnection connection, IEnumerable<string> commands)
        {
            using (var sqlCommand = connection.CreateCommand())
            {
                foreach (var command in commands)
                {
                    Helpers.TryExecute(sqlCommand, command);
                }
            }
        }
    }
}
