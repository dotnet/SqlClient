// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>Verifies prepared handle ownership across execution failures and retries.</summary>
    [Trait("Set", "3")]
    public class PreparedCommandLifecycleTests
    {
        /// <summary>Execution errors retain handles through retries and replacement.</summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData("SELECT 1 / @p;")]
        [InlineData("SET @p = 1 / @p; SELECT @p;")]
        public void ExecuteScalar_PreparedRuntimeError_PreservesHandleForRetry(string batch)
        {
            // Arrange
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Pooling = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();
            using SqlCommand command = connection.CreateCommand();
            SqlParameter parameter = command.Parameters.Add("@p", SqlDbType.Int);
            FieldInfo handleField = typeof(SqlCommand).GetField("_prepareHandle",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo capacitiesField = typeof(SqlCommand).GetField(
                "_preparedParameterByteCapacities",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                // Each changed batch replaces the previous preparation on the same connection.
                command.CommandText = batch + $" -- attempt {attempt}";
                parameter.Value = 0;
                command.Prepare();

                // Act
                SqlException exception = Assert.Throws<SqlException>(
                    () => command.ExecuteScalar());

                // Assert
                Assert.Equal(8134, exception.Number);
                int handle = Assert.IsType<int>(handleField.GetValue(command));
                Assert.True(handle >= 0,
                    "Execution failure must retain the returned prepare handle.");
                object capacities = capacitiesField.GetValue(command);
                Assert.NotNull(capacities);

                // Act: correct the value and retry without changing the batch.
                parameter.Value = 1;
                object result = command.ExecuteScalar();

                // Assert: reuse both the handle and its declaration capacity.
                Assert.Equal(1, result);
                Assert.Equal(handle, handleField.GetValue(command));
                Assert.Same(capacities, capacitiesField.GetValue(command));
            }
        }

        /// <summary>Execution errors retain handles through retries and replacement.</summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData("SELECT 1 / @p;")]
        [InlineData("SET @p = 1 / @p; SELECT @p;")]
        public async Task ExecuteScalarAsync_PreparedRuntimeError_PreservesHandleForRetry(
            string batch)
        {
            // Arrange
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Pooling = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            await connection.OpenAsync();
            using SqlCommand command = connection.CreateCommand();
            SqlParameter parameter = command.Parameters.Add("@p", SqlDbType.Int);
            FieldInfo handleField = typeof(SqlCommand).GetField("_prepareHandle",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo capacitiesField = typeof(SqlCommand).GetField(
                "_preparedParameterByteCapacities",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                // Each changed batch replaces the previous preparation on the same connection.
                command.CommandText = batch + $" -- attempt {attempt}";
                parameter.Value = 0;
                command.Prepare();

                // Act
                SqlException exception = await Assert.ThrowsAsync<SqlException>(
                    () => command.ExecuteScalarAsync());

                // Assert
                Assert.Equal(8134, exception.Number);
                int handle = Assert.IsType<int>(handleField.GetValue(command));
                Assert.True(handle >= 0,
                    "Execution failure must retain the returned prepare handle.");
                object capacities = capacitiesField.GetValue(command);
                Assert.NotNull(capacities);

                // Act: correct the value and retry without changing the batch.
                parameter.Value = 1;
                object result = await command.ExecuteScalarAsync();

                // Assert: reuse both the handle and its declaration capacity.
                Assert.Equal(1, result);
                Assert.Equal(handle, handleField.GetValue(command));
                Assert.Same(capacities, capacitiesField.GetValue(command));
            }
        }

        /// <summary>Execution errors retain handles through retries and replacement.</summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData("SELECT 1 / @p;")]
        [InlineData("SET @p = 1 / @p; SELECT @p;")]
        public void ExecuteNonQuery_PreparedRuntimeError_PreservesHandleForRetry(string batch)
        {
            // Arrange
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Pooling = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();
            using SqlCommand command = connection.CreateCommand();
            SqlParameter parameter = command.Parameters.Add("@p", SqlDbType.Int);
            FieldInfo handleField = typeof(SqlCommand).GetField("_prepareHandle",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo capacitiesField = typeof(SqlCommand).GetField(
                "_preparedParameterByteCapacities",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                // Each changed batch replaces the previous preparation on the same connection.
                command.CommandText = batch + $" -- attempt {attempt}";
                parameter.Value = 0;
                command.Prepare();

                // Act
                SqlException exception = Assert.Throws<SqlException>(
                    () => command.ExecuteNonQuery());

                // Assert
                Assert.Equal(8134, exception.Number);
                int handle = Assert.IsType<int>(handleField.GetValue(command));
                Assert.True(handle >= 0,
                    "Execution failure must retain the returned prepare handle.");
                object capacities = capacitiesField.GetValue(command);
                Assert.NotNull(capacities);

                // Act: correct the value and retry without changing the batch.
                parameter.Value = 1;
                int result = command.ExecuteNonQuery();

                // Assert: reuse both the handle and its declaration capacity.
                Assert.Equal(-1, result);
                Assert.Equal(handle, handleField.GetValue(command));
                Assert.Same(capacities, capacitiesField.GetValue(command));
            }
        }

        /// <summary>Execution errors retain handles through retries and replacement.</summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData("SELECT 1 / @p;")]
        [InlineData("SET @p = 1 / @p; SELECT @p;")]
        public async Task ExecuteNonQueryAsync_PreparedRuntimeError_PreservesHandleForRetry(
            string batch)
        {
            // Arrange
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Pooling = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            await connection.OpenAsync();
            using SqlCommand command = connection.CreateCommand();
            SqlParameter parameter = command.Parameters.Add("@p", SqlDbType.Int);
            FieldInfo handleField = typeof(SqlCommand).GetField("_prepareHandle",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo capacitiesField = typeof(SqlCommand).GetField(
                "_preparedParameterByteCapacities",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                // Each changed batch replaces the previous preparation on the same connection.
                command.CommandText = batch + $" -- attempt {attempt}";
                parameter.Value = 0;
                command.Prepare();

                // Act
                SqlException exception = await Assert.ThrowsAsync<SqlException>(
                    () => command.ExecuteNonQueryAsync());

                // Assert
                Assert.Equal(8134, exception.Number);
                int handle = Assert.IsType<int>(handleField.GetValue(command));
                Assert.True(handle >= 0,
                    "Execution failure must retain the returned prepare handle.");
                object capacities = capacitiesField.GetValue(command);
                Assert.NotNull(capacities);

                // Act: correct the value and retry without changing the batch.
                parameter.Value = 1;
                int result = await command.ExecuteNonQueryAsync();

                // Assert: reuse both the handle and its declaration capacity.
                Assert.Equal(-1, result);
                Assert.Equal(handle, handleField.GetValue(command));
                Assert.Same(capacities, capacitiesField.GetValue(command));
            }
        }
    }
}
