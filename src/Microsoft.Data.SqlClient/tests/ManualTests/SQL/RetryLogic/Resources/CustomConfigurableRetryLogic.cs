// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ClassLibrary
{
    public class CustomConfigurableRetryLogic
    {
        public static SqlRetryLogicBaseProvider GetDefaultRetry_static(SqlRetryLogicOption option = null)
        {
            return new CustomRetryLogicProvider(option?.NumberOfTries ?? 1);
        }

        public SqlRetryLogicBaseProvider GetDefaultRetry(SqlRetryLogicOption option = null)
        {
            return new CustomRetryLogicProvider(option?.NumberOfTries ?? 1);
        }
    }

    public class CustomConfigurableRetryLogicEx
    {
        public SqlRetryLogicBaseProvider GetDefaultRetry(SqlRetryLogicOption option = null)
        {
            SqlRetryLogicBaseProvider provider = new SqlCommand().RetryLogicProvider;
            Console.WriteLine(provider.RetryLogic.NumberOfTries);
            return new CustomRetryLogicProvider(option?.NumberOfTries ?? 1);
        }
    }

    public static class StaticCustomConfigurableRetryLogic
    {
        public static SqlRetryLogicBaseProvider GetDefaultRetry_static(SqlRetryLogicOption option = null)
        {
            return new CustomRetryLogicProvider(option?.NumberOfTries ?? 1);
        }
    }

    public struct StructCustomConfigurableRetryLogic
    {
        public static SqlRetryLogicBaseProvider GetDefaultRetry_static(SqlRetryLogicOption option = null)
        {
            return new CustomRetryLogicProvider(option?.NumberOfTries ?? 1);
        }

        public SqlRetryLogicBaseProvider GetDefaultRetry(SqlRetryLogicOption option = null)
        {
            return new CustomRetryLogicProvider(option?.NumberOfTries ?? 1);
        }
    }

    public class CustomRetryLogicProvider : SqlRetryLogicBaseProvider
    {
        private int _numberOfTries;

        public CustomRetryLogicProvider(int numberOfTries)
        {
            _numberOfTries = numberOfTries;
        }

        public override TResult Execute<TResult>(object sender, Func<TResult> function)
        {
            IList<Exception> exceptions = new List<Exception>();
            for (int i = 0; i < _numberOfTries; i++)
            {
                try
                {
                    return function.Invoke();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
            throw new AggregateException(exceptions);
        }

        public override async Task<TResult> ExecuteAsync<TResult>(object sender, Func<Task<TResult>> function, CancellationToken cancellationToken = default)
        {
            IList<Exception> exceptions = new List<Exception>();
            for (int i = 0; i < _numberOfTries; i++)
            {
                try
                {
                    return await function.Invoke();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
            throw new AggregateException(exceptions);
        }

        public override async Task ExecuteAsync(object sender, Func<Task> function, CancellationToken cancellationToken = default)
        {
            IList<Exception> exceptions = new List<Exception>();
            for (int i = 0; i < _numberOfTries; i++)
            {
                try
                {
                    await function.Invoke();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
            throw new AggregateException(exceptions);
        }
    }
}
