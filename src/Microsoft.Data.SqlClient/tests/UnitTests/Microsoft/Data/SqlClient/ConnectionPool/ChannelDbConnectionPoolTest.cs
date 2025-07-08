// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class ChannelDbConnectionPoolTest
    {
        private readonly ChannelDbConnectionPool _pool;

        public ChannelDbConnectionPoolTest()
        {
            _pool = new ChannelDbConnectionPool();
        }

        [Fact]
        public void TestAuthenticationContexts()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.AuthenticationContexts);
        }

        [Fact]
        public void TestConnectionFactory()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.ConnectionFactory);
        }

        [Fact]
        public void TestCount()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.Count);
        }

        [Fact]
        public void TestErrorOccurred()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.ErrorOccurred);
        }

        [Fact]
        public void TestId()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.Id);
        }

        [Fact]
        public void TestIdentity()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.Identity);
        }

        [Fact]
        public void TestIsRunning()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.IsRunning);
        }

        [Fact]
        public void TestLoadBalanceTimeout()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.LoadBalanceTimeout);
        }

        [Fact]
        public void TestPoolGroup()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.PoolGroup);
        }

        [Fact]
        public void TestPoolGroupOptions()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.PoolGroupOptions);
        }

        [Fact]
        public void TestProviderInfo()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.ProviderInfo);
        }

        [Fact]
        public void TestStateGetter()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.State);
        }

        [Fact]
        public void TestStateSetter()
        {
            Assert.Throws<NotImplementedException>(() => _pool.State = DbConnectionPoolState.Running);
        }

        [Fact]
        public void TestUseLoadBalancing()
        {
            Assert.Throws<NotImplementedException>(() => _ = _pool.UseLoadBalancing);
        }

        [Fact]
        public void TestClear()
        {
            Assert.Throws<NotImplementedException>(() => _pool.Clear());
        }

        [Fact]
        public void TestPutObjectFromTransactedPool()
        {
            Assert.Throws<NotImplementedException>(() => _pool.PutObjectFromTransactedPool(null!));
        }

        [Fact]
        public void TestReplaceConnection()
        {
            Assert.Throws<NotImplementedException>(() => _pool.ReplaceConnection(null!, null!, null!));
        }

        [Fact]
        public void TestReturnInternalConnection()
        {
            Assert.Throws<NotImplementedException>(() => _pool.ReturnInternalConnection(null!, null!));
        }

        [Fact]
        public void TestShutdown()
        {
            Assert.Throws<NotImplementedException>(() => _pool.Shutdown());
        }

        [Fact]
        public void TestStartup()
        {
            Assert.Throws<NotImplementedException>(() => _pool.Startup());
        }

        [Fact]
        public void TestTransactionEnded()
        {
            Assert.Throws<NotImplementedException>(() => _pool.TransactionEnded(null!, null!));
        }

        [Fact]
        public void TestTryGetConnection()
        {
            Assert.Throws<NotImplementedException>(() => _pool.TryGetConnection(null!, null!, null!, out _));
        }
    }
}
