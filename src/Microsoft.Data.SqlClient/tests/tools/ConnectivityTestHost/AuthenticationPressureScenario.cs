// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ConnectivityTestHost;

/// <summary>Combines synchronous pool-worker callers with cold/expired provider authentication, isolated from xUnit.</summary>
internal static class AuthenticationPressureScenario
{
    /// <summary>Runs the selected caller/cache/worker/pool dimensions; stdin controls start, token release and recovery.</summary>
    internal static void Run(int port, string scenario)
    {
        string[] mode = scenario.Split('-');
        if (mode.Length != 5 ||
            (mode[1] != "sync" && mode[1] != "dedicated" && mode[1] != "async") ||
            (mode[2] != "cold" && mode[2] != "expired" && mode[2] != "warm") ||
            (mode[3] != "capped" && mode[3] != "generous") ||
            (mode[4] != "v1" && mode[4] != "v2"))
        {
            throw new ArgumentException("Unsupported authentication-pressure scenario.");
        }
        int width = Math.Max(Environment.ProcessorCount, 4);
        var provider = new PressureProvider();
        if (!SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, provider))
        {
            throw new InvalidOperationException("Could not register the isolated managed-identity test provider.");
        }
        string connectionString = new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity,
            MaxPoolSize = width + 1,
            ConnectTimeout = 120,
            ConnectRetryCount = 0,
            Enlist = false
        }.ConnectionString;
        SqlConnection[] connections = Enumerable.Range(0, width)
            .Select(_ => new SqlConnection(connectionString)).ToArray();
        try
        {
            if (mode[2] != "cold")
            {
                foreach (SqlConnection connection in connections)
                {
                    connection.Open();
                }
                if (mode[2] == "expired")
                {
                    AgeTokens(connections);
                }
                foreach (SqlConnection connection in connections)
                {
                    connection.Close();
                }
            }
            provider.Measuring = true;
            int maximum = mode[3] == "capped" ? width : width * 4;
            ConfigureWorkers(width, maximum);
            Task.WhenAll(Enumerable.Range(0, width).Select(async _ => await Task.Yield())).GetAwaiter().GetResult();
            Console.WriteLine($"READY {width} {maximum} {mode[2]}");
            Require("START");
            using var start = new ManualResetEventSlim();
            using var admitted = new CountdownEvent(width);
            Task[] opens;
            if (mode[1] == "async")
            {
                opens = connections.Select(connection => connection.OpenAsync()).ToArray();
            }
            else
            {
                opens = connections.Select(connection => Task.Factory.StartNew(() =>
                {
                    admitted.Signal();
                    start.Wait();
                    connection.Open();
                }, CancellationToken.None, mode[1] == "sync" ? TaskCreationOptions.None : TaskCreationOptions.LongRunning,
                    TaskScheduler.Default)).ToArray();
                if (!admitted.Wait(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("Not every caller reached the explicit worker start barrier.");
                }
                start.Set();
            }
            Console.WriteLine($"CALLERS_STARTED {width}");
            Require("OBSERVE");
            if (mode[2] == "warm")
            {
                Task.WhenAll(opens).GetAwaiter().GetResult();
                Console.WriteLine($"WARM_CALLBACKS {provider.Calls}");
            }
            else
            {
                Console.WriteLine(provider.Entered.Wait(TimeSpan.FromSeconds(10))
                    ? "TOKEN_ENTERED" : "TOKEN_ENTRY_TIMEOUT");
            }
            ThreadPool.GetAvailableThreads(out int available, out _);
            Console.WriteLine($"AVAILABLE {available}");
            string release = Console.ReadLine();
            if (release == "RECOVER")
            {
                ConfigureWorkers(width, width * 4);
                Console.WriteLine("WORKERS_RELEASED");
            }
            else if (release != "RELEASE")
            {
                throw new InvalidOperationException("Parent did not release authentication.");
            }
            provider.Token.SetResult(NewToken());
            Task.WhenAll(opens).GetAwaiter().GetResult();
            Console.WriteLine($"COMPLETE {width} CALLBACKS {provider.Calls}");
        }
        finally
        {
            SqlConnection.ClearPool(connections[0]);
            foreach (SqlConnection connection in connections)
            {
                connection.Dispose();
            }
        }
    }

    /// <summary>Uses checked runtime limits, not assumptions about processor count or thread injection.</summary>
    private static void ConfigureWorkers(int width, int maximum)
    {
        ThreadPool.GetMinThreads(out int minimum, out int minIo);
        ThreadPool.GetMaxThreads(out _, out int maxIo);
        if (!ThreadPool.SetMinThreads(Math.Min(minimum, width), minIo) ||
            !ThreadPool.SetMaxThreads(maximum, maxIo) || !ThreadPool.SetMinThreads(width, minIo))
        {
            throw new InvalidOperationException("Runtime rejected the authentication worker limits.");
        }
        ThreadPool.GetMinThreads(out int actualMin, out _);
        ThreadPool.GetMaxThreads(out int actualMax, out _);
        if (actualMin != width || actualMax != maximum)
        {
            throw new InvalidOperationException("Runtime did not retain the requested authentication worker limits.");
        }
    }

    /// <summary>Ages physical and cached token metadata independently, without clearing the warmed pool or waiting for wall-clock expiry.</summary>
    private static void AgeTokens(SqlConnection[] connections)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        PropertyInfo innerProperty = typeof(SqlConnection).GetProperty("InnerConnection", flags)
            ?? throw new MissingMemberException("SqlConnection.InnerConnection");
        DateTimeOffset expired = DateTimeOffset.UtcNow.AddMinutes(-1);
        foreach (SqlConnection connection in connections)
        {
            object inner = innerProperty.GetValue(connection)
                ?? throw new InvalidOperationException("Priming did not produce a physical connection.");
            FieldInfo field = inner.GetType().GetField("_fedAuthToken", flags)
                ?? throw new MissingFieldException("Physical connection token field is unavailable.");
            ConstructorInfo constructor = field.FieldType.GetConstructor(flags, null, new[] { typeof(SqlAuthenticationToken) }, null)
                ?? throw new MissingMethodException("Physical token constructor is unavailable.");
            field.SetValue(inner, constructor.Invoke(new object[] { new SqlAuthenticationToken("test-expired", expired) }));
            PropertyInfo expiredProperty = inner.GetType().GetProperty("IsAccessTokenExpired", flags)
                ?? throw new MissingMemberException("Physical token expiry property is unavailable.");
            if (!Equals(true, expiredProperty.GetValue(inner)))
            {
                throw new InvalidOperationException("Physical token aging did not establish expiry.");
            }
            object pool = inner.GetType().GetProperty("Pool", flags)?.GetValue(inner)
                ?? throw new MissingMemberException("Physical connection pool is unavailable.");
            if (pool.GetType().GetProperty("AuthenticationContexts", flags)?.GetValue(pool) is not IDictionary cache ||
                cache.Count != 1)
            {
                throw new InvalidOperationException("Priming did not establish exactly one shared authentication context.");
            }
            IDictionaryEnumerator enumerator = cache.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new InvalidOperationException("Authentication context disappeared during preconditioning.");
            }
            DictionaryEntry entry = enumerator.Entry;
            object context = entry.Value ?? throw new InvalidOperationException("Authentication context is missing.");
            byte[] accessToken = context.GetType().GetProperty("AccessToken", flags)?.GetValue(context) as byte[]
                ?? throw new MissingMemberException("Authentication context token is unavailable.");
            ConstructorInfo contextConstructor = context.GetType().GetConstructor(flags, null,
                new[] { typeof(byte[]), typeof(DateTime) }, null)
                ?? throw new MissingMethodException("Authentication context constructor is unavailable.");
            cache[entry.Key] = contextConstructor.Invoke(new object[] { accessToken, expired.UtcDateTime });
        }
        Console.WriteLine($"EXPIRED_PHYSICAL_AND_CACHE {connections.Length}");
    }

    /// <summary>Rejects absent/out-of-order controller commands instead of continuing with a default scenario.</summary>
    private static void Require(string expected)
    {
        if (Console.ReadLine() != expected)
        {
            throw new InvalidOperationException($"Parent did not issue {expected}.");
        }
    }

    /// <summary>Returns only a fake token accepted by the loopback peer.</summary>
    private static SqlAuthenticationToken NewToken() => new("test-token", DateTimeOffset.UtcNow.AddHours(2));

    /// <summary>Priming is immediate; measured acquisition is genuinely incomplete and requires a worker continuation.</summary>
    private sealed class PressureProvider : SqlAuthenticationProvider
    {
        internal bool Measuring;
        internal int Calls;
        internal readonly ManualResetEventSlim Entered = new();
        internal readonly TaskCompletionSource<SqlAuthenticationToken> Token = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Only the explicitly selected managed-identity provider path is supported.</summary>
        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) =>
            authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;

        /// <summary>Signals measured entry separately from warm-pool preconditioning.</summary>
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            if (!Measuring)
            {
                return Task.FromResult(NewToken());
            }
            Interlocked.Increment(ref Calls);
            Entered.Set();
            return Token.Task;
        }
    }
}
