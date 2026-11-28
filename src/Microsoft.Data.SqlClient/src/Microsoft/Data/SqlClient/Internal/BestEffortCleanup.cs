// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.Data.Common;

#nullable enable

namespace Microsoft.Data.SqlClient.Internal
{
    /// <summary>
    /// Skips cleanup for uncatchable operation failures. Single failures propagate unchanged;
    /// two catchable failures are retained in order inside a SqlException's AggregateException.
    /// </summary>
    internal static class BestEffortCleanup
    {
        internal static T Run<T>(Func<T> action, Action cleanup)
        {
            Exception? originalException = null;
            try
            {
                return action();
            }
            catch (Exception e)
            {
                originalException = e;
                throw;
            }
            finally
            {
                RunCleanup(cleanup, originalException);
            }
        }

        internal static async Task<T> RunAsync<T>(Func<Task<T>> action, Action cleanup)
        {
            Exception? originalException = null;
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                originalException = e;
                throw;
            }
            finally
            {
                RunCleanup(cleanup, originalException);
            }
        }

        // Also supports existing finally blocks and continuations that already hold the failure.
        internal static void RunCleanup(Action cleanup, Exception? originalException)
        {
            if (originalException != null && !ADP.IsCatchableExceptionType(originalException))
            {
                return;
            }

            try
            {
                cleanup();
            }
            catch (Exception cleanupException) when (
                originalException != null && ADP.IsCatchableExceptionType(cleanupException))
            {
                throw CreateException(originalException, cleanupException);
            }
        }

        private static SqlException CreateException(Exception originalException, Exception cleanupException)
        {
            var aggregate = new AggregateException(originalException, cleanupException);
            var errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, 0, 0, null,
                string.Concat(originalException.Message, Environment.NewLine, cleanupException.Message), string.Empty, 0));
            return SqlException.CreateException(errors, serverVersion: null, conId: Guid.Empty,
                innerException: aggregate);
        }
    }
}
