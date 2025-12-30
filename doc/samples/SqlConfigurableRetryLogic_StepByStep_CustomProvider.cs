using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlConfigurableRetryLogic_StepByStep_CustomProvider
{
    class Program
    {
        private const string CnnStringFormat = "Server=localhost; Initial Catalog=Northwind; Integrated Security=true; pooling=false; Timeout=1";

        static void Main(string[] args)
        {
            RetryConnection(CnnStringFormat);
        }

        private static void RetryConnection(string connectionString)
        {
            // <Snippet1>
            // Define the retry logic parameters
            var options = new SqlRetryLogicOption()
            {
                // Tries 5 times before throwing an exception
                NumberOfTries = 5,
                // Preferred gap time to delay before retry
                DeltaTime = TimeSpan.FromSeconds(1),
                // Maximum gap time for each delay time before retry
                MaxTimeInterval = TimeSpan.FromSeconds(20),
                // SqlException retriable error numbers
                TransientErrors = new int[] { 4060, 1024, 1025}
            };
            // </Snippet1>

            // <Snippet2>
            // Create a custom retry logic provider
            SqlRetryLogicBaseProvider provider = CustomRetry.CreateCustomProvider(options);
            // </Snippet2>

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // <Snippet3>
                // Assumes that connection is a valid SqlConnection object 
                // Set the retry logic provider on the connection instance
                connection.RetryLogicProvider = provider;
                // Establishing the connection will trigger retry if one of the given transient failure occurs.
                connection.Open();
                // </Snippet3>
            }
        }
    }

    public class CustomRetry
    {
        // <Snippet4>
        public static SqlRetryLogicBaseProvider CreateCustomProvider(SqlRetryLogicOption options)
        {
            // 1. create an enumerator instance
            CustomEnumerator customEnumerator = new CustomEnumerator(options.DeltaTime, options.MaxTimeInterval, options.MinTimeInterval);
            // 2. Use the enumerator object to create a new RetryLogic instance
            CustomRetryLogic customRetryLogic = new CustomRetryLogic(5, customEnumerator, (e) => TransientErrorsCondition(e, options.TransientErrors));
            // 3. Create a provider using the RetryLogic object
            CustomProvider customProvider = new CustomProvider(customRetryLogic);
            return customProvider;
        }
        // </Snippet4>

        // <Snippet5>
        // Return true if the exception is a transient fault.
        private static bool TransientErrorsCondition(Exception e, IEnumerable<int> retriableConditions)
        {
            bool result = false;

            // Assess only SqlExceptions
            if (retriableConditions != null && e is SqlException ex)
            {
                foreach (SqlError item in ex.Errors)
                {
                    // Check each error number to see if it is a retriable error number
                    if (retriableConditions.Contains(item.Number))
                    {
                        result = true;
                        break;
                    }
                }
            }
            // Other types of exceptions can also be assessed
            else if (e is TimeoutException)
            {
                result = true;
            }
            return result;
        }
        // </Snippet5>
    }

    // <Snippet6>
    public class CustomEnumerator : SqlRetryIntervalBaseEnumerator
    {
        // Set the maximum acceptable time to 4 minutes
        private readonly TimeSpan _maxValue = TimeSpan.FromMinutes(4);

        public CustomEnumerator(TimeSpan timeInterval, TimeSpan maxTime, TimeSpan minTime)
            : base(timeInterval, maxTime, minTime) {}

        // Return fixed time on each request
        protected override TimeSpan GetNextInterval()
        {
            return GapTimeInterval;
        }

        // Override the validate method with the new time range validation
        protected override void Validate(TimeSpan timeInterval, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
        {
            if (minTimeInterval < TimeSpan.Zero || minTimeInterval > _maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(minTimeInterval));
            }

            if (maxTimeInterval < TimeSpan.Zero || maxTimeInterval > _maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTimeInterval));
            }

            if (timeInterval < TimeSpan.Zero || timeInterval > _maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(timeInterval));
            }

            if (maxTimeInterval < minTimeInterval)
            {
                throw new ArgumentOutOfRangeException(nameof(minTimeInterval));
            }
        }
    }
    // </Snippet6>

    // <Snippet7>
    public class CustomRetryLogic : SqlRetryLogicBase
    {
        // Maximum number of attempts
        private const int maxAttempts = 20;

        public CustomRetryLogic(int numberOfTries,
                                 SqlRetryIntervalBaseEnumerator enumerator,
                                 Predicate<Exception> transientPredicate)
        {
            if (!(numberOfTries > 0 && numberOfTries <= maxAttempts))
            {
                // 'numberOfTries' should be between 1 and 20.
                throw new ArgumentOutOfRangeException(nameof(numberOfTries));
            }

            // Assign parameters to the relevant properties
            NumberOfTries = numberOfTries;
            RetryIntervalEnumerator = enumerator;
            TransientPredicate = transientPredicate;
            Current = 0;
        }

        // Prepare this object for the next round
        public override void Reset()
        {
            Current = 0;
            RetryIntervalEnumerator.Reset();
        }

        public override bool TryNextInterval(out TimeSpan intervalTime)
        {
            intervalTime = TimeSpan.Zero;
            // First try has occurred before starting the retry process. 
            // Check if retry is still allowed
            bool result = Current < NumberOfTries - 1;

            if (result)
            {
                // Increase the number of attempts
                Current++;
                // It's okay if the RetryIntervalEnumerator gets to the last value before we've reached our maximum number of attempts.
                // MoveNext() will simply leave the enumerator on the final interval value and we will repeat that for the final attempts.
                RetryIntervalEnumerator.MoveNext();
                // Receive the current time from enumerator
                intervalTime = RetryIntervalEnumerator.Current;
            }
            return result;
        }
    }
    // </Snippet7>

    // <Snippet8>
    public class CustomProvider : SqlRetryLogicBaseProvider
    {
        // Preserve the given retryLogic on creation
        public CustomProvider(SqlRetryLogicBase retryLogic)
        {
            RetryLogic = retryLogic;
        }

        public override TResult Execute<TResult>(object sender, Func<TResult> function)
        {
            // Create a list to save transient exceptions to report later if necessary
            IList<Exception> exceptions = new List<Exception>();
            // Prepare it before reusing
            RetryLogic.Reset();
            // Create an infinite loop to attempt the defined maximum number of tries
            do
            {
                try
                {
                    // Try to invoke the function
                    return function.Invoke();
                }
                // Catch any type of exception for further investigation
                catch (Exception e)
                {
                    // Ask the RetryLogic object if this exception is a transient error
                    if (RetryLogic.TransientPredicate(e))
                    {
                        // Add the exception to the list of exceptions we've retried on
                        exceptions.Add(e);
                        // Ask the RetryLogic for the next delay time before the next attempt to run the function
                        if (RetryLogic.TryNextInterval(out TimeSpan gapTime))
                        {
                            Console.WriteLine($"Wait for {gapTime} before next try");
                            // Wait before next attempt
                            Thread.Sleep(gapTime);
                        }
                        else
                        {
                            // Number of attempts has exceeded the maximum number of tries
                            throw new AggregateException("The number of retries has exceeded the maximum number of attempts.", exceptions);
                        }
                    }
                    else
                    {
                        // If the exception wasn't a transient failure throw the original exception
                        throw;
                    }
                }
            } while (true);
        }

        public override Task<TResult> ExecuteAsync<TResult>(object sender, Func<Task<TResult>> function, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task ExecuteAsync(object sender, Func<Task> function, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
    // </Snippet8>
}
