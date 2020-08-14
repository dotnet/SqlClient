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
            tcs.SetException(new TimeoutException()); //Our task now completes with an error
        }

        [Fact]
        public void WaitForCompletion_DoesNotCreateUnobservedException()
        {
            var unobservedExceptionHappenedEvent = new AutoResetEvent(false);
            EventHandler<UnobservedTaskExceptionEventArgs> handleUnobservedException = 
                (o, a) => { unobservedExceptionHappenedEvent.Set(); };

            TaskScheduler.UnobservedTaskException += handleUnobservedException;

            try
            {
                TimeOutATask(); //Create the task in another function so the task has no reference remaining
                GC.Collect(); //Force collection of unobserved task
                GC.WaitForPendingFinalizers();

                bool unobservedExceptionHappend = unobservedExceptionHappenedEvent.WaitOne(1);
                Assert.False(unobservedExceptionHappend, "Did not expect an unobserved exception");
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= handleUnobservedException;
            }
        }
    }
}
