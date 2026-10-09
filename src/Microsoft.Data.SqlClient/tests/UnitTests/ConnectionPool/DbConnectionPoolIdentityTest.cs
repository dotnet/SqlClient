// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using Interop.Windows.Advapi32;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Guards against pooling and replenishing under the wrong network-only logon session.
    /// No network authentication is performed; connections are supplied by a stub factory.
    /// </summary>
    [Collection(AppContextSwitchTestCollection.Name)]
    public class DbConnectionPoolIdentityTest
    {
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// Network-only logons keep the local SID but must have distinct pool identities, while
        /// repeated reads and duplicated tokens from the same logon session remain equivalent.
        /// </summary>
        [ConditionalTheory(nameof(IsWindows))]
        [InlineData(false)]
        #if NET
        [InlineData(true)]
        #endif
        public void GetCurrent_NetOnlyLogons_AreDistinct(bool managedNetworking)
        {
            using var switches = new LocalAppContextSwitchesHelper();
            SetNetworking(switches, managedNetworking);
            using SafeAccessTokenHandle first = CreateToken();
            using SafeAccessTokenHandle second = CreateToken();
            using WindowsIdentity original = WindowsIdentity.GetCurrent();

            DbConnectionPoolIdentity processIdentity = DbConnectionPoolIdentity.GetCurrent();
            DbConnectionPoolIdentity firstIdentity = WindowsIdentity.RunImpersonated(first, () =>
            {
                using WindowsIdentity current = WindowsIdentity.GetCurrent();
                Assert.Equal(original.User, current.User);
                return DbConnectionPoolIdentity.GetCurrent();
            });
            DbConnectionPoolIdentity secondIdentity =
                WindowsIdentity.RunImpersonated(second, DbConnectionPoolIdentity.GetCurrent);

            Assert.NotEqual(processIdentity, firstIdentity);
            Assert.NotEqual(firstIdentity, secondIdentity);
            Assert.Equal(processIdentity, DbConnectionPoolIdentity.GetCurrent());

            WindowsIdentity.RunImpersonated(first, () =>
            {
                DbConnectionPoolIdentity repeated = DbConnectionPoolIdentity.GetCurrent();
                Assert.Equal(firstIdentity, repeated);
                Assert.Equal(firstIdentity.GetHashCode(), repeated.GetHashCode());
                using WindowsIdentity duplicate = WindowsIdentity.GetCurrent();
                WindowsIdentity.RunImpersonated(duplicate.AccessToken, () =>
                    Assert.Equal(firstIdentity, DbConnectionPoolIdentity.GetCurrent()));
            });

            var identities = new HashSet<DbConnectionPoolIdentity>
            {
                processIdentity, firstIdentity, secondIdentity,
                WindowsIdentity.RunImpersonated(first, DbConnectionPoolIdentity.GetCurrent)
            };
            Assert.Equal(3, identities.Count);
        }

        /// <summary>
        /// Both pool implementations select separate pools for identical local SIDs belonging
        /// to different logon sessions, and restore the original pool after impersonation ends.
        /// </summary>
        [ConditionalTheory(nameof(IsWindows))]
        [InlineData(false, false)]
        [InlineData(true, false)]
        #if NET
        [InlineData(false, true)]
        [InlineData(true, true)]
        #endif
        public void GetPool_NetOnlyLogons_SelectSeparatePools(bool usePoolV2, bool managedNetworking)
        {
            using var switches = new LocalAppContextSwitchesHelper();
            switches.UseConnectionPoolV2 = usePoolV2;
            SetNetworking(switches, managedNetworking);
            using SafeAccessTokenHandle first = CreateToken();
            using SafeAccessTokenHandle second = CreateToken();
            var group = CreateGroup(minPoolSize: 0);
            var factory = new ChannelDbConnectionPoolTest.SuccessfulSqlConnectionFactory();
            IDbConnectionPool processPool = group.GetConnectionPool(factory);
            IDbConnectionPool? firstPool = null;
            IDbConnectionPool? secondPool = null;
            try
            {
                firstPool = WindowsIdentity.RunImpersonated(first, () => group.GetConnectionPool(factory));
                secondPool = WindowsIdentity.RunImpersonated(second, () => group.GetConnectionPool(factory));
                Assert.NotSame(processPool, firstPool);
                Assert.NotSame(firstPool, secondPool);
                Assert.Same(processPool, group.GetConnectionPool(factory));
                Assert.Same(firstPool, WindowsIdentity.RunImpersonated(first, () => group.GetConnectionPool(factory)));
                if (usePoolV2)
                {
                    Assert.IsType<ChannelDbConnectionPool>(firstPool);
                }
                else
                {
                    Assert.IsType<WaitHandleDbConnectionPool>(firstPool);
                }
            }
            finally
            {
                processPool.Shutdown();
                firstPool?.Shutdown();
                secondPool?.Shutdown();
            }
        }

        /// <summary>
        /// Replenishment with the wrong logon session must neither create connections nor poison
        /// the pool; a later pass with the owning session must still reach the minimum.
        /// </summary>
        [ConditionalTheory(nameof(IsWindows))]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        #if NET
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        #endif
        public async Task Replenishment_WrongLogonSession_DoesNotCreateConnections(
            bool usePoolV2, bool managedNetworking, bool impersonatedPool)
        {
            using var switches = new LocalAppContextSwitchesHelper();
            SetNetworking(switches, managedNetworking);
            using SafeAccessTokenHandle token = CreateToken();
            DbConnectionPoolIdentity identity = impersonatedPool
                ? WindowsIdentity.RunImpersonated(token, DbConnectionPoolIdentity.GetCurrent)
                : DbConnectionPoolIdentity.GetCurrent();
            var factory = new ChannelDbConnectionPoolTest.CountingSuccessfulConnectionFactory();
            var group = CreateGroup(minPoolSize: 1);
            IDbConnectionPool pool = usePoolV2
                ? new ChannelDbConnectionPool(factory, group, identity, new DbConnectionPoolProviderInfo(), timeProvider: new FakeTimeProvider())
                : new WaitHandleDbConnectionPool(factory, group, identity, new DbConnectionPoolProviderInfo(), timeProvider: new FakeTimeProvider());
            try
            {
                await (impersonatedPool
                    ? Replenish(pool)
                    : WindowsIdentity.RunImpersonated(token, () => Replenish(pool)));
                Assert.Equal(0, factory.CreateCount);
                Assert.Equal(0, pool.Count);
                Assert.False(pool.ErrorOccurred);

                await (impersonatedPool
                    ? WindowsIdentity.RunImpersonated(token, () => Replenish(pool))
                    : Replenish(pool));
                Assert.Equal(1, factory.CreateCount);
                Assert.Equal(1, pool.IdleCount);
                Assert.False(pool.ErrorOccurred);
            }
            finally
            {
                pool.Shutdown();
            }
        }

        /// <summary>
        /// Synchronous and asynchronous checkouts establish connections in the requesting
        /// logon session for either pool implementation.
        /// </summary>
        [ConditionalTheory(nameof(IsWindows))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task GetConnection_NetOnlyLogon_PreservesIdentity(bool usePoolV2, bool async)
        {
            using SafeAccessTokenHandle token = CreateToken();
            DbConnectionPoolIdentity identity = WindowsIdentity.RunImpersonated(token, DbConnectionPoolIdentity.GetCurrent);
            var factory = new IdentityRecordingFactory();
            var group = CreateGroup(minPoolSize: 0);
            IDbConnectionPool pool = usePoolV2
                ? new ChannelDbConnectionPool(factory, group, identity, new DbConnectionPoolProviderInfo(), timeProvider: new FakeTimeProvider())
                : new WaitHandleDbConnectionPool(factory, group, identity, new DbConnectionPoolProviderInfo(), timeProvider: new FakeTimeProvider());
            using var owner = new SqlConnection();
            DbConnectionInternal? connection = null;
            try
            {
                connection = await WindowsIdentity.RunImpersonated(token, async () =>
                {
                    TaskCompletionSource<DbConnectionInternal>? completion = async
                        ? new TaskCompletionSource<DbConnectionInternal>(TaskCreationOptions.RunContinuationsAsynchronously)
                        : null;
                    bool completed = pool.TryGetConnection(owner, completion,
                        TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? result);
                    if (completed)
                    {
                        return result!;
                    }

                    Assert.Same(completion!.Task,
                        await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(30))));
                    return await completion.Task;
                });
                Assert.Equal(identity, factory.CreatedIdentity);
            }
            finally
            {
                if (connection != null)
                {
                    pool.ReturnInternalConnection(connection, owner);
                }
                pool.Shutdown();
            }
        }

        /// <summary>
        /// Token-query failures propagate instead of collapsing identities to a default value.
        /// </summary>
        [ConditionalFact(nameof(IsWindows))]
        public void TokenQueries_InvalidHandle_Throw()
        {
            using var token = new SafeAccessTokenHandle(IntPtr.Zero);
            Assert.Throws<System.ComponentModel.Win32Exception>(() => Advapi32.GetAuthenticationId(token));
            Assert.Throws<System.ComponentModel.Win32Exception>(() => Advapi32.GetIsTokenRestricted(token));
        }

        /// <summary>Records the identity under which a stub physical connection is created.</summary>
        private sealed class IdentityRecordingFactory : ChannelDbConnectionPoolTest.SuccessfulSqlConnectionFactory
        {
            internal DbConnectionPoolIdentity? CreatedIdentity { get; private set; }

            /// <summary>Captures the effective pool identity before creating a stub connection.</summary>
            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options, ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo, IDbConnectionPool pool,
                DbConnection owningConnection, TimeoutTimer timeout)
            {
                CreatedIdentity = DbConnectionPoolIdentity.GetCurrent();
                return base.CreateConnection(options, poolKey, poolGroupProviderInfo, pool, owningConnection, timeout);
            }
        }

        /// <summary>Creates a network-only token without usable network credentials.</summary>
        /// <returns>A token belonging to a new logon session, owned by the caller.</returns>
        private static SafeAccessTokenHandle CreateToken() =>
            WindowsImpersonationHelper.LogonNetOnly("<user>", "EXAMPLE", "<pwd>");

        /// <summary>Chooses the networking mode where that switch exists.</summary>
        /// <param name="switches">The scope restoring the cached switch after the test.</param>
        /// <param name="managedNetworking">Whether to use managed networking on Windows.</param>
        private static void SetNetworking(LocalAppContextSwitchesHelper switches, bool managedNetworking)
        {
            #if NET
            switches.UseManagedNetworking = managedNetworking;
            #endif
        }

        /// <summary>Constructs a group that pools integrated-authentication connections by identity.</summary>
        /// <param name="minPoolSize">The minimum number of connections to replenish.</param>
        /// <returns>An isolated pool group for the test.</returns>
        private static DbConnectionPoolGroup CreateGroup(int minPoolSize) =>
            new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;Integrated Security=true;"),
                new ConnectionPoolKey("IdentityTest", null, null, null, null),
                new DbConnectionPoolGroupOptions(
                    poolByIdentity: true, minPoolSize: minPoolSize, maxPoolSize: 5,
                    creationTimeout: 15000, loadBalanceTimeout: 0, hasTransactionAffinity: false, idleTimeout: 0));

        /// <summary>
        /// Executes an actual background replenishment pass and awaits its completion rather
        /// than using sleeps to infer that no connection was created.
        /// </summary>
        /// <param name="pool">The pool whose background callback is exercised.</param>
        /// <returns>A task completing when the replenishment pass finishes.</returns>
        private static async Task Replenish(IDbConnectionPool pool)
        {
            Task work;
            if (pool is ChannelDbConnectionPool channel)
            {
                channel.RequestWarmup();
                work = channel.WarmupLoopTask!;
            }
            else
            {
                MethodInfo callback = typeof(WaitHandleDbConnectionPool).GetMethod(
                    "PoolCreateRequest", BindingFlags.Instance | BindingFlags.NonPublic)!;
                work = Task.Run(() => callback.Invoke(pool, new object?[] { null }));
            }

            Assert.Same(work, await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(30))));
            await work;
        }
    }
}
