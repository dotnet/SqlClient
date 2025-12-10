using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class AsyncCancelledConnectionsTest
    {
        private readonly ITestOutputHelper _output;
        
        private const int NumberOfTasks = 100;  // How many attempts to poison the connection pool we will try

        private const int NumberOfNonPoisoned = 10;  // Number of normal requests for each attempt 

        public AsyncCancelledConnectionsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        // Disabled on Azure since this test fails on concurrent runs on same database.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CancelAsyncConnections(bool useMars)
        {
            // Arrange
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString);
            builder.MultipleActiveResultSets = useMars;

            // Act
            await RunCancelAsyncConnections(builder);
        }

        private async Task RunCancelAsyncConnections(SqlConnectionStringBuilder connectionStringBuilder)
        {
            SqlConnection.ClearAllPools();

            var tracker = new ConcurrentDictionary<int, bool>();

            _random = new Random(4); // chosen via fair dice roll.
            _watch = Stopwatch.StartNew();

            using (new Timer(TimerCallback, state: null, dueTime: TimeSpan.FromSeconds(5), period: TimeSpan.FromSeconds(5)))
            {
                Task[] tasks = new Task[NumberOfTasks];
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = DoManyAsync(i, tracker, connectionStringBuilder);
                }

                await Task.WhenAll(tasks);
            }

            DisplaySummary();
        }

        // Display one row every 5'ish seconds
        private void TimerCallback(object state)
        {
            lock (_lockObject)
            {
                DisplaySummary();
            }
        }

        private void DisplaySummary()
        {
            _output.WriteLine($"{_watch.Elapsed} {_continue} Started:{_start} Done:{_done} InFlight:{_inFlight} RowsRead:{_rowsRead} ResultRead:{_resultRead} PoisonedEnded:{_poisonedEnded} nonPoisonedExceptions:{_nonPoisonedExceptions} PoisonedCleanupExceptions:{_poisonCleanUpExceptions} Found:{_found}");
        }

        // This is the the main body that our Tasks run
        private async Task DoManyAsync(int index, ConcurrentDictionary<int,bool> tracker, SqlConnectionStringBuilder connectionStringBuilder)
        {
            Interlocked.Increment(ref _start);
            Interlocked.Increment(ref _inFlight);
            tracker[index] = true;

            using (SqlConnection marsConnection = new SqlConnection(connectionStringBuilder.ToString()))
            {
                if (connectionStringBuilder.MultipleActiveResultSets)
                {
                    await marsConnection.OpenAsync();
                }

                // First poison
                await DoOneAsync(marsConnection, connectionStringBuilder.ToString(), poison: true, index);

                for (int i = 0; i < NumberOfNonPoisoned && _continue; i++)
                {
                    // now run some without poisoning
                    await DoOneAsync(marsConnection, connectionStringBuilder.ToString(),false,index);
                }
            }
            tracker.TryRemove(index, out var _);
            Interlocked.Decrement(ref _inFlight);
            Interlocked.Increment(ref _done);
        }

        // This will do our work, open a connection, and run a query (that returns 4 results sets)
        // if we are poisoning we will 
        //   1 - Interject some sleeps in the sql statement so that it will run long enough that we can cancel it
        //   2 - Setup a time bomb task that will cancel the command a random amount of time later
        private async Task DoOneAsync(SqlConnection marsConnection, string connectionString, bool poison, int parent)
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < 4; i++)
                {
                    builder.AppendLine("SELECT name FROM sys.tables");
                    if (poison && i < 3)
                    {
                        builder.AppendLine("WAITFOR DELAY '00:00:01'");
                    }
                }

                using (var connection = new SqlConnection(connectionString))
                {
                    if (marsConnection != null && marsConnection.State == System.Data.ConnectionState.Open)
                    {
                        await RunCommand(marsConnection, builder.ToString(), poison, parent);
                    }
                    else
                    {
                        await connection.OpenAsync();
                        await RunCommand(connection, builder.ToString(), poison, parent);
                    }
                }
            }
            catch (Exception ex) when (poison && IsExpectedCancellation(ex))
            {
                // Expected cancellation from the time bomb when poisoning.
            }
            catch (Exception ex)
            {
                if (!poison)
                {
                    Interlocked.Increment(ref _nonPoisonedExceptions);
                }

                if (ex.Message.Contains("The MARS TDS header contained errors."))
                {
                    _continue = false;
                    lock (_lockObject)
                    {
                        _output.WriteLine($"{poison} {DateTime.UtcNow.ToString("O")}");
                        _output.WriteLine(ex.ToString());
                    }
                    Interlocked.Increment(ref _found);
                }

                throw;
            }
        }

        private static bool IsExpectedCancellation(Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                return true;
            }

            if (ex is SqlException sqlEx)
            {
                return sqlEx.Message.IndexOf("operation cancelled", StringComparison.OrdinalIgnoreCase) >= 0
                    || sqlEx.Message.IndexOf("operation canceled", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

        private async Task RunCommand(SqlConnection connection, string commandText, bool poison, int parent)
        {
            int rowsRead = 0;
            int resultRead = 0;

            try
            {
                using (var command = connection.CreateCommand())
                {
                    Task timeBombTask = default;
                    try
                    {
                        // Setup our time bomb
                        if (poison)
                        {
                            timeBombTask = TimeBombAsync(command);
                        }

                        command.CommandText = commandText;

                        // Attempt to read all of the data
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            try
                            {
                                do
                                {
                                    resultRead++;
                                    while (await reader.ReadAsync() && _continue)
                                    {
                                        rowsRead++;
                                    }
                                }
                                while (await reader.NextResultAsync() && _continue);
                            }
                            catch (SqlException) when (poison)
                            {
                                //  This looks a little strange, we failed to read above so this should fail too
                                //  But consider the case where this code is elsewhere (in the Dispose method of a class holding this logic)
                                try
                                {
                                    while (await reader.NextResultAsync())
                                    {
                                    }
                                }
                                catch
                                {
                                    Interlocked.Increment(ref _poisonCleanUpExceptions);
                                }

                                throw;
                            }
                        }
                    }
                    finally
                    {
                        // Make sure to clean up our time bomb
                        // It is unlikely, but the timebomb may get delayed in the Task Queue
                        // And we don't want it running after we dispose the command
                        if (timeBombTask != default)
                        {
                            await timeBombTask;
                        }
                    }
                }
            }
            finally
            {
                Interlocked.Add(ref _rowsRead, rowsRead);
                Interlocked.Add(ref _resultRead, resultRead);
                if (poison)
                {
                    Interlocked.Increment(ref _poisonedEnded);
                }
            }
        }

        private async Task TimeBombAsync(SqlCommand command)
        {
            await SleepAsync(100, 3000);
            command.Cancel();
        }

        private async Task SleepAsync(int minMs, int maxMs)
        {
            int delayMs;
            lock (_random)
            {
                delayMs = _random.Next(minMs, maxMs);
            }
            await Task.Delay(delayMs);
        }

        private Stopwatch _watch;

        private int _inFlight;
        private int _start;
        private int _done;
        private int _rowsRead;
        private int _resultRead;
        private int _nonPoisonedExceptions;
        private int _poisonedEnded;
        private int _poisonCleanUpExceptions;
        private bool _continue = true;
        private int _found;
        private Random _random;
        private object _lockObject = new object();
    }
}
