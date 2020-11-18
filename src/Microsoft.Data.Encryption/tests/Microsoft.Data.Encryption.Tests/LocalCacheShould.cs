using Microsoft.Data.Encryption.Cryptography;
using System;
using System.Threading;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests
{
    public class LocalCacheShould
    {
        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-1000)]
        [InlineData(-1)]
        [InlineData(0)]
        public void ThrowWhenMaxSizeIsNotPositive(int sizeLimit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocalCache<int, int>(maxSizeLimit: sizeLimit));
        }

        [Fact]
        public void InitializeEmpty()
        {
            LocalCache<byte[], byte[]> cache = new LocalCache<byte[], byte[]>();

            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void EvictItemAfterTtlExpires()
        {
            byte[] encryptedKey = { 4, 8, 15, 16, 23, 42 };
            LocalCache<byte[], byte[]> cache = new LocalCache<byte[], byte[]>();
            cache.TimeToLive = TimeSpan.FromSeconds(1);
            cache.GetOrCreate(encryptedKey, createItem);

            Assert.Equal(1, cache.Count);
            Assert.True(cache.Contains(encryptedKey), "The cache should contain the key.");
            Thread.Sleep(1100);
            Assert.False(cache.Contains(encryptedKey), "The cache should have expired the key.");
            Assert.Equal(0, cache.Count);

            static byte[] createItem() => new byte[] { 1, 2, 3, 4, 5 };
        }

        [Fact]
        public void CallFunctionDelegateOnlyOnceForSameKey()
        {
            int executionCount = 0;
            byte[] encryptedKey = { 4, 8, 15, 16, 23, 42 };
            LocalCache<byte[], byte[]> cache = new LocalCache<byte[], byte[]>();

            cache.GetOrCreate(encryptedKey, createItem);
            cache.GetOrCreate(encryptedKey, createItem);
            cache.GetOrCreate(encryptedKey, createItem);

            Assert.Equal(1, executionCount);

            byte[] createItem()
            {
                executionCount++;
                return new byte[] { 1, 2, 3, 4, 5 };
            }
        }

        [Fact]
        public void CallFunctionDelegateTwiceForMultipleCallsOnTwoDifferentKeys()
        {
            int executionCount = 0;
            byte[] encryptedKey1 = { 1, 1, 1, 1, 1 };
            byte[] encryptedKey2 = { 2, 2, 2, 2, 2 };
            LocalCache<byte[], byte[]> cache = new LocalCache<byte[], byte[]>();

            cache.GetOrCreate(encryptedKey1, createItem);
            cache.GetOrCreate(encryptedKey2, createItem);
            cache.GetOrCreate(encryptedKey1, createItem);
            cache.GetOrCreate(encryptedKey2, createItem);

            Assert.Equal(2, executionCount);

            byte[] createItem()
            {
                executionCount++;
                return new byte[] { 1, 2, 3, 4, 5 };
            }
        }

        [Fact]
        public void CallFunctionDelegateTwiceForSameKeyIfTtlExpires()
        {
            int executionCount = 0;
            byte[] encryptedKey1 = { 4, 8, 15, 16, 23, 42 };
            LocalCache<byte[], byte[]> cache = new LocalCache<byte[], byte[]>();
            cache.TimeToLive = TimeSpan.FromSeconds(1);

            cache.GetOrCreate(encryptedKey1, createItem);
            Thread.Sleep(1100);
            cache.GetOrCreate(encryptedKey1, createItem);

            Assert.Equal(2, executionCount);

            byte[] createItem()
            {
                executionCount++;
                return new byte[] { 1, 2, 3, 4, 5 };
            }
        }

        [Fact]
        public void HonorTheMaxSizeLimit()
        {
            LocalCache<int, string> cache = new LocalCache<int, string>(maxSizeLimit: 3);
            cache.GetOrCreate(1, createItem);
            cache.GetOrCreate(2, createItem);
            cache.GetOrCreate(3, createItem);
            cache.GetOrCreate(4, createItem);

            Assert.Equal(3, cache.Count);

            string createItem()
            {
                return "Some String";
            }
        }

        [Fact]
        public void EvictOldestEntriesWhenMaxSizeLimitIsReached()
        {
            LocalCache<int, string> cache = new LocalCache<int, string>(maxSizeLimit: 3);
            cache.GetOrCreate(1, createItem);
            cache.GetOrCreate(2, createItem);
            cache.GetOrCreate(3, createItem);
            cache.GetOrCreate(4, createItem);
            cache.GetOrCreate(5, createItem);

            Assert.Equal(3, cache.Count);
            Assert.True(cache.Contains(3));
            Assert.True(cache.Contains(4));
            Assert.True(cache.Contains(5));

            string createItem()
            {
                return "Some String";
            }
        }
    }
}
