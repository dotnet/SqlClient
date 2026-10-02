// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Covers preparation and optimized-binding call order against SQL Server.
    /// </summary>
    [Trait("Set", "3")]
    public class SqlCommandOptimizedBindingTests
    {
        /// <summary>
        /// Both execution paths explain the internal preparation handle regardless of the option's call order.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task Prepare_OptimizedBinding_ReportsIncompatibleOption(bool async, bool enableBeforePrepare)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using SqlCommand command = new("SELECT @value", connection);
            command.Parameters.Add("@value", SqlDbType.Int).Value = 42;
            command.EnableOptimizedParameterBinding = enableBeforePrepare;
            command.Prepare();
            command.EnableOptimizedParameterBinding = true;

            InvalidOperationException exception = async
                ? await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteScalarAsync())
                : Assert.Throws<InvalidOperationException>(() => command.ExecuteScalar());
            Assert.Contains("Prepare", exception.Message);
            Assert.Contains("EnableOptimizedParameterBinding", exception.Message);
        }

        /// <summary>
        /// A new command configured with either supported alternative executes after a preparation failure.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task Prepare_OptimizedBindingFailure_NewCommandSucceeds(bool async, bool prepare)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using (SqlCommand command = new("SELECT @value", connection))
            {
                command.Parameters.Add("@value", SqlDbType.Int).Value = 42;
                command.EnableOptimizedParameterBinding = true;
                command.Prepare();

                if (async)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteScalarAsync());
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => command.ExecuteScalar());
                }
            }

            using SqlCommand replacement = new("SELECT @value", connection);
            replacement.Parameters.Add("@value", SqlDbType.Int).Value = 42;
            replacement.EnableOptimizedParameterBinding = !prepare;
            if (prepare)
            {
                replacement.Prepare();
            }

            Assert.Equal(42, async ? await replacement.ExecuteScalarAsync() : replacement.ExecuteScalar());
        }

        /// <summary>
        /// Disabling optimized binding after Prepare but before the first execution allows deferred preparation.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Prepare_DisabledBeforeExecution_Succeeds(bool async)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using SqlCommand command = new("SELECT @value", connection);
            command.Parameters.Add("@value", SqlDbType.Int).Value = 42;
            command.EnableOptimizedParameterBinding = true;
            command.Prepare();
            command.EnableOptimizedParameterBinding = false;

            Assert.Equal(42, async ? await command.ExecuteScalarAsync() : command.ExecuteScalar());
        }

        /// <summary>
        /// An existing server handle remains usable; only a subsequent preparation needs the incompatible output handle.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AlreadyPrepared_EnablingOptimizedBinding_PreservesExecution(bool async)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using SqlCommand command = new("SELECT @value", connection);
            command.Parameters.Add("@value", SqlDbType.Int).Value = 42;
            command.Prepare();
            Assert.Equal(42, async ? await command.ExecuteScalarAsync() : command.ExecuteScalar());
            command.EnableOptimizedParameterBinding = true;
            Assert.Equal(42, async ? await command.ExecuteScalarAsync() : command.ExecuteScalar());

            command.CommandText = "SELECT @value + 1";
            InvalidOperationException exception = async
                ? await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteScalarAsync())
                : Assert.Throws<InvalidOperationException>(() => command.ExecuteScalar());
            Assert.Contains("Prepare", exception.Message);
        }

        /// <summary>
        /// Optimized input binding and no-op preparation continue to execute successfully.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public async Task OptimizedBinding_WithoutPreparation_Succeeds(bool async, bool parameterless)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using SqlCommand command = new(parameterless ? "SELECT 42" : "SELECT @value", connection);
            command.EnableOptimizedParameterBinding = true;
            if (parameterless)
            {
                command.Prepare();
            }
            else
            {
                command.Parameters.Add("@value", SqlDbType.Int).Value = 42;
            }

            Assert.Equal(42, async ? await command.ExecuteScalarAsync() : command.ExecuteScalar());
        }

        /// <summary>
        /// Stored procedures ignore optimized binding and retain output parameters even after no-op preparation.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false)]
        [InlineData(true)]
        public async Task StoredProcedure_Prepare_PreservesOutputParameters(bool async)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using SqlCommand command = new("sys.sp_executesql", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.EnableOptimizedParameterBinding = true;
            command.Parameters.Add("@stmt", SqlDbType.NVarChar, 100).Value = "SET @value = 42";
            command.Parameters.Add("@params", SqlDbType.NVarChar, 100).Value = "@value int OUTPUT";
            SqlParameter output = command.Parameters.Add("@value", SqlDbType.Int);
            output.Direction = ParameterDirection.Output;
            command.Prepare();

            if (async)
            {
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                command.ExecuteNonQuery();
            }

            Assert.Equal(42, output.Value);
        }
    }
}
