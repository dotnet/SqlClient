// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Test helper for creating <see cref="SqlException"/> instances. Since <see cref="SqlException"/> has
    /// an internal constructor, instances must be created via the <see cref="SqlException.CreateException"/> factory method.
    /// </summary>
    internal static class SqlExceptionHelper
    {
        /// <summary>
        /// Creates a <see cref="SqlException"/> with the specified message using the internal factory method.
        /// </summary>
        /// <param name="message">The error message for the exception.</param>
        /// <returns>A new <see cref="SqlException"/> with the specified message.</returns>
        internal static SqlException CreateSqlException(string message)
        {
            var collection = new SqlErrorCollection();
            collection.Add(new SqlError(0, (byte)0, (byte)0, "TestServer", message, "", 0));
            return SqlException.CreateException(collection, "");
        }
    }
}
