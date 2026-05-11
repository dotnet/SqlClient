namespace SqlConfigurableRetryLogic_SqlRetryLogicOptions;

using System;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

    class RetryLogicSample
    {
        static void Main(string[] args)
        {
        // <Snippet1>
            var RetryLogicOption = new SqlRetryLogicOption()
            {
                NumberOfTries = 5,
                // Declare the error number 102 as a transient error to apply the retry logic when it occurs.
                TransientErrors = new int[] { 102 },
                // When a SqlCommand executes out of a transaction, 
                // the retry logic will apply if it contains a 'select' keyword.
                AuthorizedSqlCondition = x => string.IsNullOrEmpty(x)
                        || Regex.IsMatch(x, @"\b(SELECT)\b", RegexOptions.IgnoreCase),
                DeltaTime = TimeSpan.FromSeconds(1),
                MaxTimeInterval = TimeSpan.FromSeconds(60),
                MinTimeInterval = TimeSpan.FromSeconds(3)
            };
        // </Snippet1>
        }
    }
