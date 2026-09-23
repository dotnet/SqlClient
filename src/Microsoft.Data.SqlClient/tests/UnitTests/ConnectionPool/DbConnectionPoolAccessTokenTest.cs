// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

using static Microsoft.Data.SqlClient.UnitTests.ConnectionPool.PoolTestHarness;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Verifies checkout-time token eviction separately from pool token-cache refresh.
    /// The collection isolates pool-version switches; simulated logins need no Azure credentials.
    /// </summary>
    [Collection(SimulatedServerTestCollection.Name)]
    public class DbConnectionPoolAccessTokenTest
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Both pools defer token validation until checkout, reusing valid connections and replacing expired ones.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle, false, false)]
        [InlineData(PoolImplementation.WaitHandle, false, true)]
        [InlineData(PoolImplementation.WaitHandle, true, false)]
        [InlineData(PoolImplementation.WaitHandle, true, true)]
        [InlineData(PoolImplementation.Channel, false, false)]
        [InlineData(PoolImplementation.Channel, false, true)]
        [InlineData(PoolImplementation.Channel, true, false)]
        [InlineData(PoolImplementation.Channel, true, true)]
        public void Checkout_ValidatesIdleAccessToken(PoolImplementation implementation, bool async, bool expired)
        {
            using var fixture = new TokenPool(implementation);
            using var owner = new SqlConnection();
            TokenConnection original = Request(fixture.Pool, owner, async);
            original.Expired = expired;
            int checks = original.ExpiryChecks;
            fixture.Pool.ReturnInternalConnection(original, owner);

            // Like V1, returning a connection does not evaluate its token or refresh credentials.
            Assert.Equal(checks, original.ExpiryChecks);
            Assert.False(original.Disposed);
            Assert.Equal(1, fixture.Pool.IdleCount);

            // This hits idle checkout. With one pool slot, eviction must free capacity for its replacement.
            TokenConnection served = Request(fixture.Pool, owner, async);
            Assert.Equal(!expired, ReferenceEquals(original, served));
            Assert.Equal(expired, original.Disposed);
            Assert.True(original.ExpiryChecks > checks);
            Assert.False(served.Expired);
            Assert.Equal(1, fixture.Pool.Count);
            fixture.Pool.ReturnInternalConnection(served, owner);
        }

        /// <summary>
        /// Freshly created connections must pass the expiry gate before activation, just like idle connections.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle, false)]
        [InlineData(PoolImplementation.WaitHandle, true)]
        [InlineData(PoolImplementation.Channel, false)]
        [InlineData(PoolImplementation.Channel, true)]
        public void Checkout_RejectsNewConnectionWithExpiredToken(PoolImplementation implementation, bool async)
        {
            // Only the first creation is expired, so retrying can succeed without waiting for time to pass.
            using var fixture = new TokenPool(implementation, expireFirstCreation: true);
            using var owner = new SqlConnection();
            TokenConnection served = Request(fixture.Pool, owner, async);

            Assert.Equal(2, fixture.Factory.Created.Count);
            TokenConnection expired = fixture.Factory.Created[0];
            Assert.True(expired.Disposed);
            // Activation would assign the connection to the caller; expiry must be rejected before then.
            Assert.Equal(0, expired.Activations);
            Assert.Same(fixture.Factory.Created[1], served);
            Assert.Equal(1, fixture.Pool.Count);
            fixture.Pool.ReturnInternalConnection(served, owner);
        }

        /// <summary>
        /// A direct channel handoff must validate expiry even though it bypasses the idle fast-path check.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WaitingCheckout_RejectsExpiredToken(bool async)
        {
            using var fixture = new TokenPool(PoolImplementation.Channel);
            var pool = (ChannelDbConnectionPool)fixture.Pool;
            using var owner = new SqlConnection();
            using var waitingOwner = new SqlConnection();
            TokenConnection original = Request(pool, owner, async);
            Task<TokenConnection> pending = Task.Run(() => Request(pool, waitingOwner, async));
            try
            {
                // Wait until the request is reading the channel, not merely scheduled on another thread.
                // Returning the expired connection then exercises the post-wait gate, not idle lookup.
                Assert.True(SpinWait.SpinUntil(() => pool.Reclaimer.ParkedWaiters == 1, WaitTimeout),
                    "The request did not reach the idle-channel wait.");
                original.Expired = true;
                pool.ReturnInternalConnection(original, owner);

                Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(WaitTimeout)));
                TokenConnection served = await pending;
                Assert.NotSame(original, served);
                Assert.True(original.Disposed);
                Assert.False(served.Expired);
                Assert.Equal(1, pool.Count);
                pool.ReturnInternalConnection(served, waitingOwner);
            }
            finally
            {
                pool.Shutdown();
                Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(WaitTimeout)));
            }
        }

        /// <summary>
        /// Transaction affinity overrides expiry eviction until completion, avoiding disruption of the active transaction.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle, false)]
        [InlineData(PoolImplementation.WaitHandle, true)]
        [InlineData(PoolImplementation.Channel, false)]
        [InlineData(PoolImplementation.Channel, true)]
        public void TransactionCheckout_PreservesExpiredConnectionUntilTransactionEnds(PoolImplementation implementation, bool async)
        {
            using var fixture = new TokenPool(implementation);
            using var owner = new SqlConnection();
            using var transaction = new CommittableTransaction();
            TokenConnection original;
            using (var scope = new TransactionScope(transaction))
            {
                original = Request(fixture.Pool, owner, async);
                original.Expired = true;
                int checks = original.ExpiryChecks;
                fixture.Pool.ReturnInternalConnection(original, owner);

                // Return parks this connection in the transacted store, which takes precedence over idle reuse.
                // The same transaction must get it back without checking expiry or breaking enlistment.
                TokenConnection enlisted = Request(fixture.Pool, owner, async);
                Assert.Same(original, enlisted);
                Assert.Equal(checks, original.ExpiryChecks);
                Assert.False(original.Disposed);
                fixture.Pool.ReturnInternalConnection(enlisted, owner);
                scope.Complete();
            }
            transaction.Commit();

            // Completion releases the connection to general circulation, where expiry eviction applies again.
            TokenConnection served = Request(fixture.Pool, owner, async);
            Assert.NotSame(original, served);
            Assert.True(original.Disposed);
            fixture.Pool.ReturnInternalConnection(served, owner);
        }

        /// <summary>
        /// Physical-connection expiry triggers replacement; only an expiring cached token requires another callback.
        /// </summary>
        [Theory]
        [InlineData(false, false, -1, false)]
        [InlineData(false, true, -1, false)]
        [InlineData(true, false, -1, false)]
        [InlineData(true, true, -1, false)]
        [InlineData(false, false, 300, false)]
        [InlineData(false, true, 300, false)]
        [InlineData(true, false, 300, false)]
        [InlineData(true, true, 300, false)]
        [InlineData(false, false, -1, true)]
        [InlineData(false, true, -1, true)]
        [InlineData(true, false, -1, true)]
        [InlineData(true, true, -1, true)]
        [InlineData(false, false, 300, true)]
        [InlineData(false, true, 300, true)]
        [InlineData(true, false, 300, true)]
        [InlineData(true, true, 300, true)]
        public async Task AccessTokenCallback_CheckoutRejectsExpiredToken(bool usePoolV2, bool async, int expiresInSeconds, bool expireCachedToken)
        {
            using var poolVersion = new ConnectionPoolVersionScope(usePoolV2);
            using var server = new TdsServer(new TdsServerArguments
            {
                FedAuthRequiredPreLoginOption = TdsPreLoginFedAuthRequiredOption.FedAuthRequired
            });
            server.Start();
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                MaxPoolSize = 1,
                ConnectTimeout = 600,
                Enlist = false
            };
            int callbackInvocations = 0;
            using var connection = new SqlConnection(builder.ConnectionString)
            {
                AccessTokenCallback = (_, _) =>
                {
                    Interlocked.Increment(ref callbackInvocations);
                    return Task.FromResult(new SqlAuthenticationToken("invalid", DateTimeOffset.UtcNow.AddHours(2)));
                }
            };

            try
            {
                // The first physical login populates the token cache; ordinary reopen only reuses the socket.
                await OpenConnection(connection, async);
                var original = Assert.IsType<Connection.SqlConnectionInternal>(connection.InnerConnection);
                Assert.False(original.IsAccessTokenExpired);
                Assert.Equal(1, callbackInvocations);
                connection.Close();
                await OpenConnection(connection, async);
                Assert.Same(original, connection.InnerConnection);
                Assert.Equal(1, callbackInvocations);

                // Change metadata, not wall-clock time: -1 is expired and 300 is within the 600-second buffer.
                // The physical connection and the pool cache hold separate expiry values.
                FieldInfo? tokenField = original.GetType().GetField("_fedAuthToken", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(tokenField);
                DateTimeOffset expiry = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
                tokenField!.SetValue(original, new SqlFedAuthToken(new SqlAuthenticationToken("invalid", expiry)));
                Assert.Equal(expiresInSeconds > 0, expiry > DateTimeOffset.UtcNow);
                Assert.True(original.IsAccessTokenExpired);
                IDbConnectionPool pool = original.Pool;
                var cachedToken = Assert.Single(pool.AuthenticationContexts);
                if (expireCachedToken)
                {
                    // Leaving the cache fresh models another login already having refreshed the pool's token.
                    // Aging it instead requires the replacement login to invoke the callback.
                    pool.AuthenticationContexts[cachedToken.Key] = new DbConnectionPoolAuthenticationContext(
                        cachedToken.Value.AccessToken, expiry.UtcDateTime);
                }
                connection.Close();
                Assert.Equal(1, callbackInvocations);

                // Checkout evicts the old physical connection. Its replacement either uses the fresh cache
                // or invokes the callback and updates the cache; eviction alone must not force token acquisition.
                await OpenConnection(connection, async);
                Assert.NotSame(original, connection.InnerConnection);
                Assert.False(connection.InnerConnection.IsAccessTokenExpired);
                Assert.Equal(expireCachedToken ? 2 : 1, callbackInvocations);
                Assert.True(pool.AuthenticationContexts[cachedToken.Key].ExpirationTime > expiry.UtcDateTime);

                // Once replaced, reuse must neither create another physical connection nor invoke the callback.
                DbConnectionInternal replacement = connection.InnerConnection;
                connection.Close();
                await OpenConnection(connection, async);
                Assert.Same(replacement, connection.InnerConnection);
                Assert.Equal(expireCachedToken ? 2 : 1, callbackInvocations);
            }
            finally
            {
                SqlConnection.ClearPool(connection);
            }
        }

        /// <summary>
        /// New physical logins refresh expired or nearly expired cached tokens but reuse sufficiently valid ones.
        /// </summary>
        [Theory]
        [InlineData(false, false, -1)]
        [InlineData(false, true, -1)]
        [InlineData(true, false, -1)]
        [InlineData(true, true, -1)]
        [InlineData(false, false, 300)]
        [InlineData(false, true, 300)]
        [InlineData(true, false, 300)]
        [InlineData(true, true, 300)]
        [InlineData(false, false, 3600)]
        [InlineData(false, true, 3600)]
        [InlineData(true, false, 3600)]
        [InlineData(true, true, 3600)]
        public async Task AccessTokenCallback_NewPhysicalConnectionHonorsCachedTokenExpiry(bool usePoolV2, bool async, int expiresInSeconds)
        {
            using var poolVersion = new ConnectionPoolVersionScope(usePoolV2);
            using var server = new TdsServer(new TdsServerArguments
            {
                FedAuthRequiredPreLoginOption = TdsPreLoginFedAuthRequiredOption.FedAuthRequired
            });
            server.Start();
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                MaxPoolSize = 2,
                Enlist = false
            };
            int callbackInvocations = 0;
            using var first = new SqlConnection(builder.ConnectionString)
            {
                AccessTokenCallback = (_, _) =>
                {
                    int invocation = Interlocked.Increment(ref callbackInvocations);
                    return Task.FromResult(new SqlAuthenticationToken($"invalid-{invocation}", DateTimeOffset.UtcNow.AddHours(2)));
                }
            };
            // The callback delegate is part of the pool key; share it to exercise the same token cache.
            using var second = new SqlConnection(builder.ConnectionString)
            {
                AccessTokenCallback = first.AccessTokenCallback
            };

            try
            {
                await OpenConnection(first, async);
                Assert.Equal(1, callbackInvocations);
                IDbConnectionPool pool = first.InnerConnection.Pool;
                var entry = Assert.Single(pool.AuthenticationContexts);
                var cachedToken = new DbConnectionPoolAuthenticationContext(
                    entry.Value.AccessToken, DateTime.UtcNow.AddSeconds(expiresInSeconds));
                pool.AuthenticationContexts[entry.Key] = cachedToken;

                // Keep the first connection checked out so the second must perform a physical login.
                await OpenConnection(second, async);
                Assert.NotSame(first.InnerConnection, second.InnerConnection);
                Assert.Same(pool, second.InnerConnection.Pool);
                Assert.False(second.InnerConnection.IsAccessTokenExpired);
                // -1 and 300 seconds require refresh (the cache's 10-minute window); 3600 seconds
                // is beyond even its 45-minute opportunistic refresh window, so it must reuse the token.
                bool refreshed = expiresInSeconds <= 600;
                Assert.Equal(refreshed ? 2 : 1, callbackInvocations);
                DbConnectionPoolAuthenticationContext current = pool.AuthenticationContexts[entry.Key];
                if (refreshed)
                {
                    Assert.NotSame(cachedToken, current);
                    Assert.NotEqual(cachedToken.AccessToken, current.AccessToken);
                    Assert.True(current.ExpirationTime > cachedToken.ExpirationTime);
                }
                else
                {
                    Assert.Same(cachedToken, current);
                }

                // Physical reuse skips authentication entirely, regardless of which cache branch ran above.
                DbConnectionInternal reused = second.InnerConnection;
                second.Close();
                await OpenConnection(second, async);
                Assert.Same(reused, second.InnerConnection);
                Assert.Equal(refreshed ? 2 : 1, callbackInvocations);
            }
            finally
            {
                SqlConnection.ClearPool(first);
            }
        }

        /// <summary>
        /// Exercises the selected public open API, bounding asynchronous opens with cancellation.
        /// </summary>
        /// <param name="connection">Connection to open against the simulated server.</param>
        /// <param name="async">Whether to use OpenAsync instead of Open.</param>
        /// <returns>A task completing when the connection is open.</returns>
        private static async Task OpenConnection(SqlConnection connection, bool async)
        {
            if (async)
            {
                using var cancellation = new CancellationTokenSource(WaitTimeout);
                await connection.OpenAsync(cancellation.Token);
            }
            else
            {
                connection.Open();
            }
        }

        /// <summary>
        /// Requests a stub connection directly from a pool, handling inline and deferred completion.
        /// </summary>
        /// <param name="pool">Pool under test.</param>
        /// <param name="owner">Owner passed to connection activation.</param>
        /// <param name="async">Whether to supply the completion source used by asynchronous opens.</param>
        /// <returns>The connection assigned to the owner.</returns>
        private static TokenConnection Request(IDbConnectionPool pool, SqlConnection owner, bool async)
        {
            // Async opens carry the ambient transaction in AsyncState so worker threads preserve affinity.
            TaskCompletionSource<DbConnectionInternal>? completion = async
                ? new TaskCompletionSource<DbConnectionInternal>(Transaction.Current, TaskCreationOptions.RunContinuationsAsynchronously)
                : null;
            bool completed = pool.TryGetConnection(owner, completion, TimeoutTimer.StartNew(WaitTimeout), out DbConnectionInternal? connection);
            if (!completed)
            {
                Assert.NotNull(completion);
                Assert.True(completion!.Task.Wait(WaitTimeout), "The connection request did not complete.");
                connection = completion.Task.GetAwaiter().GetResult();
            }
            return Assert.IsType<TokenConnection>(connection);
        }

        /// <summary>
        /// Uses one pool slot and the harness's frozen clock to isolate checkout decisions from maintenance.
        /// </summary>
        private sealed class TokenPool : IDisposable
        {
            internal TokenFactory Factory { get; }
            internal IDbConnectionPool Pool { get; }

            /// <summary>Creates an isolated pool backed by controllable token connections.</summary>
            /// <param name="implementation">Pool implementation to exercise.</param>
            /// <param name="expireFirstCreation">Whether the first created connection starts expired.</param>
            internal TokenPool(PoolImplementation implementation, bool expireFirstCreation = false)
            {
                Factory = new TokenFactory(expireFirstCreation);
                Pool = ConstructPool(implementation, Factory, maxPoolSize: 1, creationTimeout: 30000);
            }

            /// <summary>Stops maintenance and disposes idle connections and any left checked out by a failed test.</summary>
            public void Dispose()
            {
                Pool.Shutdown();
                Pool.Clear();
                foreach (TokenConnection connection in Factory.Created)
                {
                    if (!connection.Disposed)
                    {
                        connection.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Records physical creations and can expire the first one to force a checkout retry.
        /// </summary>
        private sealed class TokenFactory(bool expireFirstCreation) : SqlConnectionFactory
        {
            internal List<TokenConnection> Created { get; } = new();

            /// <inheritdoc />
            protected override DbConnectionInternal CreateConnection(SqlConnectionOptions options, ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo, IDbConnectionPool pool, DbConnection owningConnection, TimeoutTimer timeout)
            {
                var connection = new TokenConnection { Expired = expireFirstCreation && Created.Count == 0 };
                Created.Add(connection);
                return connection;
            }
        }

        /// <summary>
        /// Makes expiry deterministic and records whether the pool checks, activates, or disposes the connection.
        /// </summary>
        private sealed class TokenConnection : ChannelDbConnectionPoolTest.StubDbConnectionInternal
        {
            internal bool Expired { get; set; }
            internal int ExpiryChecks { get; private set; }
            internal int Activations { get; private set; }
            internal bool Disposed { get; private set; }

            internal override bool IsAccessTokenExpired
            {
                get
                {
                    ExpiryChecks++;
                    return Expired;
                }
            }

            /// <inheritdoc />
            protected override void Activate(Transaction transaction)
            {
                Activations++;
                base.Activate(transaction);
            }

            /// <inheritdoc />
            public override void Dispose()
            {
                Disposed = true;
                base.Dispose();
            }
        }
    }
}
