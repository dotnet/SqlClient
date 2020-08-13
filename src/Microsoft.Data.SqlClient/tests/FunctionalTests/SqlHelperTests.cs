using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlHelperTests
    {
        private readonly ITestOutputHelper output;
        public SqlHelperTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        AutoResetEvent UnobservedExceptionHappened = new AutoResetEvent(false);

        private void UnobservedExceptionHanlder(object sender, UnobservedTaskExceptionEventArgs eventArgs)
        {
            output.WriteLine("Exception unobserved");
            UnobservedExceptionHappened.Set();
        }

        private void HandleTimeout()
        {
            output.WriteLine("Timeout handled");
        }

        private void DoWork()
        {
            TaskScheduler.UnobservedTaskException += UnobservedExceptionHanlder;
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            AsyncHelper.WaitForCompletion(tcs.Task, 1, HandleTimeout); //Will time out as task uncompleted
            tcs.SetException(new TimeoutException()); //Our task now completes as timed out
        }

        [Fact]
        public void ReproduceUnobservedException()
        {
            DoWork(); //Do the main work in another function so the task has no reference remaining
            GC.Collect(); //Force collection of unobserved task
            GC.WaitForPendingFinalizers();

            bool unobservedHappend = UnobservedExceptionHappened.WaitOne(1);
            Assert.False(unobservedHappend, "Did not expect an exception to be unobserved");
        }
    }
}
