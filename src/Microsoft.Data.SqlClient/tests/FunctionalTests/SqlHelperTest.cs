// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlHelperTest
    {
        private void TimeOutATask()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            AsyncHelper.WaitForCompletion(tcs.Task, 1); //Will time out as task uncompleted
            tcs.SetException(new TimeoutException("Dummy timeout exception")); //Our task now completes with an error
        }

        private Exception UnwrapException(Exception e)
        {
            return e?.InnerException != null ? UnwrapException(e.InnerException) : e;
        }

        [Fact]
        public void WaitForCompletion_DoesNotCreateUnobservedException()
        {
            var unobservedExceptionHappenedEvent = new AutoResetEvent(false);
            Exception unhandledException = null;
            void handleUnobservedException(object o, UnobservedTaskExceptionEventArgs a)
            { unhandledException = a.Exception; unobservedExceptionHappenedEvent.Set(); }

            TaskScheduler.UnobservedTaskException += handleUnobservedException;

            try
            {
                TimeOutATask(); //Create the task in another function so the task has no reference remaining
                GC.Collect(); //Force collection of unobserved task
                GC.WaitForPendingFinalizers();

                bool unobservedExceptionHappend = unobservedExceptionHappenedEvent.WaitOne(1);
                if (unobservedExceptionHappend) //Save doing string interpolation in the happy case
                {
                    var e = UnwrapException(unhandledException);
                    Assert.False(true, $"Did not expect an unobserved exception, but found a {e?.GetType()} with message \"{e?.Message}\"");
                }
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= handleUnobservedException;
            }
        }
    }
}
